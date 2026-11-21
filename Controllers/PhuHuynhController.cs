using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHS.Models;

namespace QLHS.Controllers
{
    [Authorize(Roles = "phu_huynh,admin")]
    public class PhuHuynhController : Controller
    {
        private readonly QlhsContext _context;

        public PhuHuynhController(QlhsContext context)
        {
            _context = context;
        }

        // GET: /PhuHuynh/Index (Dashboard Tổng Quan)
        public async Task<IActionResult> Index()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return RedirectToAction("Login", "Account");
            }

            // Lấy hồ sơ Phụ huynh theo tài khoản
            var phuHuynh = await _context.PhuHuynhs
                .FirstOrDefaultAsync(p => p.TaiKhoanId == userId);

            if (phuHuynh == null)
            {
                ViewBag.ErrorMessage = "Chưa tìm thấy hồ sơ Phụ huynh tương ứng với tài khoản này.";
                return View(new List<HocSinh>());
            }

            ViewBag.HoTenPhuHuynh = phuHuynh.HoTen;

            // Truy vấn danh sách các con kèm thông tin Lớp, GVCN, Năm học, Điểm danh mới nhất
            var dsCon = await _context.PhuHuynhHocSinhs
                .Where(ph => ph.PhuHuynhId == phuHuynh.Id)
                .Include(ph => ph.HocSinh)
                    .ThenInclude(h => h.LopHocHienTai)
                        .ThenInclude(l => l!.KhoiLop)
                .Include(ph => ph.HocSinh)
                    .ThenInclude(h => h.LopHocHienTai)
                        .ThenInclude(l => l!.NamHoc)
                .Include(ph => ph.HocSinh)
                    .ThenInclude(h => h.LopHocHienTai)
                        .ThenInclude(l => l!.GiaoVienChuNhiem)
                .Include(ph => ph.HocSinh)
                    .ThenInclude(h => h.DiemDanhs)
                .Select(ph => ph.HocSinh)
                .Where(h => h.NgayXoaMem == null)
                .ToListAsync();

