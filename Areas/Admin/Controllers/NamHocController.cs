using System.Text.RegularExpressions;
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

        // Tự động khởi tạo Học kỳ & 35 Khung tiết chuẩn (Thứ 2-6, Sáng 4 tiết, Chiều 3 tiết) cho Năm học
        private async Task EnsureHocKyAndKhungTietAsync(NamHoc namHoc)
        {
            byte soHocKy = (byte)(namHoc.HocKyHienTai == 2 ? 2 : 1);

            var hocKy = await _context.HocKies
                .FirstOrDefaultAsync(h => h.NamHocId == namHoc.Id && h.SoHocKy == soHocKy);

            if (hocKy == null)
            {
                int startYear = DateTime.Now.Year;
                var match = Regex.Match(namHoc.TenNamHoc ?? "", @"(\d{4})");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int parsedYear))
                {
                    startYear = parsedYear;
                }

                DateOnly tuNgay = soHocKy == 1
                    ? new DateOnly(startYear, 9, 5)
                    : new DateOnly(startYear + 1, 1, 16);
                DateOnly denNgay = soHocKy == 1
                    ? new DateOnly(startYear + 1, 1, 15)
                    : new DateOnly(startYear + 1, 5, 31);

                hocKy = new HocKy
                {
                    NamHocId = namHoc.Id,
                    SoHocKy = soHocKy,
                    TuNgay = tuNgay,
                    DenNgay = denNgay
                };
                _context.HocKies.Add(hocKy);
                await _context.SaveChangesAsync();
            }

            bool hasKhungTiet = await _context.KhungTietHocs.AnyAsync(k => k.HocKyId == hocKy.Id);
            if (!hasKhungTiet)
            {
                var sangSlots = new (byte Tiet, TimeOnly Start, TimeOnly End)[]
                {
                    (1, new TimeOnly(7, 30), new TimeOnly(8, 5)),
                    (2, new TimeOnly(8, 15), new TimeOnly(8, 50)),
                    (3, new TimeOnly(9, 15), new TimeOnly(9, 50)),
                    (4, new TimeOnly(10, 0), new TimeOnly(10, 35))
                };

                var chieuSlots = new (byte Tiet, TimeOnly Start, TimeOnly End)[]
                {
                    (1, new TimeOnly(14, 0), new TimeOnly(14, 35)),
                    (2, new TimeOnly(14, 45), new TimeOnly(15, 20)),
                    (3, new TimeOnly(15, 35), new TimeOnly(16, 10))
                };

                for (byte thu = 2; thu <= 6; thu++)
                {
                    foreach (var s in sangSlots)
                    {
                        _context.KhungTietHocs.Add(new KhungTietHoc
                        {
                            HocKyId = hocKy.Id,
                            ThuTrongTuan = thu,
                            BuoiHoc = "SANG",
                            TietHoc = s.Tiet,
                            GioBatDau = s.Start,
                            GioKetThuc = s.End
                        });
                    }
                    foreach (var c in chieuSlots)
                    {
                        _context.KhungTietHocs.Add(new KhungTietHoc
                        {
                            HocKyId = hocKy.Id,
                            ThuTrongTuan = thu,
                            BuoiHoc = "CHIEU",
                            TietHoc = c.Tiet,
                            GioBatDau = c.Start,
                            GioKetThuc = c.End
                        });
                    }
                }

                await _context.SaveChangesAsync();
            }
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

            bool exists = await _context.NamHocs.AnyAsync(n =>
                n.TenNamHoc != null &&
                n.TenNamHoc.ToLower() == TenNamHoc.ToLower() &&
                n.HocKyHienTai == HocKyHienTai);

            if (exists)
            {
                return Json(new { success = false, message = $"Năm học '{TenNamHoc}' - Học kỳ {HocKyHienTai} đã tồn tại trong hệ thống!" });
            }

            using var trans = await _context.Database.BeginTransactionAsync();
            try
            {
                var namHoc = new NamHoc
                {
                    TenNamHoc = TenNamHoc,
                    HocKyHienTai = HocKyHienTai,
                    DangMoKhoa = DangMoKhoa
                };

                _context.NamHocs.Add(namHoc);
                await _context.SaveChangesAsync();

                // Tự động tạo Học kỳ & 35 Khung tiết chuẩn phục vụ lập TKB
                await EnsureHocKyAndKhungTietAsync(namHoc);

                await trans.CommitAsync();
                return Json(new { success = true, message = $"Thêm mới '{TenNamHoc}' (HK {HocKyHienTai}) thành công và đã tự động khởi tạo 35 khung tiết chuẩn!" });
            }
            catch (Exception ex)
            {
                await trans.RollbackAsync();
                return Json(new { success = false, message = "Lỗi khi tạo năm học: " + ex.Message });
            }
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

            bool exists = await _context.NamHocs.AnyAsync(n =>
                n.TenNamHoc != null &&
                n.TenNamHoc.ToLower() == TenNamHoc.ToLower() &&
                n.HocKyHienTai == HocKyHienTai &&
                n.Id != id);

            if (exists)
            {
                return Json(new { success = false, message = $"Cấu hình cho '{TenNamHoc}' - Học kỳ {HocKyHienTai} đã tồn tại ở một bản ghi khác!" });
            }

            using var trans = await _context.Database.BeginTransactionAsync();
            try
            {
                namHoc.TenNamHoc = TenNamHoc;
                namHoc.HocKyHienTai = HocKyHienTai;
                namHoc.DangMoKhoa = DangMoKhoa;

                _context.NamHocs.Update(namHoc);
                await _context.SaveChangesAsync();

                // Đảm bảo học kỳ mới chọn cũng có sẵn Học kỳ & 35 khung tiết chuẩn
                await EnsureHocKyAndKhungTietAsync(namHoc);

                await trans.CommitAsync();
                return Json(new { success = true, message = "Cập nhật thành công!" });
            }
            catch (Exception ex)
            {
                await trans.RollbackAsync();
                return Json(new { success = false, message = "Lỗi khi cập nhật năm học: " + ex.Message });
            }
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

            if (await _context.LopHocs.AnyAsync(l => l.NamHocId == id))
                return Json(new { success = false, message = "Ràng buộc dữ liệu: Không thể xóa vì đã có Lớp học liên kết!" });

            var hocKyIds = await _context.HocKies
                .Where(h => h.NamHocId == id)
                .Select(h => h.Id)
                .ToListAsync();

            if (hocKyIds.Any())
            {
                if (await _context.BanTkbs.AnyAsync(b => b.HocKyId.HasValue && hocKyIds.Contains(b.HocKyId.Value)))
                    return Json(new { success = false, message = "Ràng buộc dữ liệu: Không thể xóa vì đã có Bản Thời khóa biểu liên kết!" });

                if (await _context.PhanCongGiangDays.AnyAsync(p => p.HocKyId.HasValue && hocKyIds.Contains(p.HocKyId.Value)))
                    return Json(new { success = false, message = "Ràng buộc dữ liệu: Không thể xóa vì đã có Phân công giảng dạy liên kết!" });

                if (await _context.CauHinhMonKhois.AnyAsync(c => c.HocKyId.HasValue && hocKyIds.Contains(c.HocKyId.Value)))
                    return Json(new { success = false, message = "Ràng buộc dữ liệu: Không thể xóa vì đã có Cấu hình môn theo khối!" });
            }

            using var trans = await _context.Database.BeginTransactionAsync();
            try
            {
                // Dọn sạch khung tiết & học kỳ rỗng được tự động sinh khi tạo năm học
                if (hocKyIds.Any())
                {
                    var khungTiets = await _context.KhungTietHocs
                        .Where(k => k.HocKyId.HasValue && hocKyIds.Contains(k.HocKyId.Value))
                        .ToListAsync();
                    var khungIds = khungTiets.Select(k => k.Id).ToList();

                    if (khungIds.Any() && await _context.GioBanGiaoViens.AnyAsync(g => g.KhungTietId.HasValue && khungIds.Contains(g.KhungTietId.Value)))
                    {
                        await trans.RollbackAsync();
                        return Json(new { success = false, message = "Ràng buộc dữ liệu: Không thể xóa vì đã có Giờ bận giáo viên đăng ký!" });
                    }

                    if (khungTiets.Any()) _context.KhungTietHocs.RemoveRange(khungTiets);

                    var hocKies = await _context.HocKies.Where(h => hocKyIds.Contains(h.Id)).ToListAsync();
                    _context.HocKies.RemoveRange(hocKies);
                }

                _context.NamHocs.Remove(namHoc);
                await _context.SaveChangesAsync();
                await trans.CommitAsync();

                return Json(new { success = true, message = "Xóa năm học thành công!" });
            }
            catch (Exception ex)
            {
                await trans.RollbackAsync();
                return Json(new { success = false, message = "Lỗi khi xóa năm học: " + ex.Message });
            }
        }
    }
}
