using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Google.OrTools.Sat;
using Microsoft.EntityFrameworkCore;
using QLHS.Models;

namespace QLHS.Services
{
    public class TimetableSolverService : ITimetableSolverService
    {
        private readonly QlhsContext _context;
        private readonly IAppLockService _appLockService;

        public TimetableSolverService(QlhsContext context, IAppLockService appLockService)
        {
            _context = context;
            _appLockService = appLockService;
        }

        // =========================================================================
        // 1. KIỂM TRA ĐIỀU KIỆN ĐẦU VÀO TRƯỚC KHI CHỐT
        // =========================================================================
        public async Task<ValidationReport> ValidatePreConditionsAsync(int banTkbId, CauHinhDotXepModel? customConfig = null)
        {
            var report = new ValidationReport();

            var banTkb = await _context.BanTkbs
                .Include(b => b.HocKy)
                    .ThenInclude(h => h!.NamHoc)
                .FirstOrDefaultAsync(b => b.Id == banTkbId);

            if (banTkb == null || !banTkb.HocKyId.HasValue)
            {
                report.Errors.Add("Không tìm thấy Bản TKB hoặc bản TKB chưa gắn Học kỳ!");
                report.IsValid = false;
                return report;
            }

            int hocKyId = banTkb.HocKyId.Value;
            var hocKy = banTkb.HocKy!;
            var tuNgayBan = banTkb.TuNgay ?? hocKy.TuNgay ?? DateOnly.MinValue;
            var denNgayBan = banTkb.DenNgay ?? hocKy.DenNgay ?? DateOnly.MaxValue;

            // Đọc cấu hình từ customConfig hoặc từ banTkb.DieuKienJson
            CauHinhDotXepModel config = customConfig ?? new CauHinhDotXepModel();
            if (customConfig == null && !string.IsNullOrWhiteSpace(banTkb.DieuKienJson))
            {
                try
                {
                    config = JsonSerializer.Deserialize<CauHinhDotXepModel>(banTkb.DieuKienJson) ?? new CauHinhDotXepModel();
                }
                catch { }
            }

            // Danh sách toàn bộ lớp học trong năm học của kỳ này
            var allLopInNam = await _context.LopHocs
                .Include(l => l.KhoiLop)
                .Include(l => l.GiaoVienChuNhiem)
                .Where(l => l.NamHocId == hocKy.NamHocId)
                .ToListAsync();

            var targetLopIds = config.LopIds != null && config.LopIds.Any()
                ? config.LopIds
                : allLopInNam.Select(l => l.Id).ToList();

            var dsLopChon = allLopInNam.Where(l => targetLopIds.Contains(l.Id)).ToList();
            report.TongSoLopChon = dsLopChon.Count;

            if (!dsLopChon.Any())
            {
                report.Errors.Add("Chưa chọn bất kỳ lớp học nào để xếp thời khóa biểu!");
            }

            // Khung tiết học của học kỳ
            var dsKhungTietAll = await _context.KhungTietHocs
                .Where(k => k.HocKyId == hocKyId && k.ThuTrongTuan >= 2 && k.ThuTrongTuan <= 6)
                .OrderBy(k => k.ThuTrongTuan)
                .ThenBy(k => k.TietHoc)
                .ToListAsync();

            if (!dsKhungTietAll.Any())
            {
                report.Errors.Add("Chưa cấu hình Khung tiết học (từ thứ Hai đến thứ Sáu) cho học kỳ!");
            }

            // Kiểm tra khung tiết bị trùng giờ thực
            for (int i = 0; i < dsKhungTietAll.Count; i++)
            {
                for (int j = i + 1; j < dsKhungTietAll.Count; j++)
                {
                    var k1 = dsKhungTietAll[i];
                    var k2 = dsKhungTietAll[j];
                    if (k1.ThuTrongTuan == k2.ThuTrongTuan &&
                        k1.GioBatDau.HasValue && k1.GioKetThuc.HasValue &&
                        k2.GioBatDau.HasValue && k2.GioKetThuc.HasValue)
                    {
                        if (k1.GioBatDau.Value < k2.GioKetThuc.Value && k2.GioBatDau.Value < k1.GioKetThuc.Value)
                        {
                            report.Errors.Add($"Khung tiết bị trùng giờ thực ở Thứ {k1.ThuTrongTuan}: Tiết {k1.TietHoc} ({k1.GioBatDau}-{k1.GioKetThuc}) giao với Tiết {k2.TietHoc} ({k2.GioBatDau}-{k2.GioKetThuc})!");
                        }
                    }
                }
            }

            // Xác định các ô Chào cờ (tiết đầu thứ Hai) và Sinh hoạt lớp (tiết cuối thứ Sáu)
            var slotChaoCo = dsKhungTietAll.FirstOrDefault(k => k.ThuTrongTuan == 2 && k.TietHoc == 1 && (k.BuoiHoc ?? "SANG").ToUpper() == "SANG");
            var slotSinhHoat = dsKhungTietAll.Where(k => k.ThuTrongTuan == 6).OrderByDescending(k => k.BuoiHoc == "CHIEU" ? 1 : 0).ThenByDescending(k => k.TietHoc).FirstOrDefault();

            if (slotChaoCo == null)
            {
                report.Errors.Add("Thiếu khung tiết cho Tiết đầu thứ Hai (Chào cờ)!");
            }
            if (slotSinhHoat == null)
            {
                report.Errors.Add("Thiếu khung tiết cho Tiết cuối thứ Sáu (Sinh hoạt lớp)!");
            }

            // Kiểm tra từng lớp về GVCN (dùng cho Sinh hoạt lớp) và ô nghỉ
            foreach (var lop in dsLopChon)
            {
                if (!lop.GiaoVienChuNhiemId.HasValue)
                {
                    report.Errors.Add($"Lớp {lop.TenLop} chưa có Giáo viên chủ nhiệm để phụ trách Sinh hoạt lớp!");
                }

                // Kiểm tra xem ô Chào cờ hoặc Sinh hoạt lớp có bị đặt làm ô cấm nghỉ không
                if (config.ONghiLops != null)
                {
                    if (slotChaoCo != null && config.ONghiLops.Any(o => o.LopHocId == lop.Id && o.KhungTietId == slotChaoCo.Id && o.LaOCam))
                    {
                        report.Errors.Add($"Lớp {lop.TenLop}: Tiết đầu thứ Hai (Chào cờ) đang bị đặt làm ô nghỉ cấm học!");
                    }
                    if (slotSinhHoat != null && config.ONghiLops.Any(o => o.LopHocId == lop.Id && o.KhungTietId == slotSinhHoat.Id && o.LaOCam))
                    {
                        report.Errors.Add($"Lớp {lop.TenLop}: Tiết cuối thứ Sáu (Sinh hoạt lớp) đang bị đặt làm ô nghỉ cấm học!");
                    }
                }
            }

            // Lấy các phân công giảng dạy cho các lớp được chọn trong học kỳ
            var dsPhanCong = await _context.PhanCongGiangDays
                .Include(p => p.LopHoc)
                .Include(p => p.MonHoc)
                .Include(p => p.GiaoVien)
                .Where(p => p.HocKyId == hocKyId && targetLopIds.Contains(p.LopHocId ?? 0))
                .ToListAsync();

            var dsPhanCongHieuLuc = dsPhanCong.Where(p =>
            {
                var pTu = p.TuNgay ?? DateOnly.MinValue;
                var pDen = p.DenNgay ?? DateOnly.MaxValue;
                return pTu < denNgayBan && tuNgayBan < pDen;
            }).ToList();

            // 1. RÀNG BUỘC PHÂN CÔNG: Mỗi cặp (lớp - môn) chỉ có ĐÚNG 1 giáo viên phụ trách toàn bộ số tiết
            var lopMonGroups = dsPhanCongHieuLuc.GroupBy(p => new { p.LopHocId, p.MonHocId });
            foreach (var grp in lopMonGroups)
            {
                var distinctGv = grp.Select(p => p.GiaoVienId).Distinct().ToList();
                var tenLop = grp.First().LopHoc?.TenLop ?? $"ID {grp.Key.LopHocId}";
                var tenMon = grp.First().MonHoc?.TenMon ?? $"ID {grp.Key.MonHocId}";

                if (distinctGv.Count > 1)
                {
                    report.Errors.Add($"Lớp {tenLop}, môn {tenMon} có {distinctGv.Count} giáo viên cùng phụ trách trong cùng khoảng áp dụng! Yêu cầu giáo vụ điều chỉnh phân công hoặc tách đợt theo ngày hiệu lực.");
                }
            }

            // 2. DANH SÁCH GIÁO VIÊN ĐƯỢC CHỌN THAM GIA
            var targetGvIds = config.GiaoVienIds != null && config.GiaoVienIds.Any()
                ? config.GiaoVienIds
                : dsPhanCongHieuLuc.Select(p => p.GiaoVienId ?? 0).Where(id => id > 0).Distinct().ToList();

            // Cộng thêm các GVCN của các lớp được chọn vào danh sách GV
            foreach (var lop in dsLopChon)
            {
                if (lop.GiaoVienChuNhiemId.HasValue && !targetGvIds.Contains(lop.GiaoVienChuNhiemId.Value))
                {
                    targetGvIds.Add(lop.GiaoVienChuNhiemId.Value);
                }
            }

            report.TongSoGiaoVienChon = targetGvIds.Count;

            // Kiểm tra phân công của giáo viên và trạng thái xác nhận
            foreach (var pc in dsPhanCongHieuLuc)
            {
                var tenLop = pc.LopHoc?.TenLop ?? "---";
                var tenMon = pc.MonHoc?.TenMon ?? "---";
                var tenGv = pc.GiaoVien?.HoTen ?? "Chưa phân công";

                if (!pc.GiaoVienId.HasValue || pc.GiaoVienId.Value == 0)
                {
                    report.Errors.Add($"Lớp {tenLop}, môn {tenMon} chưa được phân công giáo viên phụ trách!");
                    continue;
                }

                if (!targetGvIds.Contains(pc.GiaoVienId.Value))
                {
                    report.Errors.Add($"Lớp {tenLop}, môn {tenMon} cần {pc.SoTietTuan} tiết/tuần nhưng giáo viên phụ trách ({tenGv}) nằm ngoài danh sách giáo viên được chọn!");
                }

                // Việc xác nhận / góp ý sửa đổi của giáo viên được thực hiện trực tiếp trên Bản TKB nháp (CHO_GV_XAC_NHAN)
            }

            // 3. KIỂM TRA SỐ TIẾT CHUẨN, MIN/MAX TUẦN, MIN/MAX NGÀY
            var dsCauHinhMonKhoi = await _context.CauHinhMonKhois
                .Include(c => c.MonHoc)
                .Where(c => c.HocKyId == hocKyId)
                .ToListAsync();

            foreach (var lop in dsLopChon)
            {
                if (!lop.KhoiLopId.HasValue) continue;
                var monCuaKhoi = dsCauHinhMonKhoi.Where(c => c.KhoiLopId == lop.KhoiLopId.Value).ToList();

                foreach (var ch in monCuaKhoi)
                {
                    int monId = ch.MonHocId ?? 0;
                    var pcLopMon = dsPhanCongHieuLuc.FirstOrDefault(p => p.LopHocId == lop.Id && p.MonHocId == monId);
                    if (pcLopMon == null)
                    {
                        report.Errors.Add($"Lớp {lop.TenLop} thiếu phân công giảng dạy cho môn '{ch.MonHoc?.TenMon}' theo cấu hình khối (cần {ch.SoTietTuan ?? 0} tiết/tuần)!");
                    }
                    else
                    {
                        var setting = config.LopMonSettings?.FirstOrDefault(s => s.LopHocId == lop.Id && s.MonHocId == monId);
                        short soTietChuan = setting != null ? setting.SoTietChuan : (ch.SoTietTuan ?? pcLopMon.SoTietTuan ?? 0);
                        short soTietXep = setting != null ? setting.SoTietTuan : soTietChuan;
                        short minTuan = setting != null ? setting.MinTietTuan : (short)0;
                        short maxTuan = setting != null ? setting.MaxTietTuan : (short)10;
                        byte minNgay = setting != null ? setting.MinTietNgay : (byte)0;
                        byte maxNgay = setting != null ? setting.MaxTietNgay : (byte)2;

                        if (minTuan > maxTuan)
                        {
                            report.Errors.Add($"Lớp {lop.TenLop}, môn {ch.MonHoc?.TenMon}: Min tiết/tuần ({minTuan}) lớn hơn Max tiết/tuần ({maxTuan})!");
                        }
                        if (minNgay > maxNgay)
                        {
                            report.Errors.Add($"Lớp {lop.TenLop}, môn {ch.MonHoc?.TenMon}: Min tiết/ngày ({minNgay}) lớn hơn Max tiết/ngày ({maxNgay})!");
                        }
                        if (soTietXep < minTuan || soTietXep > maxTuan)
                        {
                            report.Errors.Add($"Lớp {lop.TenLop}, môn {ch.MonHoc?.TenMon}: Số tiết xếp ({soTietXep}) nằm ngoài khoảng min-max tuần ({minTuan}-{maxTuan})!");
                        }
                        if (soTietXep < 0)
                        {
                            report.Errors.Add($"Lớp {lop.TenLop}, môn {ch.MonHoc?.TenMon}: Số tiết không được âm!");
                        }

                        // Nếu không bật chế độ linh hoạt, bắt buộc số tiết xếp phải bằng số tiết chuẩn đã duyệt
                        bool linhHoat = setting?.CheDoLinhHoat ?? false;
                        if (!linhHoat && soTietXep != soTietChuan)
                        {
                            report.Errors.Add($"Lớp {lop.TenLop}, môn {ch.MonHoc?.TenMon}: Chế độ linh hoạt đang TẮT nhưng số tiết xếp ({soTietXep}) khác số chuẩn ({soTietChuan})!");
                        }
                    }
                }
            }

            // 4. KIỂM TRA SỨC CHỨA CÁC BUỔI ĐƯỢC CHỌN VÀ TÍNH TIẾT CỐ ĐỊNH (CHÀO CỜ, SINH HOẠT)
            var buoiHocs = config.BuoiHocs != null && config.BuoiHocs.Any()
                ? config.BuoiHocs
                : dsKhungTietAll.Select(k => new { Thu = k.ThuTrongTuan ?? 2, Buoi = k.BuoiHoc ?? "SANG" })
                                .Distinct()
                                .Select(b => new BuoiHocCauHinh { Thu = b.Thu, Buoi = b.Buoi, CoHoc = true })
                                .ToList();

            var buoiCoHocSet = buoiHocs.Where(b => b.CoHoc).Select(b => (b.Thu, b.Buoi.ToUpper())).ToHashSet();
            var khungTietsHopLe = dsKhungTietAll.Where(k => buoiCoHocSet.Contains((k.ThuTrongTuan ?? 2, (k.BuoiHoc ?? "SANG").ToUpper()))).ToList();

            int idMonHdtn = (await _context.MonHocs.FirstOrDefaultAsync(m => m.TenMon == "Hoạt động trải nghiệm"))?.Id ?? -1;

            foreach (var lop in dsLopChon)
            {
                var pcLop = dsPhanCongHieuLuc.Where(p => p.LopHocId == lop.Id).ToList();

                // Tính tổng tiết các môn
                int tongTietMon = 0;
                foreach (var pc in pcLop)
                {
                    var setting = config.LopMonSettings?.FirstOrDefault(s => s.LopHocId == lop.Id && s.MonHocId == pc.MonHocId);
                    int st = setting != null ? setting.SoTietTuan : (pc.SoTietTuan ?? 0);

                    // Trừ tiết nếu Chào cờ hoặc Sinh hoạt lớp được tính vào môn HĐTN
                    if (pc.MonHocId == idMonHdtn)
                    {
                        if (config.TinhChaoCoVaoHdtn && st > 0) st--;
                        if (config.TinhSinhHoatVaoHdtn && st > 0) st--;
                    }
                    tongTietMon += st;
                }

                // 2 tiết cố định: Chào cờ + Sinh hoạt lớp
                int tongNhuCauLop = tongTietMon + 2;

                // Trừ các ô cấm nghỉ của lớp
                int soOCamLop = config.ONghiLops != null ? config.ONghiLops.Count(o => o.LopHocId == lop.Id && o.LaOCam) : 0;
                int soOKhaDung = khungTietsHopLe.Count - soOCamLop;

                if (tongNhuCauLop > soOKhaDung)
                {
                    report.Errors.Add($"Lớp {lop.TenLop}: Tổng nhu cầu ({tongNhuCauLop} tiết bao gồm Chào cờ & Sinh hoạt) vượt quá số ô khả dụng sau khi trừ ô nghỉ cấm học ({soOKhaDung} ô)!");
                }
            }

            // 5. THỐNG KÊ TẢI GIÁO VIÊN (TÍNH CẢ TIẾT CỦA CÁC LỚP NGOÀI NHÓM ĐÃ CÔNG BỐ)
            var publishedTkbs = await _context.BanTkbs
                .Where(b => b.TrangThai == "DA_CONG_BO" && b.Id != banTkbId)
                .ToListAsync();

            var overlappingPublishedIds = publishedTkbs.Where(b =>
            {
                var bTu = b.TuNgay ?? DateOnly.MinValue;
                var bDen = b.DenNgay ?? DateOnly.MaxValue;
                return bTu < denNgayBan && tuNgayBan < bDen;
            }).Select(b => b.Id).ToList();

            var detailsNgoaiNhom = await _context.ThoiKhoaBieus
                .Where(t => overlappingPublishedIds.Contains(t.BanTkbId ?? 0) && !targetLopIds.Contains(t.LopHocId ?? 0))
                .ToListAsync();

            foreach (var gvId in targetGvIds)
            {
                var gv = await _context.GiaoViens.FindAsync(gvId);
                var gvSetting = config.GiaoVienSettings?.FirstOrDefault(g => g.GiaoVienId == gvId);
                int maxTietTuanGv = gvSetting?.MaxTietTuan ?? 30;

                int tietTrongNhom = dsPhanCongHieuLuc.Where(p => p.GiaoVienId == gvId).Sum(p => (int)(p.SoTietTuan ?? 0));
                // Cộng thêm tiết sinh hoạt lớp nếu GV này là GVCN của lớp nào trong nhóm
                int soLopChuNhiem = dsLopChon.Count(l => l.GiaoVienChuNhiemId == gvId);
                tietTrongNhom += soLopChuNhiem;

                int tietNgoaiNhom = detailsNgoaiNhom.Count(t => t.GiaoVienId == gvId);

                var loadInfo = new TeacherLoadInfo
                {
                    GiaoVienId = gvId,
                    TenGiaoVien = gv?.HoTen ?? "---",
                    TongSoTietTrongNhom = tietTrongNhom,
                    TongSoTietNgoaiNhom = tietNgoaiNhom,
                    MaxTietTuanCuaGv = maxTietTuanGv
                };
                report.TeacherLoads.Add(loadInfo);

                if (loadInfo.IsOverloaded)
                {
                    report.Warnings.Add($"Giáo viên '{loadInfo.TenGiaoVien}' có tổng định mức ({loadInfo.TongSoTietTongCong} tiết/tuần) vượt quá mức tối đa cấu hình ({maxTietTuanGv} tiết)!");
                }
            }

            report.IsValid = !report.Errors.Any();
            return report;
        }

