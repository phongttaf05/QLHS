using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHS.Models;

namespace QLHS.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class MonHocController : Controller
    {
        private readonly QlhsContext _context;

        public MonHocController(QlhsContext context)
        {
            _context = context;
        }

        // GET: Admin/MonHoc
        public async Task<IActionResult> Index()
        {
            var listMonHoc = await _context.MonHocs
                .OrderBy(m => m.Id)
                .ToListAsync();

            return View(listMonHoc);
        }

        // GET: Admin/MonHoc/GetById/5
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var monHoc = await _context.MonHocs.FindAsync(id);
            if (monHoc == null)
            {
                return Json(new { success = false, message = "Không tìm thấy môn học!" });
            }

            return Json(new
            {
                success = true,
                data = new
                {
                    monHoc.Id,
                    monHoc.TenMon,
                    monHoc.HinhThucDanhGia
                }
            });
        }

        // POST: Admin/MonHoc/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string TenMon, string HinhThucDanhGia)
        {
            // RÀNG BUỘC 1: Kiểm tra rỗng
            if (string.IsNullOrWhiteSpace(TenMon))
            {
                return Json(new { success = false, message = "Tên môn học không được để trống!" });
            }

            TenMon = TenMon.Trim();

            // RÀNG BUỘC 2: Kiểm tra trùng tên môn học
            bool exists = await _context.MonHocs.AnyAsync(m => m.TenMon.ToLower() == TenMon.ToLower());
            if (exists)
            {
                return Json(new { success = false, message = $"Môn học '{TenMon}' đã tồn tại trong hệ thống!" });
            }

            // RÀNG BUỘC 3: Hình thức đánh giá hợp lệ theo Thông tư 27
            if (HinhThucDanhGia != "diem_nhan_xet" && HinhThucDanhGia != "chi_nhan_xet")
            {
                return Json(new { success = false, message = "Hình thức đánh giá không hợp lệ!" });
            }

            var monHoc = new MonHoc
            {
                TenMon = TenMon,
                HinhThucDanhGia = HinhThucDanhGia
            };

            _context.MonHocs.Add(monHoc);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Thêm môn học '{TenMon}' thành công!" });
        }

        // POST: Admin/MonHoc/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string TenMon, string HinhThucDanhGia)
        {
            var monHoc = await _context.MonHocs.FindAsync(id);
            if (monHoc == null)
            {
                return Json(new { success = false, message = "Môn học không tồn tại!" });
            }

            // RÀNG BUỘC 1: Kiểm tra rỗng
            if (string.IsNullOrWhiteSpace(TenMon))
            {
                return Json(new { success = false, message = "Tên môn học không được để trống!" });
            }

            TenMon = TenMon.Trim();

            // RÀNG BUỘC 2: Kiểm tra trùng tên môn với bản ghi khác
            bool exists = await _context.MonHocs.AnyAsync(m => m.TenMon.ToLower() == TenMon.ToLower() && m.Id != id);
            if (exists)
            {
                return Json(new { success = false, message = $"Tên môn học '{TenMon}' đã bị trùng với môn khác!" });
            }

            // RÀNG BUỘC 3: Hình thức đánh giá hợp lệ theo Thông tư 27
            if (HinhThucDanhGia != "diem_nhan_xet" && HinhThucDanhGia != "chi_nhan_xet")
            {
                return Json(new { success = false, message = "Hình thức đánh giá không hợp lệ!" });
            }

            monHoc.TenMon = TenMon;
            monHoc.HinhThucDanhGia = HinhThucDanhGia;

            _context.MonHocs.Update(monHoc);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Cập nhật môn học thành công!" });
        }

        // POST: Admin/MonHoc/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var monHoc = await _context.MonHocs.FindAsync(id);
            if (monHoc == null)
            {
                return Json(new { success = false, message = "Môn học không tồn tại!" });
            }

            // RÀNG BUỘC DỮ LIỆU 1: Chặn xóa nếu có Cấu hình môn học theo Khối
            bool hasConfig = await _context.CauHinhMonKhois.AnyAsync(c => c.MonHocId == id);
            if (hasConfig)
            {
                return Json(new { success = false, message = "Ràng buộc dữ liệu: Không thể xóa vì môn học đang thuộc Cấu hình môn theo khối!" });
            }

            // RÀNG BUỘC DỮ LIỆU 2: Chặn xóa nếu có Phân công giảng dạy
            bool hasAssigned = await _context.PhanCongGiangDays.AnyAsync(p => p.MonHocId == id);
            if (hasAssigned)
            {
                return Json(new { success = false, message = "Ràng buộc dữ liệu: Không thể xóa vì môn học đã có Phân công giảng dạy!" });
            }

            // RÀNG BUỘC DỮ LIỆU 3: Chặn xóa nếu đã có Bảng điểm đánh giá môn học
            bool hasGrades = await _context.DanhGiaMonHocs.AnyAsync(d => d.MonHocId == id);
            if (hasGrades)
            {
                return Json(new { success = false, message = "Ràng buộc dữ liệu: Không thể xóa vì môn học đã có kết quả Đánh giá học sinh!" });
            }

            // RÀNG BUỘC DỮ LIỆU 4: Chặn xóa nếu đã có trong Thời khóa biểu
            bool hasTkb = await _context.ThoiKhoaBieus.AnyAsync(t => t.MonHocId == id);
            if (hasTkb)
            {
                return Json(new { success = false, message = "Ràng buộc dữ liệu: Không thể xóa vì môn học đã được xếp trong Thời khóa biểu!" });
            }

            _context.MonHocs.Remove(monHoc);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Xóa môn học thành công!" });
        }
    }
}