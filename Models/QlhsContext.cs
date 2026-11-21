using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace QLHS.Models;

public partial class QlhsContext : DbContext
{
    public QlhsContext()
    {
    }

    public QlhsContext(DbContextOptions<QlhsContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BanTkb> BanTkbs { get; set; }

    public virtual DbSet<CauHinhMonKhoi> CauHinhMonKhois { get; set; }

    public virtual DbSet<DanhGiaDinhKyTongHop> DanhGiaDinhKyTongHops { get; set; }

    public virtual DbSet<DanhGiaMonHoc> DanhGiaMonHocs { get; set; }

    public virtual DbSet<DiemDanh> DiemDanhs { get; set; }

    public virtual DbSet<GiaoVien> GiaoViens { get; set; }

    public virtual DbSet<GioBanGiaoVien> GioBanGiaoViens { get; set; }

    public virtual DbSet<HocKy> HocKies { get; set; }

    public virtual DbSet<HocSinh> HocSinhs { get; set; }

    public virtual DbSet<KhoiLop> KhoiLops { get; set; }

    public virtual DbSet<KhungTietHoc> KhungTietHocs { get; set; }

    public virtual DbSet<LopHoc> LopHocs { get; set; }

    public virtual DbSet<MonHoc> MonHocs { get; set; }

    public virtual DbSet<NamHoc> NamHocs { get; set; }

    public virtual DbSet<PhanCongGiangDay> PhanCongGiangDays { get; set; }

    public virtual DbSet<PhuHuynh> PhuHuynhs { get; set; }

    public virtual DbSet<PhuHuynhHocSinh> PhuHuynhHocSinhs { get; set; }

    public virtual DbSet<TaiKhoan> TaiKhoans { get; set; }

    public virtual DbSet<ThoiKhoaBieu> ThoiKhoaBieus { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Server=localhost;Database=QLHS;User Id=sa;Password=123456;TrustServerCertificate=True;");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BanTkb>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TrangThai).HasMaxLength(24).IsUnicode(false);
            entity.Property(e => e.NgayChot).HasColumnType("datetime2(0)");
            entity.Property(e => e.NgayDuyet).HasColumnType("datetime2(0)");
            entity.Property(e => e.NgayCongBo).HasColumnType("datetime2(0)");

            entity.HasOne(d => d.HocKy)
                .WithMany(p => p.BanTkbs)
                .HasForeignKey(d => d.HocKyId);

            entity.HasOne(d => d.NguoiChot)
                .WithMany(p => p.BanTkbNguoiChots)
                .HasForeignKey(d => d.NguoiChotId);

            entity.HasOne(d => d.NguoiDuyet)
                .WithMany(p => p.BanTkbNguoiDuyets)
                .HasForeignKey(d => d.NguoiDuyetId);

            entity.HasOne(d => d.NguoiCongBo)
                .WithMany(p => p.BanTkbNguoiCongBos)
                .HasForeignKey(d => d.NguoiCongBoId);
        });

