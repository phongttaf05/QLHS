using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QLHS.Models;

[Table("mon_hoc")]
public partial class MonHoc
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("ten_mon")]
    [StringLength(100)]
    public string? TenMon { get; set; }

    [Column("hinh_thuc_danh_gia")]
    [StringLength(20)]
    [Unicode(false)]
    public string? HinhThucDanhGia { get; set; }

    [InverseProperty("MonHoc")]
    public virtual ICollection<CauHinhMonKhoi> CauHinhMonKhois { get; set; } = new List<CauHinhMonKhoi>();

    [InverseProperty("MonHoc")]
    public virtual ICollection<DanhGiaMonHoc> DanhGiaMonHocs { get; set; } = new List<DanhGiaMonHoc>();

    [InverseProperty("MonHoc")]
    public virtual ICollection<PhanCongGiangDay> PhanCongGiangDays { get; set; } = new List<PhanCongGiangDay>();

    [InverseProperty("MonHoc")]
    public virtual ICollection<ThoiKhoaBieu> ThoiKhoaBieus { get; set; } = new List<ThoiKhoaBieu>();

    [InverseProperty("MonHoc")]
    public virtual ICollection<GiaoVienMonHoc> GiaoVienMonHocs { get; set; } = new List<GiaoVienMonHoc>();
}
