using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QLHS.Controllers
{
    [Authorize(Roles = "admin")] // Chỉ tài khoản có vai_tro = 'admin' mới vào được
    public class AdminController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Index", "Home", new { area = "Admin" });
        }
    }
}