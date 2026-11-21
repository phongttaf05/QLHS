using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QLHS.Controllers
{
    [Authorize(Roles = "giao_vien,admin")] // Cả giáo viên và admin đều có thể truy cập
    public class GiaoVienController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Index", "Home", new { area = "GiaoVien" });
        }
    }
}