        modelBuilder.Entity<CauHinhMonKhoi>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SoTietTuan).HasColumnType("smallint");

            entity.HasOne(d => d.HocKy)
                .WithMany(p => p.CauHinhMonKhois)
                .HasForeignKey(d => d.HocKyId);

            entity.HasOne(d => d.KhoiLop)
                .WithMany(p => p.CauHinhMonKhois)
                .HasForeignKey(d => d.KhoiLopId);

            entity.HasOne(d => d.MonHoc)
                .WithMany(p => p.CauHinhMonKhois)
                .HasForeignKey(d => d.MonHocId);
        });

        modelBuilder.Entity<DanhGiaDinhKyTongHop>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.NangLucTuChuTuHoc).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.NangLucGiaoTiepHopTac).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.NangLucGiaiQuyetVanDe).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.PhamChatYeuNuoc).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.PhamChatNhanAi).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.PhamChatChamChi).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.PhamChatTrungThuc).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.PhamChatTrachNhiem).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.KetQuaCuoiNam).HasMaxLength(50);
            entity.Property(e => e.GoiYKhenThuong).HasMaxLength(50);

            entity.HasOne(d => d.HocSinh)
                .WithMany(p => p.DanhGiaDinhKyTongHops)
                .HasForeignKey(d => d.HocSinhId);

            entity.HasOne(d => d.LopHoc)
                .WithMany(p => p.DanhGiaDinhKyTongHops)
                .HasForeignKey(d => d.LopHocId);
        });

        modelBuilder.Entity<DanhGiaMonHoc>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.MucDanhGia).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.ThoiGianCapNhat).HasColumnType("datetime");

            entity.HasOne(d => d.HocSinh)
                .WithMany(p => p.DanhGiaMonHocs)
                .HasForeignKey(d => d.HocSinhId);

            entity.HasOne(d => d.LopHoc)
                .WithMany(p => p.DanhGiaMonHocs)
                .HasForeignKey(d => d.LopHocId);

            entity.HasOne(d => d.MonHoc)
                .WithMany(p => p.DanhGiaMonHocs)
                .HasForeignKey(d => d.MonHocId);
        });

        modelBuilder.Entity<DiemDanh>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TrangThai).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.ThoiGianCapNhat).HasColumnType("datetime");

            entity.HasOne(d => d.GiaoVienChot)
                .WithMany(p => p.DiemDanhs)
                .HasForeignKey(d => d.GiaoVienChotId);

            entity.HasOne(d => d.HocSinh)
                .WithMany(p => p.DiemDanhs)
                .HasForeignKey(d => d.HocSinhId);

            entity.HasOne(d => d.LopHoc)
                .WithMany(p => p.DiemDanhs)
                .HasForeignKey(d => d.LopHocId);
        });

        modelBuilder.Entity<GiaoVien>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.HoTen).HasMaxLength(100);
            entity.Property(e => e.Cccd).HasMaxLength(20).IsUnicode(false);

            entity.HasOne(d => d.TaiKhoan)
                .WithMany(p => p.GiaoViens)
                .HasForeignKey(d => d.TaiKhoanId);
        });

        modelBuilder.Entity<GioBanGiaoVien>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.GhiChu).HasMaxLength(300);

            entity.HasOne(d => d.GiaoVien)
                .WithMany(p => p.GioBanGiaoViens)
                .HasForeignKey(d => d.GiaoVienId);

            entity.HasOne(d => d.KhungTiet)
                .WithMany(p => p.GioBanGiaoViens)
                .HasForeignKey(d => d.KhungTietId);
        });

        modelBuilder.Entity<HocKy>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SoHocKy).HasColumnType("tinyint");

            entity.HasOne(d => d.NamHoc)
                .WithMany(p => p.HocKies)
                .HasForeignKey(d => d.NamHocId);
        });

        modelBuilder.Entity<HocSinh>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.MaHocSinh).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.HoTen).HasMaxLength(100);
            entity.Property(e => e.GioiTinh).HasMaxLength(10);
            entity.Property(e => e.TrangThaiHoc).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.NgayXoaMem).HasColumnType("datetime");

            entity.HasOne(d => d.LopHocHienTai)
                .WithMany(p => p.HocSinhs)
                .HasForeignKey(d => d.LopHocHienTaiId);
        });

        modelBuilder.Entity<KhoiLop>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<KhungTietHoc>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ThuTrongTuan).HasColumnType("tinyint");
            entity.Property(e => e.BuoiHoc).HasMaxLength(10).IsUnicode(false);
            entity.Property(e => e.TietHoc).HasColumnType("tinyint");
            entity.Property(e => e.GioBatDau).HasColumnType("time(0)");
            entity.Property(e => e.GioKetThuc).HasColumnType("time(0)");

            entity.HasOne(d => d.HocKy)
                .WithMany(p => p.KhungTietHocs)
                .HasForeignKey(d => d.HocKyId);
        });

        modelBuilder.Entity<LopHoc>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TenLop).HasMaxLength(20).IsUnicode(false);

            entity.HasOne(d => d.GiaoVienChuNhiem)
                .WithMany(p => p.LopHocs)
                .HasForeignKey(d => d.GiaoVienChuNhiemId);

            entity.HasOne(d => d.KhoiLop)
                .WithMany(p => p.LopHocs)
                .HasForeignKey(d => d.KhoiLopId);

            entity.HasOne(d => d.NamHoc)
                .WithMany(p => p.LopHocs)
                .HasForeignKey(d => d.NamHocId);
        });

        modelBuilder.Entity<MonHoc>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TenMon).HasMaxLength(100);
            entity.Property(e => e.HinhThucDanhGia).HasMaxLength(20).IsUnicode(false);
        });

        modelBuilder.Entity<NamHoc>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TenNamHoc).HasMaxLength(20).IsUnicode(false);
        });

        modelBuilder.Entity<PhanCongGiangDay>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SoTietTuan).HasColumnType("smallint");
            entity.Property(e => e.TrangThaiXacNhan).HasMaxLength(24).IsUnicode(false);
            entity.Property(e => e.NgayPhanHoi).HasColumnType("datetime2(0)");
            entity.Property(e => e.YKien).HasMaxLength(500);

            entity.HasOne(d => d.HocKy)
                .WithMany(p => p.PhanCongGiangDays)
                .HasForeignKey(d => d.HocKyId);

            entity.HasOne(d => d.GiaoVien)
                .WithMany(p => p.PhanCongGiangDays)
                .HasForeignKey(d => d.GiaoVienId);

            entity.HasOne(d => d.LopHoc)
                .WithMany(p => p.PhanCongGiangDays)
                .HasForeignKey(d => d.LopHocId);

            entity.HasOne(d => d.MonHoc)
                .WithMany(p => p.PhanCongGiangDays)
                .HasForeignKey(d => d.MonHocId);

            entity.HasOne(d => d.NguoiPhanHoi)
                .WithMany(p => p.PhanCongGiangDayNguoiPhanHois)
                .HasForeignKey(d => d.NguoiPhanHoiId);
        });

        modelBuilder.Entity<PhuHuynh>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.HoTen).HasMaxLength(100);

            entity.HasOne(d => d.TaiKhoan)
                .WithMany(p => p.PhuHuynhs)
                .HasForeignKey(d => d.TaiKhoanId);
        });

        modelBuilder.Entity<PhuHuynhHocSinh>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(d => d.HocSinh)
                .WithMany(p => p.PhuHuynhHocSinhs)
                .HasForeignKey(d => d.HocSinhId);

            entity.HasOne(d => d.PhuHuynh)
                .WithMany(p => p.PhuHuynhHocSinhs)
                .HasForeignKey(d => d.PhuHuynhId);
        });

        modelBuilder.Entity<TaiKhoan>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TenDangNhap).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.MatKhauHash).HasMaxLength(255).IsUnicode(false);
            entity.Property(e => e.Email).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.SoDienThoai).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.VaiTro).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.TrangThai).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.NgayTao).HasColumnType("datetime");
        });

        modelBuilder.Entity<ThoiKhoaBieu>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(d => d.BanTkb)
                .WithMany(p => p.ThoiKhoaBieus)
                .HasForeignKey(d => d.BanTkbId);

            entity.HasOne(d => d.PhanCong)
                .WithMany(p => p.ThoiKhoaBieus)
                .HasForeignKey(d => d.PhanCongId);

            entity.HasOne(d => d.KhungTiet)
                .WithMany(p => p.ThoiKhoaBieus)
                .HasForeignKey(d => d.KhungTietId);

            entity.HasOne(d => d.GiaoVien)
                .WithMany(p => p.ThoiKhoaBieus)
                .HasForeignKey(d => d.GiaoVienId);

            entity.HasOne(d => d.LopHoc)
                .WithMany(p => p.ThoiKhoaBieus)
                .HasForeignKey(d => d.LopHocId);

            entity.HasOne(d => d.MonHoc)
                .WithMany(p => p.ThoiKhoaBieus)
                .HasForeignKey(d => d.MonHocId);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
