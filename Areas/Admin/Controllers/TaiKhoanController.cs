using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHS.Models;

namespace QLHS.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class TaiKhoanController : Controller
    {
        private readonly QlhsContext _context;

        public TaiKhoanController(QlhsContext context)
        {
            _context = context;
        }

        // GET: Admin/TaiKhoan
        public async Task<IActionResult> Index()
        {
            var listTaiKhoan = await _context.TaiKhoans
                .OrderByDescending(t => t.Id)
                .ToListAsync();
            return View(listTaiKhoan);
        }

        // GET: Admin/TaiKhoan/GetById/5 (Dùng cho Modal Sửa)
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var taiKhoan = await _context.TaiKhoans.FindAsync(id);
            if (taiKhoan == null)
            {
                return Json(new { success = false, message = "Không tìm thấy tài khoản!" });
            }

            return Json(new
            {
                success = true,
                data = new
                {
                    taiKhoan.Id,
                    taiKhoan.TenDangNhap,
                    taiKhoan.Email,
                    taiKhoan.SoDienThoai,
                    taiKhoan.VaiTro,
                    taiKhoan.TrangThai
                }
            });
        }

        // POST: Admin/TaiKhoan/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TenDangNhap,MatKhauHash,Email,SoDienThoai,VaiTro,TrangThai")] TaiKhoan model)
        {
            if (string.IsNullOrWhiteSpace(model.TenDangNhap) || string.IsNullOrWhiteSpace(model.MatKhauHash))
            {
                return Json(new { success = false, message = "Tên đăng nhập và mật khẩu không được để trống!" });
            }

            // Kiểm tra trùng Tên đăng nhập
            bool exists = await _context.TaiKhoans.AnyAsync(t => t.TenDangNhap == model.TenDangNhap);
            if (exists)
            {
                return Json(new { success = false, message = "Tên đăng nhập này đã tồn tại trong hệ thống!" });
            }

            model.NgayTao = DateTime.Now;
            if (string.IsNullOrEmpty(model.TrangThai)) model.TrangThai = "hoat_dong";

            _context.Add(model);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Thêm mới tài khoản thành công!" });
        }

        // POST: Admin/TaiKhoan/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string? MatKhauMoi, string? Email, string? SoDienThoai, string VaiTro, string TrangThai)
        {
            var taiKhoan = await _context.TaiKhoans.FindAsync(id);
            if (taiKhoan == null)
            {
                return Json(new { success = false, message = "Tài khoản không tồn tại!" });
            }

            // Cập nhật thông tin
            taiKhoan.Email = Email;
            taiKhoan.SoDienThoai = SoDienThoai;
            taiKhoan.VaiTro = VaiTro;
            taiKhoan.TrangThai = TrangThai;

            // Nếu nhập mật khẩu mới thì cập nhật, không thì giữ nguyên
            if (!string.IsNullOrWhiteSpace(MatKhauMoi))
            {
                taiKhoan.MatKhauHash = MatKhauMoi;
            }

            _context.Update(taiKhoan);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Cập nhật tài khoản thành công!" });
        }

        // POST: Admin/TaiKhoan/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var currentIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(currentIdClaim, out int currentUserId) && currentUserId == id)
            {
                return Json(new { success = false, message = "Không thể tự xóa tài khoản đang đăng nhập!" });
            }

            var taiKhoan = await _context.TaiKhoans.FindAsync(id);
            if (taiKhoan == null)
            {
                return Json(new { success = false, message = "Tài khoản không tồn tại!" });
            }

            if (await _context.GiaoViens.AnyAsync(g => g.TaiKhoanId == id))
                return Json(new { success = false, message = "Không thể xóa: Tài khoản đang liên kết với hồ sơ Giáo viên!" });

            if (await _context.PhuHuynhs.AnyAsync(p => p.TaiKhoanId == id))
                return Json(new { success = false, message = "Không thể xóa: Tài khoản đang liên kết với hồ sơ Phụ huynh!" });

            if (await _context.BanTkbs.AnyAsync(b => b.NguoiChotId == id || b.NguoiDuyetId == id || b.NguoiCongBoId == id))
                return Json(new { success = false, message = "Không thể xóa: Tài khoản đã tham gia chốt/duyệt/công bố Thời khóa biểu!" });

            if (await _context.PhanCongGiangDays.AnyAsync(p => p.NguoiPhanHoiId == id))
                return Json(new { success = false, message = "Không thể xóa: Tài khoản đã có lịch sử phản hồi Phân công giảng dạy!" });

            _context.TaiKhoans.Remove(taiKhoan);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Xóa tài khoản thành công!" });
        }
    }
}