            return View(dsCon);
        }
        // GET: /PhuHuynh/BangDiem?studentId=1&hocKy=1&namHocId=2
        public async Task<IActionResult> BangDiem(int? studentId, int? hocKy, int? namHocId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return RedirectToAction("Login", "Account");
            }

            // 1. Kiểm tra tài khoản Phụ huynh
            var phuHuynh = await _context.PhuHuynhs.FirstOrDefaultAsync(p => p.TaiKhoanId == userId);
            if (phuHuynh == null && !User.IsInRole("admin"))
            {
                return Forbid();
            }

            // 2. Lấy danh sách các con của Phụ huynh để hiển thị ở Dropdown chọn con
            var dsCon = await _context.PhuHuynhHocSinhs
                .Where(ph => phuHuynh != null && ph.PhuHuynhId == phuHuynh.Id)
                .Include(ph => ph.HocSinh)
                .Select(ph => ph.HocSinh)
                .Where(h => h.NgayXoaMem == null)
                .ToListAsync();

            if (!dsCon.Any() && !User.IsInRole("admin"))
            {
                ViewBag.ErrorMessage = "Tài khoản chưa được liên kết với học sinh nào.";
                return View(new List<DanhGiaMonHoc>());
            }

            // Xác định học sinh cần xem (Mặc định là con đầu tiên nếu không truyền studentId)
            int selectedStudentId = studentId ?? dsCon.FirstOrDefault()?.Id ?? 0;

            // Kiểm tra bảo mật: Phụ huynh có đúng là cha/mẹ của học sinh này không?
            if (!User.IsInRole("admin") && !dsCon.Any(c => c.Id == selectedStudentId))
            {
                return Forbid();
            }

            // Lấy thông tin chi tiết học sinh đang chọn
            var hocSinh = await _context.HocSinhs
                .Include(h => h.LopHocHienTai)
                    .ThenInclude(l => l!.KhoiLop)
                .Include(h => h.LopHocHienTai)
                    .ThenInclude(l => l!.NamHoc)
                .FirstOrDefaultAsync(h => h.Id == selectedStudentId);

            if (hocSinh == null)
            {
                return NotFound();
            }

            // 3. Xử lý bộ lọc Năm học & Học kỳ
            var dsNamHoc = await _context.NamHocs.OrderByDescending(n => n.TenNamHoc).ToListAsync();
            int selectedNamHocId = namHocId ?? hocSinh.LopHocHienTai?.NamHocId ?? dsNamHoc.FirstOrDefault()?.Id ?? 0;
            int selectedHocKy = hocKy ?? 1;

            // 4. Truy vấn bảng đánh giá môn học (Thông tư 27)
            var bangDiem = await _context.DanhGiaMonHocs
                .Include(d => d.MonHoc)
                .Include(d => d.LopHoc)
                .Where(d => d.HocSinhId == selectedStudentId && 
                            d.LopHoc.NamHocId == selectedNamHocId && 
                            d.HocKy == selectedHocKy)
                .OrderBy(d => d.MonHocId)
                .ToListAsync();

            // Truy vấn cấu hình kiểm tra giữa kỳ của khối lớp theo đúng năm học & học kỳ (tránh trùng key)
            int khoiLopId = hocSinh.LopHocHienTai?.KhoiLopId ?? 0;
            var cauHinhList = await _context.CauHinhMonKhois
                .Include(c => c.HocKy)
                .Where(c => c.KhoiLopId == khoiLopId &&
                            (c.HocKy == null || (c.HocKy.NamHocId == selectedNamHocId && c.HocKy.SoHocKy == selectedHocKy)))
                .ToListAsync();

            var cauHinhMonKhoi = cauHinhList
                .Where(c => c.MonHocId.HasValue)
                .GroupBy(c => c.MonHocId!.Value)
                .ToDictionary(g => (int?)g.Key, g => g.Any(x => x.CoKiemTraGiuaKy == true));

            // Gửi dữ liệu sang View
            ViewBag.DanhSachCon = dsCon;
            ViewBag.HocSinhHienTai = hocSinh;
            ViewBag.DanhSachNamHoc = dsNamHoc;
            ViewBag.SelectedStudentId = selectedStudentId;
            ViewBag.SelectedNamHocId = selectedNamHocId;
            ViewBag.SelectedHocKy = selectedHocKy;
            ViewBag.CauHinhMonKhoi = cauHinhMonKhoi;

            return View(bangDiem);
        }
        // GET: /PhuHuynh/NangLucPhamChat?studentId=1&hocKy=1&namHocId=2
        public async Task<IActionResult> NangLucPhamChat(int? studentId, int? hocKy, int? namHocId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return RedirectToAction("Login", "Account");
            }

            // 1. Kiểm tra tài khoản Phụ huynh
            var phuHuynh = await _context.PhuHuynhs.FirstOrDefaultAsync(p => p.TaiKhoanId == userId);
            if (phuHuynh == null && !User.IsInRole("admin"))
            {
                return Forbid();
            }

            // 2. Lấy danh sách con của Phụ huynh
            var dsCon = await _context.PhuHuynhHocSinhs
                .Where(ph => phuHuynh != null && ph.PhuHuynhId == phuHuynh.Id)
                .Include(ph => ph.HocSinh)
                .Select(ph => ph.HocSinh)
                .Where(h => h.NgayXoaMem == null)
                .ToListAsync();

            if (!dsCon.Any() && !User.IsInRole("admin"))
            {
                ViewBag.ErrorMessage = "Tài khoản chưa được liên kết với học sinh nào.";
                return View((DanhGiaDinhKyTongHop?)null);
            }

            int selectedStudentId = studentId ?? dsCon.FirstOrDefault()?.Id ?? 0;

            // Kiểm tra bảo mật
            if (!User.IsInRole("admin") && !dsCon.Any(c => c.Id == selectedStudentId))
            {
                return Forbid();
            }

            // Lấy thông tin học sinh được chọn
            var hocSinh = await _context.HocSinhs
                .Include(h => h.LopHocHienTai)
                    .ThenInclude(l => l!.KhoiLop)
                .Include(h => h.LopHocHienTai)
                    .ThenInclude(l => l!.NamHoc)
                .FirstOrDefaultAsync(h => h.Id == selectedStudentId);

            if (hocSinh == null)
            {
                return NotFound();
            }

            // 3. Xử lý bộ lọc Năm học & Học kỳ
            var dsNamHoc = await _context.NamHocs.OrderByDescending(n => n.TenNamHoc).ToListAsync();
            int selectedNamHocId = namHocId ?? hocSinh.LopHocHienTai?.NamHocId ?? dsNamHoc.FirstOrDefault()?.Id ?? 0;
            int selectedHocKy = hocKy ?? 1;

            // 4. Truy vấn bảng Đánh giá định kỳ tổng hợp (Năng lực & Phẩm chất)
            var danhGiaTongHop = await _context.DanhGiaDinhKyTongHops
                .Include(d => d.LopHoc)
                .FirstOrDefaultAsync(d => d.HocSinhId == selectedStudentId && 
                                           d.LopHoc.NamHocId == selectedNamHocId && 
                                           d.HocKy == selectedHocKy);

            // Gửi dữ liệu sang View
            ViewBag.DanhSachCon = dsCon;
            ViewBag.HocSinhHienTai = hocSinh;
            ViewBag.DanhSachNamHoc = dsNamHoc;
            ViewBag.SelectedStudentId = selectedStudentId;
            ViewBag.SelectedNamHocId = selectedNamHocId;
            ViewBag.SelectedHocKy = selectedHocKy;

            return View(danhGiaTongHop);
        }
        // GET: /PhuHuynh/DiemDanh?studentId=1&thang=11&nam=2026
