using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHS.Models;
using QLHS.Services;

namespace QLHS.Areas.GiaoVien.Controllers
{
    [Area("GiaoVien")]
    [Authorize(Roles = "giao_vien")]
    public class DiemDanhController : Controller
    {
        private readonly QlhsContext _context;
        private readonly IAppLockService _appLockService;

        public DiemDanhController(QlhsContext context, IAppLockService appLockService)
        {
            _context = context;
            _appLockService = appLockService;
        }

        private static string? NormalizeStatus(string? status)
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

        private async Task<bool> LaGiaoVienChuNhiemAsync(int giaoVienId, int lopHocId)
        {
            return await _context.LopHocs.AnyAsync(l => l.Id == lopHocId && l.GiaoVienChuNhiemId == giaoVienId);
        }

        // GET: /GiaoVien/DiemDanh?lopHocId=1&ngay=2026-11-21&tab=diem-danh
        public async Task<IActionResult> Index(int? lopHocId, string? ngay, string? tuNgay, string? denNgay, string? tab)
        {
            int userId = GetCurrentUserId();
            var gv = await _context.GiaoViens.FirstOrDefaultAsync(g => g.TaiKhoanId == userId);
            if (gv == null)
            {
                ViewBag.ErrorMessage = "Chưa liên kết hồ sơ Giáo viên với tài khoản này.";
                return View();
            }

            ViewBag.TenGiaoVien = gv.HoTen;
            string activeTab = tab == "thong-ke" ? "thong-ke" : "diem-danh";
            ViewBag.ActiveTab = activeTab;

            // Danh sách các lớp mà GV làm chủ nhiệm hoặc được phân công giảng dạy
            var dsLop = await _context.LopHocs
                .Include(l => l.KhoiLop)
                .Where(l => l.GiaoVienChuNhiemId == gv.Id ||
                            _context.PhanCongGiangDays.Any(p => p.LopHocId == l.Id && p.GiaoVienId == gv.Id))
                .OrderByDescending(l => l.GiaoVienChuNhiemId == gv.Id)
                .ThenBy(l => l.KhoiLopId)
                .ThenBy(l => l.TenLop)
                .ToListAsync();

            ViewBag.DanhSachLop = dsLop;

            int selectedLopId = (lopHocId.HasValue && dsLop.Any(l => l.Id == lopHocId.Value))
                ? lopHocId.Value
                : (dsLop.FirstOrDefault()?.Id ?? 0);
            ViewBag.SelectedLopId = selectedLopId;
            ViewBag.GiaoVienId = gv.Id;
            ViewBag.LaGvcn = dsLop.Any(l => l.Id == selectedLopId && l.GiaoVienChuNhiemId == gv.Id);

            var today = DateOnly.FromDateTime(DateTime.Now);
            var selectedDate = !string.IsNullOrWhiteSpace(ngay) && DateOnly.TryParse(ngay, out var d) ? d : today;
            var fromDate = !string.IsNullOrWhiteSpace(tuNgay) && DateOnly.TryParse(tuNgay, out var parsedTu)
                ? parsedTu
                : new DateOnly(selectedDate.Year, selectedDate.Month, 1);
            var toDate = !string.IsNullOrWhiteSpace(denNgay) && DateOnly.TryParse(denNgay, out var parsedDen)
                ? parsedDen
                : selectedDate;

            if (fromDate > toDate) (fromDate, toDate) = (toDate, fromDate);

            ViewBag.SelectedDate = selectedDate;
            ViewBag.TuNgay = fromDate;
            ViewBag.DenNgay = toDate;

            // Danh sách học sinh đang học của lớp đang chọn
            var dsHocSinh = await _context.HocSinhs
                .Where(h => h.LopHocHienTaiId == selectedLopId && h.TrangThaiHoc == "dang_hoc" && h.NgayXoaMem == null)
                .OrderBy(h => h.HoTen)
                .ToListAsync();

            ViewBag.DanhSachHocSinh = dsHocSinh;
            var hsIds = dsHocSinh.Select(h => h.Id).ToList();

            // 1. Bản ghi điểm danh trong ngày đang chọn (chuẩn hóa trạng thái)
            var dsDiemDanhNgay = await _context.DiemDanhs
                .Include(dd => dd.GiaoVienChot)
                .Where(dd => dd.NgayDiemDanh == selectedDate && dd.HocSinhId.HasValue && hsIds.Contains(dd.HocSinhId.Value))
                .ToListAsync();

            var diemDanhRecordMap = dsDiemDanhNgay
                .GroupBy(dd => dd.HocSinhId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ThoiGianCapNhat).First());

