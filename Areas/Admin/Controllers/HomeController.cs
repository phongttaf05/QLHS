using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHS.Models;

namespace QLHS.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class HomeController : Controller
    {
        private readonly QlhsContext _context;

        public HomeController(QlhsContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // 1. Thống kê tổng số lượng
            ViewBag.TongHocSinh = await _context.HocSinhs.CountAsync(h => h.NgayXoaMem == null);
            ViewBag.TongGiaoVien = await _context.GiaoViens.CountAsync();
            ViewBag.TongLopHoc = await _context.LopHocs.CountAsync();
            ViewBag.TongTaiKhoan = await _context.TaiKhoans.CountAsync(t => t.TrangThai == "hoat_dong" || t.TrangThai == "1");

            // 2. Thống kê số lượng học sinh theo từng khối lớp (dành cho vẽ biểu đồ Chart.js)
            var staticByKhoi = await _context.KhoiLops
                .Select(k => new
                {
                    TenKhoi = "Khối " + k.SoKhoi,
                    SoLuong = _context.HocSinhs.Count(h => h.LopHocHienTai != null && h.LopHocHienTai.KhoiLopId == k.Id && h.NgayXoaMem == null)
                })
                .ToListAsync();

            ViewBag.KhoiLabels = staticByKhoi.Select(x => x.TenKhoi).ToArray();
            ViewBag.KhoiData = staticByKhoi.Select(x => x.SoLuong).ToArray();

            // 3. Lấy danh sách 5 học sinh mới thêm gần đây
            var hocSinhMoi = await _context.HocSinhs
                .Include(h => h.LopHocHienTai)
                .Where(h => h.NgayXoaMem == null)
                .OrderByDescending(h => h.Id)
                .Take(5)
                .ToListAsync();

            return View(hocSinhMoi);
        }
    }
}