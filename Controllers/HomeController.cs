using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHS.Models;

namespace QLHS.Controllers
{
    public class HomeController : Controller
    {
        private readonly QlhsContext _context;

        public HomeController(QlhsContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Lấy thông tin Năm học đang mở khóa từ CSDL
            var namHocHienTai = await _context.NamHocs
                .Where(n => n.DangMoKhoa == true)
                .OrderByDescending(n => n.TenNamHoc)
                .FirstOrDefaultAsync();

            ViewBag.NamHocHienTai = namHocHienTai?.TenNamHoc ?? "2026 - 2027";
            ViewBag.HocKyHienTai = namHocHienTai?.HocKyHienTai ?? 1;

            // Kiểm tra nếu là Phụ huynh -> Lấy danh sách con để làm thẻ tóm tắt
            if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("phu_huynh"))
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdClaim, out int userId))
                {
                    var phuHuynh = await _context.PhuHuynhs.FirstOrDefaultAsync(p => p.TaiKhoanId == userId);
                    if (phuHuynh != null)
                    {
                        ViewBag.HoTenPhuHuynh = phuHuynh.HoTen;

                        var dsCon = await _context.PhuHuynhHocSinhs
                            .Where(ph => ph.PhuHuynhId == phuHuynh.Id)
                            .Include(ph => ph.HocSinh)
                                .ThenInclude(h => h.LopHocHienTai)
                            .Select(ph => ph.HocSinh)
                            .Where(h => h.NgayXoaMem == null)
                            .ToListAsync();

                        ViewBag.DanhSachConSummary = dsCon;
                    }
                }
            }

            return View();
        }
    }
}