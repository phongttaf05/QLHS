namespace QLHS.Areas.Admin.ViewModels
{
    public class GiaoVienViewModel
    {
        public int Id { get; set; }
        
        // Thông tin Giáo Viên (bảng giao_vien)
        public string HoTen { get; set; } = string.Empty;
        public string? Cccd { get; set; }

        // Thông tin Tài Khoản (bảng tai_khoan)
        public string TenDangNhap { get; set; } = string.Empty;
        public string? MatKhau { get; set; }
        public string? SoDienThoai { get; set; }
        public string? Email { get; set; }

        // Danh sách Môn học chuyên môn (bảng giao_vien_mon_hoc)
        public List<int>? MonHocIds { get; set; }
    }
}