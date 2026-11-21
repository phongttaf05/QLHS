using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QLHS.Models;

[Table("danh_gia_dinh_ky_tong_hop")]
public partial class DanhGiaDinhKyTongHop
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("hoc_sinh_id")]
    public int? HocSinhId { get; set; }

    [Column("lop_hoc_id")]
    public int? LopHocId { get; set; }

    [Column("hoc_ky")]
    public int? HocKy { get; set; }

    [Column("nang_luc_tu_chu_tu_hoc")]
    [StringLength(20)]
    [Unicode(false)]
    public string? NangLucTuChuTuHoc { get; set; }

    [Column("nang_luc_giao_tiep_hop_tac")]
    [StringLength(20)]
    [Unicode(false)]
    public string? NangLucGiaoTiepHopTac { get; set; }

    [Column("nang_luc_giai_quyet_van_de")]
    [StringLength(20)]
    [Unicode(false)]
    public string? NangLucGiaiQuyetVanDe { get; set; }

    [Column("pham_chat_yeu_nuoc")]
    [StringLength(20)]
    [Unicode(false)]
    public string? PhamChatYeuNuoc { get; set; }

    [Column("pham_chat_nhan_ai")]
    [StringLength(20)]
    [Unicode(false)]
    public string? PhamChatNhanAi { get; set; }

    [Column("pham_chat_cham_chi")]
    [StringLength(20)]
    [Unicode(false)]
    public string? PhamChatChamChi { get; set; }

    [Column("pham_chat_trung_thuc")]
    [StringLength(20)]
    [Unicode(false)]
    public string? PhamChatTrungThuc { get; set; }

    [Column("pham_chat_trach_nhiem")]
    [StringLength(20)]
    [Unicode(false)]
    public string? PhamChatTrachNhiem { get; set; }

    [Column("ket_qua_cuoi_nam")]
    [StringLength(50)]
    public string? KetQuaCuoiNam { get; set; }

    [Column("goi_y_khen_thuong")]
    [StringLength(50)]
    public string? GoiYKhenThuong { get; set; }

    [Column("da_khoa_so")]
    public bool? DaKhoaSo { get; set; }

    [ForeignKey("HocSinhId")]
    [InverseProperty("DanhGiaDinhKyTongHops")]
    public virtual HocSinh? HocSinh { get; set; }

    [ForeignKey("LopHocId")]
    [InverseProperty("DanhGiaDinhKyTongHops")]
    public virtual LopHoc? LopHoc { get; set; }
}
