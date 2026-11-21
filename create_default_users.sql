USE QLHS;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Cập nhật tất cả tài khoản hiện có về mật khẩu 123456 và trạng thái hoat_dong
UPDATE dbo.tai_khoan 
SET mat_khau_hash = '123456', 
    trang_thai = 'hoat_dong';

-- 1. Tài khoản Quản trị viên chính: 'admin'
IF NOT EXISTS (SELECT 1 FROM dbo.tai_khoan WHERE ten_dang_nhap = 'admin')
BEGIN
    INSERT INTO dbo.tai_khoan (ten_dang_nhap, mat_khau_hash, email, so_dien_thoai, vai_tro, trang_thai, ngay_tao)
    VALUES ('admin', '123456', 'admin@qlhs.edu.vn', '0901234567', 'admin', 'hoat_dong', GETUTCDATE());
END
ELSE
BEGIN
    UPDATE dbo.tai_khoan 
    SET mat_khau_hash = '123456', vai_tro = 'admin', trang_thai = 'hoat_dong'
    WHERE ten_dang_nhap = 'admin';
END

-- 2. Tài khoản Giáo viên mẫu: 'giaovien'
IF NOT EXISTS (SELECT 1 FROM dbo.tai_khoan WHERE ten_dang_nhap = 'giaovien')
BEGIN
    INSERT INTO dbo.tai_khoan (ten_dang_nhap, mat_khau_hash, email, so_dien_thoai, vai_tro, trang_thai, ngay_tao)
    VALUES ('giaovien', '123456', 'giaovien@qlhs.edu.vn', '0912345678', 'giao_vien', 'hoat_dong', GETUTCDATE());

    DECLARE @NewGvTaiKhoanId INT = SCOPE_IDENTITY();
    
    INSERT INTO dbo.giao_vien (tai_khoan_id, ho_ten, cccd)
    VALUES (@NewGvTaiKhoanId, N'Thầy Giáo Mẫu', '001200000001');
END
ELSE
BEGIN
    UPDATE dbo.tai_khoan 
    SET mat_khau_hash = '123456', vai_tro = 'giao_vien', trang_thai = 'hoat_dong'
    WHERE ten_dang_nhap = 'giaovien';
END

-- 3. Tài khoản Phụ huynh mẫu: 'phuhuynh'
IF NOT EXISTS (SELECT 1 FROM dbo.tai_khoan WHERE ten_dang_nhap = 'phuhuynh')
BEGIN
    INSERT INTO dbo.tai_khoan (ten_dang_nhap, mat_khau_hash, email, so_dien_thoai, vai_tro, trang_thai, ngay_tao)
    VALUES ('phuhuynh', '123456', 'phuhuynh@qlhs.edu.vn', '0987654321', 'phu_huynh', 'hoat_dong', GETUTCDATE());

    DECLARE @NewPhTaiKhoanId INT = SCOPE_IDENTITY();

    INSERT INTO dbo.phu_huynh (tai_khoan_id, ho_ten)
    VALUES (@NewPhTaiKhoanId, N'Phụ Huynh Học Sinh');

    DECLARE @NewPhId INT = SCOPE_IDENTITY();

    -- Liên kết với học sinh đầu tiên nếu có
    DECLARE @HsId INT = (SELECT TOP 1 id FROM dbo.hoc_sinh WHERE ngay_xoa_mem IS NULL ORDER BY id);
    IF @HsId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.phu_huynh_hoc_sinh WHERE phu_huynh_id = @NewPhId AND hoc_sinh_id = @HsId)
        BEGIN
            INSERT INTO dbo.phu_huynh_hoc_sinh (phu_huynh_id, hoc_sinh_id)
            VALUES (@NewPhId, @HsId);
        END
    END
END
ELSE
BEGIN
    UPDATE dbo.tai_khoan 
    SET mat_khau_hash = '123456', vai_tro = 'phu_huynh', trang_thai = 'hoat_dong'
    WHERE ten_dang_nhap = 'phuhuynh';
END

COMMIT TRANSACTION;
GO
