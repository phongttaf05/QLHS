using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QLHS.Models;

[Table("phu_huynh_hoc_sinh")]
public partial class PhuHuynhHocSinh
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("phu_huynh_id")]
    public int? PhuHuynhId { get; set; }

    [Column("hoc_sinh_id")]
    public int? HocSinhId { get; set; }

    [ForeignKey("HocSinhId")]
    [InverseProperty("PhuHuynhHocSinhs")]
    public virtual HocSinh? HocSinh { get; set; }

    [ForeignKey("PhuHuynhId")]
    [InverseProperty("PhuHuynhHocSinhs")]
    public virtual PhuHuynh? PhuHuynh { get; set; }
}
