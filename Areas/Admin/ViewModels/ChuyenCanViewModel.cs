using System;
using System.Collections.Generic;
using QLHS.Models;

namespace QLHS.Areas.Admin.ViewModels
{
    public class ChuyenCanIndexViewModel
    {
        // Bộ lọc hiện tại
        public int? NamHocId { get; set; }
        public int? KhoiLopId { get; set; }
        public int? LopHocId { get; set; }
        public DateOnly SelectedDate { get; set; }
        public DateOnly TuNgay { get; set; }
        public DateOnly DenNgay { get; set; }
        public string? TrangThai { get; set; }
        public string? Search { get; set; }
        public string ActiveTab { get; set; } = "theo-ngay";

        // Dữ liệu dropdown
        public List<NamHoc> NamHocs { get; set; } = new();
        public List<KhoiLop> KhoiLops { get; set; } = new();
        public List<LopHoc> LopHocs { get; set; } = new();

        // Thống kê tổng quan theo ngày đang chọn
        public int TongHocSinh { get; set; }
        public int SoCoMat { get; set; }
        public int SoNghiCoPhep { get; set; }
        public int SoNghiKhongPhep { get; set; }
        public int SoChuaDiemDanh { get; set; }
        public double TiLeChuyenCanNgay => TongHocSinh > 0
            ? Math.Round((double)SoCoMat / TongHocSinh * 100, 1)
            : 0;

        // Danh sách tổng hợp theo lớp (trong ngày đang chọn)
        public List<LopChuyenCanSummaryItem> TongHopTheoLop { get; set; } = new();

        // Danh sách chi tiết học sinh trong ngày đang chọn
        public List<HocSinhDiemDanhNgayItem> ChiTietNgay { get; set; } = new();

        // Thống kê khoảng thời gian (từ TuNgay đến DenNgay)
        public int TongBanGhiKhoang { get; set; }
        public int TongCoMatKhoang { get; set; }
        public int TongCoPhepKhoang { get; set; }
        public int TongKhongPhepKhoang { get; set; }
        public double TiLeChuyenCanKhoang => TongBanGhiKhoang > 0
            ? Math.Round((double)TongCoMatKhoang / TongBanGhiKhoang * 100, 1)
            : 0;

        public List<HocSinhThongKeKhoangItem> ThongKeTheoHocSinh { get; set; } = new();
        public List<DiemDanhLichSuItem> LichSuDiemDanh { get; set; } = new();
    }

    public class LopChuyenCanSummaryItem
    {
        public int LopHocId { get; set; }
        public string TenLop { get; set; } = string.Empty;
        public int SoKhoi { get; set; }
        public string TenNamHoc { get; set; } = string.Empty;
        public string TenGvcn { get; set; } = "Chưa phân công";
        public int SiSo { get; set; }
        public int CoMat { get; set; }
        public int NghiCoPhep { get; set; }
        public int NghiKhongPhep { get; set; }
        public int ChuaDiemDanh { get; set; }
        public bool DaHoanTatDiemDanh => SiSo > 0 && ChuaDiemDanh == 0;
        public double TiLeChuyenCan => SiSo > 0 ? Math.Round((double)CoMat / SiSo * 100, 1) : 0;
    }

    public class HocSinhDiemDanhNgayItem
    {
        public int? DiemDanhId { get; set; }
        public int HocSinhId { get; set; }
        public string MaHocSinh { get; set; } = string.Empty;
        public string HoTen { get; set; } = string.Empty;
        public string GioiTinh { get; set; } = string.Empty;
        public DateOnly? NgaySinh { get; set; }
        public int LopHocId { get; set; }
        public string TenLop { get; set; } = string.Empty;
        public int SoKhoi { get; set; }
        public string? TrangThaiChuanHoa { get; set; } // co_mat, co_phep, khong_phep, null
        public string? NguoiCapNhat { get; set; }
        public DateTime? ThoiGianCapNhat { get; set; }
    }

    public class HocSinhThongKeKhoangItem
    {
        public int HocSinhId { get; set; }
        public string MaHocSinh { get; set; } = string.Empty;
        public string HoTen { get; set; } = string.Empty;
        public int LopHocId { get; set; }
        public string TenLop { get; set; } = string.Empty;
        public int SoBuoiCoMat { get; set; }
        public int SoBuoiCoPhep { get; set; }
        public int SoBuoiKhongPhep { get; set; }
        public int TongBuoiGhiNhan => SoBuoiCoMat + SoBuoiCoPhep + SoBuoiKhongPhep;
        public double TiLeChuyenCan => TongBuoiGhiNhan > 0
            ? Math.Round((double)SoBuoiCoMat / TongBuoiGhiNhan * 100, 1)
            : 0;
        public bool CanhBao => SoBuoiKhongPhep >= 2 || (SoBuoiCoPhep + SoBuoiKhongPhep) >= 4;
    }

    public class DiemDanhLichSuItem
    {
        public int Id { get; set; }
        public DateOnly NgayDiemDanh { get; set; }
        public int HocSinhId { get; set; }
        public string MaHocSinh { get; set; } = string.Empty;
        public string HoTen { get; set; } = string.Empty;
        public int LopHocId { get; set; }
        public string TenLop { get; set; } = string.Empty;
        public string TrangThaiChuanHoa { get; set; } = string.Empty;
        public string NguoiCapNhat { get; set; } = "Quản trị viên";
        public DateTime? ThoiGianCapNhat { get; set; }
    }
}
