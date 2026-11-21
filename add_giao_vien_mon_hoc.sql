USE QLHS;
GO

IF OBJECT_ID(N'dbo.giao_vien_mon_hoc', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.giao_vien_mon_hoc (
        giao_vien_id INT NOT NULL,
        mon_hoc_id INT NOT NULL,
        CONSTRAINT PK_giao_vien_mon_hoc PRIMARY KEY (giao_vien_id, mon_hoc_id),
        CONSTRAINT FK_gvmh_giao_vien FOREIGN KEY (giao_vien_id) REFERENCES dbo.giao_vien(id) ON DELETE CASCADE,
        CONSTRAINT FK_gvmh_mon_hoc FOREIGN KEY (mon_hoc_id) REFERENCES dbo.mon_hoc(id) ON DELETE CASCADE
    );

    -- Tự động khởi tạo chuyên môn ban đầu từ lịch sử phân công giảng dạy hiện có
    INSERT INTO dbo.giao_vien_mon_hoc (giao_vien_id, mon_hoc_id)
    SELECT DISTINCT giao_vien_id, mon_hoc_id
    FROM dbo.phan_cong_giang_day
    WHERE giao_vien_id IS NOT NULL AND mon_hoc_id IS NOT NULL;
END
GO
