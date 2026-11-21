using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHS.Models;
using QLHS.Services;
using System.Security.Claims;

namespace QLHS.Areas.GiaoVien.Controllers
{
    [Area("GiaoVien")]
    [Authorize(Roles = "giao_vien,admin")]
    public class HomeController : Controller
    {
        private readonly QlhsContext _context;
        private readonly IAppLockService _appLockService;

        public HomeController(QlhsContext context, IAppLockService appLockService)
        {
            _context = context;
            _appLockService = appLockService;
        }

        private static string? NormalizeAttendanceStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return null;
            return status.Trim().ToLower() switch
            {
                "co_mat" => "co_mat",
                "co_phep" or "nghi_co_phep" => "nghi_co_phep",
                "khong_phep" or "nghi_khong_phep" => "nghi_khong_phep",
                _ => null
            };
        }

        public async Task<IActionResult> Index()
        {
            int userId = GetCurrentUserId();
            var gv = await _context.GiaoViens.FirstOrDefaultAsync(g => g.TaiKhoanId == userId);
            if (gv == null)
            {
                ViewBag.ErrorMessage = "Chưa liên kết hồ sơ Giáo viên với tài khoản này.";
                return View();
            }

            ViewBag.TenGiaoVien = gv.HoTen;
            ViewBag.GiaoVienId = gv.Id;

            // Danh sách lớp chủ nhiệm (chỉ đếm HS đang học, chưa xóa mềm)
            ViewBag.LopChuNhiem = await _context.LopHocs
                .Include(l => l.KhoiLop)
                .Include(l => l.NamHoc)
                .Include(l => l.HocSinhs.Where(h => h.NgayXoaMem == null && h.TrangThaiHoc == "dang_hoc"))
                .Where(l => l.GiaoVienChuNhiemId == gv.Id)
                .ToListAsync();

            // Danh sách các lớp được phân công dạy (cả chủ nhiệm và bộ môn)
            var dsLopDay = await _context.PhanCongGiangDays
                .Where(p => p.GiaoVienId == gv.Id && p.LopHoc != null)
                .Select(p => p.LopHoc!)
                .Distinct()
                .Include(l => l.KhoiLop)
                .Include(l => l.NamHoc)
                .Include(l => l.HocSinhs.Where(h => h.NgayXoaMem == null && h.TrangThaiHoc == "dang_hoc"))
                .ToListAsync();

            ViewBag.DanhSachLopDay = dsLopDay;

            // Danh sách học sinh theo các lớp phụ trách (lọc bỏ đã xóa mềm)
            var lopIds = dsLopDay.Select(l => l.Id).ToList();
            var dsHocSinh = await _context.HocSinhs
                .Include(h => h.LopHocHienTai)
                .Where(h => h.LopHocHienTaiId != null &&
                            lopIds.Contains(h.LopHocHienTaiId.Value) &&
                            h.TrangThaiHoc == "dang_hoc" &&
                            h.NgayXoaMem == null)
                .OrderBy(h => h.LopHocHienTaiId)
                .ThenBy(h => h.HoTen)
                .ToListAsync();

            ViewBag.DanhSachHocSinh = dsHocSinh;

            // Lấy điểm danh hôm nay (giờ Việt Nam, tránh trùng key)
            var today = DateOnly.FromDateTime(DateTime.Now);
            ViewBag.NgayHomNay = today;
            var ddHomNayList = await _context.DiemDanhs
                .Where(d => d.NgayDiemDanh == today && d.HocSinhId.HasValue && lopIds.Contains(d.LopHocId ?? 0))
                .ToListAsync();

            var dsDiemDanhHomNay = ddHomNayList
                .GroupBy(d => d.HocSinhId!.Value)
                .ToDictionary(g => g.Key, g => NormalizeAttendanceStatus(g.OrderByDescending(x => x.ThoiGianCapNhat).First().TrangThai));
            ViewBag.DiemDanhHomNay = dsDiemDanhHomNay;

            // Lấy đánh giá môn học của học sinh trong các lớp này
            var dsDanhGia = await _context.DanhGiaMonHocs
                .Include(d => d.MonHoc)
                .Include(d => d.HocSinh)
                .Where(d => lopIds.Contains(d.LopHocId ?? 0))
                .OrderByDescending(d => d.ThoiGianCapNhat)
                .Take(50)
                .ToListAsync();
            ViewBag.DanhGiaMonHoc = dsDanhGia;

            // Danh sách phân công giảng dạy
            var dsPhanCong = await _context.PhanCongGiangDays
                .Include(p => p.LopHoc)
                .Include(p => p.MonHoc)
                .Include(p => p.HocKy)
                .ThenInclude(h => h!.NamHoc)
                .Where(p => p.GiaoVienId == gv.Id)
                .OrderByDescending(p => p.Id)
                .ToListAsync();

            ViewBag.DanhSachPhanCong = dsPhanCong;

            return View();
        }

