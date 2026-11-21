USE QLHS;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. Thêm 4 bảng mới nếu chưa có
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'hoc_ky')
BEGIN
    CREATE TABLE dbo.hoc_ky (
        id INT IDENTITY(1,1) PRIMARY KEY,
        nam_hoc_id INT NULL REFERENCES dbo.nam_hoc(id),
        so_hoc_ky TINYINT NULL,
        tu_ngay DATE NULL,
        den_ngay DATE NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'khung_tiet_hoc')
BEGIN
    CREATE TABLE dbo.khung_tiet_hoc (
        id INT IDENTITY(1,1) PRIMARY KEY,
        hoc_ky_id INT NULL REFERENCES dbo.hoc_ky(id),
        thu_trong_tuan TINYINT NULL,
        buoi_hoc VARCHAR(10) NULL,
        tiet_hoc TINYINT NULL,
        gio_bat_dau TIME(0) NULL,
        gio_ket_thuc TIME(0) NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'gio_ban_giao_vien')
BEGIN
    CREATE TABLE dbo.gio_ban_giao_vien (
        id INT IDENTITY(1,1) PRIMARY KEY,
        giao_vien_id INT NULL REFERENCES dbo.giao_vien(id),
        khung_tiet_id INT NULL REFERENCES dbo.khung_tiet_hoc(id),
        bat_buoc BIT NULL,
        ghi_chu NVARCHAR(300) NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ban_tkb')
BEGIN
    CREATE TABLE dbo.ban_tkb (
        id INT IDENTITY(1,1) PRIMARY KEY,
        hoc_ky_id INT NULL REFERENCES dbo.hoc_ky(id),
        so_phien_ban INT NULL,
        tu_ngay DATE NULL,
        den_ngay DATE NULL,
        trang_thai VARCHAR(24) NULL,
        dieu_kien_json NVARCHAR(MAX) NULL,
        dau_vao_da_chot_json NVARCHAR(MAX) NULL,
        nguoi_chot_id INT NULL REFERENCES dbo.tai_khoan(id),
        ngay_chot DATETIME2(0) NULL,
        nguoi_duyet_id INT NULL REFERENCES dbo.tai_khoan(id),
        ngay_duyet DATETIME2(0) NULL,
        nguoi_cong_bo_id INT NULL REFERENCES dbo.tai_khoan(id),
        ngay_cong_bo DATETIME2(0) NULL,
        bao_cao_xep_lich NVARCHAR(MAX) NULL
    );
END

-- 2. Đổi tên cột gia_vien_chu_nhiem_id -> giao_vien_chu_nhiem_id trong lop_hoc
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.lop_hoc') AND name = 'gia_vien_chu_nhiem_id')
BEGIN
    EXEC sp_rename 'dbo.lop_hoc.gia_vien_chu_nhiem_id', 'giao_vien_chu_nhiem_id', 'COLUMN';
END

-- 3. Cập nhật bảng cau_hinh_mon_khoi
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.cau_hinh_mon_khoi') AND name = 'hoc_ky_id')
BEGIN
    ALTER TABLE dbo.cau_hinh_mon_khoi ADD hoc_ky_id INT NULL REFERENCES dbo.hoc_ky(id);
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.cau_hinh_mon_khoi') AND name = 'so_tiet_tuan')
BEGIN
    ALTER TABLE dbo.cau_hinh_mon_khoi ADD so_tiet_tuan SMALLINT NULL;
END

-- 4. Cập nhật bảng phan_cong_giang_day
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.phan_cong_giang_day') AND name = 'hoc_ky_id')
BEGIN
    ALTER TABLE dbo.phan_cong_giang_day ADD hoc_ky_id INT NULL REFERENCES dbo.hoc_ky(id);
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.phan_cong_giang_day') AND name = 'so_tiet_tuan')
BEGIN
    ALTER TABLE dbo.phan_cong_giang_day ADD so_tiet_tuan SMALLINT NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.phan_cong_giang_day') AND name = 'tu_ngay')
BEGIN
    ALTER TABLE dbo.phan_cong_giang_day ADD tu_ngay DATE NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.phan_cong_giang_day') AND name = 'den_ngay')
BEGIN
    ALTER TABLE dbo.phan_cong_giang_day ADD den_ngay DATE NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.phan_cong_giang_day') AND name = 'trang_thai_xac_nhan')
BEGIN
    ALTER TABLE dbo.phan_cong_giang_day ADD trang_thai_xac_nhan VARCHAR(24) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.phan_cong_giang_day') AND name = 'nguoi_phan_hoi_id')
BEGIN
    ALTER TABLE dbo.phan_cong_giang_day ADD nguoi_phan_hoi_id INT NULL REFERENCES dbo.tai_khoan(id);
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.phan_cong_giang_day') AND name = 'ngay_phan_hoi')
BEGIN
    ALTER TABLE dbo.phan_cong_giang_day ADD ngay_phan_hoi DATETIME2(0) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.phan_cong_giang_day') AND name = 'y_kien')
BEGIN
    ALTER TABLE dbo.phan_cong_giang_day ADD y_kien NVARCHAR(500) NULL;
END

-- 5. Cập nhật bảng thoi_khoa_bieu
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.thoi_khoa_bieu') AND name = 'ban_tkb_id')
BEGIN
    ALTER TABLE dbo.thoi_khoa_bieu ADD ban_tkb_id INT NULL REFERENCES dbo.ban_tkb(id);
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.thoi_khoa_bieu') AND name = 'phan_cong_id')
BEGIN
    ALTER TABLE dbo.thoi_khoa_bieu ADD phan_cong_id INT NULL REFERENCES dbo.phan_cong_giang_day(id);
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.thoi_khoa_bieu') AND name = 'khung_tiet_id')
BEGIN
    ALTER TABLE dbo.thoi_khoa_bieu ADD khung_tiet_id INT NULL REFERENCES dbo.khung_tiet_hoc(id);
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.thoi_khoa_bieu') AND name = 'thu_trong_tuan')
BEGIN
    ALTER TABLE dbo.thoi_khoa_bieu DROP COLUMN thu_trong_tuan;
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.thoi_khoa_bieu') AND name = 'buoi_hoc')
BEGIN
    ALTER TABLE dbo.thoi_khoa_bieu DROP COLUMN buoi_hoc;
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.thoi_khoa_bieu') AND name = 'tiet_hoc')
BEGIN
    ALTER TABLE dbo.thoi_khoa_bieu DROP COLUMN tiet_hoc;
END

COMMIT TRANSACTION;
GO
