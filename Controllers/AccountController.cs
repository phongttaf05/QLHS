using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHS.Models;

namespace QLHS.Controllers
{
    public class AccountController : Controller
    {
        private readonly QlhsContext _context;

        public AccountController(QlhsContext context)
        {
            _context = context;
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
                return View(model);

            // 1. Kiểm tra tài khoản trong CSDL QLHS
            // (Lưu ý: Thực tế nên kiểm tra mật khẩu đã mã hóa Hash)
            var taiKhoan = await _context.TaiKhoans
                .FirstOrDefaultAsync(u => u.TenDangNhap == model.TenDangNhap && u.MatKhauHash == model.MatKhau);

            if (taiKhoan == null)
            {
                ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không chính xác.");
                return View(model);
            }

            if (taiKhoan.TrangThai == "bi_khoa" || taiKhoan.TrangThai == "0")
            {
                ModelState.AddModelError("", "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ Admin.");
                return View(model);
            }

            // 2. Tạo danh sách Claims (Thông tin người dùng lưu vào Cookie)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, taiKhoan.Id.ToString()),
                new Claim(ClaimTypes.Name, taiKhoan.TenDangNhap),
                new Claim(ClaimTypes.Role, taiKhoan.VaiTro ?? "phu_huynh") // Gán Vai trò (admin, giao_vien, phu_huynh)
            };

            // Tìm tên hiển thị tương ứng theo Role để đưa vào Claim
            if (taiKhoan.VaiTro == "giao_vien")
            {
                var gv = await _context.GiaoViens.FirstOrDefaultAsync(g => g.TaiKhoanId == taiKhoan.Id);
                if (gv != null) claims.Add(new Claim("FullName", gv.HoTen));
            }
            else if (taiKhoan.VaiTro == "phu_huynh")
            {
                var ph = await _context.PhuHuynhs.FirstOrDefaultAsync(p => p.TaiKhoanId == taiKhoan.Id);
                if (ph != null) claims.Add(new Claim("FullName", ph.HoTen));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.GhiNho,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            };

            // 3. Đăng nhập và tạo Cookie
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // 4. Điều hướng sau khi đăng nhập thành công
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return taiKhoan.VaiTro switch
            {
                "admin" => RedirectToAction("Index", "Home", new { area = "Admin" }),
                "giao_vien" => RedirectToAction("Index", "Home", new { area = "GiaoVien" }),
                _ => RedirectToAction("Index", "PhuHuynh") // Phụ huynh nằm ở tổng (không có area)
            };
        }

        // GET/POST: /Account/Logout
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        // GET: /Account/AccessDenied
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}