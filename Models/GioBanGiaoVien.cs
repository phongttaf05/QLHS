using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QLHS.Models;

[Table("gio_ban_giao_vien")]
public partial class GioBanGiaoVien
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("giao_vien_id")]
    public int? GiaoVienId { get; set; }

    [Column("khung_tiet_id")]
    public int? KhungTietId { get; set; }

    [Column("bat_buoc")]
    public bool? BatBuoc { get; set; }

    [Column("ghi_chu")]
    [StringLength(300)]
    public string? GhiChu { get; set; }

    [ForeignKey("GiaoVienId")]
    [InverseProperty("GioBanGiaoViens")]
    public virtual GiaoVien? GiaoVien { get; set; }

    [ForeignKey("KhungTietId")]
    [InverseProperty("GioBanGiaoViens")]
    public virtual KhungTietHoc? KhungTiet { get; set; }
}
