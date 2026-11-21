using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHS.Models;
using System.Security.Claims;

namespace QLHS.Areas.GiaoVien.Controllers
{
    [Area("GiaoVien")]
    [Authorize(Roles = "giao_vien,admin")]
    public class ThoiKhoaBieuController : Controller
    {
        private readonly QlhsContext _context;

        public ThoiKhoaBieuController(QlhsContext context)
        {
            _context = context;
        }

        // GET: /GiaoVien/ThoiKhoaBieu?mode=chinh-thuc&draftId=2
        public async Task<IActionResult> Index(string? mode, int? draftId)
        {
            int userId = GetCurrentUserId();
            var gv = await _context.GiaoViens.FirstOrDefaultAsync(g => g.TaiKhoanId == userId);
            if (gv == null)
            {
                ViewBag.ErrorMessage = "Chưa liên kết hồ sơ Giáo viên với tài khoản này.";
                return View();
            }

            ViewBag.TenGiaoVien = gv.HoTen;

            // 1. Tìm các Bản TKB nháp mà Admin đã gửi cho giáo viên này xác nhận (CHO_GV_XAC_NHAN)
            var dsBanNhapChoXn = await _context.BanTkbXacNhanGvs
                .Include(x => x.BanTkb)
                    .ThenInclude(b => b.HocKy)
                        .ThenInclude(h => h!.NamHoc)
                .Where(x => x.GiaoVienId == gv.Id && x.BanTkb.TrangThai == "CHO_GV_XAC_NHAN")
                .OrderByDescending(x => x.BanTkbId)
                .ToListAsync();

            ViewBag.DsBanNhapChoXn = dsBanNhapChoXn;

            bool xemBanNhap = mode == "nhap" && dsBanNhapChoXn.Any();
            ViewBag.Mode = xemBanNhap ? "nhap" : "chinh-thuc";

            BanTkb? banTkb = null;
            BanTkbXacNhanGv? xacNhanHienTai = null;

            if (xemBanNhap)
            {
                xacNhanHienTai = (draftId.HasValue
                    ? dsBanNhapChoXn.FirstOrDefault(x => x.BanTkbId == draftId.Value)
                    : null) ?? dsBanNhapChoXn.First();
                banTkb = xacNhanHienTai.BanTkb;
                ViewBag.XacNhanHienTai = xacNhanHienTai;
            }
            else
            {
                // Mặc định luôn hiển thị TKB ĐÃ CÔNG BỐ (DA_CONG_BO)
                var today = DateOnly.FromDateTime(DateTime.Now);
                var dsBanCongBo = await _context.BanTkbs
                    .Include(b => b.HocKy)
                        .ThenInclude(h => h!.NamHoc)
                    .Where(b => b.TrangThai == "DA_CONG_BO")
                    .OrderByDescending(b => b.Id)
                    .ToListAsync();

                var banCoGvIds = await _context.ThoiKhoaBieus
                    .Where(t => t.BanTkbId.HasValue && (t.GiaoVienId == gv.Id || (t.PhanCong != null && t.PhanCong.GiaoVienId == gv.Id)))
                    .Select(t => t.BanTkbId!.Value)
                    .Distinct()
                    .ToListAsync();

                banTkb = dsBanCongBo.FirstOrDefault(b =>
                        banCoGvIds.Contains(b.Id) &&
                        (!b.TuNgay.HasValue || b.TuNgay.Value <= today) &&
                        (!b.DenNgay.HasValue || b.DenNgay.Value >= today))
                    ?? dsBanCongBo.FirstOrDefault(b => banCoGvIds.Contains(b.Id))
                    ?? dsBanCongBo.FirstOrDefault();
            }

            if (banTkb == null)
            {
                ViewBag.ThongBao = "Hiện chưa có Bản Thời khóa biểu nào được BGH phê duyệt & công bố chính thức.";
                return View();
            }

            ViewBag.BanTkb = banTkb;

            var dsKhungTiet = await _context.KhungTietHocs
                .Where(k => k.HocKyId == banTkb.HocKyId)
                .OrderBy(k => k.ThuTrongTuan)
                .ThenBy(k => k.BuoiHoc == "SANG" ? 1 : 2)
                .ThenBy(k => k.TietHoc)
                .ToListAsync();

            ViewBag.DsKhungTiet = dsKhungTiet;

            var dsTietDay = await _context.ThoiKhoaBieus
                .Include(t => t.KhungTiet)
                .Include(t => t.MonHoc)
                .Include(t => t.LopHoc)
                .Include(t => t.PhanCong)
                    .ThenInclude(p => p!.MonHoc)
                .Include(t => t.PhanCong)
                    .ThenInclude(p => p!.LopHoc)
                .Where(t => t.BanTkbId == banTkb.Id &&
                            (t.GiaoVienId == gv.Id || (t.PhanCong != null && t.PhanCong.GiaoVienId == gv.Id)))
                .ToListAsync();

            ViewBag.DsTietDay = dsTietDay;
            ViewBag.TongSoTiet = dsTietDay.Count;

            return View();
        }

        // POST: /GiaoVien/ThoiKhoaBieu/PhanHoiBanNhap
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PhanHoiBanNhap(int banTkbId, string trangThai, string? yKien)
        {
            int userId = GetCurrentUserId();
            var gv = await _context.GiaoViens.FirstOrDefaultAsync(g => g.TaiKhoanId == userId);
            if (gv == null)
                return Json(new { success = false, message = "Không tìm thấy hồ sơ giáo viên!" });

            var xacNhan = await _context.BanTkbXacNhanGvs
                .Include(x => x.BanTkb)
                .FirstOrDefaultAsync(x => x.BanTkbId == banTkbId && x.GiaoVienId == gv.Id);

            if (xacNhan == null)
                return Json(new { success = false, message = "Không tìm thấy yêu cầu xác nhận bản nháp dành cho Thầy/Cô!" });

            if (xacNhan.BanTkb.TrangThai != "CHO_GV_XAC_NHAN")
                return Json(new { success = false, message = "Bản TKB nháp này đã kết thúc thời gian lấy ý kiến!" });

            if (trangThai == "CAN_DIEU_CHINH" && string.IsNullOrWhiteSpace(yKien))
                return Json(new { success = false, message = "Vui lòng nhập lý do/nội dung đề nghị sửa đổi!" });

            xacNhan.TrangThai = trangThai == "CAN_DIEU_CHINH" ? "CAN_DIEU_CHINH" : "DA_XAC_NHAN";
            xacNhan.YKien = trangThai == "CAN_DIEU_CHINH" ? yKien?.Trim() : null;
            xacNhan.NgayPhanHoi = DateTime.Now;

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = xacNhan.TrangThai == "DA_XAC_NHAN"
                    ? "Đã xác nhận đồng ý với Bản TKB nháp!"
                    : "Đã gửi yêu cầu sửa đổi bản nháp tới Ban Giám Hiệu!"
            });
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out int id) ? id : 0;
        }
    }
}
