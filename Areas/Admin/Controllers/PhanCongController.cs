using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHS.Models;

namespace QLHS.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class PhanCongController : Controller
    {
        private readonly QlhsContext _context;

        public PhanCongController(QlhsContext context)
        {
            _context = context;
        }

        // GET: Admin/PhanCong
        public async Task<IActionResult> Index(int? hocKyId, int? lopHocId, int? giaoVienId, int? monHocId)
        {
            var dsHocKy = await _context.HocKies
                .Include(h => h.NamHoc)
                .OrderByDescending(h => h.Id)
                .ToListAsync();

            int selectedHocKyId = hocKyId ?? dsHocKy.FirstOrDefault()?.Id ?? 0;
            var selectedHocKy = dsHocKy.FirstOrDefault(h => h.Id == selectedHocKyId);

            var lopQuery = _context.LopHocs
                .Include(l => l.KhoiLop)
                .Include(l => l.GiaoVienChuNhiem)
                .AsQueryable();

            if (selectedHocKy?.NamHocId != null)
                lopQuery = lopQuery.Where(l => l.NamHocId == selectedHocKy.NamHocId);

            var dsLop = await lopQuery
                .OrderBy(l => l.KhoiLop != null ? l.KhoiLop.SoKhoi : 0)
                .ThenBy(l => l.TenLop)
                .ToListAsync();

            var dsGiaoVien = await _context.GiaoViens
                .Include(g => g.GiaoVienMonHocs)
                .OrderBy(g => g.HoTen)
                .ToListAsync();
            var dsMonHoc = await _context.MonHocs.OrderBy(m => m.TenMon).ToListAsync();

            var chuyenMonMap = dsGiaoVien.Select(g => new
            {
                id = g.Id,
                hoTen = g.HoTen ?? "---",
                monHocIds = g.GiaoVienMonHocs.Select(gm => gm.MonHocId).ToList()
            }).ToList();
            ViewBag.ChuyenMonJson = System.Text.Json.JsonSerializer.Serialize(chuyenMonMap);

            var query = _context.PhanCongGiangDays
                .Include(p => p.HocKy).ThenInclude(h => h!.NamHoc)
                .Include(p => p.LopHoc).ThenInclude(l => l!.KhoiLop)
                .Include(p => p.MonHoc)
                .Include(p => p.GiaoVien)
                .AsQueryable();

            if (selectedHocKyId > 0)
                query = query.Where(p => p.HocKyId == selectedHocKyId);

            var allInHocKy = await query.ToListAsync();

            // KPI tổng quan phân công trong học kỳ
            ViewBag.TongPhanCong = allInHocKy.Count;
            ViewBag.TongSoTiet = allInHocKy.Sum(p => p.SoTietTuan ?? 0);
            ViewBag.SoLopDaPhanCong = allInHocKy.Where(p => p.LopHocId.HasValue).Select(p => p.LopHocId!.Value).Distinct().Count();
            ViewBag.SoGiaoVienThamGia = allInHocKy.Where(p => p.GiaoVienId.HasValue).Select(p => p.GiaoVienId!.Value).Distinct().Count();

            IEnumerable<PhanCongGiangDay> filtered = allInHocKy;

            if (lopHocId.HasValue && lopHocId > 0)
                filtered = filtered.Where(p => p.LopHocId == lopHocId.Value);

            if (giaoVienId.HasValue && giaoVienId > 0)
                filtered = filtered.Where(p => p.GiaoVienId == giaoVienId.Value);

            if (monHocId.HasValue && monHocId > 0)
                filtered = filtered.Where(p => p.MonHocId == monHocId.Value);

            var result = filtered
                .OrderBy(p => p.LopHoc?.TenLop)
                .ThenBy(p => p.MonHoc?.TenMon)
                .ToList();

            ViewBag.DanhSachHocKy = dsHocKy;
            ViewBag.DanhSachLop = dsLop;
            ViewBag.DanhSachGiaoVien = dsGiaoVien;
            ViewBag.DanhSachMonHoc = dsMonHoc;

            ViewBag.SelectedHocKyId = selectedHocKyId;
            ViewBag.SelectedHocKy = selectedHocKy;
            ViewBag.SelectedLopId = lopHocId;
            ViewBag.SelectedGiaoVienId = giaoVienId;
            ViewBag.SelectedMonId = monHocId;

            return View(result);
        }

        // GET: Admin/PhanCong/GetById/5
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var pc = await _context.PhanCongGiangDays
                .Include(p => p.LopHoc)
                .Include(p => p.MonHoc)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pc == null)
                return Json(new { success = false, message = "Không tìm thấy phân công!" });

            return Json(new
            {
                success = true,
                data = new
                {
                    pc.Id,
                    pc.HocKyId,
                    pc.LopHocId,
                    TenLop = pc.LopHoc?.TenLop ?? "",
                    pc.MonHocId,
                    TenMon = pc.MonHoc?.TenMon ?? "",
                    pc.GiaoVienId,
                    SoTietTuan = pc.SoTietTuan ?? 1,
                    TuNgay = pc.TuNgay?.ToString("yyyy-MM-dd") ?? "",
                    DenNgay = pc.DenNgay?.ToString("yyyy-MM-dd") ?? ""
                }
            });
        }

        // GET: Admin/PhanCong/GetSoTietChuan?hocKyId=1&lopHocId=1&monHocId=1
        [HttpGet]
        public async Task<IActionResult> GetSoTietChuan(int hocKyId, int lopHocId, int monHocId)
        {
            var lop = await _context.LopHocs.FindAsync(lopHocId);
            if (lop == null || !lop.KhoiLopId.HasValue)
                return Json(new { soTiet = 2, gvcnId = (int?)null });

            var cauHinh = await _context.CauHinhMonKhois
                .FirstOrDefaultAsync(c => c.HocKyId == hocKyId && c.KhoiLopId == lop.KhoiLopId && c.MonHocId == monHocId);

            return Json(new
            {
                soTiet = cauHinh?.SoTietTuan ?? 2,
                gvcnId = lop.GiaoVienChuNhiemId
            });
        }

        // POST: Admin/PhanCong/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int hocKyId, int lopHocId, int monHocId, int giaoVienId, short soTietTuan, DateOnly? tuNgay, DateOnly? denNgay)
        {
            var hocKy = await _context.HocKies.FindAsync(hocKyId);
            if (hocKy == null)
                return Json(new { success = false, message = "Học kỳ không hợp lệ!" });

            if (!await _context.LopHocs.AnyAsync(l => l.Id == lopHocId))
                return Json(new { success = false, message = "Lớp học không hợp lệ!" });

            if (!await _context.MonHocs.AnyAsync(m => m.Id == monHocId))
                return Json(new { success = false, message = "Môn học không hợp lệ!" });

            if (!await _context.GiaoViens.AnyAsync(g => g.Id == giaoVienId))
                return Json(new { success = false, message = "Giáo viên không hợp lệ!" });

            if (soTietTuan < 1 || soTietTuan > 15)
                return Json(new { success = false, message = "Số tiết/tuần phải từ 1 đến 15!" });

            bool exists = await _context.PhanCongGiangDays.AnyAsync(p =>
                p.HocKyId == hocKyId && p.LopHocId == lopHocId && p.MonHocId == monHocId);
            if (exists)
                return Json(new { success = false, message = "Lớp và môn học này đã có phân công trong học kỳ! Vui lòng sửa phân công hiện có." });

            var effTu = tuNgay ?? hocKy.TuNgay;
            var effDen = denNgay ?? hocKy.DenNgay;
            if (effTu.HasValue && effDen.HasValue && effTu > effDen)
                return Json(new { success = false, message = "Ngày bắt đầu không được lớn hơn ngày kết thúc!" });

            var pc = new PhanCongGiangDay
            {
                HocKyId = hocKyId,
                LopHocId = lopHocId,
                MonHocId = monHocId,
                GiaoVienId = giaoVienId,
                SoTietTuan = soTietTuan,
                TuNgay = effTu,
                DenNgay = effDen,
                TrangThaiXacNhan = "DA_XAC_NHAN"
            };

            _context.PhanCongGiangDays.Add(pc);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Thêm phân công giảng dạy thành công!" });
        }

        // POST: Admin/PhanCong/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, int giaoVienId, short soTietTuan, DateOnly? tuNgay, DateOnly? denNgay)
        {
            var pc = await _context.PhanCongGiangDays.FindAsync(id);
            if (pc == null)
                return Json(new { success = false, message = "Phân công không tồn tại!" });

            if (!await _context.GiaoViens.AnyAsync(g => g.Id == giaoVienId))
                return Json(new { success = false, message = "Giáo viên được chọn không tồn tại!" });

            if (soTietTuan < 1 || soTietTuan > 15)
                return Json(new { success = false, message = "Số tiết/tuần phải từ 1 đến 15!" });

            if (tuNgay.HasValue && denNgay.HasValue && tuNgay > denNgay)
                return Json(new { success = false, message = "Ngày bắt đầu không được lớn hơn ngày kết thúc!" });

            pc.GiaoVienId = giaoVienId;
            pc.SoTietTuan = soTietTuan;
            if (tuNgay.HasValue) pc.TuNgay = tuNgay;
            if (denNgay.HasValue) pc.DenNgay = denNgay;
            pc.TrangThaiXacNhan = "DA_XAC_NHAN";

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Cập nhật phân công thành công!" });
        }

        // POST: Admin/PhanCong/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var pc = await _context.PhanCongGiangDays.FindAsync(id);
            if (pc == null)
                return Json(new { success = false, message = "Phân công không tồn tại!" });

            bool usedInTkb = await _context.ThoiKhoaBieus.AnyAsync(t => t.PhanCongId == id);
            if (usedInTkb)
                return Json(new { success = false, message = "Không thể xóa: Phân công này đã được xếp trong bảng Thời khóa biểu (bạn vẫn có thể bấm Sửa để đổi giáo viên/số tiết cho đợt xếp mới)!" });

            _context.PhanCongGiangDays.Remove(pc);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã xóa phân công giảng dạy thành công!" });
        }
    }
}
