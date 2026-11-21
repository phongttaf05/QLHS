using System.ComponentModel.DataAnnotations.Schema;

namespace QLHS.Models;

[Table("giao_vien_mon_hoc")]
public partial class GiaoVienMonHoc
{
    [Column("giao_vien_id")]
    public int GiaoVienId { get; set; }

    [Column("mon_hoc_id")]
    public int MonHocId { get; set; }

    [ForeignKey("GiaoVienId")]
    [InverseProperty("GiaoVienMonHocs")]
    public virtual GiaoVien GiaoVien { get; set; } = null!;

    [ForeignKey("MonHocId")]
    [InverseProperty("GiaoVienMonHocs")]
    public virtual MonHoc MonHoc { get; set; } = null!;
}
