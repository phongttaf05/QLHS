using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QLHS.Models;

[Table("lop_hoc")]
public partial class LopHoc
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("ten_lop")]
    [StringLength(20)]
    [Unicode(false)]
    public string? TenLop { get; set; }

    [Column("khoi_lop_id")]
    public int? KhoiLopId { get; set; }

    [Column("nam_hoc_id")]
    public int? NamHocId { get; set; }

    [Column("giao_vien_chu_nhiem_id")]
    public int? GiaoVienChuNhiemId { get; set; }

    [NotMapped]
    public int? GiaVienChuNhiemId
    {
        get => GiaoVienChuNhiemId;
        set => GiaoVienChuNhiemId = value;
    }

    [InverseProperty("LopHoc")]
    public virtual ICollection<DanhGiaDinhKyTongHop> DanhGiaDinhKyTongHops { get; set; } = new List<DanhGiaDinhKyTongHop>();

    [InverseProperty("LopHoc")]
    public virtual ICollection<DanhGiaMonHoc> DanhGiaMonHocs { get; set; } = new List<DanhGiaMonHoc>();

    [InverseProperty("LopHoc")]
    public virtual ICollection<DiemDanh> DiemDanhs { get; set; } = new List<DiemDanh>();

    [ForeignKey("GiaoVienChuNhiemId")]
    [InverseProperty("LopHocs")]
    public virtual GiaoVien? GiaoVienChuNhiem { get; set; }

    [NotMapped]
    public virtual GiaoVien? GiaVienChuNhiem
    {
        get => GiaoVienChuNhiem;
        set => GiaoVienChuNhiem = value;
    }

    [InverseProperty("LopHocHienTai")]
    public virtual ICollection<HocSinh> HocSinhs { get; set; } = new List<HocSinh>();

    [ForeignKey("KhoiLopId")]
    [InverseProperty("LopHocs")]
    public virtual KhoiLop? KhoiLop { get; set; }

    [ForeignKey("NamHocId")]
    [InverseProperty("LopHocs")]
    public virtual NamHoc? NamHoc { get; set; }

    [InverseProperty("LopHoc")]
    public virtual ICollection<PhanCongGiangDay> PhanCongGiangDays { get; set; } = new List<PhanCongGiangDay>();

    [InverseProperty("LopHoc")]
    public virtual ICollection<ThoiKhoaBieu> ThoiKhoaBieus { get; set; } = new List<ThoiKhoaBieu>();
}
