using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHS.Models;
using QLHS.Services;
using System.Security.Claims;
using System.Text.Json;

namespace QLHS.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class ThoiKhoaBieuController : Controller
    {
        private readonly QlhsContext _context;
        private readonly ITimetableSolverService _solverService;

        public ThoiKhoaBieuController(QlhsContext context, ITimetableSolverService solverService)
        {
            _context = context;
            _solverService = solverService;
        }

        // GET: Admin/ThoiKhoaBieu?banTkbId=1&lopHocId=1&viewType=class
        public async Task<IActionResult> Index(int? banTkbId, int? lopHocId, int? giaoVienId, string viewType = "class")
        {
            var dsHocKy = await _context.HocKies
                .Include(h => h.NamHoc)
                .OrderByDescending(h => h.Id)
                .ToListAsync();
            ViewBag.DanhSachHocKy = dsHocKy;

            var dsBanTkb = await _context.BanTkbs
                .Include(b => b.HocKy)
                    .ThenInclude(h => h!.NamHoc)
                .OrderByDescending(b => b.Id)
                .ToListAsync();
            ViewBag.DanhSachBanTkb = dsBanTkb;

            int selectedBanTkbId = banTkbId ?? dsBanTkb.FirstOrDefault()?.Id ?? 0;
            ViewBag.SelectedBanTkbId = selectedBanTkbId;
            var banTkbHienTai = dsBanTkb.FirstOrDefault(b => b.Id == selectedBanTkbId);
            ViewBag.BanTkbHienTai = banTkbHienTai;

            var dsLopHoc = new List<LopHoc>();
            var dsAllLopInNam = new List<LopHoc>();
            var dsGiaoVienInNam = new List<QLHS.Models.GiaoVien>();
            var dsKhoi = new List<KhoiLop>();

            if (banTkbHienTai != null && banTkbHienTai.HocKy != null)
            {
                dsAllLopInNam = await _context.LopHocs
                    .Include(l => l.KhoiLop)
                    .Include(l => l.NamHoc)
                    .Include(l => l.GiaoVienChuNhiem)
                    .Where(l => l.NamHocId == banTkbHienTai.HocKy.NamHocId)
                    .OrderBy(l => l.KhoiLop != null ? l.KhoiLop.SoKhoi : 0)
                    .ThenBy(l => l.TenLop)
                    .ToListAsync();

                dsKhoi = await _context.KhoiLops.OrderBy(k => k.SoKhoi).ToListAsync();
                dsGiaoVienInNam = await _context.GiaoViens.OrderBy(g => g.HoTen).ToListAsync();

                CauHinhDotXepModel? config = null;
                if (!string.IsNullOrWhiteSpace(banTkbHienTai.DieuKienJson))
                {
                    try { config = JsonSerializer.Deserialize<CauHinhDotXepModel>(banTkbHienTai.DieuKienJson); } catch { }
                }

                if (config != null && config.LopIds != null && config.LopIds.Any())
                {
                    dsLopHoc = dsAllLopInNam.Where(l => config.LopIds.Contains(l.Id)).ToList();
                }
                else
                {
                    dsLopHoc = dsAllLopInNam;
                }

                ViewBag.CurrentConfig = config;
            }

            ViewBag.DanhSachLop = dsLopHoc;
            ViewBag.AllLopInNam = dsAllLopInNam;
            ViewBag.AllKhoi = dsKhoi;
            ViewBag.AllGiaoVien = dsGiaoVienInNam;
            ViewBag.ViewType = viewType;

            int selectedLopId = lopHocId ?? dsLopHoc.FirstOrDefault()?.Id ?? 0;
            ViewBag.SelectedLopId = selectedLopId;
            ViewBag.LopHocHienTai = dsLopHoc.FirstOrDefault(l => l.Id == selectedLopId);

            int selectedGvId = giaoVienId ?? dsGiaoVienInNam.FirstOrDefault()?.Id ?? 0;
            ViewBag.SelectedGiaoVienId = selectedGvId;
            ViewBag.GiaoVienHienTai = dsGiaoVienInNam.FirstOrDefault(g => g.Id == selectedGvId);

            var dsTkb = new List<ThoiKhoaBieu>();
            if (selectedBanTkbId > 0)
            {
                var query = _context.ThoiKhoaBieus
                    .Include(t => t.MonHoc)
                    .Include(t => t.GiaoVien)
                    .Include(t => t.LopHoc)
                    .Include(t => t.KhungTiet)
                    .Where(t => t.BanTkbId == selectedBanTkbId);

                if (viewType == "teacher" && selectedGvId > 0)
                {
                    query = query.Where(t => t.GiaoVienId == selectedGvId);
                }
                else if (selectedLopId > 0)
                {
                    query = query.Where(t => t.LopHocId == selectedLopId);
                }

                dsTkb = await query
                    .OrderBy(t => t.KhungTiet != null ? t.KhungTiet.ThuTrongTuan : 0)
                    .ThenBy(t => t.KhungTiet != null ? t.KhungTiet.TietHoc : 0)
                    .ToListAsync();
            }

            ViewBag.DanhSachMonHoc = await _context.MonHocs.OrderBy(m => m.TenMon).ToListAsync();

            var dsKhungTiet = new List<KhungTietHoc>();
            if (banTkbHienTai != null && banTkbHienTai.HocKyId.HasValue)
            {
                dsKhungTiet = await _context.KhungTietHocs
                    .Where(k => k.HocKyId == banTkbHienTai.HocKyId.Value && k.ThuTrongTuan >= 2 && k.ThuTrongTuan <= 6)
                    .OrderBy(k => k.ThuTrongTuan)
                    .ThenBy(k => k.TietHoc)
                    .ToListAsync();
            }
            ViewBag.DanhSachKhungTiet = dsKhungTiet;

            return View(dsTkb);
        }

        // GET: Admin/ThoiKhoaBieu/GetConfigData?banTkbId=1
        [HttpGet]
        public async Task<IActionResult> GetConfigData(int banTkbId)
        {
            var banTkb = await _context.BanTkbs
                .Include(b => b.HocKy)
                    .ThenInclude(h => h!.NamHoc)
                .FirstOrDefaultAsync(b => b.Id == banTkbId);

            if (banTkb == null || !banTkb.HocKyId.HasValue)
            {
                return Json(new { success = false, message = "Không tìm thấy Bản TKB!" });
            }

            int hocKyId = banTkb.HocKyId.Value;
            var hocKy = banTkb.HocKy!;
            var tuNgayBan = banTkb.TuNgay ?? hocKy.TuNgay ?? DateOnly.MinValue;
            var denNgayBan = banTkb.DenNgay ?? hocKy.DenNgay ?? DateOnly.MaxValue;

            var dsLop = await _context.LopHocs
                .Include(l => l.KhoiLop)
                .Include(l => l.GiaoVienChuNhiem)
                .Where(l => l.NamHocId == hocKy.NamHocId)
                .OrderBy(l => l.KhoiLop != null ? l.KhoiLop.SoKhoi : 0)
                .ThenBy(l => l.TenLop)
                .Select(l => new {
                    id = l.Id,
                    tenLop = l.TenLop,
                    khoiId = l.KhoiLopId,
                    soKhoi = l.KhoiLop != null ? l.KhoiLop.SoKhoi : 0,
                    gvcnId = l.GiaoVienChuNhiemId,
                    tenGvcn = l.GiaoVienChuNhiem != null ? l.GiaoVienChuNhiem.HoTen : "Chưa có"
                })
                .ToListAsync();

            var dsGv = await _context.GiaoViens
                .OrderBy(g => g.HoTen)
                .Select(g => new { id = g.Id, hoTen = g.HoTen })
                .ToListAsync();

            var dsKhung = await _context.KhungTietHocs
                .Where(k => k.HocKyId == hocKyId && k.ThuTrongTuan >= 2 && k.ThuTrongTuan <= 6)
                .OrderBy(k => k.ThuTrongTuan)
                .ThenBy(k => k.TietHoc)
                .Select(k => new {
                    id = k.Id,
                    thu = k.ThuTrongTuan,
                    buoi = k.BuoiHoc,
                    tiet = k.TietHoc,
                    batDau = k.GioBatDau.ToString(),
                    ketThuc = k.GioKetThuc.ToString()
                })
                .ToListAsync();

            var dsGioBan = await _context.GioBanGiaoViens
                .Include(g => g.KhungTiet)
                .Where(g => g.KhungTiet != null && g.KhungTiet.HocKyId == hocKyId)
                .Select(g => new {
                    id = g.Id,
                    giaoVienId = g.GiaoVienId,
                    khungTietId = g.KhungTietId,
                    thu = g.KhungTiet!.ThuTrongTuan,
                    tiet = g.KhungTiet.TietHoc,
                    buoi = g.KhungTiet.BuoiHoc,
                    batBuoc = g.BatBuoc ?? true,
                    ghiChu = g.GhiChu ?? ""
                })
                .ToListAsync();

            var dsCauHinhMonKhoi = await _context.CauHinhMonKhois
                .Include(c => c.MonHoc)
                .Where(c => c.HocKyId == hocKyId)
                .Select(c => new {
                    khoiId = c.KhoiLopId,
                    monId = c.MonHocId,
                    tenMon = c.MonHoc != null ? c.MonHoc.TenMon : "",
                    soTietTuan = c.SoTietTuan ?? 0
                })
                .ToListAsync();

            var dsPhanCong = await _context.PhanCongGiangDays
                .Include(p => p.LopHoc)
                .Include(p => p.MonHoc)
                .Include(p => p.GiaoVien)
                .Where(p => p.HocKyId == hocKyId)
                .ToListAsync();

            var dsPhanCongHieuLuc = dsPhanCong.Where(p =>
            {
                var pTu = p.TuNgay ?? DateOnly.MinValue;
                var pDen = p.DenNgay ?? DateOnly.MaxValue;
                return pTu < denNgayBan && tuNgayBan < pDen;
            }).Select(p => new {
                id = p.Id,
                lopId = p.LopHocId,
                tenLop = p.LopHoc?.TenLop ?? "",
                monId = p.MonHocId,
                tenMon = p.MonHoc?.TenMon ?? "",
                giaoVienId = p.GiaoVienId,
                tenGiaoVien = p.GiaoVien?.HoTen ?? "Chưa phân công",
                soTietTuan = p.SoTietTuan ?? 0,
                trangThaiXacNhan = p.TrangThaiXacNhan ?? "CHO_XAC_NHAN",
                yKien = p.YKien ?? ""
            }).ToList();

            CauHinhDotXepModel? currentConfig = null;
            if (!string.IsNullOrWhiteSpace(banTkb.DieuKienJson))
            {
                try { currentConfig = JsonSerializer.Deserialize<CauHinhDotXepModel>(banTkb.DieuKienJson); } catch { }
            }

            return Json(new {
                success = true,
                banTkb = new {
                    id = banTkb.Id,
                    soPhienBan = banTkb.SoPhienBan,
                    hocKyId = banTkb.HocKyId,
                    tenNamHoc = hocKy.NamHoc?.TenNamHoc,
                    soHocKy = hocKy.SoHocKy,
                    tuNgay = banTkb.TuNgay?.ToString("yyyy-MM-dd"),
                    denNgay = banTkb.DenNgay?.ToString("yyyy-MM-dd"),
                    trangThai = banTkb.TrangThai,
                    baoCao = banTkb.BaoCaoXepLich
                },
                currentConfig = currentConfig,
                danhSachLop = dsLop,
                danhSachGiaoVien = dsGv,
                khungTiets = dsKhung,
                gioBans = dsGioBan,
                cauHinhMonKhoi = dsCauHinhMonKhoi,
                phanCongs = dsPhanCongHieuLuc
            });
        }

        // POST: Admin/ThoiKhoaBieu/LuuCauHinhDot
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LuuCauHinhDot([FromBody] SaveConfigRequest request)
        {
            if (request == null || request.BanTkbId <= 0 || request.Config == null)
            {
                return Json(new { success = false, message = "Dữ liệu cấu hình không hợp lệ!" });
            }

            var banTkb = await _context.BanTkbs.FindAsync(request.BanTkbId);
            if (banTkb == null) return Json(new { success = false, message = "Không tìm thấy Bản TKB!" });

            if (banTkb.TrangThai == "DA_DUYET" || banTkb.TrangThai == "DA_CONG_BO")
            {
                return Json(new { success = false, message = "Bản TKB đã được duyệt/công bố, không thể chỉnh sửa cấu hình!" });
            }

            string json = JsonSerializer.Serialize(request.Config);
            banTkb.DieuKienJson = json;

            // Nếu thay đổi cấu hình, đưa về NHAP_DAU_VAO
            if (banTkb.TrangThai == "DA_XAC_NHAN" || banTkb.TrangThai == "XEP_THAT_BAI")
            {
                banTkb.TrangThai = "NHAP_DAU_VAO";
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã lưu cấu hình đợt xếp thành công!" });
        }

        // POST: Admin/ThoiKhoaBieu/CreateBanTkb
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBanTkb(int hocKyId, DateOnly? tuNgay, DateOnly? denNgay)
        {
            var hocKy = await _context.HocKies.FindAsync(hocKyId);
            if (hocKy == null) return Json(new { success = false, message = "Không tìm thấy Học kỳ!" });

            int maxVersion = await _context.BanTkbs
                .Where(b => b.HocKyId == hocKyId)
                .Select(b => (int?)b.SoPhienBan)
                .MaxAsync() ?? 0;

            var newBan = new BanTkb
            {
                HocKyId = hocKyId,
                SoPhienBan = maxVersion + 1,
                TuNgay = tuNgay ?? hocKy.TuNgay,
                DenNgay = denNgay ?? hocKy.DenNgay,
                TrangThai = "NHAP_DAU_VAO"
            };

            _context.BanTkbs.Add(newBan);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Đã tạo Bản TKB số {newBan.SoPhienBan} thành công!", banTkbId = newBan.Id });
        }

        // POST: Admin/ThoiKhoaBieu/ValidatePreCheck
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ValidatePreCheck([FromBody] ValidatePreCheckRequest request)
        {
            if (request == null || request.BanTkbId <= 0)
            {
                return Json(new { isValid = false, errors = new List<string> { "Tham số không hợp lệ!" } });
            }

            var report = await _solverService.ValidatePreConditionsAsync(request.BanTkbId, request.Config);
            return Json(report);
        }

        // POST: Admin/ThoiKhoaBieu/ChotDauVao
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChotDauVao([FromBody] ChotDauVaoRequest request)
        {
            if (request == null || request.BanTkbId <= 0)
            {
                return Json(new { success = false, message = "Tham số không hợp lệ!" });
            }

            int userId = GetCurrentUserId();
            var res = await _solverService.ChotDauVaoAsync(request.BanTkbId, userId, request.Config);
            return Json(new { success = res.Success, message = res.Message });
        }

        // POST: Admin/ThoiKhoaBieu/AutoSchedule
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutoSchedule(int banTkbId)
        {
            var result = await _solverService.SolveBanTkbAsync(banTkbId, 30);
            return Json(new { success = result.Success, message = result.Message, penalty = result.PenaltyScore });
        }

        // POST: Admin/ThoiKhoaBieu/UpdateDraftSlot (Kéo thả / Chưa xếp)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDraftSlot([FromBody] MoveSlotRequest request)
        {
            int userId = GetCurrentUserId();
            var result = await _solverService.UpdateDraftSlotAsync(request, userId);
            return Json(new { success = result.Success, message = result.Message });
        }

        // POST: Admin/ThoiKhoaBieu/ReSolveRemaining (Xếp lại phần còn lại sau khi chỉnh nháp)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReSolveRemaining(int banTkbId)
        {
            var result = await _solverService.ReSolveRemainingDraftAsync(banTkbId, 30);
            return Json(new { success = result.Success, message = result.Message });
        }

        // POST: Admin/ThoiKhoaBieu/DuyetBanTkb
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DuyetBanTkb(int banTkbId)
        {
            var banTkb = await _context.BanTkbs
                .Include(b => b.ThoiKhoaBieus)
                .FirstOrDefaultAsync(b => b.Id == banTkbId);

            if (banTkb == null) return Json(new { success = false, message = "Không tìm thấy Bản TKB!" });

            if (banTkb.TrangThai != "LICH_NHAP")
            {
                return Json(new { success = false, message = "Chỉ có thể duyệt khi bản TKB ở trạng thái LICH_NHAP!" });
            }

            // KIỂM TRA CHẶN: Không duyệt lịch khi còn tiết chưa xếp hoặc thiếu tiết
            if (string.IsNullOrWhiteSpace(banTkb.DauVaoDaChotJson))
            {
                return Json(new { success = false, message = "Bản TKB thiếu bản chụp đầu vào!" });
            }

            var snapshot = JsonSerializer.Deserialize<DauVaoTkbSnapshot>(banTkb.DauVaoDaChotJson);
            if (snapshot != null)
            {
                foreach (var pc in snapshot.PhanCongs)
                {
                    int soTietDaXep = banTkb.ThoiKhoaBieus.Count(t => t.PhanCongId == pc.Id);
                    if (soTietDaXep != pc.SoTietTuan)
                    {
                        return Json(new {
                            success = false,
                            message = $"KHÔNG THỂ DUYỆT: Môn '{pc.TenMon}' của lớp '{pc.TenLop}' đang thiếu tiết (yêu cầu {pc.SoTietTuan} tiết, hiện có {soTietDaXep} tiết)! Vui lòng xếp đủ tiết trước khi duyệt."
                        });
                    }
                }
            }

            banTkb.TrangThai = "DA_DUYET";
            banTkb.NguoiDuyetId = GetCurrentUserId();
            banTkb.NgayDuyet = DateTime.Now;

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Đã duyệt Thời khóa biểu thành công! Sẵn sàng công bố." });
        }

        // POST: Admin/ThoiKhoaBieu/CongBo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CongBo(int banTkbId)
        {
            int userId = GetCurrentUserId();
            var result = await _solverService.CongBoBanTkbAsync(banTkbId, userId);
            return Json(new { success = result.Success, message = result.Message });
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out int id) ? id : 0;
        }
    }

    public class SaveConfigRequest
    {
        public int BanTkbId { get; set; }
        public CauHinhDotXepModel? Config { get; set; }
    }

    public class ValidatePreCheckRequest
    {
        public int BanTkbId { get; set; }
        public CauHinhDotXepModel? Config { get; set; }
    }

    public class ChotDauVaoRequest
    {
        public int BanTkbId { get; set; }
        public CauHinhDotXepModel? Config { get; set; }
    }
}
