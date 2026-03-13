-- ============================================================================
-- SCRIPT KIỂM TRA ĐỒNG BỘ DATABASE - WEBHS
-- ============================================================================
-- Mục đích: Kiểm tra cấu trúc database WebHSDb
-- Sử dụng: Mở trong SQL Server Management Studio và chạy
-- ============================================================================

USE WebHSDb;
GO

PRINT '============================================================================';
PRINT 'BẮT ĐẦU KIỂM TRA ĐỒNG BỘ DATABASE';
PRINT '============================================================================';
PRINT '';

-- ============================================================================
-- 1. KIỂM TRA TABLES
-- ============================================================================
PRINT '1. KIỂM TRA TABLES';
PRINT '-------------------';

SELECT 
    TABLE_NAME AS 'Table Name',
    TABLE_TYPE AS 'Type'
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_NAME;

PRINT '';
PRINT 'Số lượng tables:';
SELECT COUNT(*) AS 'Total Tables'
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE';

PRINT '';
PRINT '============================================================================';
PRINT '';

-- ============================================================================
-- 2. KIỂM TRA COLUMNS CỦA BOOKING TABLE
-- ============================================================================
PRINT '2. KIỂM TRA BOOKING TABLE';
PRINT '-------------------';

SELECT 
    COLUMN_NAME AS 'Column Name',
    DATA_TYPE AS 'Data Type',
    CHARACTER_MAXIMUM_LENGTH AS 'Max Length',
    IS_NULLABLE AS 'Nullable',
    COLUMN_DEFAULT AS 'Default'
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Bookings'
ORDER BY ORDINAL_POSITION;

PRINT '';
PRINT 'Kiểm tra HostReply và HostReplyDate:';
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'HostReply'
)
    PRINT '✅ HostReply column EXISTS'
ELSE
    PRINT '❌ HostReply column MISSING - CẦN THÊM!';

IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'HostReplyDate'
)
    PRINT '✅ HostReplyDate column EXISTS'
ELSE
    PRINT '❌ HostReplyDate column MISSING - CẦN THÊM!';

PRINT '';
PRINT '============================================================================';
PRINT '';

-- ============================================================================
-- 3. KIỂM TRA MESSAGE TABLE
-- ============================================================================
PRINT '3. KIỂM TRA MESSAGE TABLE';
PRINT '-------------------';

SELECT 
    COLUMN_NAME AS 'Column Name',
    DATA_TYPE AS 'Data Type',
    IS_NULLABLE AS 'Nullable'
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Messages'
ORDER BY ORDINAL_POSITION;

PRINT '';
PRINT 'Kiểm tra ConversationId:';
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Messages' AND COLUMN_NAME = 'ConversationId'
)
    PRINT '✅ ConversationId column EXISTS'
ELSE
    PRINT '❌ ConversationId column MISSING';

PRINT '';
PRINT '============================================================================';
PRINT '';

-- ============================================================================
-- 4. KIỂM TRA INDEXES
-- ============================================================================
PRINT '4. KIỂM TRA INDEXES';
PRINT '-------------------';

-- Homestay Indexes
PRINT 'Homestay Indexes:';
SELECT 
    i.name AS 'Index Name',
    t.name AS 'Table Name',
    STUFF((
        SELECT ', ' + c.name
        FROM sys.index_columns ic
        INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
        ORDER BY ic.key_ordinal
        FOR XML PATH('')
    ), 1, 2, '') AS 'Columns',
    i.is_unique AS 'Is Unique'
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE t.name = 'Homestays' AND i.type > 0
ORDER BY i.name;

PRINT '';

-- Booking Indexes
PRINT 'Booking Indexes:';
SELECT 
    i.name AS 'Index Name',
    t.name AS 'Table Name',
    STUFF((
        SELECT ', ' + c.name
        FROM sys.index_columns ic
        INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
        ORDER BY ic.key_ordinal
        FOR XML PATH('')
    ), 1, 2, '') AS 'Columns',
    i.is_unique AS 'Is Unique'
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE t.name = 'Bookings' AND i.type > 0
ORDER BY i.name;

PRINT '';

-- Payment Indexes
PRINT 'Payment Indexes:';
SELECT 
    i.name AS 'Index Name',
    t.name AS 'Table Name',
    STUFF((
        SELECT ', ' + c.name
        FROM sys.index_columns ic
        INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
        ORDER BY ic.key_ordinal
        FOR XML PATH('')
    ), 1, 2, '') AS 'Columns',
    i.is_unique AS 'Is Unique'
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE t.name = 'Payments' AND i.type > 0
ORDER BY i.name;

PRINT '';

-- Message Indexes
PRINT 'Message Indexes:';
SELECT 
    i.name AS 'Index Name',
    t.name AS 'Table Name',
    STUFF((
        SELECT ', ' + c.name
        FROM sys.index_columns ic
        INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
        ORDER BY ic.key_ordinal
        FOR XML PATH('')
    ), 1, 2, '') AS 'Columns',
    i.is_unique AS 'Is Unique'
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE t.name = 'Messages' AND i.type > 0
ORDER BY i.name;

PRINT '';

-- Conversation Indexes
PRINT 'Conversation Indexes:';
SELECT 
    i.name AS 'Index Name',
    t.name AS 'Table Name',
    STUFF((
        SELECT ', ' + c.name
        FROM sys.index_columns ic
        INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
        ORDER BY ic.key_ordinal
        FOR XML PATH('')
    ), 1, 2, '') AS 'Columns',
    i.is_unique AS 'Is Unique'
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE t.name = 'Conversations' AND i.type > 0
ORDER BY i.name;

