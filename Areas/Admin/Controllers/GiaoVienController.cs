using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHS.Models;
using QLHS.Areas.Admin.ViewModels;

namespace QLHS.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class GiaoVienController : Controller
    {
        private readonly QlhsContext _context;

        public GiaoVienController(QlhsContext context)
        {
            _context = context;
        }

        // GET: Admin/GiaoVien
        public async Task<IActionResult> Index(string? search)
        {
            var query = _context.GiaoViens
                .Include(g => g.TaiKhoan)
                .Include(g => g.LopHocs)
                .Include(g => g.GiaoVienMonHocs)
                    .ThenInclude(gm => gm.MonHoc)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(g => g.HoTen.ToLower().Contains(search) ||
                                         (g.Cccd != null && g.Cccd.ToLower().Contains(search)) ||
                                         (g.TaiKhoan != null && g.TaiKhoan.TenDangNhap!.ToLower().Contains(search)) ||
                                         (g.TaiKhoan != null && g.TaiKhoan.SoDienThoai != null && g.TaiKhoan.SoDienThoai.Contains(search)) ||
                                         g.GiaoVienMonHocs.Any(gm => gm.MonHoc.TenMon != null && gm.MonHoc.TenMon.ToLower().Contains(search)));
            }

            var listGiaoVien = await query.OrderBy(g => g.HoTen).ToListAsync();
            ViewBag.CurrentSearch = search;
            ViewBag.MonHocs = await _context.MonHocs.OrderBy(m => m.TenMon).ToListAsync();

            return View(listGiaoVien);
        }

        // GET: Admin/GiaoVien/GetById/5
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var gv = await _context.GiaoViens
                .Include(g => g.TaiKhoan)
                .Include(g => g.GiaoVienMonHocs)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (gv == null)
            {
                return Json(new { success = false, message = "Không tìm thấy hồ sơ giáo viên!" });
            }

            return Json(new
            {
                success = true,
                data = new
                {
                    gv.Id,
                    gv.HoTen,
                    gv.Cccd,
                    TenDangNhap = gv.TaiKhoan?.TenDangNhap ?? "",
                    SoDienThoai = gv.TaiKhoan?.SoDienThoai ?? "",
                    Email = gv.TaiKhoan?.Email ?? "",
                    MonHocIds = gv.GiaoVienMonHocs.Select(x => x.MonHocId).ToList()
                }
            });
        }

        // POST: Admin/GiaoVien/Create (Mật khẩu mặc định là 123456)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(GiaoVienViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.HoTen))
                return Json(new { success = false, message = "Họ tên giáo viên không được để trống!" });

            if (string.IsNullOrWhiteSpace(model.TenDangNhap))
                return Json(new { success = false, message = "Tên đăng nhập không được để trống!" });

            model.TenDangNhap = model.TenDangNhap.Trim();
            model.HoTen = model.HoTen.Trim();
            model.Cccd = model.Cccd?.Trim();

            bool existsTk = await _context.TaiKhoans.AnyAsync(t => t.TenDangNhap == model.TenDangNhap);
            if (existsTk)
            {
                return Json(new { success = false, message = $"Tên đăng nhập '{model.TenDangNhap}' đã tồn tại!" });
            }

            if (!string.IsNullOrEmpty(model.Cccd))
            {
                bool existsCccd = await _context.GiaoViens.AnyAsync(g => g.Cccd == model.Cccd);
                if (existsCccd)
                {
                    return Json(new { success = false, message = $"Số CCCD '{model.Cccd}' đã tồn tại!" });
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Mật khẩu tạo mới mặc định luôn là 123456
                var taiKhoan = new TaiKhoan
                {
                    TenDangNhap = model.TenDangNhap,
                    MatKhauHash = "123456",
                    Email = model.Email?.Trim(),
                    SoDienThoai = model.SoDienThoai?.Trim(),
                    VaiTro = "giao_vien",
                    TrangThai = "hoat_dong",
                    NgayTao = DateTime.Now
                };

                _context.TaiKhoans.Add(taiKhoan);
                await _context.SaveChangesAsync();

                var giaoVien = new QLHS.Models.GiaoVien
                {
                    HoTen = model.HoTen,
                    Cccd = model.Cccd,
                    TaiKhoanId = taiKhoan.Id
                };

                _context.GiaoViens.Add(giaoVien);
                await _context.SaveChangesAsync();

                // Lưu danh sách chuyên môn giảng dạy
                if (model.MonHocIds != null && model.MonHocIds.Any())
                {
                    foreach (var monId in model.MonHocIds.Distinct())
                    {
                        _context.GiaoVienMonHocs.Add(new GiaoVienMonHoc
                        {
                            GiaoVienId = giaoVien.Id,
                            MonHocId = monId
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                return Json(new { success = true, message = $"Tạo giáo viên '{model.HoTen}' thành công với mật khẩu mặc định 123456!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        // POST: Admin/GiaoVien/Edit (Chỉ sửa thông tin cá nhân & Tài khoản, không sửa mật khẩu)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(GiaoVienViewModel model)
        {
            var giaoVien = await _context.GiaoViens
                .Include(g => g.TaiKhoan)
                .Include(g => g.GiaoVienMonHocs)
                .FirstOrDefaultAsync(g => g.Id == model.Id);

            if (giaoVien == null)
            {
                return Json(new { success = false, message = "Hồ sơ giáo viên không tồn tại!" });
            }

            if (string.IsNullOrWhiteSpace(model.HoTen))
                return Json(new { success = false, message = "Họ tên không được để trống!" });

            if (string.IsNullOrWhiteSpace(model.TenDangNhap))
                return Json(new { success = false, message = "Tên đăng nhập không được để trống!" });

            model.TenDangNhap = model.TenDangNhap.Trim();
            model.HoTen = model.HoTen.Trim();
            model.Cccd = model.Cccd?.Trim();

            if (!string.IsNullOrEmpty(model.Cccd))
            {
                bool existsCccd = await _context.GiaoViens.AnyAsync(g => g.Cccd == model.Cccd && g.Id != model.Id);
                if (existsCccd)
                {
                    return Json(new { success = false, message = $"Số CCCD '{model.Cccd}' đã bị trùng!" });
                }
            }

            int currentTaiKhoanId = giaoVien.TaiKhoanId ?? 0;
            bool existsTk = await _context.TaiKhoans.AnyAsync(t => t.TenDangNhap == model.TenDangNhap && t.Id != currentTaiKhoanId);
            if (existsTk)
            {
                return Json(new { success = false, message = $"Tên đăng nhập '{model.TenDangNhap}' đã bị trùng!" });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (giaoVien.TaiKhoan != null)
                {
                    giaoVien.TaiKhoan.TenDangNhap = model.TenDangNhap;
                    giaoVien.TaiKhoan.SoDienThoai = model.SoDienThoai?.Trim();
                    giaoVien.TaiKhoan.Email = model.Email?.Trim();
                    _context.TaiKhoans.Update(giaoVien.TaiKhoan);
                }
                else
                {
                    var taiKhoanMoi = new TaiKhoan
                    {
                        TenDangNhap = model.TenDangNhap,
                        MatKhauHash = "123456",
                        Email = model.Email?.Trim(),
                        SoDienThoai = model.SoDienThoai?.Trim(),
                        VaiTro = "giao_vien",
                        TrangThai = "hoat_dong",
                        NgayTao = DateTime.Now
                    };
                    _context.TaiKhoans.Add(taiKhoanMoi);
                    await _context.SaveChangesAsync();
                    giaoVien.TaiKhoanId = taiKhoanMoi.Id;
                }

                giaoVien.HoTen = model.HoTen;
                giaoVien.Cccd = model.Cccd;
                _context.GiaoViens.Update(giaoVien);

                // Đồng bộ danh sách chuyên môn giảng dạy
                if (giaoVien.GiaoVienMonHocs.Any())
                {
                    _context.GiaoVienMonHocs.RemoveRange(giaoVien.GiaoVienMonHocs);
                }
                if (model.MonHocIds != null && model.MonHocIds.Any())
                {
                    foreach (var monId in model.MonHocIds.Distinct())
                    {
                        _context.GiaoVienMonHocs.Add(new GiaoVienMonHoc
                        {
                            GiaoVienId = giaoVien.Id,
                            MonHocId = monId
                        });
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new { success = true, message = "Cập nhật hồ sơ giáo viên thành công!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi khi cập nhật: " + ex.Message });
            }
        }

        // POST: Admin/GiaoVien/ResetPassword (RESET MẬT KHẨU VỀ 123456)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id)
        {
            var giaoVien = await _context.GiaoViens
                .Include(g => g.TaiKhoan)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (giaoVien == null || giaoVien.TaiKhoan == null)
            {
                return Json(new { success = false, message = "Không tìm thấy tài khoản để reset mật khẩu!" });
            }

            giaoVien.TaiKhoan.MatKhauHash = "123456";
            _context.TaiKhoans.Update(giaoVien.TaiKhoan);
            await _context.SaveChangesAsync();

            return Json(new { 
                success = true, 
                message = $"Đã reset mật khẩu của giáo viên '{giaoVien.HoTen}' (Tài khoản: {giaoVien.TaiKhoan.TenDangNhap}) về '123456' thành công!" 
            });
        }

        // POST: Admin/GiaoVien/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var giaoVien = await _context.GiaoViens
                .Include(g => g.TaiKhoan)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (giaoVien == null)
            {
                return Json(new { success = false, message = "Hồ sơ giáo viên không tồn tại!" });
            }

            if (await _context.LopHocs.AnyAsync(l => l.GiaoVienChuNhiemId == id))
                return Json(new { success = false, message = "Không thể xóa: Giáo viên đang làm Chủ nhiệm lớp!" });

            if (await _context.PhanCongGiangDays.AnyAsync(p => p.GiaoVienId == id))
                return Json(new { success = false, message = "Không thể xóa: Giáo viên đã có Phân công giảng dạy!" });

            if (await _context.ThoiKhoaBieus.AnyAsync(t => t.GiaoVienId == id))
                return Json(new { success = false, message = "Không thể xóa: Giáo viên đã có Thời khóa biểu!" });

            if (await _context.DiemDanhs.AnyAsync(d => d.GiaoVienChotId == id))
                return Json(new { success = false, message = "Không thể xóa: Giáo viên đã thực hiện điểm danh!" });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var taiKhoan = giaoVien.TaiKhoan;

                _context.GiaoViens.Remove(giaoVien);

                if (taiKhoan != null)
                {
                    _context.TaiKhoans.Remove(taiKhoan);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new { success = true, message = $"Xóa thành công giáo viên '{giaoVien.HoTen}' và tài khoản!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi khi xóa: " + ex.Message });
            }
        }
    }
}