public async Task<IActionResult> DiemDanh(int? studentId, int? thang, int? nam)
{
    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (!int.TryParse(userIdClaim, out int userId))
    {
        return RedirectToAction("Login", "Account");
    }

    var phuHuynh = await _context.PhuHuynhs.FirstOrDefaultAsync(p => p.TaiKhoanId == userId);
    if (phuHuynh == null && !User.IsInRole("admin"))
    {
        return Forbid();
    }

    var dsCon = await _context.PhuHuynhHocSinhs
        .Where(ph => phuHuynh != null && ph.PhuHuynhId == phuHuynh.Id)
        .Include(ph => ph.HocSinh)
        .Select(ph => ph.HocSinh)
        .Where(h => h.NgayXoaMem == null)
        .ToListAsync();

    if (!dsCon.Any() && !User.IsInRole("admin"))
    {
        ViewBag.ErrorMessage = "Tài khoản chưa được liên kết với học sinh nào.";
        return View(new List<DiemDanh>());
    }

    int selectedStudentId = studentId ?? dsCon.FirstOrDefault()?.Id ?? 0;

    if (!User.IsInRole("admin") && !dsCon.Any(c => c.Id == selectedStudentId))
    {
        return Forbid();
    }

    var hocSinh = await _context.HocSinhs
        .Include(h => h.LopHocHienTai)
            .ThenInclude(l => l!.GiaoVienChuNhiem)
        .FirstOrDefaultAsync(h => h.Id == selectedStudentId);

    if (hocSinh == null)
    {
        return NotFound();
    }

    int selectedThang = thang ?? DateTime.Now.Month;
    int selectedNam = nam ?? DateTime.Now.Year;

    // Lấy lịch sử điểm danh kèm thông tin giáo viên xác nhận
    var dsDiemDanh = await _context.DiemDanhs
        .Include(d => d.GiaoVienChot)
        .Where(d => d.HocSinhId == selectedStudentId && 
                    d.NgayDiemDanh.HasValue &&
                    d.NgayDiemDanh.Value.Month == selectedThang && 
                    d.NgayDiemDanh.Value.Year == selectedNam)
        .OrderByDescending(d => d.NgayDiemDanh)
        .ToListAsync();

    int tongCoMat = dsDiemDanh.Count(d => d.TrangThai == "co_mat");
    int tongCoPhep = dsDiemDanh.Count(d => d.TrangThai == "nghi_co_phep" || d.TrangThai == "co_phep");
    int tongKhongPhep = dsDiemDanh.Count(d => d.TrangThai == "nghi_khong_phep" || d.TrangThai == "khong_phep");
    int tongBuoi = tongCoMat + tongCoPhep + tongKhongPhep;

    ViewBag.TongCoMat = tongCoMat;
    ViewBag.TongNghiCoPhep = tongCoPhep;
    ViewBag.TongNghiKhongPhep = tongKhongPhep;
    ViewBag.TiLeChuyenCan = tongBuoi > 0 ? Math.Round((double)tongCoMat / tongBuoi * 100, 1) : 0;

    ViewBag.DanhSachCon = dsCon;
    ViewBag.HocSinhHienTai = hocSinh;
    ViewBag.SelectedStudentId = selectedStudentId;
    ViewBag.SelectedThang = selectedThang;
    ViewBag.SelectedNam = selectedNam;

    return View(dsDiemDanh);
}