        // POST: GiaoVien/Home/XacNhanPhanCong
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XacNhanPhanCong(int phanCongId, string trangThai, string? yKien)
        {
            int userId = GetCurrentUserId();
            var gv = await _context.GiaoViens.FirstOrDefaultAsync(g => g.TaiKhoanId == userId);
            if (gv == null) return Json(new { success = false, message = "Bạn không có quyền thao tác!" });

            var pc = await _context.PhanCongGiangDays.FindAsync(phanCongId);
            if (pc == null || pc.GiaoVienId != gv.Id)
            {
                return Json(new { success = false, message = "Không tìm thấy phân công hoặc bạn không phụ trách phân công này!" });
            }

            // Chặn sửa đổi khi học kỳ này đã có bản TKB công bố
            bool daCongBo = await _context.BanTkbs.AnyAsync(b => b.HocKyId == pc.HocKyId && b.TrangThai == "DA_CONG_BO");
            if (daCongBo)
            {
                return Json(new { success = false, message = "Thời khóa biểu học kỳ này đã được CÔNG BỐ CHÍNH THỨC. Không thể điều chỉnh phân công!" });
            }

            if (trangThai == "CAN_DIEU_CHINH" && string.IsNullOrWhiteSpace(yKien))
            {
                return Json(new { success = false, message = "Vui lòng nhập ý kiến giải trình khi yêu cầu điều chỉnh!" });
            }

            pc.TrangThaiXacNhan = trangThai == "CAN_DIEU_CHINH" ? "CAN_DIEU_CHINH" : "DA_XAC_NHAN";
            pc.NguoiPhanHoiId = userId;
            pc.NgayPhanHoi = DateTime.Now;
            pc.YKien = yKien?.Trim();

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã gửi phản hồi phân công thành công!" });
        }

        // POST: GiaoVien/Home/DiemDanhHocSinh (Chỉ Giáo viên chủ nhiệm của lớp mới được điểm danh đầu buổi)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DiemDanhHocSinh(int hocSinhId, int lopHocId, DateOnly ngayDiemDanh, string trangThai)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            if (ngayDiemDanh > today)
            {
                return Json(new { success = false, message = "Không được phép điểm danh cho ngày trong tương lai!" });
            }

            var normalized = NormalizeAttendanceStatus(trangThai);
            if (normalized == null)
            {
                return Json(new { success = false, message = "Trạng thái điểm danh không hợp lệ!" });
            }

            int userId = GetCurrentUserId();
            var gv = await _context.GiaoViens.FirstOrDefaultAsync(g => g.TaiKhoanId == userId);
            if (gv == null) return Json(new { success = false, message = "Không tìm thấy hồ sơ giáo viên!" });

            // Chỉ GVCN của lớp mới được phép điểm danh đầu buổi
            bool laGvcn = await _context.LopHocs.AnyAsync(l => l.Id == lopHocId && l.GiaoVienChuNhiemId == gv.Id);
            if (!laGvcn)
            {
                return Json(new { success = false, message = "Chỉ Giáo viên chủ nhiệm của lớp mới có quyền điểm danh đầu buổi!" });
            }

