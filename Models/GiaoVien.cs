using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QLHS.Models;

[Table("giao_vien")]
public partial class GiaoVien
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tai_khoan_id")]
    public int? TaiKhoanId { get; set; }

    [Column("ho_ten")]
    [StringLength(100)]
    public string? HoTen { get; set; }

    [Column("cccd")]
    [StringLength(20)]
    [Unicode(false)]
    public string? Cccd { get; set; }

    [InverseProperty("GiaoVienChot")]
    public virtual ICollection<DiemDanh> DiemDanhs { get; set; } = new List<DiemDanh>();

    [InverseProperty("GiaoVienChuNhiem")]
    public virtual ICollection<LopHoc> LopHocs { get; set; } = new List<LopHoc>();

    [InverseProperty("GiaoVien")]
    public virtual ICollection<PhanCongGiangDay> PhanCongGiangDays { get; set; } = new List<PhanCongGiangDay>();

    [InverseProperty("GiaoVien")]
    public virtual ICollection<GioBanGiaoVien> GioBanGiaoViens { get; set; } = new List<GioBanGiaoVien>();

    [ForeignKey("TaiKhoanId")]
    [InverseProperty("GiaoViens")]
    public virtual TaiKhoan? TaiKhoan { get; set; }

    [InverseProperty("GiaoVien")]
    public virtual ICollection<ThoiKhoaBieu> ThoiKhoaBieus { get; set; } = new List<ThoiKhoaBieu>();
}
