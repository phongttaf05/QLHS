using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using QLHS.Models;

namespace QLHS.Services
{
    // ==========================================
    // 1. CẤU HÌNH ĐỢT XẾP (LƯU TRONG ban_tkb.dieu_kien_json)
    // ==========================================
    public class CauHinhDotXepModel
    {
        [JsonPropertyName("lop_ids")]
        public List<int> LopIds { get; set; } = new List<int>();

        [JsonPropertyName("giao_vien_ids")]
        public List<int> GiaoVienIds { get; set; } = new List<int>();

        [JsonPropertyName("buoi_hocs")]
        public List<BuoiHocCauHinh> BuoiHocs { get; set; } = new List<BuoiHocCauHinh>();

        [JsonPropertyName("lop_mon_settings")]
        public List<LopMonSetting> LopMonSettings { get; set; } = new List<LopMonSetting>();

        [JsonPropertyName("giao_vien_settings")]
        public List<GiaoVienDotSetting> GiaoVienSettings { get; set; } = new List<GiaoVienDotSetting>();

        [JsonPropertyName("o_nghi_lop")]
        public List<ONghiLopSetting> ONghiLops { get; set; } = new List<ONghiLopSetting>();

        // Cấu hình Chào cờ & Sinh hoạt lớp
        [JsonPropertyName("tinh_chao_co_vao_hdtn")]
        public bool TinhChaoCoVaoHdtn { get; set; } = true;

        [JsonPropertyName("tinh_sinh_hoat_vao_hdtn")]
        public bool TinhSinhHoatVaoHdtn { get; set; } = true;

        // Tiết khóa thủ công trong quá trình tinh chỉnh nháp
        [JsonPropertyName("locked_slots")]
        public List<LockedSlotSetting> LockedSlots { get; set; } = new List<LockedSlotSetting>();
    }

    public class BuoiHocCauHinh
    {
        [JsonPropertyName("thu")]
        public byte Thu { get; set; } // 2..6

        [JsonPropertyName("buoi")]
        public string Buoi { get; set; } = "SANG"; // SANG / CHIEU

        [JsonPropertyName("co_hoc")]
        public bool CoHoc { get; set; } = true;
    }

    public class LopMonSetting
    {
        [JsonPropertyName("lop_hoc_id")]
        public int LopHocId { get; set; }

        [JsonPropertyName("mon_hoc_id")]
        public int MonHocId { get; set; }

        // Số tiết chuẩn theo kế hoạch khối (hoặc kế thừa)
        [JsonPropertyName("so_tiet_chuan")]
        public short SoTietChuan { get; set; }

        // Số tiết thực tế sẽ xếp (mặc định == SoTietChuan, có thể thay đổi nếu che_do_linh_hoat = true)
        [JsonPropertyName("so_tiet_tuan")]
        public short SoTietTuan { get; set; }

        [JsonPropertyName("min_tiet_ngay")]
        public byte MinTietNgay { get; set; } = 0; // Mặc định 0; áp dụng vào ngày có xếp môn (hoặc ngày bắt buộc)

        [JsonPropertyName("max_tiet_ngay")]
        public byte MaxTietNgay { get; set; } = 2; // Tối đa số tiết cả ngày (sáng + chiều cộng lại)

        [JsonPropertyName("min_tiet_tuan")]
        public short MinTietTuan { get; set; } = 0;

        [JsonPropertyName("max_tiet_tuan")]
        public short MaxTietTuan { get; set; } = 10;

        [JsonPropertyName("che_do_linh_hoat")]
        public bool CheDoLinhHoat { get; set; } = false;

        [JsonPropertyName("ngay_bat_buoc_hoc")]
        public List<byte> NgayBatBuocHoc { get; set; } = new List<byte>(); // Các thứ bắt buộc phải có môn này

        [JsonPropertyName("giao_vien_id")]
        public int GiaoVienId { get; set; }
    }

