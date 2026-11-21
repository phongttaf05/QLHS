using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QLHS.Models;

[Table("khung_tiet_hoc")]
public partial class KhungTietHoc
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("hoc_ky_id")]
    public int? HocKyId { get; set; }

    [Column("thu_trong_tuan")]
    public byte? ThuTrongTuan { get; set; }

    [Column("buoi_hoc")]
    [StringLength(10)]
    [Unicode(false)]
    public string? BuoiHoc { get; set; }

    [Column("tiet_hoc")]
    public byte? TietHoc { get; set; }

    [Column("gio_bat_dau")]
    public TimeOnly? GioBatDau { get; set; }

    [Column("gio_ket_thuc")]
    public TimeOnly? GioKetThuc { get; set; }

    [ForeignKey("HocKyId")]
    [InverseProperty("KhungTietHocs")]
    public virtual HocKy? HocKy { get; set; }

    [InverseProperty("KhungTiet")]
    public virtual ICollection<GioBanGiaoVien> GioBanGiaoViens { get; set; } = new List<GioBanGiaoVien>();

    [InverseProperty("KhungTiet")]
    public virtual ICollection<ThoiKhoaBieu> ThoiKhoaBieus { get; set; } = new List<ThoiKhoaBieu>();
}