        // =========================================================================
        // 2. BAN GIÁM HIỆU CHỐT ĐẦU VÀO TẠO SNAPSHOT BẤT BIẾN
        // =========================================================================
        public async Task<(bool Success, string Message, string SnapshotJson)> ChotDauVaoAsync(int banTkbId, int nguoiChotId, CauHinhDotXepModel? customConfig = null)
        {
            using var trans = await _context.Database.BeginTransactionAsync();
            try
            {
                bool locked = await _appLockService.AcquireLockAsync(trans, $"BanTkb_{banTkbId}", 10);
                if (!locked) return (false, "Hệ thống đang bận xử lý bản TKB này, vui lòng thử lại sau!", string.Empty);

                var banTkb = await _context.BanTkbs
                    .Include(b => b.HocKy)
                        .ThenInclude(h => h!.NamHoc)
                    .FirstOrDefaultAsync(b => b.Id == banTkbId);

                if (banTkb == null) return (false, "Không tìm thấy Bản TKB!", string.Empty);
                if (banTkb.TrangThai != "NHAP_DAU_VAO" && banTkb.TrangThai != "XEP_THAT_BAI")
                {
                    return (false, $"Chỉ có thể chốt đầu vào khi bản TKB ở trạng thái NHAP_DAU_VAO hoặc XEP_THAT_BAI. Trạng thái hiện tại: {banTkb.TrangThai}", string.Empty);
                }

                if (!banTkb.HocKyId.HasValue) return (false, "Bản TKB chưa gán Học kỳ!", string.Empty);

                var preValidation = await ValidatePreConditionsAsync(banTkbId, customConfig);
                if (!preValidation.IsValid)
                {
                    return (false, "Đầu vào chưa đủ điều kiện: \n- " + string.Join("\n- ", preValidation.Errors), string.Empty);
                }

                int hocKyId = banTkb.HocKyId.Value;
                var hocKy = banTkb.HocKy!;
                var tuNgayBan = banTkb.TuNgay ?? hocKy.TuNgay ?? DateOnly.MinValue;
                var denNgayBan = banTkb.DenNgay ?? hocKy.DenNgay ?? DateOnly.MaxValue;

                CauHinhDotXepModel config = customConfig ?? new CauHinhDotXepModel();
                if (customConfig == null && !string.IsNullOrWhiteSpace(banTkb.DieuKienJson))
                {
                    try { config = JsonSerializer.Deserialize<CauHinhDotXepModel>(banTkb.DieuKienJson) ?? new CauHinhDotXepModel(); } catch { }
                }

                var allLopInNam = await _context.LopHocs
                    .Include(l => l.GiaoVienChuNhiem)
                    .Where(l => l.NamHocId == hocKy.NamHocId)
                    .ToListAsync();

                var targetLopIds = config.LopIds != null && config.LopIds.Any() ? config.LopIds : allLopInNam.Select(l => l.Id).ToList();

                var lopGvcnMap = new Dictionary<int, int>();
                foreach (var lop in allLopInNam.Where(l => targetLopIds.Contains(l.Id)))
                {
                    if (lop.GiaoVienChuNhiemId.HasValue)
                    {
                        lopGvcnMap[lop.Id] = lop.GiaoVienChuNhiemId.Value;
                    }
                }

                var dsKhungTietAll = await _context.KhungTietHocs
                    .Where(k => k.HocKyId == hocKyId && k.ThuTrongTuan >= 2 && k.ThuTrongTuan <= 6)
                    .OrderBy(k => k.ThuTrongTuan)
                    .ThenBy(k => k.TietHoc)
                    .ToListAsync();

                var buoiHocs = config.BuoiHocs != null && config.BuoiHocs.Any()
                    ? config.BuoiHocs
                    : dsKhungTietAll.Select(k => new { Thu = k.ThuTrongTuan ?? 2, Buoi = k.BuoiHoc ?? "SANG" })
                                    .Distinct()
                                    .Select(b => new BuoiHocCauHinh { Thu = b.Thu, Buoi = b.Buoi, CoHoc = true })
                                    .ToList();

                var buoiCoHocSet = buoiHocs.Where(b => b.CoHoc).Select(b => (b.Thu, b.Buoi.ToUpper())).ToHashSet();
                var dsKhungTiet = dsKhungTietAll.Where(k => buoiCoHocSet.Contains((k.ThuTrongTuan ?? 2, (k.BuoiHoc ?? "SANG").ToUpper()))).ToList();

                var dsPhanCong = await _context.PhanCongGiangDays
                    .Include(p => p.LopHoc)
                    .Include(p => p.MonHoc)
                    .Include(p => p.GiaoVien)
                    .Where(p => p.HocKyId == hocKyId && targetLopIds.Contains(p.LopHocId ?? 0))
                    .ToListAsync();

                var dsPhanCongHieuLuc = dsPhanCong.Where(p =>
                {
                    var pTu = p.TuNgay ?? DateOnly.MinValue;
                    var pDen = p.DenNgay ?? DateOnly.MaxValue;
                    return pTu < denNgayBan && tuNgayBan < pDen;
                }).ToList();

                int idMonHdtn = (await _context.MonHocs.FirstOrDefaultAsync(m => m.TenMon == "Hoạt động trải nghiệm"))?.Id ?? -1;

                var snapshotPhanCongs = new List<SnapshotPhanCong>();
                foreach (var p in dsPhanCongHieuLuc)
                {
                    var setting = config.LopMonSettings?.FirstOrDefault(s => s.LopHocId == p.LopHocId && s.MonHocId == p.MonHocId);
                    short soTiet = setting != null ? setting.SoTietTuan : (p.SoTietTuan ?? 0);

                    // Trừ số tiết môn Hoạt động trải nghiệm nếu tính Chào cờ hoặc Sinh hoạt lớp vào môn này
                    if (p.MonHocId == idMonHdtn)
                    {
                        if (config.TinhChaoCoVaoHdtn && soTiet > 0) soTiet--;
                        if (config.TinhSinhHoatVaoHdtn && soTiet > 0) soTiet--;
                    }

                    byte minNgay = setting != null ? setting.MinTietNgay : (byte)0;
                    byte maxNgay = setting != null ? setting.MaxTietNgay : (byte)2;
                    short minTuan = setting != null ? setting.MinTietTuan : (short)0;
                    short maxTuan = setting != null ? setting.MaxTietTuan : (short)10;
                    var ngayBatBuoc = setting?.NgayBatBuocHoc ?? new List<byte>();

                    snapshotPhanCongs.Add(new SnapshotPhanCong
                    {
                        Id = p.Id,
                        LopHocId = p.LopHocId ?? 0,
                        TenLop = p.LopHoc?.TenLop ?? "",
                        MonHocId = p.MonHocId ?? 0,
                        TenMon = p.MonHoc?.TenMon ?? "",
                        GiaoVienId = p.GiaoVienId ?? 0,
                        TenGiaoVien = p.GiaoVien?.HoTen ?? "",
                        SoTietTuan = soTiet,
                        MinTietNgay = minNgay,
                        MaxTietNgay = maxNgay,
                        MinTietTuan = minTuan,
                        MaxTietTuan = maxTuan,
                        NgayBatBuocHoc = ngayBatBuoc,
                        TuNgay = p.TuNgay,
                        DenNgay = p.DenNgay,
                        TrangThaiXacNhan = p.TrangThaiXacNhan ?? ""
                    });
                }

                var dsGvId = snapshotPhanCongs.Select(p => p.GiaoVienId).Where(id => id > 0).Distinct().ToList();
                foreach (var gvCnId in lopGvcnMap.Values)
                {
                    if (!dsGvId.Contains(gvCnId)) dsGvId.Add(gvCnId);
                }

                var dsGioBan = await _context.GioBanGiaoViens
                    .Where(g => g.GiaoVienId.HasValue && dsGvId.Contains(g.GiaoVienId.Value))
                    .ToListAsync();

                // Lấy lịch lớp ngoài nhóm đã công bố
                var dsLichNgoaiNhom = new List<SnapshotLichNgoaiNhom>();
                var publishedTkbs = await _context.BanTkbs
                    .Include(b => b.HocKy)
                    .Where(b => b.TrangThai == "DA_CONG_BO" && b.Id != banTkbId)
                    .ToListAsync();

                var overlappingPublishedTkbs = publishedTkbs.Where(b =>
                {
                    var bTu = b.TuNgay ?? DateOnly.MinValue;
                    var bDen = b.DenNgay ?? DateOnly.MaxValue;
                    return bTu < denNgayBan && tuNgayBan < bDen;
                }).Select(b => b.Id).ToList();

                if (overlappingPublishedTkbs.Any())
                {
                    var detailsNgoaiNhom = await _context.ThoiKhoaBieus
                        .Include(t => t.KhungTiet)
                        .Include(t => t.LopHoc)
                        .Where(t => overlappingPublishedTkbs.Contains(t.BanTkbId ?? 0) &&
                                    !targetLopIds.Contains(t.LopHocId ?? 0) &&
                                    t.GiaoVienId.HasValue && dsGvId.Contains(t.GiaoVienId.Value) &&
                                    t.KhungTietId.HasValue)
                        .ToListAsync();

                    foreach (var det in detailsNgoaiNhom)
                    {
                        if (det.KhungTiet == null) continue;
                        dsLichNgoaiNhom.Add(new SnapshotLichNgoaiNhom
                        {
                            GiaoVienId = det.GiaoVienId!.Value,
                            KhungTietId = det.KhungTietId!.Value,
                            LopHocId = det.LopHocId ?? 0,
                            TenLop = det.LopHoc?.TenLop ?? "",
                            ThuTrongTuan = det.KhungTiet.ThuTrongTuan ?? 2,
                            GioBatDau = det.KhungTiet.GioBatDau?.ToString("HH:mm:ss") ?? "",
                            GioKetThuc = det.KhungTiet.GioKetThuc?.ToString("HH:mm:ss") ?? ""
                        });
                    }
                }

                var snapshot = new DauVaoTkbSnapshot
                {
                    BanTkbId = banTkb.Id,
                    HocKyId = hocKyId,
                    TenHocKy = $"Học kỳ {banTkb.HocKy?.SoHocKy}",
                    TenNamHoc = banTkb.HocKy?.NamHoc?.TenNamHoc ?? "",
                    TuNgay = banTkb.TuNgay,
                    DenNgay = banTkb.DenNgay,
                    LopIds = targetLopIds,
                    GiaoVienIds = dsGvId,
                    LopGvcnMap = lopGvcnMap,
                    KhungTiets = dsKhungTiet.Select(k => new SnapshotKhungTiet
                    {
                        Id = k.Id,
                        ThuTrongTuan = k.ThuTrongTuan ?? 2,
                        BuoiHoc = k.BuoiHoc ?? "SANG",
                        TietHoc = k.TietHoc ?? 1,
                        GioBatDau = k.GioBatDau?.ToString("HH:mm:ss") ?? "",
                        GioKetThuc = k.GioKetThuc?.ToString("HH:mm:ss") ?? ""
                    }).ToList(),
                    PhanCongs = snapshotPhanCongs,
                    GioBans = dsGioBan.Select(g => new SnapshotGioBan
                    {
                        GiaoVienId = g.GiaoVienId ?? 0,
                        KhungTietId = g.KhungTietId ?? 0,
                        BatBuoc = g.BatBuoc ?? true,
                        GhiChu = g.GhiChu ?? ""
                    }).ToList(),
                    CauHinhDotXep = config,
                    LichLopNgoaiNhom = dsLichNgoaiNhom
                };

                string snapshotJson = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });

