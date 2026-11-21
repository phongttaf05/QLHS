using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHS.Models;

namespace QLHS.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class NamHocController : Controller
    {
        private readonly QlhsContext _context;

        public NamHocController(QlhsContext context)
        {
            _context = context;
        }

        // GET: Admin/NamHoc
        public async Task<IActionResult> Index()
        {
            var listNamHoc = await _context.NamHocs
                .OrderByDescending(n => n.TenNamHoc)
                .ThenBy(n => n.HocKyHienTai)
                .ToListAsync();
            return View(listNamHoc);
        }

        // GET: Admin/NamHoc/GetById/5
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var namHoc = await _context.NamHocs.FindAsync(id);
            if (namHoc == null)
            {
                return Json(new { success = false, message = "Không tìm thấy dữ liệu năm học!" });
            }

            return Json(new
            {
                success = true,
                data = new
                {
                    namHoc.Id,
                    namHoc.TenNamHoc,
                    namHoc.HocKyHienTai,
                    namHoc.DangMoKhoa
                }
            });
        }

        // POST: Admin/NamHoc/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string TenNamHoc, int HocKyHienTai, bool DangMoKhoa)
        {
            if (string.IsNullOrWhiteSpace(TenNamHoc))
            {
                return Json(new { success = false, message = "Tên năm học không được để trống!" });
            }

            TenNamHoc = TenNamHoc.Trim();

            // RÀNG BUỘC CẶP (TÊN NĂM HỌC + HỌC KỲ): Cho phép cùng năm học nhưng khác học kỳ
            bool exists = await _context.NamHocs.AnyAsync(n => 
                n.TenNamHoc.ToLower() == TenNamHoc.ToLower() && 
                n.HocKyHienTai == HocKyHienTai);

            if (exists)
            {
                return Json(new { success = false, message = $"Năm học '{TenNamHoc}' - Học kỳ {HocKyHienTai} đã tồn tại trong hệ thống!" });
            }

            var namHoc = new NamHoc
            {
                TenNamHoc = TenNamHoc,
                HocKyHienTai = HocKyHienTai,
                DangMoKhoa = DangMoKhoa
            };

            _context.NamHocs.Add(namHoc);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Thêm mới thành công!" });
        }

        // POST: Admin/NamHoc/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string TenNamHoc, int HocKyHienTai, bool DangMoKhoa)
        {
            var namHoc = await _context.NamHocs.FindAsync(id);
            if (namHoc == null)
            {
                return Json(new { success = false, message = "Năm học không tồn tại!" });
            }

            if (string.IsNullOrWhiteSpace(TenNamHoc))
            {
                return Json(new { success = false, message = "Tên năm học không được để trống!" });
            }

            TenNamHoc = TenNamHoc.Trim();

            // RÀNG BUỘC CẶP KHI SỬA: Cho phép trùng tên nếu khác học kỳ hoặc chính là bản ghi hiện tại
            bool exists = await _context.NamHocs.AnyAsync(n => 
                n.TenNamHoc.ToLower() == TenNamHoc.ToLower() && 
                n.HocKyHienTai == HocKyHienTai && 
                n.Id != id);

            if (exists)
            {
                return Json(new { success = false, message = $"Cấu hình cho '{TenNamHoc}' - Học kỳ {HocKyHienTai} đã tồn tại ở một bản ghi khác!" });
            }

            namHoc.TenNamHoc = TenNamHoc;
            namHoc.HocKyHienTai = HocKyHienTai;
            namHoc.DangMoKhoa = DangMoKhoa;

            _context.NamHocs.Update(namHoc);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Cập nhật thành công!" });
        }

        // POST: Admin/NamHoc/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var namHoc = await _context.NamHocs.FindAsync(id);
            if (namHoc == null)
            {
                return Json(new { success = false, message = "Năm học không tồn tại!" });
            }

            // Ràng buộc dữ liệu: Không cho xóa nếu đã gắn với Lớp học hoặc Học kỳ
            bool hasClasses = await _context.LopHocs.AnyAsync(l => l.NamHocId == id);
            if (hasClasses)
            {
                return Json(new { success = false, message = "Ràng buộc dữ liệu: Không thể xóa vì đã có dữ liệu Lớp học đang liên kết!" });
            }

            bool hasHocKy = await _context.HocKies.AnyAsync(h => h.NamHocId == id);
            if (hasHocKy)
            {
                return Json(new { success = false, message = "Ràng buộc dữ liệu: Không thể xóa vì đã có dữ liệu Học kỳ đang liên kết!" });
            }

            _context.NamHocs.Remove(namHoc);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Xóa năm học thành công!" });
        }
    }
}