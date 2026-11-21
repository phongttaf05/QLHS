using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHS.Models;

namespace QLHS.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class KhoiLopController : Controller
    {
        private readonly QlhsContext _context;

        public KhoiLopController(QlhsContext context)
        {
            _context = context;
        }

        // GET: Admin/KhoiLop
        public async Task<IActionResult> Index()
        {
            var listKhoiLop = await _context.KhoiLops
                .Include(k => k.LopHocs)
                .OrderBy(k => k.SoKhoi)
                .ToListAsync();

            return View(listKhoiLop);
        }

        // GET: Admin/KhoiLop/GetById/5
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var khoiLop = await _context.KhoiLops.FindAsync(id);
            if (khoiLop == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thông tin khối lớp!" });
            }

            return Json(new
            {
                success = true,
                data = new
                {
                    khoiLop.Id,
                    khoiLop.SoKhoi
                }
            });
        }

        // POST: Admin/KhoiLop/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int SoKhoi)
        {
            // RÀNG BUỘC 1: Kiểm tra miền giá trị khối lớp tiểu học (1 - 5)
            if (SoKhoi < 1 || SoKhoi > 5)
            {
                return Json(new { success = false, message = "Khối lớp tiểu học chỉ chấp nhận giá trị từ Khối 1 đến Khối 5!" });
            }

            // RÀNG BUỘC 2: Kiểm tra trùng khối lớp
            bool exists = await _context.KhoiLops.AnyAsync(k => k.SoKhoi == SoKhoi);
            if (exists)
            {
                return Json(new { success = false, message = $"Khối {SoKhoi} đã tồn tại trong hệ thống!" });
            }

            var khoiLop = new KhoiLop
            {
                SoKhoi = SoKhoi
            };

            _context.KhoiLops.Add(khoiLop);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Thêm Khối {SoKhoi} thành công!" });
        }

        // POST: Admin/KhoiLop/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, int SoKhoi)
        {
            var khoiLop = await _context.KhoiLops.FindAsync(id);
            if (khoiLop == null)
            {
                return Json(new { success = false, message = "Khối lớp không tồn tại!" });
            }

            // RÀNG BUỘC 1: Kiểm tra miền giá trị
            if (SoKhoi < 1 || SoKhoi > 5)
            {
                return Json(new { success = false, message = "Khối lớp tiểu học chỉ từ Khối 1 đến Khối 5!" });
            }

            // RÀNG BUỘC 2: Kiểm tra trùng khối lớp khác
            bool exists = await _context.KhoiLops.AnyAsync(k => k.SoKhoi == SoKhoi && k.Id != id);
            if (exists)
            {
                return Json(new { success = false, message = $"Khối {SoKhoi} đã tồn tại ở bản ghi khác!" });
            }

            khoiLop.SoKhoi = SoKhoi;

            _context.KhoiLops.Update(khoiLop);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Cập nhật khối lớp thành công!" });
        }

        // POST: Admin/KhoiLop/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var khoiLop = await _context.KhoiLops.FindAsync(id);
            if (khoiLop == null)
            {
                return Json(new { success = false, message = "Khối lớp không tồn tại!" });
            }

            // RÀNG BUỘC DỮ LIỆU 1: Chặn xóa nếu đã có Lớp học thuộc khối này
            bool hasClasses = await _context.LopHocs.AnyAsync(l => l.KhoiLopId == id);
            if (hasClasses)
            {
                return Json(new { success = false, message = "Ràng buộc dữ liệu: Không thể xóa Khối lớp này vì đang có Lớp học thuộc khối!" });
            }

            // RÀNG BUỘC DỮ LIỆU 2: Chặn xóa nếu đã có Cấu hình môn học theo khối
            bool hasSubjectConfig = await _context.CauHinhMonKhois.AnyAsync(c => c.KhoiLopId == id);
            if (hasSubjectConfig)
            {
                return Json(new { success = false, message = "Ràng buộc dữ liệu: Không thể xóa vì Khối lớp đã có cấu hình Môn học liên kết!" });
            }

            _context.KhoiLops.Remove(khoiLop);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Xóa khối lớp thành công!" });
        }
    }
}