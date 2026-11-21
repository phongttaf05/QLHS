using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QLHS.Models;

[Table("thoi_khoa_bieu")]
public partial class ThoiKhoaBieu
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("ban_tkb_id")]
    public int? BanTkbId { get; set; }

    [Column("phan_cong_id")]
    public int? PhanCongId { get; set; }

    [Column("khung_tiet_id")]
    public int? KhungTietId { get; set; }

    [Column("lop_hoc_id")]
    public int? LopHocId { get; set; }

    [Column("mon_hoc_id")]
    public int? MonHocId { get; set; }

    [Column("giao_vien_id")]
    public int? GiaoVienId { get; set; }

    [ForeignKey("BanTkbId")]
    [InverseProperty("ThoiKhoaBieus")]
    public virtual BanTkb? BanTkb { get; set; }

    [ForeignKey("PhanCongId")]
    [InverseProperty("ThoiKhoaBieus")]
    public virtual PhanCongGiangDay? PhanCong { get; set; }

    [ForeignKey("KhungTietId")]
    [InverseProperty("ThoiKhoaBieus")]
    public virtual KhungTietHoc? KhungTiet { get; set; }

    [ForeignKey("GiaoVienId")]
    [InverseProperty("ThoiKhoaBieus")]
    public virtual GiaoVien? GiaoVien { get; set; }

    [ForeignKey("LopHocId")]
    [InverseProperty("ThoiKhoaBieus")]
    public virtual LopHoc? LopHoc { get; set; }

    [ForeignKey("MonHocId")]
    [InverseProperty("ThoiKhoaBieus")]
    public virtual MonHoc? MonHoc { get; set; }
}
