using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QLHS.Models;

[Table("phan_cong_giang_day")]
public partial class PhanCongGiangDay
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("hoc_ky_id")]
    public int? HocKyId { get; set; }

    [Column("so_tiet_tuan")]
    public short? SoTietTuan { get; set; }

    [Column("tu_ngay")]
    public DateOnly? TuNgay { get; set; }

    [Column("den_ngay")]
    public DateOnly? DenNgay { get; set; }

    [Column("trang_thai_xac_nhan")]
    [StringLength(24)]
    [Unicode(false)]
    public string? TrangThaiXacNhan { get; set; }

    [Column("nguoi_phan_hoi_id")]
    public int? NguoiPhanHoiId { get; set; }

    [Column("ngay_phan_hoi")]
    public DateTime? NgayPhanHoi { get; set; }

    [Column("y_kien")]
    [StringLength(500)]
    public string? YKien { get; set; }

    [Column("giao_vien_id")]
    public int? GiaoVienId { get; set; }

    [Column("lop_hoc_id")]
    public int? LopHocId { get; set; }

    [Column("mon_hoc_id")]
    public int? MonHocId { get; set; }

    [ForeignKey("HocKyId")]
    [InverseProperty("PhanCongGiangDays")]
    public virtual HocKy? HocKy { get; set; }

    [ForeignKey("GiaoVienId")]
    [InverseProperty("PhanCongGiangDays")]
    public virtual GiaoVien? GiaoVien { get; set; }

    [ForeignKey("LopHocId")]
    [InverseProperty("PhanCongGiangDays")]
    public virtual LopHoc? LopHoc { get; set; }

    [ForeignKey("MonHocId")]
    [InverseProperty("PhanCongGiangDays")]
    public virtual MonHoc? MonHoc { get; set; }

    [ForeignKey("NguoiPhanHoiId")]
    [InverseProperty("PhanCongGiangDayNguoiPhanHois")]
    public virtual TaiKhoan? NguoiPhanHoi { get; set; }

    [InverseProperty("PhanCong")]
    public virtual ICollection<ThoiKhoaBieu> ThoiKhoaBieus { get; set; } = new List<ThoiKhoaBieu>();
}
