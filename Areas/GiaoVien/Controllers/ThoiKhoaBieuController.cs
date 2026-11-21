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

        // GET: /GiaoVien/ThoiKhoaBieu
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

            // Ưu tiên tìm bản TKB đã công bố có chứa tiết dạy của GV và đang trong thời gian hiệu lực
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

            var banTkb = dsBanCongBo.FirstOrDefault(b =>
                    banCoGvIds.Contains(b.Id) &&
                    (!b.TuNgay.HasValue || b.TuNgay.Value <= today) &&
                    (!b.DenNgay.HasValue || b.DenNgay.Value >= today))
                ?? dsBanCongBo.FirstOrDefault(b => banCoGvIds.Contains(b.Id))
                ?? dsBanCongBo.FirstOrDefault();

            if (banTkb == null)
            {
                ViewBag.ThongBao = "Hiện chưa có Bản Thời khóa biểu nào được BGH phê duyệt & công bố chính thức.";
                return View();
            }

            ViewBag.BanTkb = banTkb;

            // Lấy toàn bộ các khung tiết của học kỳ
            var dsKhungTiet = await _context.KhungTietHocs
                .Where(k => k.HocKyId == banTkb.HocKyId)
                .OrderBy(k => k.ThuTrongTuan)
                .ThenBy(k => k.BuoiHoc == "SANG" ? 1 : 2)
                .ThenBy(k => k.TietHoc)
                .ToListAsync();

            ViewBag.DsKhungTiet = dsKhungTiet;

            // Lấy đầy đủ các tiết dạy của giáo viên (gồm cả tiết không gắn PhanCongId như Sinh hoạt lớp)
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

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out int id) ? id : 0;
        }
    }
}
