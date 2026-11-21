using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QLHS.Areas.Admin.ViewModels;
using QLHS.Models;

namespace QLHS.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "admin")]
    public class ChuyenCanController : Controller
    {
        private readonly QlhsContext _context;

        public ChuyenCanController(QlhsContext context)
        {
            _context = context;
        }

        // Chuẩn hóa trạng thái điểm danh (hỗ trợ cả dữ liệu cũ co_phep/khong_phep và chuẩn nghi_co_phep/nghi_khong_phep)
        private static string? NormalizeStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return null;
            return status.Trim().ToLower() switch
            {
                "co_mat" => "co_mat",
                "co_phep" or "nghi_co_phep" => "nghi_co_phep",
                "khong_phep" or "nghi_khong_phep" => "nghi_khong_phep",
                _ => null
            };
        }

        // GET: Admin/ChuyenCan (Chỉ giám sát, thống kê và xuất báo cáo — không điểm danh thay Giáo viên)
        public async Task<IActionResult> Index(
            int? namHocId,
            int? khoiLopId,
            int? lopHocId,
            string? ngay,
            string? tuNgay,
            string? denNgay,
            string? trangThai,
            string? search,
            string? tab)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var selectedDate = !string.IsNullOrWhiteSpace(ngay) && DateOnly.TryParse(ngay, out var parsedNgay)
                ? parsedNgay
                : today;

            var fromDate = !string.IsNullOrWhiteSpace(tuNgay) && DateOnly.TryParse(tuNgay, out var parsedTu)
                ? parsedTu
                : new DateOnly(selectedDate.Year, selectedDate.Month, 1);

            var toDate = !string.IsNullOrWhiteSpace(denNgay) && DateOnly.TryParse(denNgay, out var parsedDen)
                ? parsedDen
                : selectedDate;

            if (fromDate > toDate)
            {
                (fromDate, toDate) = (toDate, fromDate);
            }

            string activeTab = tab is "thong-ke" or "lich-su" ? tab : "theo-ngay";
            string? normalizedFilterStatus = trangThai == "chua_diem_danh"
                ? "chua_diem_danh"
                : NormalizeStatus(trangThai);

            // 1. Dữ liệu dropdown
            var namHocs = await _context.NamHocs
                .OrderByDescending(n => n.TenNamHoc)
                .ThenBy(n => n.HocKyHienTai)
                .ToListAsync();

            var khoiLops = await _context.KhoiLops
                .OrderBy(k => k.SoKhoi)
                .ToListAsync();

            var lopQuery = _context.LopHocs
                .Include(l => l.KhoiLop)
                .Include(l => l.NamHoc)
                .Include(l => l.GiaoVienChuNhiem)
                .AsQueryable();

            if (namHocId.HasValue && namHocId > 0)
                lopQuery = lopQuery.Where(l => l.NamHocId == namHocId);
            if (khoiLopId.HasValue && khoiLopId > 0)
                lopQuery = lopQuery.Where(l => l.KhoiLopId == khoiLopId);

            var dsLopFiltered = await lopQuery
                .OrderBy(l => l.KhoiLop != null ? l.KhoiLop.SoKhoi : 0)
                .ThenBy(l => l.TenLop)
                .ToListAsync();

            // 2. Lấy danh sách học sinh đang học theo bộ lọc Năm học / Khối / Lớp
            var hsBaseQuery = _context.HocSinhs
                .Include(h => h.LopHocHienTai)
                    .ThenInclude(l => l!.KhoiLop)
                .Include(h => h.LopHocHienTai)
                    .ThenInclude(l => l!.NamHoc)
                .Where(h => h.NgayXoaMem == null && h.TrangThaiHoc == "dang_hoc" && h.LopHocHienTaiId != null);

            if (namHocId.HasValue && namHocId > 0)
                hsBaseQuery = hsBaseQuery.Where(h => h.LopHocHienTai != null && h.LopHocHienTai.NamHocId == namHocId);
            if (khoiLopId.HasValue && khoiLopId > 0)
                hsBaseQuery = hsBaseQuery.Where(h => h.LopHocHienTai != null && h.LopHocHienTai.KhoiLopId == khoiLopId);

            var allActiveStudentsInScope = await hsBaseQuery
                .OrderBy(h => h.LopHocHienTai != null ? h.LopHocHienTai.TenLop : "")
                .ThenBy(h => h.HoTen)
                .ToListAsync();

            var scopeStudentIds = allActiveStudentsInScope.Select(h => h.Id).ToList();

            // 3. Dữ liệu điểm danh TRONG NGÀY (selectedDate)
            var diemDanhNgayList = await _context.DiemDanhs
                .Include(d => d.GiaoVienChot)
                .Where(d => d.NgayDiemDanh == selectedDate && d.HocSinhId.HasValue && scopeStudentIds.Contains(d.HocSinhId.Value))
                .ToListAsync();

            var diemDanhNgayMap = diemDanhNgayList
                .GroupBy(d => d.HocSinhId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ThoiGianCapNhat).First());

            var studentsByLop = allActiveStudentsInScope
                .GroupBy(h => h.LopHocHienTaiId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var tongHopTheoLop = new List<LopChuyenCanSummaryItem>();
            foreach (var lop in dsLopFiltered)
            {
                if (lopHocId.HasValue && lopHocId > 0 && lop.Id != lopHocId.Value)
                    continue;

                var hsInLop = studentsByLop.TryGetValue(lop.Id, out var list) ? list : new List<HocSinh>();
                int coMat = 0, coPhep = 0, khongPhep = 0, chua = 0;

                foreach (var hs in hsInLop)
                {
                    if (diemDanhNgayMap.TryGetValue(hs.Id, out var dd))
                    {
                        switch (NormalizeStatus(dd.TrangThai))
                        {
                            case "co_mat": coMat++; break;
                            case "nghi_co_phep": coPhep++; break;
                            case "nghi_khong_phep": khongPhep++; break;
                            default: chua++; break;
                        }
                    }
                    else
                    {
                        chua++;
                    }
                }

                tongHopTheoLop.Add(new LopChuyenCanSummaryItem
                {
                    LopHocId = lop.Id,
                    TenLop = lop.TenLop ?? "---",
                    SoKhoi = lop.KhoiLop?.SoKhoi ?? 0,
                    TenNamHoc = lop.NamHoc?.TenNamHoc ?? "---",
                    TenGvcn = lop.GiaoVienChuNhiem?.HoTen ?? "Chưa phân công",
                    SiSo = hsInLop.Count,
                    CoMat = coMat,
                    NghiCoPhep = coPhep,
                    NghiKhongPhep = khongPhep,
                    ChuaDiemDanh = chua
                });
            }

            var studentsForDetail = lopHocId.HasValue && lopHocId > 0
                ? allActiveStudentsInScope.Where(h => h.LopHocHienTaiId == lopHocId.Value).ToList()
                : allActiveStudentsInScope;

            int kpiCoMat = 0, kpiCoPhep = 0, kpiKhongPhep = 0, kpiChua = 0;
            var chiTietNgayAll = new List<HocSinhDiemDanhNgayItem>();

            foreach (var hs in studentsForDetail)
            {
                diemDanhNgayMap.TryGetValue(hs.Id, out var dd);
                var normStatus = NormalizeStatus(dd?.TrangThai);

                switch (normStatus)
                {
                    case "co_mat": kpiCoMat++; break;
                    case "nghi_co_phep": kpiCoPhep++; break;
                    case "nghi_khong_phep": kpiKhongPhep++; break;
                    default: kpiChua++; break;
                }

                chiTietNgayAll.Add(new HocSinhDiemDanhNgayItem
                {
                    DiemDanhId = dd?.Id,
                    HocSinhId = hs.Id,
                    MaHocSinh = hs.MaHocSinh ?? "---",
                    HoTen = hs.HoTen ?? "---",
                    GioiTinh = hs.GioiTinh ?? "---",
                    NgaySinh = hs.NgaySinh,
                    LopHocId = hs.LopHocHienTaiId ?? 0,
                    TenLop = hs.LopHocHienTai?.TenLop ?? "---",
                    SoKhoi = hs.LopHocHienTai?.KhoiLop?.SoKhoi ?? 0,
                    TrangThaiChuanHoa = normStatus,
                    NguoiCapNhat = dd == null ? null : (dd.GiaoVienChot?.HoTen ?? "Phụ huynh gửi đơn"),
                    ThoiGianCapNhat = dd?.ThoiGianCapNhat
                });
            }

            string? searchLower = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLower();
            IEnumerable<HocSinhDiemDanhNgayItem> chiTietNgayFiltered = chiTietNgayAll;

            if (!string.IsNullOrWhiteSpace(normalizedFilterStatus))
            {
                chiTietNgayFiltered = normalizedFilterStatus == "chua_diem_danh"
                    ? chiTietNgayFiltered.Where(x => x.TrangThaiChuanHoa == null)
                    : chiTietNgayFiltered.Where(x => x.TrangThaiChuanHoa == normalizedFilterStatus);
            }

            if (searchLower != null)
            {
                chiTietNgayFiltered = chiTietNgayFiltered.Where(x =>
                    x.MaHocSinh.ToLower().Contains(searchLower) ||
                    x.HoTen.ToLower().Contains(searchLower));
            }

            // 4. Dữ liệu thống kê & lịch sử trong KHOẢNG THỜI GIAN
            var detailStudentIds = studentsForDetail.Select(h => h.Id).ToList();
            var diemDanhKhoangList = await _context.DiemDanhs
                .Include(d => d.HocSinh)
                .Include(d => d.LopHoc)
                .Include(d => d.GiaoVienChot)
                .Where(d => d.NgayDiemDanh >= fromDate && d.NgayDiemDanh <= toDate &&
                            d.HocSinhId.HasValue && detailStudentIds.Contains(d.HocSinhId.Value))
                .OrderByDescending(d => d.NgayDiemDanh)
                .ThenByDescending(d => d.ThoiGianCapNhat)
                .ToListAsync();

            int tongCoMatKhoang = 0, tongCoPhepKhoang = 0, tongKhongPhepKhoang = 0;
            foreach (var d in diemDanhKhoangList)
            {
                switch (NormalizeStatus(d.TrangThai))
                {
                    case "co_mat": tongCoMatKhoang++; break;
                    case "nghi_co_phep": tongCoPhepKhoang++; break;
                    case "nghi_khong_phep": tongKhongPhepKhoang++; break;
                }
            }

            var ddByStudentInRange = diemDanhKhoangList
                .GroupBy(d => d.HocSinhId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var thongKeTheoHocSinh = studentsForDetail
                .Where(hs => searchLower == null ||
                             (hs.MaHocSinh?.ToLower().Contains(searchLower) ?? false) ||
                             (hs.HoTen?.ToLower().Contains(searchLower) ?? false))
                .Select(hs =>
                {
                    var records = ddByStudentInRange.TryGetValue(hs.Id, out var r) ? r : new List<DiemDanh>();
                    return new HocSinhThongKeKhoangItem
                    {
                        HocSinhId = hs.Id,
                        MaHocSinh = hs.MaHocSinh ?? "---",
                        HoTen = hs.HoTen ?? "---",
                        LopHocId = hs.LopHocHienTaiId ?? 0,
                        TenLop = hs.LopHocHienTai?.TenLop ?? "---",
                        SoBuoiCoMat = records.Count(x => NormalizeStatus(x.TrangThai) == "co_mat"),
                        SoBuoiCoPhep = records.Count(x => NormalizeStatus(x.TrangThai) == "nghi_co_phep"),
                        SoBuoiKhongPhep = records.Count(x => NormalizeStatus(x.TrangThai) == "nghi_khong_phep")
                    };
                })
                .OrderByDescending(x => x.SoBuoiKhongPhep)
                .ThenByDescending(x => x.SoBuoiCoPhep)
                .ThenBy(x => x.TenLop)
                .ThenBy(x => x.HoTen)
                .ToList();

            var lichSuDiemDanh = diemDanhKhoangList
                .Where(d =>
                {
                    var norm = NormalizeStatus(d.TrangThai);
                    if (!string.IsNullOrWhiteSpace(normalizedFilterStatus) && normalizedFilterStatus != "chua_diem_danh" && norm != normalizedFilterStatus)
                        return false;
                    if (searchLower != null)
                    {
                        bool matchMa = d.HocSinh?.MaHocSinh?.ToLower().Contains(searchLower) ?? false;
                        bool matchTen = d.HocSinh?.HoTen?.ToLower().Contains(searchLower) ?? false;
                        if (!matchMa && !matchTen) return false;
                    }
                    return true;
                })
                .Take(300)
                .Select(d => new DiemDanhLichSuItem
                {
                    Id = d.Id,
                    NgayDiemDanh = d.NgayDiemDanh ?? today,
                    HocSinhId = d.HocSinhId ?? 0,
                    MaHocSinh = d.HocSinh?.MaHocSinh ?? "---",
                    HoTen = d.HocSinh?.HoTen ?? "---",
                    LopHocId = d.LopHocId ?? 0,
                    TenLop = d.LopHoc?.TenLop ?? "---",
                    TrangThaiChuanHoa = NormalizeStatus(d.TrangThai) ?? "nghi_khong_phep",
                    NguoiCapNhat = d.GiaoVienChot?.HoTen ?? "Phụ huynh gửi đơn",
                    ThoiGianCapNhat = d.ThoiGianCapNhat
                })
                .ToList();

            var vm = new ChuyenCanIndexViewModel
            {
                NamHocId = namHocId,
                KhoiLopId = khoiLopId,
                LopHocId = lopHocId,
                SelectedDate = selectedDate,
                TuNgay = fromDate,
                DenNgay = toDate,
                TrangThai = normalizedFilterStatus,
                Search = search,
                ActiveTab = activeTab,

                NamHocs = namHocs,
                KhoiLops = khoiLops,
                LopHocs = dsLopFiltered,

                TongHocSinh = studentsForDetail.Count,
                SoCoMat = kpiCoMat,
                SoNghiCoPhep = kpiCoPhep,
                SoNghiKhongPhep = kpiKhongPhep,
                SoChuaDiemDanh = kpiChua,

                TongHopTheoLop = tongHopTheoLop,
                ChiTietNgay = chiTietNgayFiltered.ToList(),

                TongBanGhiKhoang = tongCoMatKhoang + tongCoPhepKhoang + tongKhongPhepKhoang,
                TongCoMatKhoang = tongCoMatKhoang,
                TongCoPhepKhoang = tongCoPhepKhoang,
                TongKhongPhepKhoang = tongKhongPhepKhoang,

                ThongKeTheoHocSinh = thongKeTheoHocSinh,
                LichSuDiemDanh = lichSuDiemDanh
            };

            return View(vm);
        }

        // GET: Admin/ChuyenCan/ExportCsv
        [HttpGet]
        public async Task<IActionResult> ExportCsv(int? namHocId, int? khoiLopId, int? lopHocId, string? tuNgay, string? denNgay)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var fromDate = !string.IsNullOrWhiteSpace(tuNgay) && DateOnly.TryParse(tuNgay, out var parsedTu)
                ? parsedTu
                : new DateOnly(today.Year, today.Month, 1);
            var toDate = !string.IsNullOrWhiteSpace(denNgay) && DateOnly.TryParse(denNgay, out var parsedDen)
                ? parsedDen
                : today;

            if (fromDate > toDate)
                (fromDate, toDate) = (toDate, fromDate);

            var hsQuery = _context.HocSinhs
                .Include(h => h.LopHocHienTai)
                    .ThenInclude(l => l!.KhoiLop)
                .Where(h => h.NgayXoaMem == null && h.TrangThaiHoc == "dang_hoc" && h.LopHocHienTaiId != null);

            if (namHocId.HasValue && namHocId > 0)
                hsQuery = hsQuery.Where(h => h.LopHocHienTai != null && h.LopHocHienTai.NamHocId == namHocId);
            if (khoiLopId.HasValue && khoiLopId > 0)
                hsQuery = hsQuery.Where(h => h.LopHocHienTai != null && h.LopHocHienTai.KhoiLopId == khoiLopId);
            if (lopHocId.HasValue && lopHocId > 0)
                hsQuery = hsQuery.Where(h => h.LopHocHienTaiId == lopHocId);

            var dsHocSinh = await hsQuery
                .OrderBy(h => h.LopHocHienTai != null ? h.LopHocHienTai.TenLop : "")
                .ThenBy(h => h.HoTen)
                .ToListAsync();

            var hsIds = dsHocSinh.Select(h => h.Id).ToList();
            var dsDiemDanh = await _context.DiemDanhs
                .Where(d => d.NgayDiemDanh >= fromDate && d.NgayDiemDanh <= toDate &&
                            d.HocSinhId.HasValue && hsIds.Contains(d.HocSinhId.Value))
                .ToListAsync();

            var ddMap = dsDiemDanh
                .GroupBy(d => d.HocSinhId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var sb = new StringBuilder();
            sb.AppendLine("STT,Mã Học Sinh,Họ Và Tên,Lớp,Khối,Có Mặt,Nghỉ Có Phép,Nghỉ Không Phép,Tổng Buổi Ghi Nhận,Tỉ Lệ Chuyên Cần (%)");

            int stt = 1;
            foreach (var hs in dsHocSinh)
            {
                var recs = ddMap.TryGetValue(hs.Id, out var list) ? list : new List<DiemDanh>();
                int coMat = recs.Count(x => NormalizeStatus(x.TrangThai) == "co_mat");
                int coPhep = recs.Count(x => NormalizeStatus(x.TrangThai) == "nghi_co_phep");
                int khongPhep = recs.Count(x => NormalizeStatus(x.TrangThai) == "nghi_khong_phep");
                int tong = coMat + coPhep + khongPhep;
                double tiLe = tong > 0 ? Math.Round((double)coMat / tong * 100, 1) : 0;

                string EscapeCsv(string? val) => $"\"{(val ?? "").Replace("\"", "\"\"")}\"";

                sb.AppendLine(string.Join(",",
                    stt++,
                    EscapeCsv(hs.MaHocSinh),
                    EscapeCsv(hs.HoTen),
                    EscapeCsv(hs.LopHocHienTai?.TenLop),
                    hs.LopHocHienTai?.KhoiLop?.SoKhoi ?? 0,
                    coMat,
                    coPhep,
                    khongPhep,
                    tong,
                    tiLe.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                ));
            }

            var utf8Bom = new UTF8Encoding(true);
            var bytes = utf8Bom.GetBytes(sb.ToString());
            string fileName = $"BaoCaoChuyenCan_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }
    }
}
