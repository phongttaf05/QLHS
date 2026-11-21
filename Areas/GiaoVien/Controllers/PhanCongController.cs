using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHS.Models;
using System.Security.Claims;

namespace QLHS.Areas.GiaoVien.Controllers
{
    [Area("GiaoVien")]
    [Authorize(Roles = "giao_vien,admin")]
    public class PhanCongController : Controller
    {
        private readonly QlhsContext _context;

        public PhanCongController(QlhsContext context)
        {
            _context = context;
        }

        // GET: /GiaoVien/PhanCong
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

            var dsPhanCong = await _context.PhanCongGiangDays
                .Include(p => p.LopHoc)
                .Include(p => p.MonHoc)
                .Include(p => p.HocKy)
                    .ThenInclude(h => h!.NamHoc)
                .Where(p => p.GiaoVienId == gv.Id)
                .OrderBy(p => p.LopHoc!.TenLop)
                .ThenBy(p => p.MonHoc!.TenMon)
                .ToListAsync();

            // Kiểm tra khóa phân công theo đúng các Học kỳ trong danh sách phân công của giáo viên
            var hocKyIds = dsPhanCong.Where(p => p.HocKyId.HasValue).Select(p => p.HocKyId!.Value).Distinct().ToList();
            var banCongBoList = await _context.BanTkbs
                .Where(b => b.TrangThai == "DA_CONG_BO" && b.HocKyId.HasValue && hocKyIds.Contains(b.HocKyId.Value))
                .OrderByDescending(b => b.Id)
                .ToListAsync();

            var lockedHocKyIds = banCongBoList.Select(b => b.HocKyId!.Value).ToHashSet();
            bool daKhoaPhanCong = hocKyIds.Any() && hocKyIds.All(id => lockedHocKyIds.Contains(id));
            var banCongBoMoiNhat = banCongBoList.FirstOrDefault();

            ViewBag.LockedHocKyIds = lockedHocKyIds;
            ViewBag.DaKhoaPhanCong = daKhoaPhanCong;
            ViewBag.ThongBaoKhoa = daKhoaPhanCong
                ? $"Bản Thời khóa biểu số {banCongBoMoiNhat?.SoPhienBan} đã được Ban Giám Hiệu CÔNG BỐ CHÍNH THỨC. Phân công giảng dạy đã được chốt và khóa sổ, không thể thay đổi."
                : null;

            return View(dsPhanCong);
        }

        // POST: /GiaoVien/PhanCong/XacNhan
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> XacNhan(int phanCongId, string trangThai, string? yKien)
        {
            int userId = GetCurrentUserId();
            var gv = await _context.GiaoViens.FirstOrDefaultAsync(g => g.TaiKhoanId == userId);
            if (gv == null) return Json(new { success = false, message = "Bạn không có quyền thao tác!" });

            var pc = await _context.PhanCongGiangDays.FindAsync(phanCongId);
            if (pc == null || pc.GiaoVienId != gv.Id)
            {
                return Json(new { success = false, message = "Không tìm thấy phân công hoặc bạn không phụ trách môn này!" });
            }

            // Chặn sửa đổi khi đã có bản TKB công bố
            bool daCongBo = await _context.BanTkbs.AnyAsync(b => b.HocKyId == pc.HocKyId && b.TrangThai == "DA_CONG_BO");
            if (daCongBo)
            {
                return Json(new { success = false, message = "Thời khóa biểu đã được Ban Giám Hiệu CÔNG BỐ CHÍNH THỨC. Phân công giảng dạy đã được chốt cố định và không thể điều chỉnh!" });
            }

            if (trangThai == "CAN_DIEU_CHINH" && string.IsNullOrWhiteSpace(yKien))
            {
                return Json(new { success = false, message = "Vui lòng nhập lý do/nguyện vọng khi yêu cầu điều chỉnh phân công!" });
            }

            pc.TrangThaiXacNhan = trangThai == "CAN_DIEU_CHINH" ? "CAN_DIEU_CHINH" : "DA_XAC_NHAN";
            pc.NguoiPhanHoiId = userId;
            pc.NgayPhanHoi = DateTime.Now;
            pc.YKien = yKien?.Trim();

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = trangThai == "CAN_DIEU_CHINH" ? "Đã gửi ý kiến đề nghị điều chỉnh đến BGH!" : "Đã xác nhận phân công giảng dạy thành công!" });
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out int id) ? id : 0;
        }
    }
}
