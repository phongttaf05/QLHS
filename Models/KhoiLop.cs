using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QLHS.Models;

[Table("khoi_lop")]
public partial class KhoiLop
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("so_khoi")]
    public int? SoKhoi { get; set; }

    [InverseProperty("KhoiLop")]
    public virtual ICollection<CauHinhMonKhoi> CauHinhMonKhois { get; set; } = new List<CauHinhMonKhoi>();

    [InverseProperty("KhoiLop")]
    public virtual ICollection<LopHoc> LopHocs { get; set; } = new List<LopHoc>();
}
