using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QLHS.Models;

[Table("hoc_ky")]
public partial class HocKy
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("nam_hoc_id")]
    public int? NamHocId { get; set; }

    [Column("so_hoc_ky")]
    public byte? SoHocKy { get; set; }

    [Column("tu_ngay")]
    public DateOnly? TuNgay { get; set; }

    [Column("den_ngay")]
    public DateOnly? DenNgay { get; set; }

    [ForeignKey("NamHocId")]
    [InverseProperty("HocKies")]
    public virtual NamHoc? NamHoc { get; set; }

    [InverseProperty("HocKy")]
    public virtual ICollection<CauHinhMonKhoi> CauHinhMonKhois { get; set; } = new List<CauHinhMonKhoi>();

    [InverseProperty("HocKy")]
    public virtual ICollection<KhungTietHoc> KhungTietHocs { get; set; } = new List<KhungTietHoc>();

    [InverseProperty("HocKy")]
    public virtual ICollection<BanTkb> BanTkbs { get; set; } = new List<BanTkb>();

    [InverseProperty("HocKy")]
    public virtual ICollection<PhanCongGiangDay> PhanCongGiangDays { get; set; } = new List<PhanCongGiangDay>();
}
