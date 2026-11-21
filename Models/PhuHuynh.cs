using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QLHS.Models;

[Table("phu_huynh")]
public partial class PhuHuynh
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tai_khoan_id")]
    public int? TaiKhoanId { get; set; }

    [Column("ho_ten")]
    [StringLength(100)]
    public string? HoTen { get; set; }

    [InverseProperty("PhuHuynh")]
    public virtual ICollection<PhuHuynhHocSinh> PhuHuynhHocSinhs { get; set; } = new List<PhuHuynhHocSinh>();

    [ForeignKey("TaiKhoanId")]
    [InverseProperty("PhuHuynhs")]
    public virtual TaiKhoan? TaiKhoan { get; set; }
}