            var diemDanhStatusMap = diemDanhRecordMap
                .ToDictionary(kv => kv.Key, kv => NormalizeStatus(kv.Value.TrangThai));

            ViewBag.DiemDanhMap = diemDanhStatusMap;
            ViewBag.DiemDanhRecordMap = diemDanhRecordMap;

            ViewBag.SoCoMat = diemDanhStatusMap.Values.Count(v => v == "co_mat");
            ViewBag.SoNghiCoPhep = diemDanhStatusMap.Values.Count(v => v == "nghi_co_phep");
            ViewBag.SoNghiKhongPhep = diemDanhStatusMap.Values.Count(v => v == "nghi_khong_phep");
            ViewBag.SoChuaDiemDanh = dsHocSinh.Count - diemDanhStatusMap.Values.Count(v => v != null);

            // 2. Thống kê chuyên cần lớp theo khoảng thời gian (Tab 2)
            var dsDiemDanhKhoang = await _context.DiemDanhs
                .Where(dd => dd.NgayDiemDanh >= fromDate && dd.NgayDiemDanh <= toDate &&
                             dd.HocSinhId.HasValue && hsIds.Contains(dd.HocSinhId.Value))
                .ToListAsync();

            var ddByHs = dsDiemDanhKhoang
                .GroupBy(dd => dd.HocSinhId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var thongKeLop = dsHocSinh.Select(hs =>
            {
                var recs = ddByHs.TryGetValue(hs.Id, out var list) ? list : new List<DiemDanh>();
                int coMat = recs.Count(x => NormalizeStatus(x.TrangThai) == "co_mat");
                int coPhep = recs.Count(x => NormalizeStatus(x.TrangThai) == "nghi_co_phep");
                int khongPhep = recs.Count(x => NormalizeStatus(x.TrangThai) == "nghi_khong_phep");
                int tong = coMat + coPhep + khongPhep;
                double tiLe = tong > 0 ? Math.Round((double)coMat / tong * 100, 1) : 0;
                return new GiaoVienThongKeHocSinhItem
                {
                    HocSinhId = hs.Id,
                    MaHocSinh = hs.MaHocSinh ?? "---",
                    HoTen = hs.HoTen ?? "---",
                    SoBuoiCoMat = coMat,
                    SoBuoiCoPhep = coPhep,
                    SoBuoiKhongPhep = khongPhep,
                    TongBuoi = tong,
                    TiLeChuyenCan = tiLe,
                    CanhBao = khongPhep >= 2 || (coPhep + khongPhep) >= 4
                };
            })
            .OrderByDescending(x => x.SoBuoiKhongPhep)
            .ThenByDescending(x => x.SoBuoiCoPhep)
            .ThenBy(x => x.HoTen)
            .ToList();

            ViewBag.ThongKeLop = thongKeLop;

            return View();
        }

        // POST: /GiaoVien/DiemDanh/CapNhat
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CapNhat(int hocSinhId, int lopHocId, DateOnly ngayDiemDanh, string trangThai)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            if (ngayDiemDanh > today)
                return Json(new { success = false, message = "Không được phép điểm danh cho ngày trong tương lai!" });

            var normalized = NormalizeStatus(trangThai);
            if (normalized == null)
                return Json(new { success = false, message = "Trạng thái điểm danh không hợp lệ!" });

            int userId = GetCurrentUserId();
            var gv = await _context.GiaoViens.FirstOrDefaultAsync(g => g.TaiKhoanId == userId);
            if (gv == null)
                return Json(new { success = false, message = "Không tìm thấy hồ sơ giáo viên!" });

            if (!await LaGiaoVienChuNhiemAsync(gv.Id, lopHocId))
                return Json(new { success = false, message = "Chỉ Giáo viên chủ nhiệm của lớp mới có quyền điểm danh đầu buổi!" });

