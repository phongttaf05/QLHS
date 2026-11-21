using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QLHS.Models;

[Table("ban_tkb_xac_nhan_gv")]
public partial class BanTkbXacNhanGv
{
    [Column("ban_tkb_id")]
    public int BanTkbId { get; set; }

    [Column("giao_vien_id")]
    public int GiaoVienId { get; set; }

    [Column("trang_thai")]
    [StringLength(24)]
    [Unicode(false)]
    public string TrangThai { get; set; } = "CHO_XAC_NHAN";

    [Column("y_kien")]
    [StringLength(500)]
    public string? YKien { get; set; }

    [Column("ngay_phan_hoi")]
    public DateTime? NgayPhanHoi { get; set; }

    [ForeignKey("BanTkbId")]
    [InverseProperty("BanTkbXacNhanGvs")]
    public virtual BanTkb BanTkb { get; set; } = null!;

    [ForeignKey("GiaoVienId")]
    [InverseProperty("BanTkbXacNhanGvs")]
    public virtual GiaoVien GiaoVien { get; set; } = null!;
}
