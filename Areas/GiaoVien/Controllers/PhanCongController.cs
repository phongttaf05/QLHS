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

        // GET: /GiaoVien/PhanCong (Chỉ xem danh sách phân công giảng dạy của tôi; việc xác nhận lịch thực hiện trên Bản TKB nháp)
        public async Task<IActionResult> Index()
        {
            int userId = GetCurrentUserId();
            var gv = await _context.GiaoViens.FirstOrDefaultAsync(g => g.TaiKhoanId == userId);
            if (gv == null)
            {
                ViewBag.ErrorMessage = "Chưa liên kết hồ sơ Giáo viên với tài khoản này.";
                return View(new List<PhanCongGiangDay>());
            }

            ViewBag.TenGiaoVien = gv.HoTen;

            var dsPhanCong = await _context.PhanCongGiangDays
                .Include(p => p.LopHoc).ThenInclude(l => l!.KhoiLop)
                .Include(p => p.MonHoc)
                .Include(p => p.HocKy).ThenInclude(h => h!.NamHoc)
                .Where(p => p.GiaoVienId == gv.Id)
                .OrderByDescending(p => p.HocKyId)
                .ThenBy(p => p.LopHoc!.TenLop)
                .ThenBy(p => p.MonHoc!.TenMon)
                .ToListAsync();

            // Đếm số bản TKB nháp đang chờ GV này xác nhận để nhắc chuyển sang trang TKB
            ViewBag.SoBanNhapChoXn = await _context.BanTkbXacNhanGvs
                .CountAsync(x => x.GiaoVienId == gv.Id &&
                                 x.BanTkb.TrangThai == "CHO_GV_XAC_NHAN" &&
                                 x.TrangThai == "CHO_XAC_NHAN");

            return View(dsPhanCong);
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out int id) ? id : 0;
        }
    }
}
