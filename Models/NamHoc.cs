using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QLHS.Models;

[Table("nam_hoc")]
public partial class NamHoc
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("ten_nam_hoc")]
    [StringLength(20)]
    [Unicode(false)]
    public string? TenNamHoc { get; set; }

    [Column("hoc_ky_hien_tai")]
    public int? HocKyHienTai { get; set; }

    [Column("dang_mo_khoa")]
    public bool? DangMoKhoa { get; set; }

    [InverseProperty("NamHoc")]
    public virtual ICollection<LopHoc> LopHocs { get; set; } = new List<LopHoc>();

    [InverseProperty("NamHoc")]
    public virtual ICollection<HocKy> HocKies { get; set; } = new List<HocKy>();
}
