using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHS.Models;
using System.Security.Claims;

namespace QLHS.Areas.GiaoVien.Controllers
{
    [Area("GiaoVien")]
    [Authorize(Roles = "giao_vien,admin")]
    public class LopChuNhiemController : Controller
    {
        private readonly QlhsContext _context;

        public LopChuNhiemController(QlhsContext context)
        {
            _context = context;
        }

        // GET: /GiaoVien/LopChuNhiem
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

            var dsLop = await _context.LopHocs
                .Include(l => l.KhoiLop)
                .Include(l => l.NamHoc)
                .Include(l => l.HocSinhs.Where(h => h.NgayXoaMem == null && h.TrangThaiHoc == "dang_hoc"))
                .Where(l => l.GiaoVienChuNhiemId == gv.Id)
                .ToListAsync();

            return View(dsLop);
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out int id) ? id : 0;
        }
    }
}
