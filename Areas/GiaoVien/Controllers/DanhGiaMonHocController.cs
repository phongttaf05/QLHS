using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHS.Models;
using System.Security.Claims;

namespace QLHS.Areas.GiaoVien.Controllers
{
    [Area("GiaoVien")]
    [Authorize(Roles = "giao_vien,admin")]
    public class DanhGiaMonHocController : Controller
    {
        private readonly QlhsContext _context;

        public DanhGiaMonHocController(QlhsContext context)
        {
            _context = context;
        }

        // GET: /GiaoVien/DanhGiaMonHoc?lopHocId=1&monHocId=1&hocKy=1
        public async Task<IActionResult> Index(int? lopHocId, int? monHocId, int? hocKy)
        {
            int userId = GetCurrentUserId();
            var gv = await _context.GiaoViens.FirstOrDefaultAsync(g => g.TaiKhoanId == userId);
            if (gv == null)
            {
                ViewBag.ErrorMessage = "Chưa liên kết hồ sơ Giáo viên với tài khoản này.";
                return View();
            }

            ViewBag.TenGiaoVien = gv.HoTen;

            // Danh sách phân công môn học của giáo viên
            var dsPhanCong = await _context.PhanCongGiangDays
                .Include(p => p.LopHoc)
                    .ThenInclude(l => l!.KhoiLop)
                .Include(p => p.MonHoc)
                .Where(p => p.GiaoVienId == gv.Id)
                .ToListAsync();

            ViewBag.DsPhanCong = dsPhanCong;

            // Mặc định chọn phân công đầu tiên nếu chưa chọn
            var defaultPc = dsPhanCong.FirstOrDefault();
            int selectedLopId = lopHocId ?? defaultPc?.LopHocId ?? 0;
            int selectedMonId = monHocId ?? defaultPc?.MonHocId ?? 0;
            int selectedHocKy = hocKy ?? 1;

            ViewBag.SelectedLopId = selectedLopId;
            ViewBag.SelectedMonId = selectedMonId;
            ViewBag.SelectedHocKy = selectedHocKy;

            // Lấy danh sách học sinh đang học, chưa bị xóa mềm
            var dsHocSinh = await _context.HocSinhs
                .Where(h => h.LopHocHienTaiId == selectedLopId && h.TrangThaiHoc == "dang_hoc" && h.NgayXoaMem == null)
                .OrderBy(h => h.HoTen)
                .ToListAsync();

            ViewBag.DanhSachHocSinh = dsHocSinh;

            // Lấy các bản ghi đánh giá hiện có (gom nhóm an toàn tránh trùng key)
            var dsDanhGiaList = await _context.DanhGiaMonHocs
                .Where(d => d.LopHocId == selectedLopId && d.MonHocId == selectedMonId && d.HocKy == selectedHocKy && d.HocSinhId.HasValue)
                .ToListAsync();

            var dsDanhGia = dsDanhGiaList
                .GroupBy(d => d.HocSinhId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ThoiGianCapNhat).First());

            ViewBag.DanhGiaMap = dsDanhGia;
            ViewBag.SoHsDaNhap = dsHocSinh.Count(hs => dsDanhGia.ContainsKey(hs.Id));
            ViewBag.DaChotDiem = dsHocSinh.Any() && dsHocSinh.All(hs => dsDanhGia.TryGetValue(hs.Id, out var d) && d.DaKhoaSo == true);

            return View();
        }

        // POST: /GiaoVien/DanhGiaMonHoc/ChotDiem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChotDiem(int lopHocId, int monHocId, int hocKy)
        {
            int userId = GetCurrentUserId();
            var gv = await _context.GiaoViens.FirstOrDefaultAsync(g => g.TaiKhoanId == userId);
            if (gv == null)
                return Json(new { success = false, message = "Không tìm thấy hồ sơ giáo viên!" });

            bool duocPhanCong = await _context.PhanCongGiangDays.AnyAsync(p =>
                p.GiaoVienId == gv.Id && p.LopHocId == lopHocId && p.MonHocId == monHocId);
            if (!duocPhanCong)
                return Json(new { success = false, message = "Thầy/Cô không được phân công giảng dạy môn học này cho lớp!" });

            var dsHocSinh = await _context.HocSinhs
                .Where(h => h.LopHocHienTaiId == lopHocId && h.TrangThaiHoc == "dang_hoc" && h.NgayXoaMem == null)
                .OrderBy(h => h.HoTen)
                .ToListAsync();

            if (!dsHocSinh.Any())
                return Json(new { success = false, message = "Lớp học chưa có học sinh đang học để chốt điểm!" });

            var dsDanhGia = await _context.DanhGiaMonHocs
                .Where(d => d.LopHocId == lopHocId && d.MonHocId == monHocId && d.HocKy == hocKy && d.HocSinhId.HasValue)
                .ToListAsync();

            var dgMap = dsDanhGia
                .GroupBy(d => d.HocSinhId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            var hsChuaNhap = dsHocSinh
                .Where(hs => !dgMap.TryGetValue(hs.Id, out var d) || string.IsNullOrWhiteSpace(d.MucDanhGia))
                .ToList();

            if (hsChuaNhap.Any())
            {
                var tenMau = string.Join(", ", hsChuaNhap.Take(3).Select(h => h.HoTen));
                string them = hsChuaNhap.Count > 3 ? $" và {hsChuaNhap.Count - 3} HS khác" : "";
                return Json(new
                {
                    success = false,
                    message = $"Không thể chốt sổ: Còn {hsChuaNhap.Count} học sinh chưa lưu đánh giá ({tenMau}{them}). Vui lòng lưu đầy đủ trước khi chốt điểm!"
                });
            }

            if (dsDanhGia.All(d => d.DaKhoaSo == true))
                return Json(new { success = false, message = "Bảng điểm môn học này đã được chốt sổ trước đó!" });

            var now = DateTime.Now;
            foreach (var dg in dsDanhGia)
            {
                dg.DaKhoaSo = true;
                dg.ThoiGianCapNhat = now;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"Đã chốt sổ điểm thành công cho {dsHocSinh.Count} học sinh! Bảng điểm đã được khóa cố định." });
        }

        // POST: /GiaoVien/DanhGiaMonHoc/LuuDanhGia
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LuuDanhGia(int hocSinhId, int lopHocId, int monHocId, int hocKy, int? diemGiuaKy, int? diemCuoiKy, string? mucDanhGia, string? nhanXet)
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
                return Json(new { success = false, message = "Bảng đánh giá môn học này đã bị BGH KHÓA SỔ!" });
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
            return Json(new { success = true, message = "Lưu kết quả đánh giá môn học thành công!" });
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out int id) ? id : 0;
        }
    }
}