// POST: /PhuHuynh/XinNghiHoc
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> XinNghiHoc(int hocSinhId, DateTime ngayNghi, string lyDo)
{
    if (string.IsNullOrWhiteSpace(lyDo))
    {
        return Json(new { success = false, message = "Vui lòng nhập lý do xin nghỉ học!" });
    }

    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (!int.TryParse(userIdClaim, out int userId))
    {
        return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn!" });
    }

    if (!User.IsInRole("admin"))
    {
        var phuHuynh = await _context.PhuHuynhs.FirstOrDefaultAsync(p => p.TaiKhoanId == userId);
        bool laConCuaPhuHuynh = phuHuynh != null && await _context.PhuHuynhHocSinhs
            .AnyAsync(x => x.PhuHuynhId == phuHuynh.Id && x.HocSinhId == hocSinhId);
        if (!laConCuaPhuHuynh)
        {
            return Json(new { success = false, message = "Bạn không có quyền gửi đơn xin nghỉ cho học sinh này!" });
        }
    }

    var hocSinh = await _context.HocSinhs.FindAsync(hocSinhId);
    if (hocSinh == null || hocSinh.LopHocHienTaiId == null)
    {
        return Json(new { success = false, message = "Không tìm thấy thông tin học sinh hoặc chưa xếp lớp!" });
    }

    DateOnly targetDate = DateOnly.FromDateTime(ngayNghi);
    DateOnly today = DateOnly.FromDateTime(DateTime.Now);

    if (targetDate < today)
    {
        return Json(new { success = false, message = "Không thể gửi đơn xin nghỉ cho ngày đã qua!" });
    }

    var diemDanhExist = await _context.DiemDanhs
        .FirstOrDefaultAsync(d => d.HocSinhId == hocSinhId && d.NgayDiemDanh == targetDate);

    if (diemDanhExist != null)
    {
        if (diemDanhExist.TrangThai == "co_mat")
        {
            return Json(new { success = false, message = $"Ngày {targetDate:dd/MM/yyyy} học sinh đã được giáo viên điểm danh Có mặt!" });
        }

        diemDanhExist.TrangThai = "nghi_co_phep";
        diemDanhExist.GiaoVienChotId = null; // Đánh dấu đơn mới từ phụ huynh để GV nhận biết
        diemDanhExist.ThoiGianCapNhat = DateTime.Now;
        _context.DiemDanhs.Update(diemDanhExist);
    }
    else
    {
        var donXinNghi = new DiemDanh
        {
            HocSinhId = hocSinhId,
            LopHocId = hocSinh.LopHocHienTaiId.Value,
            NgayDiemDanh = targetDate,
            TrangThai = "nghi_co_phep",
            GiaoVienChotId = null,
            ThoiGianCapNhat = DateTime.Now
        };
        _context.DiemDanhs.Add(donXinNghi);
    }

    await _context.SaveChangesAsync();

    return Json(new { 
        success = true, 
        message = $"Đã gửi đơn xin nghỉ học ngày {targetDate:dd/MM/yyyy} thành công cho Giáo viên chủ nhiệm!" 
    });
}
// GET: /PhuHuynh/ThoiKhoaBieu?studentId=1
        public async Task<IActionResult> ThoiKhoaBieu(int? studentId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
            {
                return RedirectToAction("Login", "Account");
            }

            // 1. Kiểm tra tài khoản Phụ huynh
            var phuHuynh = await _context.PhuHuynhs.FirstOrDefaultAsync(p => p.TaiKhoanId == userId);
            if (phuHuynh == null && !User.IsInRole("admin"))
            {
                return Forbid();
            }

            // 2. Lấy danh sách học sinh (các con) của Phụ huynh
            var dsCon = await _context.PhuHuynhHocSinhs
                .Where(ph => phuHuynh != null && ph.PhuHuynhId == phuHuynh.Id)
                .Include(ph => ph.HocSinh)
                .Select(ph => ph.HocSinh)
                .Where(h => h.NgayXoaMem == null)
                .ToListAsync();

            if (!dsCon.Any() && !User.IsInRole("admin"))
            {
                ViewBag.ErrorMessage = "Tài khoản chưa được liên kết với học sinh nào.";
                return View(new List<ThoiKhoaBieu>());
            }

            int selectedStudentId = studentId ?? dsCon.FirstOrDefault()?.Id ?? 0;

            // Kiểm tra bảo mật: Phụ huynh có đúng là cha/mẹ của học sinh này không
            if (!User.IsInRole("admin") && !dsCon.Any(c => c.Id == selectedStudentId))
            {
                return Forbid();
            }

            // 3. Lấy thông tin chi tiết học sinh & lớp học hiện tại
            var hocSinh = await _context.HocSinhs
                .Include(h => h.LopHocHienTai)
                    .ThenInclude(l => l!.KhoiLop)
                .Include(h => h.LopHocHienTai)
                    .ThenInclude(l => l!.NamHoc)
                .Include(h => h.LopHocHienTai)
                    .ThenInclude(l => l!.GiaoVienChuNhiem)
                .FirstOrDefaultAsync(h => h.Id == selectedStudentId);

            if (hocSinh == null)
            {
                return NotFound();
            }

            // 4. Truy vấn thời khóa biểu của lớp học: CHỈ LẤY BẢN TKB ĐÃ CÔNG BỐ (DA_CONG_BO)
            var dsThoiKhoaBieu = new List<ThoiKhoaBieu>();
            BanTkb? banTkbHienHanh = null;

            if (hocSinh.LopHocHienTaiId.HasValue)
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                int namHocId = hocSinh.LopHocHienTai?.NamHocId ?? 0;

                // Ưu tiên lấy bản TKB đã công bố đang có hiệu lực tại ngày hiện tại, fallback sang bản mới nhất của lớp
                var dsBanCongBo = await _context.BanTkbs
                    .Include(b => b.HocKy)
                    .Where(b => b.TrangThai == "DA_CONG_BO" &&
                                (b.HocKy == null || b.HocKy.NamHocId == namHocId) &&
                                b.ThoiKhoaBieus.Any(t => t.LopHocId == hocSinh.LopHocHienTaiId.Value))
                    .OrderByDescending(b => b.Id)
                    .ToListAsync();

                banTkbHienHanh = dsBanCongBo.FirstOrDefault(b =>
                    (!b.TuNgay.HasValue || b.TuNgay.Value <= today) &&
                    (!b.DenNgay.HasValue || b.DenNgay.Value >= today))
                    ?? dsBanCongBo.FirstOrDefault();

                if (banTkbHienHanh != null)
                {
                    dsThoiKhoaBieu = await _context.ThoiKhoaBieus
                        .Include(t => t.PhanCong)
                            .ThenInclude(p => p!.MonHoc)
                        .Include(t => t.PhanCong)
                            .ThenInclude(p => p!.GiaoVien)
                        .Include(t => t.MonHoc)
                        .Include(t => t.GiaoVien)
                        .Include(t => t.KhungTiet)
                        .Where(t => t.BanTkbId == banTkbHienHanh.Id && 
                                    (t.LopHocId == hocSinh.LopHocHienTaiId.Value || (t.PhanCong != null && t.PhanCong.LopHocId == hocSinh.LopHocHienTaiId.Value)))
                        .OrderBy(t => t.KhungTiet != null ? t.KhungTiet.ThuTrongTuan : 0)
                        .ThenBy(t => t.KhungTiet != null ? (t.KhungTiet.BuoiHoc == "SANG" ? 1 : 2) : 1)
                        .ThenBy(t => t.KhungTiet != null ? t.KhungTiet.TietHoc : 0)
                        .ToListAsync();
                }
            }

            // Gửi dữ liệu sang View
            ViewBag.DanhSachCon = dsCon;
            ViewBag.HocSinhHienTai = hocSinh;
            ViewBag.SelectedStudentId = selectedStudentId;
            ViewBag.BanTkbHienHanh = banTkbHienHanh;

            return View(dsThoiKhoaBieu);
        }
    }
}