using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QLHS.Models;

[Table("danh_gia_mon_hoc")]
public partial class DanhGiaMonHoc
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("hoc_sinh_id")]
    public int? HocSinhId { get; set; }

    [Column("mon_hoc_id")]
    public int? MonHocId { get; set; }

    [Column("lop_hoc_id")]
    public int? LopHocId { get; set; }

    [Column("hoc_ky")]
    public int? HocKy { get; set; }

    [Column("diem_giua_ky")]
    public int? DiemGiuaKy { get; set; }

    [Column("diem_cuoi_ky")]
    public int? DiemCuoiKy { get; set; }

    [Column("muc_danh_gia")]
    [StringLength(20)]
    [Unicode(false)]
    public string? MucDanhGia { get; set; }

    [Column("nhan_xet_giao_vien")]
    public string? NhanXetGiaoVien { get; set; }

    [Column("da_khoa_so")]
    public bool? DaKhoaSo { get; set; }

    [Column("thoi_gian_cap_nhat", TypeName = "datetime")]
    public DateTime? ThoiGianCapNhat { get; set; }

    [ForeignKey("HocSinhId")]
    [InverseProperty("DanhGiaMonHocs")]
    public virtual HocSinh? HocSinh { get; set; }

    [ForeignKey("LopHocId")]
    [InverseProperty("DanhGiaMonHocs")]
    public virtual LopHoc? LopHoc { get; set; }

    [ForeignKey("MonHocId")]
    [InverseProperty("DanhGiaMonHocs")]
    public virtual MonHoc? MonHoc { get; set; }
}