    public class GiaoVienDotSetting
    {
        [JsonPropertyName("giao_vien_id")]
        public int GiaoVienId { get; set; }

        [JsonPropertyName("max_tiet_ngay")]
        public byte MaxTietNgay { get; set; } = 7;

        [JsonPropertyName("max_tiet_tuan")]
        public short MaxTietTuan { get; set; } = 30;

        [JsonPropertyName("ngay_nghi_co_dinh")]
        public List<byte> NgayNghiCoDinh { get; set; } = new List<byte>(); // Các thứ cố định không dạy
    }

    public class ONghiLopSetting
    {
        [JsonPropertyName("lop_hoc_id")]
        public int LopHocId { get; set; }

        [JsonPropertyName("khung_tiet_id")]
        public int KhungTietId { get; set; }

        [JsonPropertyName("la_o_cam")]
        public bool LaOCam { get; set; } = true; // true = Ô cấm (bắt buộc nghỉ); false = Ô muốn tránh (ưu tiên mềm)
    }

    public class LockedSlotSetting
    {
        [JsonPropertyName("lop_hoc_id")]
        public int LopHocId { get; set; }

        [JsonPropertyName("khung_tiet_id")]
        public int KhungTietId { get; set; }

        [JsonPropertyName("mon_hoc_id")]
        public int MonHocId { get; set; }

        [JsonPropertyName("giao_vien_id")]
        public int GiaoVienId { get; set; }

        [JsonPropertyName("phan_cong_id")]
        public int PhanCongId { get; set; }

        [JsonPropertyName("loai_hoat_dong")]
        public string? LoaiHoatDong { get; set; } // CHAO_CO, SINH_HOAT, MON_HOC
    }

    // ==========================================
    // 2. BÁO CÁO KIỂM TRA ĐẦU VÀO TRƯỚC KHI CHỐT
    // ==========================================
    public class ValidationReport
    {
        public bool IsValid { get; set; }
        public int TongSoLopChon { get; set; }
        public int TongSoGiaoVienChon { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
        public List<TeacherLoadInfo> TeacherLoads { get; set; } = new List<TeacherLoadInfo>();
    }

    public class TeacherLoadInfo
    {
        public int GiaoVienId { get; set; }
        public string TenGiaoVien { get; set; } = string.Empty;
        public int TongSoTietTrongNhom { get; set; }
        public int TongSoTietNgoaiNhom { get; set; }
        public int TongSoTietTongCong => TongSoTietTrongNhom + TongSoTietNgoaiNhom;
        public int MaxTietTuanCuaGv { get; set; } = 30;
        public bool IsOverloaded => TongSoTietTongCong > MaxTietTuanCuaGv;
    }

    // ==========================================
    // 3. BẢN CHỤP BẤT BIẾN (dau_vao_da_chot_json)
    // ==========================================
    public class DauVaoTkbSnapshot
    {
        public int BanTkbId { get; set; }
        public int HocKyId { get; set; }
        public string TenHocKy { get; set; } = string.Empty;
        public string TenNamHoc { get; set; } = string.Empty;
        public DateOnly? TuNgay { get; set; }
        public DateOnly? DenNgay { get; set; }

        public List<int> LopIds { get; set; } = new List<int>();
        public List<int> GiaoVienIds { get; set; } = new List<int>();

        public List<SnapshotKhungTiet> KhungTiets { get; set; } = new List<SnapshotKhungTiet>();
        public List<SnapshotPhanCong> PhanCongs { get; set; } = new List<SnapshotPhanCong>();
        public List<SnapshotGioBan> GioBans { get; set; } = new List<SnapshotGioBan>();
        public CauHinhDotXepModel? CauHinhDotXep { get; set; }

        // Lịch đã công bố của các lớp khác ngoài nhóm (để tránh xếp trùng GV)
        public List<SnapshotLichNgoaiNhom> LichLopNgoaiNhom { get; set; } = new List<SnapshotLichNgoaiNhom>();

