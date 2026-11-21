using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QLHS.Models;

[Table("cau_hinh_mon_khoi")]
public partial class CauHinhMonKhoi
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("hoc_ky_id")]
    public int? HocKyId { get; set; }

    [Column("so_tiet_tuan")]
    public short? SoTietTuan { get; set; }

    [Column("khoi_lop_id")]
    public int? KhoiLopId { get; set; }

    [Column("mon_hoc_id")]
    public int? MonHocId { get; set; }

    [Column("co_kiem_tra_giua_ky")]
    public bool? CoKiemTraGiuaKy { get; set; }

    [ForeignKey("HocKyId")]
    [InverseProperty("CauHinhMonKhois")]
    public virtual HocKy? HocKy { get; set; }

    [ForeignKey("KhoiLopId")]
    [InverseProperty("CauHinhMonKhois")]
    public virtual KhoiLop? KhoiLop { get; set; }

    [ForeignKey("MonHocId")]
    [InverseProperty("CauHinhMonKhois")]
    public virtual MonHoc? MonHoc { get; set; }
}