            using var trans = await _context.Database.BeginTransactionAsync();
            try
            {
                bool locked = await _appLockService.AcquireLockAsync(trans, $"DiemDanh_{hocSinhId}_{ngayDiemDanh:yyyyMMdd}", 5);
                if (!locked) return Json(new { success = false, message = "Dữ liệu điểm danh đang được xử lý bởi người khác!" });

                var dd = await _context.DiemDanhs.FirstOrDefaultAsync(d => d.HocSinhId == hocSinhId && d.NgayDiemDanh == ngayDiemDanh);
                if (dd == null)
                {
                    dd = new DiemDanh
                    {
                        HocSinhId = hocSinhId,
                        LopHocId = lopHocId,
                        NgayDiemDanh = ngayDiemDanh,
                        TrangThai = normalized,
                        GiaoVienChotId = gv.Id,
                        ThoiGianCapNhat = DateTime.Now
                    };
                    _context.DiemDanhs.Add(dd);
                }
                else
                {
                    dd.LopHocId = lopHocId;
                    dd.TrangThai = normalized;
                    dd.GiaoVienChotId = gv.Id;
                    dd.ThoiGianCapNhat = DateTime.Now;
                }

                await _context.SaveChangesAsync();
                await trans.CommitAsync();

                return Json(new { success = true, message = "Cập nhật điểm danh thành công!" });
            }
            catch (Exception ex)
            {
                await trans.RollbackAsync();
                return Json(new { success = false, message = "Lỗi điểm danh: " + ex.Message });
            }
        }

        // POST: GiaoVien/Home/NhapDiem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NhapDiem(int hocSinhId, int lopHocId, int monHocId, int hocKy, int? diemGiuaKy, int? diemCuoiKy, string? mucDanhGia, string? nhanXet)
        {
            int userId = GetCurrentUserId();
            var gv = await _context.GiaoViens.FirstOrDefaultAsync(g => g.TaiKhoanId == userId);
            if (gv == null)
                return Json(new { success = false, message = "Không tìm thấy hồ sơ giáo viên!" });

            bool duocPhanCong = await _context.PhanCongGiangDays.AnyAsync(p =>
                p.GiaoVienId == gv.Id && p.LopHocId == lopHocId && p.MonHocId == monHocId);
            if (!duocPhanCong)
                return Json(new { success = false, message = "Thầy/Cô không được phân công giảng dạy môn học này cho lớp!" });

            var dg = await _context.DanhGiaMonHocs.FirstOrDefaultAsync(d =>
                d.HocSinhId == hocSinhId && d.LopHocId == lopHocId && d.MonHocId == monHocId && d.HocKy == hocKy);

            if (dg != null && dg.DaKhoaSo == true)
            {
                return Json(new { success = false, message = "Kết quả đánh giá môn học này đã bị KHÓA SỔ. Không thể chỉnh sửa!" });
            }

            if (diemGiuaKy.HasValue && (diemGiuaKy.Value < 0 || diemGiuaKy.Value > 10))
                return Json(new { success = false, message = "Điểm giữa kỳ phải từ 0 đến 10!" });

            if (diemCuoiKy.HasValue && (diemCuoiKy.Value < 0 || diemCuoiKy.Value > 10))
                return Json(new { success = false, message = "Điểm cuối kỳ phải từ 0 đến 10!" });

            if (dg == null)
            {
                dg = new DanhGiaMonHoc
                {
                    HocSinhId = hocSinhId,
                    LopHocId = lopHocId,
                    MonHocId = monHocId,
                    HocKy = hocKy,
                    DiemGiuaKy = diemGiuaKy,
                    DiemCuoiKy = diemCuoiKy,
                    MucDanhGia = mucDanhGia,
                    NhanXetGiaoVien = nhanXet,
                    DaKhoaSo = false,
                    ThoiGianCapNhat = DateTime.Now
                };
                _context.DanhGiaMonHocs.Add(dg);
            }
            else
            {
                dg.DiemGiuaKy = diemGiuaKy;
                dg.DiemCuoiKy = diemCuoiKy;
                dg.MucDanhGia = mucDanhGia;
                dg.NhanXetGiaoVien = nhanXet;
                dg.ThoiGianCapNhat = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Cập nhật đánh giá môn học thành công!" });
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out int id) ? id : 0;
        }
    }
}
