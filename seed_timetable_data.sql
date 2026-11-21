USE QLHS;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. Lấy hoặc tạo Năm học đang mở khóa (2026-2027)
DECLARE @NamHocId INT = (SELECT TOP 1 id FROM dbo.nam_hoc WHERE dang_mo_khoa = 1 ORDER BY id);
IF @NamHocId IS NULL
BEGIN
    INSERT INTO dbo.nam_hoc (ten_nam_hoc, hoc_ky_hien_tai, dang_mo_khoa)
    VALUES ('2026-2027', 1, 1);
    SET @NamHocId = SCOPE_IDENTITY();
END

-- 2. Tạo Học kỳ 1 cho năm học nếu chưa có
DECLARE @HocKyId INT = (SELECT TOP 1 id FROM dbo.hoc_ky WHERE nam_hoc_id = @NamHocId AND so_hoc_ky = 1);
IF @HocKyId IS NULL
BEGIN
    INSERT INTO dbo.hoc_ky (nam_hoc_id, so_hoc_ky, tu_ngay, den_ngay)
    VALUES (@NamHocId, 1, '2026-09-05', '2027-01-15');
    SET @HocKyId = SCOPE_IDENTITY();
END

-- 3. Tạo 35 khung tiết chuẩn cho Học kỳ 1 (Thứ 2 đến Thứ 6: Sáng 4 tiết, Chiều 3 tiết)
IF NOT EXISTS (SELECT 1 FROM dbo.khung_tiet_hoc WHERE hoc_ky_id = @HocKyId)
BEGIN
    DECLARE @thu TINYINT = 2;
    WHILE @thu <= 6
    BEGIN
        -- Sáng (Tiết 1 -> 4)
        INSERT INTO dbo.khung_tiet_hoc (hoc_ky_id, thu_trong_tuan, buoi_hoc, tiet_hoc, gio_bat_dau, gio_ket_thuc)
        VALUES 
        (@HocKyId, @thu, 'SANG', 1, '07:30:00', '08:05:00'),
        (@HocKyId, @thu, 'SANG', 2, '08:15:00', '08:50:00'),
        (@HocKyId, @thu, 'SANG', 3, '09:15:00', '09:50:00'),
        (@HocKyId, @thu, 'SANG', 4, '10:00:00', '10:35:00');

        -- Chiều (Tiết 1 -> 3)
        INSERT INTO dbo.khung_tiet_hoc (hoc_ky_id, thu_trong_tuan, buoi_hoc, tiet_hoc, gio_bat_dau, gio_ket_thuc)
        VALUES 
        (@HocKyId, @thu, 'CHIEU', 1, '14:00:00', '14:35:00'),
        (@HocKyId, @thu, 'CHIEU', 2, '14:45:00', '15:20:00'),
        (@HocKyId, @thu, 'CHIEU', 3, '15:35:00', '16:10:00');

        SET @thu = @thu + 1;
    END
END

-- 4. Đồng bộ dữ liệu phân công giảng dạy cho Học kỳ 1
UPDATE dbo.phan_cong_giang_day
SET hoc_ky_id = @HocKyId,
    so_tiet_tuan = CASE 
        WHEN mon_hoc_id = 1 THEN 5 -- Toán
        WHEN mon_hoc_id = 2 THEN 7 -- Tiếng Việt
        WHEN mon_hoc_id = 3 THEN 4 -- Tiếng Anh
        WHEN mon_hoc_id = 7 THEN 2 -- Tin học
        ELSE 2
    END,
    tu_ngay = '2026-09-05',
    den_ngay = '2027-01-15',
    trang_thai_xac_nhan = 'DA_XAC_NHAN'
WHERE hoc_ky_id IS NULL OR trang_thai_xac_nhan IS NULL;

-- 5. Tạo một bản TKB mẫu số 1 ở trạng thái NHAP_DAU_VAO nếu chưa có
IF NOT EXISTS (SELECT 1 FROM dbo.ban_tkb WHERE hoc_ky_id = @HocKyId)
BEGIN
    INSERT INTO dbo.ban_tkb (hoc_ky_id, so_phien_ban, tu_ngay, den_ngay, trang_thai)
    VALUES (@HocKyId, 1, '2026-09-05', '2027-01-15', 'NHAP_DAU_VAO');
END

COMMIT TRANSACTION;
GO