        // Thông tin GVCN từng lớp để xếp Sinh hoạt lớp
        public Dictionary<int, int> LopGvcnMap { get; set; } = new Dictionary<int, int>();
    }

    public class SnapshotKhungTiet
    {
        public int Id { get; set; }
        public byte ThuTrongTuan { get; set; } // 2..6
        public string BuoiHoc { get; set; } = string.Empty; // SANG / CHIEU
        public byte TietHoc { get; set; }
        public string GioBatDau { get; set; } = string.Empty; // HH:mm:ss
        public string GioKetThuc { get; set; } = string.Empty; // HH:mm:ss
    }

    public class SnapshotPhanCong
    {
        public int Id { get; set; }
        public int LopHocId { get; set; }
        public string TenLop { get; set; } = string.Empty;
        public int MonHocId { get; set; }
        public string TenMon { get; set; } = string.Empty;
        public int GiaoVienId { get; set; }
        public string TenGiaoVien { get; set; } = string.Empty;
        public short SoTietTuan { get; set; }
        public byte MinTietNgay { get; set; } = 0;
        public byte MaxTietNgay { get; set; } = 2;
        public short MinTietTuan { get; set; } = 0;
        public short MaxTietTuan { get; set; } = 10;
        public List<byte> NgayBatBuocHoc { get; set; } = new List<byte>();
        public DateOnly? TuNgay { get; set; }
        public DateOnly? DenNgay { get; set; }
        public string TrangThaiXacNhan { get; set; } = string.Empty;
    }

    public class SnapshotGioBan
    {
        public int GiaoVienId { get; set; }
        public int KhungTietId { get; set; }
        public bool BatBuoc { get; set; } // true = Cấm, false = Nguyện vọng tránh
        public string GhiChu { get; set; } = string.Empty;
    }

    public class SnapshotLichNgoaiNhom
    {
        public int GiaoVienId { get; set; }
        public int KhungTietId { get; set; }
        public int LopHocId { get; set; }
        public string TenLop { get; set; } = string.Empty;
        public byte ThuTrongTuan { get; set; }
        public string GioBatDau { get; set; } = string.Empty;
        public string GioKetThuc { get; set; } = string.Empty;
    }

    // ==========================================
    // 4. MODEL ĐIỀU CHỈNH LỊCH NHÁP (KÉO THẢ / CHƯA XẾP)
    // ==========================================
    public class MoveSlotRequest
    {
        public int BanTkbId { get; set; }
        public int LopHocId { get; set; }
        public int FromKhungTietId { get; set; } // 0 nếu kéo từ danh sách chưa xếp
        public int ToKhungTietId { get; set; } // 0 nếu kéo ra danh sách chưa xếp
        public int MonHocId { get; set; }
        public int GiaoVienId { get; set; }
        public int PhanCongId { get; set; }
    }

    // ==========================================
    // 5. INTERFACE DỊCH VỤ XẾP THỜI KHÓA BIỂU
    // ==========================================
    public interface ITimetableSolverService
    {
        Task<ValidationReport> ValidatePreConditionsAsync(int banTkbId, CauHinhDotXepModel? customConfig = null);
        Task<(bool Success, string Message, string SnapshotJson)> ChotDauVaoAsync(int banTkbId, int nguoiChotId, CauHinhDotXepModel? customConfig = null);
        Task<(bool Success, string Message, int PenaltyScore)> SolveBanTkbAsync(int banTkbId, int? maxTimeInSeconds = 30);
        Task<(bool Success, string Message)> UpdateDraftSlotAsync(MoveSlotRequest request, int userId);
        Task<(bool Success, string Message)> ReSolveRemainingDraftAsync(int banTkbId, int? maxTimeInSeconds = 30);
        Task<(bool Success, string Message)> CongBoBanTkbAsync(int banTkbId, int nguoiCongBoId);
        Task<List<ThoiKhoaBieu>> GetActiveTimetableForClassAsync(int lopHocId, DateOnly targetDate);
    }
}
