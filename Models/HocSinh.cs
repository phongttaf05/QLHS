using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QLHS.Models;

[Table("hoc_sinh")]
public partial class HocSinh
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("ma_hoc_sinh")]
    [StringLength(50)]
    [Unicode(false)]
    public string? MaHocSinh { get; set; }

    [Column("ho_ten")]
    [StringLength(100)]
    public string? HoTen { get; set; }

    [Column("ngay_sinh")]
    public DateOnly? NgaySinh { get; set; }

    [Column("gioi_tinh")]
    [StringLength(10)]
    public string? GioiTinh { get; set; }

    [Column("trang_thai_hoc")]
    [StringLength(20)]
    [Unicode(false)]
    public string? TrangThaiHoc { get; set; }

    [Column("lop_hoc_hien_tai_id")]
    public int? LopHocHienTaiId { get; set; }

    [Column("ngay_xoa_mem", TypeName = "datetime")]
    public DateTime? NgayXoaMem { get; set; }

    [InverseProperty("HocSinh")]
    public virtual ICollection<DanhGiaDinhKyTongHop> DanhGiaDinhKyTongHops { get; set; } = new List<DanhGiaDinhKyTongHop>();

    [InverseProperty("HocSinh")]
    public virtual ICollection<DanhGiaMonHoc> DanhGiaMonHocs { get; set; } = new List<DanhGiaMonHoc>();

    [InverseProperty("HocSinh")]
    public virtual ICollection<DiemDanh> DiemDanhs { get; set; } = new List<DiemDanh>();

    [ForeignKey("LopHocHienTaiId")]
    [InverseProperty("HocSinhs")]
    public virtual LopHoc? LopHocHienTai { get; set; }

    [InverseProperty("HocSinh")]
    public virtual ICollection<PhuHuynhHocSinh> PhuHuynhHocSinhs { get; set; } = new List<PhuHuynhHocSinh>();
}