                banTkb.DieuKienJson = JsonSerializer.Serialize(config);
                banTkb.DauVaoDaChotJson = snapshotJson;
                banTkb.NguoiChotId = nguoiChotId;
                banTkb.NgayChot = DateTime.UtcNow;
                banTkb.TrangThai = "DA_XAC_NHAN";

                await _context.SaveChangesAsync();
                await trans.CommitAsync();

                return (true, "Đã chốt đầu vào thành công và tạo bản chụp bất biến!", snapshotJson);
            }
            catch (Exception ex)
            {
                await trans.RollbackAsync();
                return (false, "Lỗi khi chốt đầu vào: " + ex.Message, string.Empty);
            }
        }

        // =========================================================================
        // 3. BỘ GIẢI GOOGLE OR-TOOLS THEO SNAPSHOT BẤT BIẾN
        // =========================================================================
        public async Task<(bool Success, string Message, int PenaltyScore)> SolveBanTkbAsync(int banTkbId, int? maxTimeInSeconds = 30)
        {
            DauVaoTkbSnapshot snapshot;

            using (var trans = await _context.Database.BeginTransactionAsync())
            {
                bool locked = await _appLockService.AcquireLockAsync(trans, $"BanTkb_{banTkbId}", 10);
                if (!locked) return (false, "Đang có tiến trình khác thao tác trên bản TKB này!", 0);

                var banTkb = await _context.BanTkbs.FirstOrDefaultAsync(b => b.Id == banTkbId);
                if (banTkb == null) return (false, "Không tìm thấy Bản TKB!", 0);

                if (banTkb.TrangThai != "DA_XAC_NHAN" && banTkb.TrangThai != "XEP_THAT_BAI" && banTkb.TrangThai != "LICH_NHAP" && banTkb.TrangThai != "CHO_GV_XAC_NHAN")
                {
                    return (false, $"Chỉ có thể chạy xếp lịch khi bản TKB ở trạng thái DA_XAC_NHAN, XEP_THAT_BAI, LICH_NHAP hoặc CHO_GV_XAC_NHAN. Trạng thái hiện tại: '{banTkb.TrangThai}'", 0);
                }

                if (string.IsNullOrWhiteSpace(banTkb.DauVaoDaChotJson))
                {
                    return (false, "Bản TKB chưa có bản chụp đầu vào (dau_vao_da_chot_json)! Hãy chốt đầu vào trước khi xếp.", 0);
                }

                try
                {
                    snapshot = JsonSerializer.Deserialize<DauVaoTkbSnapshot>(banTkb.DauVaoDaChotJson)!;
                }
                catch (Exception ex)
                {
                    return (false, "Lỗi đọc dữ liệu bản chụp đầu vào JSON: " + ex.Message, 0);
                }

                if (snapshot == null || !snapshot.KhungTiets.Any())
                {
                    return (false, "Bản chụp đầu vào rỗng hoặc không có dữ liệu khung tiết!", 0);
                }

                banTkb.TrangThai = "DANG_XEP";
                await _context.SaveChangesAsync();
                await trans.CommitAsync();
            }

            var khungTiets = snapshot.KhungTiets;
            var phanCongs = snapshot.PhanCongs;
            var gioBans = snapshot.GioBans ?? new List<SnapshotGioBan>();
            var lichNgoaiNhom = snapshot.LichLopNgoaiNhom ?? new List<SnapshotLichNgoaiNhom>();
            var config = snapshot.CauHinhDotXep ?? new CauHinhDotXepModel();
            var lopIds = snapshot.LopIds;

            // Xác định ô Chào cờ & Sinh hoạt lớp
            var slotChaoCo = khungTiets.FirstOrDefault(k => k.ThuTrongTuan == 2 && k.TietHoc == 1 && k.BuoiHoc.ToUpper() == "SANG");
            var slotSinhHoat = khungTiets.Where(k => k.ThuTrongTuan == 6).OrderByDescending(k => k.BuoiHoc == "CHIEU" ? 1 : 0).ThenByDescending(k => k.TietHoc).FirstOrDefault();

            int idSlotChaoCo = slotChaoCo?.Id ?? -1;
            int idSlotSinhHoat = slotSinhHoat?.Id ?? -1;

            // Lập bản đồ các cặp khung tiết chồng giờ thực
            var overlappingSlots = new HashSet<(int SlotA, int SlotB)>();
            for (int i = 0; i < khungTiets.Count; i++)
            {
                var s1 = khungTiets[i];
                TimeOnly.TryParse(s1.GioBatDau, out var t1Start);
                TimeOnly.TryParse(s1.GioKetThuc, out var t1End);

                for (int j = i + 1; j < khungTiets.Count; j++)
                {
                    var s2 = khungTiets[j];
                    if (s1.ThuTrongTuan == s2.ThuTrongTuan)
                    {
                        TimeOnly.TryParse(s2.GioBatDau, out var t2Start);
                        TimeOnly.TryParse(s2.GioKetThuc, out var t2End);

                        if (t1Start < t2End && t2Start < t1End)
                        {
                            overlappingSlots.Add((s1.Id, s2.Id));
                            overlappingSlots.Add((s2.Id, s1.Id));
                        }
                    }
                }
            }

            CpModel model = new CpModel();

            // Biến quyết định cho từng phân công: x[(pcId, slotId)]
            var x = new Dictionary<(int PcId, int SlotId), BoolVar>();
            foreach (var pc in phanCongs)
            {
                foreach (var slot in khungTiets)
                {
                    x[(pc.Id, slot.Id)] = model.NewBoolVar($"x_{pc.Id}_{slot.Id}");
                }
            }

            // RÀNG BUỘC CỨNG 1: Khóa 2 ô cố định: Tiết đầu Thứ Hai (Chào cờ) và Tiết cuối Thứ Sáu (Sinh hoạt lớp)
            // Không môn học thông thường nào được xếp vào 2 ô này!
            foreach (var pc in phanCongs)
            {
                if (idSlotChaoCo > 0) model.Add(x[(pc.Id, idSlotChaoCo)] == 0);
                if (idSlotSinhHoat > 0) model.Add(x[(pc.Id, idSlotSinhHoat)] == 0);
            }

            // RÀNG BUỘC CỨNG 2: Xếp đủ chính xác số tiết mỗi tuần của từng phân công
            foreach (var pc in phanCongs)
            {
                var pcVars = khungTiets.Select(s => x[(pc.Id, s.Id)]).ToList();
                model.Add(LinearExpr.Sum(pcVars) == pc.SoTietTuan);
            }

            // RÀNG BUỘC CỨNG 3: Mỗi lớp trong cùng 1 khung tiết (hoặc chồng giờ) học tối đa 1 tiết
            foreach (var lopId in lopIds)
            {
                var lopPcs = phanCongs.Where(p => p.LopHocId == lopId).ToList();

                foreach (var slot in khungTiets)
                {
                    // Ô chào cờ và sinh hoạt lớp đã chiếm chỗ của lớp
                    if (slot.Id == idSlotChaoCo || slot.Id == idSlotSinhHoat) continue;

                    model.Add(LinearExpr.Sum(lopPcs.Select(p => x[(p.Id, slot.Id)])) <= 1);
                }

                for (int i = 0; i < khungTiets.Count; i++)
                {
                    for (int j = i + 1; j < khungTiets.Count; j++)
                    {
                        int idA = khungTiets[i].Id;
                        int idB = khungTiets[j].Id;
                        if (overlappingSlots.Contains((idA, idB)))
                        {
                            var listA = lopPcs.Select(p => x[(p.Id, idA)]);
                            var listB = lopPcs.Select(p => x[(p.Id, idB)]);
                            model.Add(LinearExpr.Sum(listA.Concat(listB)) <= 1);
                        }
                    }
                }
            }

            // RÀNG BUỘC CỨNG 4: Mỗi giáo viên trong cùng 1 khung tiết (hoặc chồng giờ) dạy tối đa 1 lớp
            // Chú ý: Tiết Chào cờ không bị tính trùng giáo viên vì là hoạt động tập thể
            // Tiết Sinh hoạt lớp: GVCN phụ trách lớp mình nên không thể dạy lớp khác ở ô Sinh hoạt lớp!
            var gvIds = phanCongs.Select(p => p.GiaoVienId).Distinct().ToList();
            foreach (var gvId in gvIds)
            {
                var gvPcs = phanCongs.Where(p => p.GiaoVienId == gvId).ToList();

                foreach (var slot in khungTiets)
                {
                    if (slot.Id == idSlotChaoCo) continue; // Bỏ qua chào cờ

                    if (slot.Id == idSlotSinhHoat)
                    {
                        // Nếu GV này là GVCN của lớp nào đó trong nhóm, họ bận sinh hoạt lớp, không được dạy lớp khác!
                        bool laGvcn = snapshot.LopGvcnMap.Values.Contains(gvId);
                        if (laGvcn)
                        {
                            model.Add(LinearExpr.Sum(gvPcs.Select(p => x[(p.Id, slot.Id)])) == 0);
                        }
                        continue;
                    }

                    model.Add(LinearExpr.Sum(gvPcs.Select(p => x[(p.Id, slot.Id)])) <= 1);
                }

                for (int i = 0; i < khungTiets.Count; i++)
                {
                    for (int j = i + 1; j < khungTiets.Count; j++)
                    {
                        int idA = khungTiets[i].Id;
                        int idB = khungTiets[j].Id;
                        if (overlappingSlots.Contains((idA, idB)))
                        {
                            var listA = gvPcs.Select(p => x[(p.Id, idA)]);
                            var listB = gvPcs.Select(p => x[(p.Id, idB)]);
                            model.Add(LinearExpr.Sum(listA.Concat(listB)) <= 1);
                        }
                    }
                }
            }

            // RÀNG BUỘC CỨNG 5: Giờ bận bắt buộc của Giáo viên
            foreach (var gb in gioBans.Where(g => g.BatBuoc))
            {
                var gvPcs = phanCongs.Where(p => p.GiaoVienId == gb.GiaoVienId).ToList();
                if (khungTiets.Any(s => s.Id == gb.KhungTietId))
                {
                    foreach (var pc in gvPcs)
                    {
                        model.Add(x[(pc.Id, gb.KhungTietId)] == 0);
                    }
                }
            }

            // RÀNG BUỘC CỨNG 6: Tránh trùng giờ với LỊCH ĐÃ CÔNG BỐ CỦA CÁC LỚP NGOÀI NHÓM
            foreach (var ngoai in lichNgoaiNhom)
            {
                var gvPcs = phanCongs.Where(p => p.GiaoVienId == ngoai.GiaoVienId).ToList();
                if (!gvPcs.Any()) continue;

                TimeOnly.TryParse(ngoai.GioBatDau, out var nStart);
                TimeOnly.TryParse(ngoai.GioKetThuc, out var nEnd);

                foreach (var slot in khungTiets)
                {
                    if (slot.ThuTrongTuan == ngoai.ThuTrongTuan)
                    {
                        TimeOnly.TryParse(slot.GioBatDau, out var sStart);
                        TimeOnly.TryParse(slot.GioKetThuc, out var sEnd);

                        if (slot.Id == ngoai.KhungTietId || (sStart < nEnd && nStart < sEnd))
                        {
                            foreach (var pc in gvPcs)
                            {
                                model.Add(x[(pc.Id, slot.Id)] == 0);
                            }
                        }
                    }
                }
            }

            // RÀNG BUỘC CỨNG 7: Ô CẤM NGHỈ CỦA LỚP
            if (config.ONghiLops != null)
            {
                foreach (var o in config.ONghiLops.Where(o => o.LaOCam))
                {
                    var lopPcs = phanCongs.Where(p => p.LopHocId == o.LopHocId).ToList();
                    foreach (var pc in lopPcs)
                    {
                        if (khungTiets.Any(k => k.Id == o.KhungTietId))
                        {
                            model.Add(x[(pc.Id, o.KhungTietId)] == 0);
                        }
                    }
                }
            }

            // RÀNG BUỘC CỨNG 8: GIỚI HẠN THEO NGÀY CỦA TỪNG MÔN (MIN / MAX NGÀY CỘNG CẢ SÁNG VÀ CHIỀU)
            // Mức tối thiểu/ngày chỉ áp dụng vào ngày có xếp môn đó, trừ ngày được đánh dấu bắt buộc học môn
            var distinctDays = khungTiets.Select(k => k.ThuTrongTuan).Distinct().ToList();
            foreach (var pc in phanCongs)
            {
                byte maxN = pc.MaxTietNgay > 0 ? pc.MaxTietNgay : (byte)2;
                byte minN = pc.MinTietNgay;

                foreach (var thu in distinctDays)
                {
                    var slotsCuaNgay = khungTiets.Where(k => k.ThuTrongTuan == thu).Select(k => k.Id).ToList();
                    var pcDayVars = slotsCuaNgay.Select(sId => x[(pc.Id, sId)]).ToList();

                    // Tối đa/ngày: tổng tiết sáng + chiều <= maxN
                    model.Add(LinearExpr.Sum(pcDayVars) <= maxN);

                    bool isBatBuoc = pc.NgayBatBuocHoc != null && pc.NgayBatBuocHoc.Contains(thu);

                    if (isBatBuoc)
                    {
                        // Ngày bắt buộc phải học: số tiết >= max(1, minN)
                        int minYeuCau = Math.Max(1, (int)minN);
                        model.Add(LinearExpr.Sum(pcDayVars) >= minYeuCau);
                    }
                    else if (minN > 0)
                    {
                        // Mức tối thiểu/ngày chỉ áp dụng vào ngày có xếp môn đó
                        BoolVar hasLessonInDay = model.NewBoolVar($"hasDay_{pc.Id}_{thu}");
                        model.Add(LinearExpr.Sum(pcDayVars) >= hasLessonInDay * minN);
                        model.Add(LinearExpr.Sum(pcDayVars) <= hasLessonInDay * maxN);
                    }
                }
            }

            // RÀNG BUỘC CỨNG 9: GIỚI HẠN GIÁO VIÊN THEO NGÀY & NGÀY NGHỈ CỐ ĐỊNH
            if (config.GiaoVienSettings != null)
            {
                foreach (var gvSetting in config.GiaoVienSettings)
                {
                    int gvId = gvSetting.GiaoVienId;
                    var gvPcs = phanCongs.Where(p => p.GiaoVienId == gvId).ToList();

                    // Ngày nghỉ cố định
                    if (gvSetting.NgayNghiCoDinh != null)
                    {
                        foreach (var thuNghi in gvSetting.NgayNghiCoDinh)
                        {
                            var slotsNghi = khungTiets.Where(k => k.ThuTrongTuan == thuNghi).Select(k => k.Id).ToList();
                            foreach (var pc in gvPcs)
                            {
                                foreach (var sId in slotsNghi)
                                {
                                    model.Add(x[(pc.Id, sId)] == 0);
                                }
                            }
                        }
                    }

                    // Tối đa số tiết/ngày
                    byte maxGvNgay = gvSetting.MaxTietNgay > 0 ? gvSetting.MaxTietNgay : (byte)7;
                    foreach (var thu in distinctDays)
                    {
                        var slotsCuaNgay = khungTiets.Where(k => k.ThuTrongTuan == thu).Select(k => k.Id).ToList();
                        var dayGvVars = new List<BoolVar>();
                        foreach (var pc in gvPcs)
                        {
                            foreach (var sId in slotsCuaNgay)
                            {
                                dayGvVars.Add(x[(pc.Id, sId)]);
                            }
                        }
                        model.Add(LinearExpr.Sum(dayGvVars) <= maxGvNgay);
                    }
                }
            }

            // RÀNG BUỘC CỨNG 10: TIẾT KHÓA THỦ CÔNG (KHI GIÁO VỤ TINH CHỈNH VÀ XẾP LẠI PHẦN CÒN LẠI)
            if (config.LockedSlots != null)
            {
                foreach (var lk in config.LockedSlots)
                {
                    if (khungTiets.Any(k => k.Id == lk.KhungTietId))
                    {
                        if (x.ContainsKey((lk.PhanCongId, lk.KhungTietId)))
                        {
                            model.Add(x[(lk.PhanCongId, lk.KhungTietId)] == 1);
                        }
                    }
                }
            }

            // HÀM MỤC TIÊU PHẠT (MỤC TIÊU MỀM):
            // 1. Tránh giờ bận nguyện vọng của GV (phạt 10 điểm)
            // 2. Tránh ô muốn tránh của lớp (phạt 5 điểm)
            // 3. Dàn đều môn trong tuần
            List<IntVar> softPenalties = new List<IntVar>();

            foreach (var gb in gioBans.Where(g => !g.BatBuoc))
            {
                var gvPcs = phanCongs.Where(p => p.GiaoVienId == gb.GiaoVienId).ToList();
                if (khungTiets.Any(s => s.Id == gb.KhungTietId))
                {
                    foreach (var pc in gvPcs)
                    {
                        IntVar pVar = model.NewIntVar(0, 10, $"soft_gb_{pc.Id}_{gb.KhungTietId}");
                        model.Add(pVar == x[(pc.Id, gb.KhungTietId)] * 10);
                        softPenalties.Add(pVar);
                    }
                }
            }

            if (config.ONghiLops != null)
            {
                foreach (var o in config.ONghiLops.Where(o => !o.LaOCam))
                {
                    var lopPcs = phanCongs.Where(p => p.LopHocId == o.LopHocId).ToList();
                    if (khungTiets.Any(s => s.Id == o.KhungTietId))
                    {
                        foreach (var pc in lopPcs)
                        {
                            IntVar pVar = model.NewIntVar(0, 5, $"soft_avoid_{pc.Id}_{o.KhungTietId}");
                            model.Add(pVar == x[(pc.Id, o.KhungTietId)] * 5);
                            softPenalties.Add(pVar);
                        }
                    }
                }
            }

            if (softPenalties.Any())
            {
                model.Minimize(LinearExpr.Sum(softPenalties));
            }

            CpSolver solver = new CpSolver();
            double seconds = maxTimeInSeconds ?? 30;
            solver.StringParameters = $"max_time_in_seconds: {seconds.ToString("0.0", CultureInfo.InvariantCulture)}";
            CpSolverStatus status = solver.Solve(model);

            using (var trans = await _context.Database.BeginTransactionAsync())
            {
                bool locked = await _appLockService.AcquireLockAsync(trans, $"BanTkb_{banTkbId}", 10);
                if (!locked) return (false, "Lỗi khóa khi lưu kết quả xếp lịch!", 0);

                var banTkb = await _context.BanTkbs.FirstOrDefaultAsync(b => b.Id == banTkbId);
                if (banTkb == null) return (false, "Bản TKB không còn tồn tại!", 0);

                if (status != CpSolverStatus.Optimal && status != CpSolverStatus.Feasible)
                {
                    banTkb.TrangThai = "XEP_THAT_BAI";
                    string lyDo = status == CpSolverStatus.Infeasible
                        ? "Bộ giải chứng minh bài toán VÔ NGHIỆM (Xung đột giữa phân công, giới hạn tiết/ngày của môn, khung giờ khóa Chào cờ/Sinh hoạt hoặc giờ bận bắt buộc của giáo viên)."
                        : $"Chưa tìm được lịch hợp lệ trong thời gian quy định ({maxTimeInSeconds}s).";

                    banTkb.BaoCaoXepLich = $"[XẾP THẤT BÀI - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] {lyDo}";
                    await _context.SaveChangesAsync();
                    await trans.CommitAsync();

                    return (false, banTkb.BaoCaoXepLich, 0);
                }

                // Xếp thành công: Chèn các tiết môn học + Chèn 2 tiết cố định (Chào cờ & Sinh hoạt lớp)
                var newDetails = new List<ThoiKhoaBieu>();

                // 1. Chèn tiết môn học
                foreach (var pc in phanCongs)
                {
                    int soTietDaXep = 0;
                    foreach (var slot in khungTiets)
                    {
                        if (solver.BooleanValue(x[(pc.Id, slot.Id)]))
                        {
                            soTietDaXep++;
                            newDetails.Add(new ThoiKhoaBieu
                            {
                                BanTkbId = banTkbId,
                                PhanCongId = pc.Id,
                                KhungTietId = slot.Id,
                                LopHocId = pc.LopHocId,
                                MonHocId = pc.MonHocId,
                                GiaoVienId = pc.GiaoVienId
                            });
                        }
                    }

                    if (soTietDaXep != pc.SoTietTuan)
                    {
                        banTkb.TrangThai = "XEP_THAT_BAI";
                        banTkb.BaoCaoXepLich = $"Lỗi toàn vẹn: Môn {pc.TenMon} của lớp {pc.TenLop} cần {pc.SoTietTuan} tiết nhưng xếp được {soTietDaXep} tiết!";
                        await _context.SaveChangesAsync();
                        await trans.CommitAsync();
                        return (false, banTkb.BaoCaoXepLich, 0);
                    }
                }

                // 2. Chèn 2 tiết cố định: Chào cờ (Thứ Hai tiết 1) và Sinh hoạt lớp (Thứ Sáu tiết cuối)
                int idMonChaoCo = (await _context.MonHocs.FirstOrDefaultAsync(m => m.TenMon.Contains("Chào cờ") || m.TenMon == "Hoạt động trải nghiệm"))?.Id ?? 1;
                int idMonSinhHoat = (await _context.MonHocs.FirstOrDefaultAsync(m => m.TenMon.Contains("Sinh hoạt") || m.TenMon == "Hoạt động trải nghiệm"))?.Id ?? 1;

                foreach (var lopId in lopIds)
                {
                    int gvCnId = snapshot.LopGvcnMap.ContainsKey(lopId) ? snapshot.LopGvcnMap[lopId] : 1;

                    // Chào cờ
                    if (idSlotChaoCo > 0)
                    {
                        newDetails.Add(new ThoiKhoaBieu
                        {
                            BanTkbId = banTkbId,
                            KhungTietId = idSlotChaoCo,
                            LopHocId = lopId,
                            MonHocId = idMonChaoCo,
                            GiaoVienId = gvCnId
                        });
                    }

                    // Sinh hoạt lớp
                    if (idSlotSinhHoat > 0)
                    {
                        newDetails.Add(new ThoiKhoaBieu
                        {
                            BanTkbId = banTkbId,
                            KhungTietId = idSlotSinhHoat,
                            LopHocId = lopId,
                            MonHocId = idMonSinhHoat,
                            GiaoVienId = gvCnId
                        });
                    }
                }

                var oldDetails = await _context.ThoiKhoaBieus.Where(t => t.BanTkbId == banTkbId).ToListAsync();
                _context.ThoiKhoaBieus.RemoveRange(oldDetails);
                _context.ThoiKhoaBieus.AddRange(newDetails);

                int penalty = softPenalties.Any() ? (int)solver.ObjectiveValue : 0;
                banTkb.TrangThai = "LICH_NHAP";
                banTkb.BaoCaoXepLich = $"Xếp lịch thành công lúc {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC. Trạng thái: {status}. Điểm phạt: {penalty}. Tổng số tiết đã xếp: {newDetails.Count} tiết cho {lopIds.Count} lớp. Sẵn sàng cho BGH duyệt!";

                await _context.SaveChangesAsync();
                await trans.CommitAsync();

                return (true, banTkb.BaoCaoXepLich, penalty);
            }
        }

        // =========================================================================
        // 4. TINH CHỈNH LỊCH NHÁP (KÉO THẢ / CHƯA XẾP / XẾP LẠI PHẦN CÒN LẠI)
        // =========================================================================
        public async Task<(bool Success, string Message)> UpdateDraftSlotAsync(MoveSlotRequest request, int userId)
        {
            using var trans = await _context.Database.BeginTransactionAsync();
            try
            {
                var banTkb = await _context.BanTkbs.FirstOrDefaultAsync(b => b.Id == request.BanTkbId);
                if (banTkb == null) return (false, "Không tìm thấy Bản TKB!");
                if (banTkb.TrangThai != "LICH_NHAP" && banTkb.TrangThai != "CHO_GV_XAC_NHAN")
                {
                    return (false, "Chỉ có thể chỉnh sửa lịch khi ở trạng thái LICH_NHAP hoặc CHO_GV_XAC_NHAN!");
                }

                // Kiểm tra 2 ô khóa Chào cờ & Sinh hoạt lớp: KHÔNG CHO PHÉP KÉO THẢ VÀO HOẶC RA
                var dsKhung = await _context.KhungTietHocs.Where(k => k.HocKyId == banTkb.HocKyId).ToListAsync();
                var slotChaoCo = dsKhung.FirstOrDefault(k => k.ThuTrongTuan == 2 && k.TietHoc == 1 && (k.BuoiHoc ?? "SANG").ToUpper() == "SANG");
                var slotSinhHoat = dsKhung.Where(k => k.ThuTrongTuan == 6).OrderByDescending(k => k.BuoiHoc == "CHIEU" ? 1 : 0).ThenByDescending(k => k.TietHoc).FirstOrDefault();

                if (request.FromKhungTietId == slotChaoCo?.Id || request.FromKhungTietId == slotSinhHoat?.Id ||
                    request.ToKhungTietId == slotChaoCo?.Id || request.ToKhungTietId == slotSinhHoat?.Id)
                {
                    return (false, "Tiết Chào cờ (tiết đầu thứ Hai) và Sinh hoạt lớp (tiết cuối thứ Sáu) là cố định và bị khóa, không được phép di chuyển!");
                }

                // 1. Trường hợp kéo ra danh sách "CHƯA XẾP" (ToKhungTietId = 0)
                if (request.ToKhungTietId == 0)
                {
                    var itemToRemove = await _context.ThoiKhoaBieus.FirstOrDefaultAsync(t =>
                        t.BanTkbId == request.BanTkbId &&
                        t.LopHocId == request.LopHocId &&
                        t.KhungTietId == request.FromKhungTietId);

                    if (itemToRemove != null)
                    {
                        _context.ThoiKhoaBieus.Remove(itemToRemove);
                        await _context.SaveChangesAsync();
                        await trans.CommitAsync();
                        return (true, "Đã đưa tiết học ra danh sách Chưa Xếp!");
                    }
                    return (false, "Không tìm thấy tiết học tại vị trí này!");
                }

                // 2. Trường hợp kéo vào hoặc đổi chỗ giữa 2 ô
                // Kiểm tra ô đích có bị chiếm chỗ bởi tiết khác của lớp này không
                var existingAtDest = await _context.ThoiKhoaBieus.FirstOrDefaultAsync(t =>
                    t.BanTkbId == request.BanTkbId &&
                    t.LopHocId == request.LopHocId &&
                    t.KhungTietId == request.ToKhungTietId);

                // Kiểm tra xung đột giáo viên tại ô đích
                var gvConflict = await _context.ThoiKhoaBieus.AnyAsync(t =>
                    t.BanTkbId == request.BanTkbId &&
                    t.LopHocId != request.LopHocId &&
                    t.GiaoVienId == request.GiaoVienId &&
                    t.KhungTietId == request.ToKhungTietId);

                if (gvConflict)
                {
                    return (false, "Xung đột: Giáo viên phụ trách đã có tiết dạy ở lớp khác tại khung giờ này!");
                }

                // Kiểm tra giờ bận bắt buộc của giáo viên
                var gvBan = await _context.GioBanGiaoViens.AnyAsync(g =>
                    g.GiaoVienId == request.GiaoVienId &&
                    g.KhungTietId == request.ToKhungTietId &&
                    g.BatBuoc == true);

                if (gvBan)
                {
                    return (false, "Xung đột: Giáo viên đã đăng ký giờ bận bắt buộc tại khung giờ này!");
                }

                if (request.FromKhungTietId > 0)
                {
                    // Di chuyển từ ô cũ sang ô mới
                    var item = await _context.ThoiKhoaBieus.FirstOrDefaultAsync(t =>
                        t.BanTkbId == request.BanTkbId &&
                        t.LopHocId == request.LopHocId &&
                        t.KhungTietId == request.FromKhungTietId);

                    if (item == null) return (false, "Không tìm thấy tiết học nguồn!");

                    if (existingAtDest != null)
                    {
                        // Đổi chỗ 2 tiết (Swap)
                        existingAtDest.KhungTietId = request.FromKhungTietId;
                    }
                    item.KhungTietId = request.ToKhungTietId;
                }
                else
                {
                    // Kéo từ danh sách chưa xếp vào ô đích
                    if (existingAtDest != null)
                    {
                        return (false, "Ô đích đã có tiết học khác. Hãy đưa tiết đó ra danh sách chưa xếp trước!");
                    }

                    var newItem = new ThoiKhoaBieu
                    {
                        BanTkbId = request.BanTkbId,
                        LopHocId = request.LopHocId,
                        KhungTietId = request.ToKhungTietId,
                        MonHocId = request.MonHocId,
                        GiaoVienId = request.GiaoVienId,
                        PhanCongId = request.PhanCongId > 0 ? request.PhanCongId : (int?)null
                    };
                    _context.ThoiKhoaBieus.Add(newItem);
                }

                await _context.SaveChangesAsync();
                await trans.CommitAsync();

                return (true, "Đã cập nhật vị trí tiết học trên lịch nháp thành công!");
            }
            catch (Exception ex)
            {
                await trans.RollbackAsync();
                return (false, "Lỗi cập nhật tiết nháp: " + ex.Message);
            }
        }

        public async Task<(bool Success, string Message)> ReSolveRemainingDraftAsync(int banTkbId, int? maxTimeInSeconds = 30)
        {
            // Tự động thu thập các tiết đã xếp làm LockedSlots trong config và chạy SolveBanTkbAsync
            var banTkb = await _context.BanTkbs
                .Include(b => b.ThoiKhoaBieus)
                .FirstOrDefaultAsync(b => b.Id == banTkbId);

            if (banTkb == null) return (false, "Không tìm thấy Bản TKB!");
            if (banTkb.TrangThai != "LICH_NHAP" && banTkb.TrangThai != "CHO_GV_XAC_NHAN") return (false, "Chỉ có thể xếp lại phần còn lại khi ở trạng thái LICH_NHAP hoặc CHO_GV_XAC_NHAN!");

            CauHinhDotXepModel config = new CauHinhDotXepModel();
            if (!string.IsNullOrWhiteSpace(banTkb.DieuKienJson))
            {
                try { config = JsonSerializer.Deserialize<CauHinhDotXepModel>(banTkb.DieuKienJson) ?? new CauHinhDotXepModel(); } catch { }
            }

            // Ghi các tiết hiện đang có vào locked_slots
            config.LockedSlots = banTkb.ThoiKhoaBieus.Where(t => t.KhungTietId.HasValue && t.PhanCongId.HasValue).Select(t => new LockedSlotSetting
            {
                LopHocId = t.LopHocId ?? 0,
                KhungTietId = t.KhungTietId!.Value,
                MonHocId = t.MonHocId ?? 0,
                GiaoVienId = t.GiaoVienId ?? 0,
                PhanCongId = t.PhanCongId!.Value
            }).ToList();

            banTkb.DieuKienJson = JsonSerializer.Serialize(config);
            await _context.SaveChangesAsync();

            var res = await SolveBanTkbAsync(banTkbId, maxTimeInSeconds);
            return (res.Success, res.Message);
        }

        // =========================================================================
        // 5. BGH CÔNG BỐ THỜI KHÓA BIỂU THEO PHẠM VI LỚP ĐƯỢC CHỌN
        // =========================================================================
        public async Task<(bool Success, string Message)> CongBoBanTkbAsync(int banTkbId, int nguoiCongBoId)
        {
            using var trans = await _context.Database.BeginTransactionAsync();
            try
            {
                bool locked = await _appLockService.AcquireLockAsync(trans, "School_Publish_Tkb", 15);
                if (!locked) return (false, "Hệ thống đang có thao tác công bố TKB khác, vui lòng đợi!");

                var banTkb = await _context.BanTkbs
                    .Include(b => b.ThoiKhoaBieus)
                    .FirstOrDefaultAsync(b => b.Id == banTkbId);

                if (banTkb == null) return (false, "Không tìm thấy Bản TKB!");

                // Chặn công bố nếu không ở trạng thái DA_DUYET
                if (banTkb.TrangThai != "DA_DUYET")
                {
                    return (false, $"Chỉ có thể công bố bản TKB khi ở trạng thái DA_DUYET! Trạng thái hiện tại: {banTkb.TrangThai}");
                }

                if (!banTkb.TuNgay.HasValue)
                {
                    return (false, "Bản TKB chưa thiết lập Từ ngày áp dụng!");
                }

                var tuNgayMoi = banTkb.TuNgay.Value;
                var denNgayMoi = banTkb.DenNgay ?? DateOnly.MaxValue;

                var lopIdsInBan = banTkb.ThoiKhoaBieus
                    .Select(t => t.LopHocId)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .Distinct()
                    .ToList();

                if (!lopIdsInBan.Any())
                {
                    return (false, "Bản TKB không có chi tiết lịch học nào để công bố!");
                }

                // Cập nhật các bản TKB cũ:
                // CHỈ THAY LỊCH CỦA CÁC LỚP ĐƯỢC CHỌN TRONG KHOẢNG ÁP DỤNG.
                // Các lớp ngoài phạm vi giữ nguyên lịch của mình.
                var dsCongBoCu = await _context.BanTkbs
                    .Include(b => b.ThoiKhoaBieus)
                    .Where(b => b.TrangThai == "DA_CONG_BO" && b.Id != banTkbId)
                    .ToListAsync();

                foreach (var oldBan in dsCongBoCu)
                {
                    var oldTu = oldBan.TuNgay ?? DateOnly.MinValue;
                    var oldDen = oldBan.DenNgay ?? DateOnly.MaxValue;

                    if (oldTu < denNgayMoi && tuNgayMoi < oldDen)
                    {
                        var oldLopIds = oldBan.ThoiKhoaBieus.Select(t => t.LopHocId ?? 0).Distinct().ToList();
                        bool coversOnlyTargetClasses = oldLopIds.All(id => lopIdsInBan.Contains(id));

                        if (coversOnlyTargetClasses)
                        {
                            if (oldTu < tuNgayMoi)
                            {
                                oldBan.DenNgay = tuNgayMoi;
                            }
                            else
                            {
                                oldBan.TrangThai = "NGUNG_AP_DUNG";
                            }
                        }
                        else
                        {
                            // Bản cũ chứa cả các lớp ngoài phạm vi:
                            // Giữ nguyên bản cũ cho các lớp ngoài phạm vi
                            if (oldTu >= tuNgayMoi)
                            {
                                var detailsToRemove = oldBan.ThoiKhoaBieus.Where(t => lopIdsInBan.Contains(t.LopHocId ?? 0)).ToList();
                                _context.ThoiKhoaBieus.RemoveRange(detailsToRemove);
                            }
                            else
                            {
                                oldBan.DenNgay = tuNgayMoi;
                            }
                        }
                    }
                }

                banTkb.TrangThai = "DA_CONG_BO";
                banTkb.NguoiCongBoId = nguoiCongBoId;
                banTkb.NgayCongBo = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await trans.CommitAsync();

                return (true, $"Đã công bố Thời khóa biểu thành công cho {lopIdsInBan.Count} lớp học được chọn!");
            }
            catch (Exception ex)
            {
                await trans.RollbackAsync();
                return (false, "Lỗi công bố TKB: " + ex.Message);
            }
        }

        // =========================================================================
        // 6. TRA CỨU THỜI KHÓA BIỂU CÓ HIỆU LỰC THEO TỪNG LỚP TẠI MỘT NGÀY
        // Phân xử ưu tiên: ngay_cong_bo DESC, id DESC
        // =========================================================================
        public async Task<List<ThoiKhoaBieu>> GetActiveTimetableForClassAsync(int lopHocId, DateOnly targetDate)
        {
            var activeBan = await _context.BanTkbs
                .Where(b => b.TrangThai == "DA_CONG_BO" &&
                            (!b.TuNgay.HasValue || b.TuNgay.Value <= targetDate) &&
                            (!b.DenNgay.HasValue || b.DenNgay.Value > targetDate) &&
                            b.ThoiKhoaBieus.Any(t => t.LopHocId == lopHocId))
                .OrderByDescending(b => b.NgayCongBo)
                .ThenByDescending(b => b.Id)
                .FirstOrDefaultAsync();

            if (activeBan == null) return new List<ThoiKhoaBieu>();

            return await _context.ThoiKhoaBieus
                .Include(t => t.MonHoc)
                .Include(t => t.GiaoVien)
                .Include(t => t.KhungTiet)
                .Where(t => t.BanTkbId == activeBan.Id && t.LopHocId == lopHocId)
                .OrderBy(t => t.KhungTiet != null ? t.KhungTiet.ThuTrongTuan : 0)
                .ThenBy(t => t.KhungTiet != null ? t.KhungTiet.TietHoc : 0)
                .ToListAsync();
        }
    }
}