PRINT '';
PRINT '============================================================================';
PRINT '';

-- ============================================================================
-- 5. KIỂM TRA CHECK CONSTRAINTS
-- ============================================================================
PRINT '5. KIỂM TRA CHECK CONSTRAINTS';
PRINT '-------------------';

SELECT 
    t.name AS 'Table Name',
    con.name AS 'Constraint Name',
    con.definition AS 'Definition'
FROM sys.check_constraints con
INNER JOIN sys.tables t ON con.parent_object_id = t.object_id
WHERE t.name IN ('Homestays', 'Bookings', 'Payments', 'HomestayPricings')
ORDER BY t.name, con.name;

PRINT '';
PRINT '============================================================================';
PRINT '';

-- ============================================================================
-- 6. KIỂM TRA FOREIGN KEYS
-- ============================================================================
PRINT '6. KIỂM TRA FOREIGN KEYS';
PRINT '-------------------';

SELECT 
    fk.name AS 'Foreign Key Name',
    tp.name AS 'Parent Table',
    cp.name AS 'Parent Column',
    tr.name AS 'Referenced Table',
    cr.name AS 'Referenced Column',
    fk.delete_referential_action_desc AS 'Delete Action'
FROM sys.foreign_keys fk
INNER JOIN sys.tables tp ON fk.parent_object_id = tp.object_id
INNER JOIN sys.tables tr ON fk.referenced_object_id = tr.object_id
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
INNER JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
WHERE tp.name IN ('Bookings', 'Payments', 'Messages', 'Homestays')
ORDER BY tp.name, fk.name;

PRINT '';
PRINT '============================================================================';
PRINT '';

-- ============================================================================
-- 7. KIỂM TRA DATA STATISTICS
-- ============================================================================
PRINT '7. KIỂM TRA DỮ LIỆU';
PRINT '-------------------';

PRINT 'Số lượng records trong các bảng chính:';

SELECT 'Users' AS 'Table', COUNT(*) AS 'Count' FROM AspNetUsers
UNION ALL
SELECT 'Homestays', COUNT(*) FROM Homestays
UNION ALL
SELECT 'Bookings', COUNT(*) FROM Bookings
UNION ALL
SELECT 'Payments', COUNT(*) FROM Payments
UNION ALL
SELECT 'Messages', COUNT(*) FROM Messages
UNION ALL
SELECT 'Conversations', COUNT(*) FROM Conversations
UNION ALL
SELECT 'HomestayImages', COUNT(*) FROM HomestayImages
UNION ALL
SELECT 'Amenities', COUNT(*) FROM Amenities;

PRINT '';
PRINT '============================================================================';
PRINT '';

-- ============================================================================
-- 8. KIỂM TRA IDENTITY TABLES
-- ============================================================================
PRINT '8. KIỂM TRA ASP.NET IDENTITY TABLES';
PRINT '-------------------';

SELECT 
    TABLE_NAME AS 'Table Name'
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME LIKE 'AspNet%'
ORDER BY TABLE_NAME;

PRINT '';
PRINT '============================================================================';
PRINT '';

-- ============================================================================
-- 9. KIỂM TRA DECIMAL PRECISION
-- ============================================================================
PRINT '9. KIỂM TRA DECIMAL PRECISION';
PRINT '-------------------';

SELECT 
    TABLE_NAME AS 'Table',
    COLUMN_NAME AS 'Column',
    DATA_TYPE AS 'Type',
    NUMERIC_PRECISION AS 'Precision',
    NUMERIC_SCALE AS 'Scale'
FROM INFORMATION_SCHEMA.COLUMNS
WHERE DATA_TYPE IN ('decimal', 'numeric')
    AND TABLE_NAME IN ('Homestays', 'Bookings', 'Payments', 'HomestayPricings')
ORDER BY TABLE_NAME, COLUMN_NAME;

PRINT '';
PRINT '============================================================================';
PRINT '';

-- ============================================================================
-- 10. SUMMARY & RECOMMENDATIONS
-- ============================================================================
PRINT '10. SUMMARY & RECOMMENDATIONS';
PRINT '-------------------';

DECLARE @missingHostReply BIT = 0;
DECLARE @missingHostReplyDate BIT = 0;

IF NOT EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'HostReply'
)
    SET @missingHostReply = 1;

IF NOT EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'HostReplyDate'
)
    SET @missingHostReplyDate = 1;

IF @missingHostReply = 1 OR @missingHostReplyDate = 1
BEGIN
    PRINT '❌ CẦN THỰC HIỆN:';
    IF @missingHostReply = 1
        PRINT '  - Thêm column HostReply vào Bookings table';
    IF @missingHostReplyDate = 1
        PRINT '  - Thêm column HostReplyDate vào Bookings table';
    PRINT '';
    PRINT 'Chạy migration:';
    PRINT '  cd d:\ST4_3\WebHS';
    PRINT '  dotnet ef migrations add AddHostReplyToBooking';
    PRINT '  dotnet ef database update';
END
ELSE
BEGIN
    PRINT '✅ DATABASE ĐÃ ĐỒNG BỘ HOÀN TẤT!';
END

PRINT '';
PRINT '============================================================================';
PRINT 'KẾT THÚC KIỂM TRA';
PRINT '============================================================================';
GO
