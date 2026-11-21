using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHS.Models;
using QLHS.Services;

namespace QLHS.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class LopHocController : Controller
    {
        private readonly QlhsContext _context;
        private readonly IAppLockService _appLockService;

        public LopHocController(QlhsContext context, IAppLockService appLockService)
        {
            _context = context;
            _appLockService = appLockService;
        }

        // GET: Admin/LopHoc
        public async Task<IActionResult> Index(int? namHocId, int? khoiLopId)
        {
            // Lấy danh sách dropdown lọc
            ViewBag.NamHocs = await _context.NamHocs
                .OrderByDescending(n => n.TenNamHoc)
                .ThenBy(n => n.HocKyHienTai)
                .ToListAsync();

            ViewBag.KhoiLops = await _context.KhoiLops
                .OrderBy(k => k.SoKhoi)
                .ToListAsync();

            ViewBag.GiaoViens = await _context.GiaoViens
                .OrderBy(g => g.HoTen)
                .ToListAsync();

            // Truy vấn danh sách Lớp học có Include các bảng liên quan
            var query = _context.LopHocs
                .Include(l => l.KhoiLop)
                .Include(l => l.NamHoc)
                .Include(l => l.GiaoVienChuNhiem)
                .Include(l => l.HocSinhs.Where(h => h.NgayXoaMem == null && h.TrangThaiHoc == "dang_hoc"))
                .AsQueryable();

            if (namHocId.HasValue && namHocId > 0)
            {
                query = query.Where(l => l.NamHocId == namHocId);
            }

            if (khoiLopId.HasValue && khoiLopId > 0)
            {
                query = query.Where(l => l.KhoiLopId == khoiLopId);
            }

            var listLopHoc = await query
                .OrderBy(l => l.KhoiLop != null ? l.KhoiLop.SoKhoi : 0)
                .ThenBy(l => l.TenLop)
                .ToListAsync();

            ViewBag.CurrentNamHocId = namHocId;
            ViewBag.CurrentKhoiLopId = khoiLopId;

            return View(listLopHoc);
        }

        // GET: Admin/LopHoc/GetById/5
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var lopHoc = await _context.LopHocs.FindAsync(id);
            if (lopHoc == null)
            {
                return Json(new { success = false, message = "Không tìm thấy lớp học!" });
            }

            return Json(new
            {
                success = true,
                data = new
                {
                    lopHoc.Id,
                    lopHoc.TenLop,
                    lopHoc.KhoiLopId,
                    lopHoc.NamHocId,
                    giaVienChuNhiemId = lopHoc.GiaoVienChuNhiemId
                }
            });
        }

        // POST: Admin/LopHoc/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string TenLop, int KhoiLopId, int NamHocId, int? GiaVienChuNhiemId)
        {
            if (string.IsNullOrWhiteSpace(TenLop))
            {
                return Json(new { success = false, message = "Tên lớp không được để trống!" });
            }

            TenLop = TenLop.Trim();

            // Kiểm tra Năm học và Khối lớp hợp lệ
            bool validNamHoc = await _context.NamHocs.AnyAsync(n => n.Id == NamHocId);
            if (!validNamHoc)
            {
                return Json(new { success = false, message = "Năm học không hợp lệ!" });
            }

            bool validKhoi = await _context.KhoiLops.AnyAsync(k => k.Id == KhoiLopId);
            if (!validKhoi)
            {
                return Json(new { success = false, message = "Khối lớp không hợp lệ!" });
            }

            int? gvId = (GiaVienChuNhiemId.HasValue && GiaVienChuNhiemId.Value > 0) ? GiaVienChuNhiemId : null;
            if (gvId.HasValue)
            {
                bool validGv = await _context.GiaoViens.AnyAsync(g => g.Id == gvId.Value);
                if (!validGv)
                {
                    return Json(new { success = false, message = "Giáo viên chủ nhiệm được chọn không tồn tại!" });
                }
            }

            using var trans = await _context.Database.BeginTransactionAsync();
            try
            {
                // Khóa đồng thời tránh tạo 2 lớp cùng tên trong cùng năm học
                bool locked = await _appLockService.AcquireLockAsync(trans, $"LopHoc_{NamHocId}_{TenLop.ToUpper()}", 5);
                if (!locked)
                {
                    return Json(new { success = false, message = "Hệ thống đang xử lý tạo lớp tương tự, vui lòng thử lại!" });
                }

                // RÀNG BUỘC THEO QUY TRÌNH NGHIỆP VỤ: Tên lớp không trùng trong cùng Năm học
                bool exists = await _context.LopHocs.AnyAsync(l => 
                    l.NamHocId == NamHocId && 
                    l.TenLop != null && 
                    l.TenLop.ToLower() == TenLop.ToLower());

                if (exists)
                {
                    return Json(new { success = false, message = $"Tên lớp '{TenLop}' đã tồn tại trong năm học này!" });
                }

                var lopHoc = new LopHoc
                {
                    TenLop = TenLop,
                    KhoiLopId = KhoiLopId,
                    NamHocId = NamHocId,
                    GiaoVienChuNhiemId = gvId
                };

                _context.LopHocs.Add(lopHoc);
                await _context.SaveChangesAsync();
                await trans.CommitAsync();

                return Json(new { success = true, message = $"Thêm lớp '{TenLop}' thành công!" });
            }
            catch (Exception ex)
            {
                await trans.RollbackAsync();
                return Json(new { success = false, message = "Lỗi khi tạo lớp: " + ex.Message });
            }
        }

        // POST: Admin/LopHoc/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string TenLop, int KhoiLopId, int NamHocId, int? GiaVienChuNhiemId)
        {
            var lopHoc = await _context.LopHocs.FindAsync(id);
            if (lopHoc == null)
            {
                return Json(new { success = false, message = "Lớp học không tồn tại!" });
            }

            if (string.IsNullOrWhiteSpace(TenLop))
            {
                return Json(new { success = false, message = "Tên lớp không được để trống!" });
            }

            TenLop = TenLop.Trim();

            // Kiểm tra Năm học và Khối lớp hợp lệ
            bool validNamHoc = await _context.NamHocs.AnyAsync(n => n.Id == NamHocId);
            if (!validNamHoc)
            {
                return Json(new { success = false, message = "Năm học không hợp lệ!" });
            }

            bool validKhoi = await _context.KhoiLops.AnyAsync(k => k.Id == KhoiLopId);
            if (!validKhoi)
            {
                return Json(new { success = false, message = "Khối lớp không hợp lệ!" });
            }

            int? gvId = (GiaVienChuNhiemId.HasValue && GiaVienChuNhiemId.Value > 0) ? GiaVienChuNhiemId : null;
            if (gvId.HasValue)
            {
                bool validGv = await _context.GiaoViens.AnyAsync(g => g.Id == gvId.Value);
                if (!validGv)
                {
                    return Json(new { success = false, message = "Giáo viên chủ nhiệm được chọn không tồn tại!" });
                }
            }

            using var trans = await _context.Database.BeginTransactionAsync();
            try
            {
                bool locked = await _appLockService.AcquireLockAsync(trans, $"LopHoc_{NamHocId}_{TenLop.ToUpper()}", 5);
                if (!locked)
                {
                    return Json(new { success = false, message = "Hệ thống đang xử lý thông tin lớp này, vui lòng thử lại!" });
                }

                // RÀNG BUỘC SỬA: Kiểm tra trùng tên lớp trong cùng năm học với lớp khác
                bool exists = await _context.LopHocs.AnyAsync(l => 
                    l.NamHocId == NamHocId && 
                    l.TenLop != null && 
                    l.TenLop.ToLower() == TenLop.ToLower() && 
                    l.Id != id);

                if (exists)
                {
                    return Json(new { success = false, message = $"Tên lớp '{TenLop}' đã bị trùng với một lớp khác trong cùng năm học!" });
                }

                lopHoc.TenLop = TenLop;
                lopHoc.KhoiLopId = KhoiLopId;
                lopHoc.NamHocId = NamHocId;
                lopHoc.GiaoVienChuNhiemId = gvId;

                _context.LopHocs.Update(lopHoc);
                await _context.SaveChangesAsync();
                await trans.CommitAsync();

                return Json(new { success = true, message = "Cập nhật thông tin lớp học thành công!" });
            }
            catch (Exception ex)
            {
                await trans.RollbackAsync();
                return Json(new { success = false, message = "Lỗi khi cập nhật lớp học: " + ex.Message });
            }
        }

        // POST: Admin/LopHoc/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var lopHoc = await _context.LopHocs.FindAsync(id);
            if (lopHoc == null)
            {
                return Json(new { success = false, message = "Lớp học không tồn tại!" });
            }

            // RÀNG BUỘC 1: Chặn xóa nếu có Học sinh đang học trong lớp
            bool hasStudents = await _context.HocSinhs.AnyAsync(h => h.LopHocHienTaiId == id && h.NgayXoaMem == null);
            if (hasStudents)
            {
                return Json(new { success = false, message = "Ràng buộc dữ liệu: Không thể xóa lớp đang có danh sách Học sinh!" });
            }

            // RÀNG BUỘC 2: Chặn xóa nếu đã có Thời khóa biểu
            bool hasTimetable = await _context.ThoiKhoaBieus.AnyAsync(t => t.LopHocId == id);
            if (hasTimetable)
            {
                return Json(new { success = false, message = "Ràng buộc dữ liệu: Không thể xóa lớp đã được xếp Thời khóa biểu!" });
            }

            // RÀNG BUỘC 3: Chặn xóa nếu đã có Phân công giảng dạy
            bool hasPhanCong = await _context.PhanCongGiangDays.AnyAsync(p => p.LopHocId == id);
            if (hasPhanCong)
            {
                return Json(new { success = false, message = "Ràng buộc dữ liệu: Không thể xóa lớp đã có Phân công giảng dạy!" });
            }

            // RÀNG BUỘC 4: Chặn xóa nếu đã có Điểm danh hoặc Đánh giá
            bool hasDiemDanh = await _context.DiemDanhs.AnyAsync(d => d.LopHocId == id);
            if (hasDiemDanh)
            {
                return Json(new { success = false, message = "Ràng buộc dữ liệu: Không thể xóa lớp đã có dữ liệu Điểm danh!" });
            }

            bool hasDanhGia = await _context.DanhGiaMonHocs.AnyAsync(d => d.LopHocId == id) ||
                              await _context.DanhGiaDinhKyTongHops.AnyAsync(d => d.LopHocId == id);
            if (hasDanhGia)
            {
                return Json(new { success = false, message = "Ràng buộc dữ liệu: Không thể xóa lớp đã có kết quả Đánh giá học tập!" });
            }

            _context.LopHocs.Remove(lopHoc);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Xóa lớp học thành công!" });
        }
    }
}
