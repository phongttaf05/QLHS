using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QLHS.Models;

[Table("ban_tkb")]
public partial class BanTkb
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("hoc_ky_id")]
    public int? HocKyId { get; set; }

    [Column("so_phien_ban")]
    public int? SoPhienBan { get; set; }

    [Column("tu_ngay")]
    public DateOnly? TuNgay { get; set; }

    [Column("den_ngay")]
    public DateOnly? DenNgay { get; set; }

    [Column("trang_thai")]
    [StringLength(24)]
    [Unicode(false)]
    public string? TrangThai { get; set; }

    [Column("dieu_kien_json")]
    public string? DieuKienJson { get; set; }

    [Column("dau_vao_da_chot_json")]
    public string? DauVaoDaChotJson { get; set; }

    [Column("nguoi_chot_id")]
    public int? NguoiChotId { get; set; }

    [Column("ngay_chot")]
    public DateTime? NgayChot { get; set; }

    [Column("nguoi_duyet_id")]
    public int? NguoiDuyetId { get; set; }

    [Column("ngay_duyet")]
    public DateTime? NgayDuyet { get; set; }

    [Column("nguoi_cong_bo_id")]
    public int? NguoiCongBoId { get; set; }

    [Column("ngay_cong_bo")]
    public DateTime? NgayCongBo { get; set; }

    [Column("bao_cao_xep_lich")]
    public string? BaoCaoXepLich { get; set; }

    [ForeignKey("HocKyId")]
    [InverseProperty("BanTkbs")]
    public virtual HocKy? HocKy { get; set; }

    [ForeignKey("NguoiChotId")]
    [InverseProperty("BanTkbNguoiChots")]
    public virtual TaiKhoan? NguoiChot { get; set; }

    [ForeignKey("NguoiDuyetId")]
    [InverseProperty("BanTkbNguoiDuyets")]
    public virtual TaiKhoan? NguoiDuyet { get; set; }

    [ForeignKey("NguoiCongBoId")]
    [InverseProperty("BanTkbNguoiCongBos")]
    public virtual TaiKhoan? NguoiCongBo { get; set; }

    [InverseProperty("BanTkb")]
    public virtual ICollection<ThoiKhoaBieu> ThoiKhoaBieus { get; set; } = new List<ThoiKhoaBieu>();

    [InverseProperty("BanTkb")]
    public virtual ICollection<BanTkbXacNhanGv> BanTkbXacNhanGvs { get; set; } = new List<BanTkbXacNhanGv>();
}