            using var trans = await _context.Database.BeginTransactionAsync();
            try
            {
                bool locked = await _appLockService.AcquireLockAsync(trans, $"DiemDanh_{hocSinhId}_{ngayDiemDanh:yyyyMMdd}", 5);
                if (!locked)
                    return Json(new { success = false, message = "Bản ghi đang được xử lý bởi người khác!" });

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

        // POST: /GiaoVien/DiemDanh/DiemDanhCaLop
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DiemDanhCaLop(int lopHocId, DateOnly ngayDiemDanh, string trangThai, bool chiChuaDiemDanh = true)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            if (ngayDiemDanh > today)
                return Json(new { success = false, message = "Không được phép điểm danh cho ngày trong tương lai!" });

            var normalized = NormalizeStatus(trangThai);
            if (normalized == null)
                return Json(new { success = false, message = "Trạng thái điểm danh không hợp lệ!" });

            int userId = GetCurrentUserId();
            var gv = await _context.GiaoViens.FirstOrDefaultAsync(g => g.TaiKhoanId == userId);
            if (gv == null)
                return Json(new { success = false, message = "Không tìm thấy hồ sơ giáo viên!" });

            if (!await LaGiaoVienChuNhiemAsync(gv.Id, lopHocId))
                return Json(new { success = false, message = "Chỉ Giáo viên chủ nhiệm của lớp mới có quyền điểm danh đầu buổi!" });

            var dsHocSinh = await _context.HocSinhs
                .Where(h => h.LopHocHienTaiId == lopHocId && h.TrangThaiHoc == "dang_hoc" && h.NgayXoaMem == null)
                .ToListAsync();

            if (!dsHocSinh.Any())
                return Json(new { success = false, message = "Lớp học chưa có học sinh đang học!" });

            using var trans = await _context.Database.BeginTransactionAsync();
            try
            {
                bool locked = await _appLockService.AcquireLockAsync(trans, $"DiemDanhLop_{lopHocId}_{ngayDiemDanh:yyyyMMdd}", 10);
                if (!locked)
                    return Json(new { success = false, message = "Lớp học đang được điểm danh bởi người khác!" });

                var hsIds = dsHocSinh.Select(h => h.Id).ToList();
                var existingMap = await _context.DiemDanhs
                    .Where(d => d.NgayDiemDanh == ngayDiemDanh && d.HocSinhId.HasValue && hsIds.Contains(d.HocSinhId.Value))
                    .ToDictionaryAsync(d => d.HocSinhId!.Value);

                var now = DateTime.Now;
                int count = 0;

                foreach (var hs in dsHocSinh)
                {
                    if (existingMap.TryGetValue(hs.Id, out var dd))
                    {
                        if (chiChuaDiemDanh) continue;
                        dd.LopHocId = lopHocId;
                        dd.TrangThai = normalized;
                        dd.GiaoVienChotId = gv.Id;
                        dd.ThoiGianCapNhat = now;
                        count++;
                    }
                    else
                    {
                        _context.DiemDanhs.Add(new DiemDanh
                        {
                            HocSinhId = hs.Id,
                            LopHocId = lopHocId,
                            NgayDiemDanh = ngayDiemDanh,
                            TrangThai = normalized,
                            GiaoVienChotId = gv.Id,
                            ThoiGianCapNhat = now
                        });
                        count++;
                    }
                }

                await _context.SaveChangesAsync();
                await trans.CommitAsync();

                return Json(new { success = true, message = $"Đã cập nhật điểm danh cho {count} học sinh!" });
            }
            catch (Exception ex)
            {
                await trans.RollbackAsync();
                return Json(new { success = false, message = "Lỗi điểm danh cả lớp: " + ex.Message });
            }
        }

        // POST: /GiaoVien/DiemDanh/XoaBanGhi
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XoaBanGhi(int id, int lopHocId)
        {
            int userId = GetCurrentUserId();
            var gv = await _context.GiaoViens.FirstOrDefaultAsync(g => g.TaiKhoanId == userId);
            if (gv == null || !await LaGiaoVienChuNhiemAsync(gv.Id, lopHocId))
                return Json(new { success = false, message = "Chỉ Giáo viên chủ nhiệm của lớp mới có quyền hủy bản ghi điểm danh!" });

            var dd = await _context.DiemDanhs.FirstOrDefaultAsync(d => d.Id == id && d.LopHocId == lopHocId);
            if (dd == null)
                return Json(new { success = false, message = "Không tìm thấy bản ghi điểm danh!" });

            _context.DiemDanhs.Remove(dd);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã hủy bản ghi điểm danh!" });
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out int id) ? id : 0;
        }
    }

    public class GiaoVienThongKeHocSinhItem
    {
        public int HocSinhId { get; set; }
        public string MaHocSinh { get; set; } = string.Empty;
        public string HoTen { get; set; } = string.Empty;
        public int SoBuoiCoMat { get; set; }
        public int SoBuoiCoPhep { get; set; }
        public int SoBuoiKhongPhep { get; set; }
        public int TongBuoi { get; set; }
        public double TiLeChuyenCan { get; set; }
        public bool CanhBao { get; set; }
    }
}
