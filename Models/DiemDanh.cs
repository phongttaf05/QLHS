using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QLHS.Models;

[Table("diem_danh")]
public partial class DiemDanh
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("hoc_sinh_id")]
    public int? HocSinhId { get; set; }

    [Column("lop_hoc_id")]
    public int? LopHocId { get; set; }

    [Column("ngay_diem_danh")]
    public DateOnly? NgayDiemDanh { get; set; }

    [Column("trang_thai")]
    [StringLength(20)]
    [Unicode(false)]
    public string? TrangThai { get; set; }

    [Column("giao_vien_chot_id")]
    public int? GiaoVienChotId { get; set; }

    [Column("thoi_gian_cap_nhat", TypeName = "datetime")]
    public DateTime? ThoiGianCapNhat { get; set; }

    [ForeignKey("GiaoVienChotId")]
    [InverseProperty("DiemDanhs")]
    public virtual GiaoVien? GiaoVienChot { get; set; }

    [ForeignKey("HocSinhId")]
    [InverseProperty("DiemDanhs")]
    public virtual HocSinh? HocSinh { get; set; }

    [ForeignKey("LopHocId")]
    [InverseProperty("DiemDanhs")]
    public virtual LopHoc? LopHoc { get; set; }
}
