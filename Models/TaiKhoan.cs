using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QLHS.Models;

[Table("tai_khoan")]
public partial class TaiKhoan
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("ten_dang_nhap")]
    [StringLength(50)]
    [Unicode(false)]
    public string? TenDangNhap { get; set; }

    [Column("mat_khau_hash")]
    [StringLength(255)]
    [Unicode(false)]
    public string? MatKhauHash { get; set; }

    [Column("email")]
    [StringLength(100)]
    [Unicode(false)]
    public string? Email { get; set; }

    [Column("so_dien_thoai")]
    [StringLength(20)]
    [Unicode(false)]
    public string? SoDienThoai { get; set; }

    [Column("vai_tro")]
    [StringLength(20)]
    [Unicode(false)]
    public string? VaiTro { get; set; }

    [Column("trang_thai")]
    [StringLength(20)]
    [Unicode(false)]
    public string? TrangThai { get; set; }

    [Column("ngay_tao", TypeName = "datetime")]
    public DateTime? NgayTao { get; set; }

    [InverseProperty("TaiKhoan")]
    public virtual ICollection<GiaoVien> GiaoViens { get; set; } = new List<GiaoVien>();

    [InverseProperty("TaiKhoan")]
    public virtual ICollection<PhuHuynh> PhuHuynhs { get; set; } = new List<PhuHuynh>();

    [InverseProperty("NguoiPhanHoi")]
    public virtual ICollection<PhanCongGiangDay> PhanCongGiangDayNguoiPhanHois { get; set; } = new List<PhanCongGiangDay>();

    [InverseProperty("NguoiChot")]
    public virtual ICollection<BanTkb> BanTkbNguoiChots { get; set; } = new List<BanTkb>();

    [InverseProperty("NguoiDuyet")]
    public virtual ICollection<BanTkb> BanTkbNguoiDuyets { get; set; } = new List<BanTkb>();

    [InverseProperty("NguoiCongBo")]
    public virtual ICollection<BanTkb> BanTkbNguoiCongBos { get; set; } = new List<BanTkb>();
}
