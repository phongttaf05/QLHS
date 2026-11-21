using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHS.Models;

namespace QLHS.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class HocSinhController : Controller
    {
        private readonly QlhsContext _context;

        public HocSinhController(QlhsContext context)
        {
            _context = context;
        }

        // GET: Admin/HocSinh
        public async Task<IActionResult> Index(int? namHocId, int? khoiLopId, int? lopHocId, string? trangThaiHoc, string? search)
        {
            // 1. Dữ liệu các dropdown bộ lọc
            ViewBag.NamHocs = await _context.NamHocs
                .OrderByDescending(n => n.TenNamHoc)
                .ThenBy(n => n.HocKyHienTai)
                .ToListAsync();

            ViewBag.KhoiLops = await _context.KhoiLops
                .OrderBy(k => k.SoKhoi)
                .ToListAsync();

            var lopHocQuery = _context.LopHocs.AsQueryable();
            if (namHocId.HasValue && namHocId > 0)
                lopHocQuery = lopHocQuery.Where(l => l.NamHocId == namHocId);
            if (khoiLopId.HasValue && khoiLopId > 0)
                lopHocQuery = lopHocQuery.Where(l => l.KhoiLopId == khoiLopId);

            ViewBag.LopHocs = await lopHocQuery.OrderBy(l => l.TenLop).ToListAsync();

            // 2. Truy vấn danh sách Học sinh (Lọc bỏ học sinh đã xóa mềm)
            var query = _context.HocSinhs
                .Include(h => h.LopHocHienTai)
                    .ThenInclude(l => l!.KhoiLop)
                .Include(h => h.LopHocHienTai)
                    .ThenInclude(l => l!.NamHoc)
                .Where(h => h.NgayXoaMem == null)
                .AsQueryable();

            if (namHocId.HasValue && namHocId > 0)
            {
                query = query.Where(h => h.LopHocHienTai != null && h.LopHocHienTai.NamHocId == namHocId);
            }

            if (khoiLopId.HasValue && khoiLopId > 0)
            {
                query = query.Where(h => h.LopHocHienTai != null && h.LopHocHienTai.KhoiLopId == khoiLopId);
            }

            if (lopHocId.HasValue && lopHocId > 0)
            {
                query = query.Where(h => h.LopHocHienTaiId == lopHocId);
            }

            if (!string.IsNullOrWhiteSpace(trangThaiHoc))
            {
                query = query.Where(h => h.TrangThaiHoc == trangThaiHoc);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(h => h.MaHocSinh.ToLower().Contains(search) || h.HoTen.ToLower().Contains(search));
            }

            var listHocSinh = await query
                .OrderBy(h => h.LopHocHienTai != null ? h.LopHocHienTai.TenLop : "")
                .ThenBy(h => h.HoTen)
                .ToListAsync();

            ViewBag.CurrentNamHocId = namHocId;
            ViewBag.CurrentKhoiLopId = khoiLopId;
            ViewBag.CurrentLopHocId = lopHocId;
            ViewBag.CurrentTrangThaiHoc = trangThaiHoc;
            ViewBag.CurrentSearch = search;

            return View(listHocSinh);
        }

        // GET: Admin/HocSinh/GetById/5
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var hs = await _context.HocSinhs.FindAsync(id);
            if (hs == null || hs.NgayXoaMem != null)
            {
                return Json(new { success = false, message = "Không tìm thấy học sinh!" });
            }

            return Json(new
            {
                success = true,
                data = new
                {
                    hs.Id,
                    hs.MaHocSinh,
                    hs.HoTen,
                    hs.GioiTinh,
                    NgaySinh = hs.NgaySinh.HasValue ? hs.NgaySinh.Value.ToString("yyyy-MM-dd") : "",
                    hs.TrangThaiHoc,
                    hs.LopHocHienTaiId
                }
            });
        }

        // POST: Admin/HocSinh/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string MaHocSinh, string HoTen, string GioiTinh, DateOnly? NgaySinh, string? TrangThaiHoc, int? LopHocHienTaiId)
        {
            if (string.IsNullOrWhiteSpace(MaHocSinh))
                return Json(new { success = false, message = "Mã học sinh không được để trống!" });

            if (string.IsNullOrWhiteSpace(HoTen))
                return Json(new { success = false, message = "Họ tên học sinh không được để trống!" });

            MaHocSinh = MaHocSinh.Trim().ToUpper();
            HoTen = HoTen.Trim();

            bool exists = await _context.HocSinhs.AnyAsync(h => h.MaHocSinh == MaHocSinh && h.NgayXoaMem == null);
            if (exists)
            {
                return Json(new { success = false, message = $"Mã học sinh '{MaHocSinh}' đã tồn tại!" });
            }

            var hocSinh = new HocSinh
            {
                MaHocSinh = MaHocSinh,
                HoTen = HoTen,
                GioiTinh = string.IsNullOrWhiteSpace(GioiTinh) ? "Nam" : GioiTinh,
                NgaySinh = NgaySinh,
                TrangThaiHoc = string.IsNullOrWhiteSpace(TrangThaiHoc) ? "dang_hoc" : TrangThaiHoc,
                LopHocHienTaiId = LopHocHienTaiId > 0 ? LopHocHienTaiId : null
            };

            _context.HocSinhs.Add(hocSinh);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Thêm học sinh '{HoTen}' thành công!" });
        }

        // POST: Admin/HocSinh/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string MaHocSinh, string HoTen, string GioiTinh, DateOnly? NgaySinh, string? TrangThaiHoc, int? LopHocHienTaiId)
        {
            var hocSinh = await _context.HocSinhs.FindAsync(id);
            if (hocSinh == null || hocSinh.NgayXoaMem != null)
            {
                return Json(new { success = false, message = "Học sinh không tồn tại!" });
            }

            if (string.IsNullOrWhiteSpace(MaHocSinh))
                return Json(new { success = false, message = "Mã học sinh không được để trống!" });

            if (string.IsNullOrWhiteSpace(HoTen))
                return Json(new { success = false, message = "Họ tên không được để trống!" });

            MaHocSinh = MaHocSinh.Trim().ToUpper();
            HoTen = HoTen.Trim();

            bool exists = await _context.HocSinhs.AnyAsync(h => h.MaHocSinh == MaHocSinh && h.Id != id && h.NgayXoaMem == null);
            if (exists)
            {
                return Json(new { success = false, message = $"Mã học sinh '{MaHocSinh}' đã bị trùng!" });
            }

            hocSinh.MaHocSinh = MaHocSinh;
            hocSinh.HoTen = HoTen;
            hocSinh.GioiTinh = GioiTinh;
            hocSinh.NgaySinh = NgaySinh;
            hocSinh.TrangThaiHoc = string.IsNullOrWhiteSpace(TrangThaiHoc) ? "dang_hoc" : TrangThaiHoc;
            hocSinh.LopHocHienTaiId = LopHocHienTaiId > 0 ? LopHocHienTaiId : null;

            _context.HocSinhs.Update(hocSinh);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Cập nhật hồ sơ học sinh thành công!" });
        }

        // POST: Admin/HocSinh/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var hocSinh = await _context.HocSinhs.FindAsync(id);
            if (hocSinh == null || hocSinh.NgayXoaMem != null)
            {
                return Json(new { success = false, message = "Học sinh không tồn tại!" });
            }

            hocSinh.NgayXoaMem = DateTime.Now;
            _context.HocSinhs.Update(hocSinh);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Đã xóa mềm học sinh '{hocSinh.HoTen}'!" });
        }
    }
}