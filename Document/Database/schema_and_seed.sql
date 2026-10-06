-- ==============================================================================
-- DATABASE INITIALIZATION SCRIPT - ORDER MANAGEMENT SERVICE
-- PT ADAYA SOLUSI TEKNOLOGI
-- Database Engine: Microsoft SQL Server 2019 / 2022
-- ==============================================================================

-- 1. Buat Database jika belum ada
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'OrdersDb')
BEGIN
    CREATE DATABASE OrdersDb;
    PRINT 'Database OrdersDb berhasil dibuat.';
END
GO

USE OrdersDb;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ==============================================================================
-- 2. TABEL: Products (Manajemen Inventori & Stok Produk)
-- Memuat Concurrency Token (RowVersion) dan Check Constraint Non-Negative
-- ==============================================================================
IF OBJECT_ID('dbo.Products', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products (
        Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Products_Id DEFAULT NEWID(),
        Name                NVARCHAR(200)    NOT NULL,
        StockQuantity       INT              NOT NULL CONSTRAINT DF_Products_Stock DEFAULT 0,
        Price               DECIMAL(18, 2)   NOT NULL CONSTRAINT DF_Products_Price DEFAULT 0,
        RowVersion          ROWVERSION       NOT NULL, -- Concurrency Token untuk Optimistic Concurrency
        
        -- Audit Trail & Status
        CreatedBy           NVARCHAR(100)    NOT NULL CONSTRAINT DF_Products_CreatedBy DEFAULT 'SYSTEM',
        CreatedDate         DATETIME2        NOT NULL CONSTRAINT DF_Products_CreatedDate DEFAULT SYSUTCDATETIME(),
        ModifiedBy          NVARCHAR(100)    NOT NULL CONSTRAINT DF_Products_ModifiedBy DEFAULT 'SYSTEM',
        ModifiedDate        DATETIME2        NULL,
        IsDeleted           BIT              NOT NULL CONSTRAINT DF_Products_IsDeleted DEFAULT 0,
        RowStatus           INT              NOT NULL CONSTRAINT DF_Products_RowStatus DEFAULT 0,

        CONSTRAINT PK_Products PRIMARY KEY CLUSTERED (Id),
        -- Invarian Kritis: Stok tidak boleh minus dalam kondisi konkurensi apapun!
        CONSTRAINT CK_Product_StockQuantity_NonNegative CHECK ([StockQuantity] >= 0)
    );

    CREATE NONCLUSTERED INDEX IX_Products_IsDeleted ON dbo.Products(IsDeleted);
    PRINT 'Tabel Products berhasil dibuat dengan Check Constraint CK_Product_StockQuantity_NonNegative.';
END
GO

-- ==============================================================================
-- 3. TABEL: Orders (Header Pesanan)
-- Memuat Status State Machine, Idempotency Unique Index, dan RowVersion Token
-- ==============================================================================
IF OBJECT_ID('dbo.Orders', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Orders (
        OrderId             UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Orders_OrderId DEFAULT NEWID(),
        OrderNumber         NVARCHAR(50)     NOT NULL,
        CustomerId          NVARCHAR(100)    NOT NULL,
        CustomerName        NVARCHAR(200)    NULL,
        ShippingAddress     NVARCHAR(500)    NOT NULL,
        OrderDate           DATETIME2        NOT NULL CONSTRAINT DF_Orders_OrderDate DEFAULT SYSUTCDATETIME(),
        TotalAmount         DECIMAL(18, 2)   NOT NULL CONSTRAINT DF_Orders_TotalAmount DEFAULT 0,
        StatusCode          NVARCHAR(50)     NOT NULL CONSTRAINT DF_Orders_StatusCode DEFAULT 'Pending',
        PaymentStatus       NVARCHAR(50)     NULL CONSTRAINT DF_Orders_PaymentStatus DEFAULT 'Unpaid',
        Notes               NVARCHAR(500)    NULL,
        IdempotencyKey      NVARCHAR(100)    NULL,
        RowVersion          ROWVERSION       NOT NULL, -- Concurrency Token untuk State Machine Concurrency (Skenario B)

        -- Audit Trail & Status
        CreatedBy           NVARCHAR(100)    NOT NULL CONSTRAINT DF_Orders_CreatedBy DEFAULT 'SYSTEM',
        CreatedDate         DATETIME2        NOT NULL CONSTRAINT DF_Orders_CreatedDate DEFAULT SYSUTCDATETIME(),
        ModifiedBy          NVARCHAR(100)    NOT NULL CONSTRAINT DF_Orders_ModifiedBy DEFAULT 'SYSTEM',
        ModifiedDate        DATETIME2        NULL,
        IsDeleted           BIT              NOT NULL CONSTRAINT DF_Orders_IsDeleted DEFAULT 0,
        RowStatus           INT              NOT NULL CONSTRAINT DF_Orders_RowStatus DEFAULT 0,

        CONSTRAINT PK_Orders PRIMARY KEY CLUSTERED (OrderId)
    );

    -- Unique Filtered Index: Mencegah duplicate order dengan IdempotencyKey yang sama
    CREATE UNIQUE NONCLUSTERED INDEX UX_Orders_IdempotencyKey 
        ON dbo.Orders(IdempotencyKey) 
        WHERE IdempotencyKey IS NOT NULL;

    CREATE NONCLUSTERED INDEX IX_Orders_OrderNumber ON dbo.Orders(OrderNumber);
    CREATE NONCLUSTERED INDEX IX_Orders_CustomerId ON dbo.Orders(CustomerId);
    CREATE NONCLUSTERED INDEX IX_Orders_StatusCode ON dbo.Orders(StatusCode);
    CREATE NONCLUSTERED INDEX IX_Orders_OrderDate ON dbo.Orders(OrderDate DESC);

    PRINT 'Tabel Orders berhasil dibuat dengan Filtered Unique Index pada IdempotencyKey.';
END
GO

-- ==============================================================================
-- 4. TABEL: OrderDetails (Detail Item Pesanan)
-- ==============================================================================
IF OBJECT_ID('dbo.OrderDetails', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderDetails (
        OrderDetailId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_OrderDetails_Id DEFAULT NEWID(),
        OrderId             UNIQUEIDENTIFIER NOT NULL,
        ProductId           NVARCHAR(100)    NOT NULL,
        ProductName         NVARCHAR(200)    NOT NULL,
        Quantity            INT              NOT NULL,
        UnitPrice           DECIMAL(18, 2)   NOT NULL,
        SubTotal            DECIMAL(18, 2)   NOT NULL,
        Notes               NVARCHAR(500)    NULL,

        -- Audit Trail & Status
        CreatedBy           NVARCHAR(100)    NOT NULL CONSTRAINT DF_OrderDetails_CreatedBy DEFAULT 'SYSTEM',
        CreatedDate         DATETIME2        NOT NULL CONSTRAINT DF_OrderDetails_CreatedDate DEFAULT SYSUTCDATETIME(),
        ModifiedBy          NVARCHAR(100)    NOT NULL CONSTRAINT DF_OrderDetails_ModifiedBy DEFAULT 'SYSTEM',
        ModifiedDate        DATETIME2        NULL,
        IsDeleted           BIT              NOT NULL CONSTRAINT DF_OrderDetails_IsDeleted DEFAULT 0,
        RowStatus           INT              NOT NULL CONSTRAINT DF_OrderDetails_RowStatus DEFAULT 0,

        CONSTRAINT PK_OrderDetails PRIMARY KEY CLUSTERED (OrderDetailId),
        CONSTRAINT FK_OrderDetails_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderId) ON DELETE CASCADE,
        CONSTRAINT CK_OrderDetails_Quantity CHECK ([Quantity] > 0)
    );

    CREATE NONCLUSTERED INDEX IX_OrderDetails_OrderId ON dbo.OrderDetails(OrderId);
    CREATE NONCLUSTERED INDEX IX_OrderDetails_ProductId ON dbo.OrderDetails(ProductId);
    PRINT 'Tabel OrderDetails berhasil dibuat.';
END
GO

-- ==============================================================================
-- 5. TABEL: IdempotencyRecords (Cache Kunci Idempotensi & Hasil Respon)
-- ==============================================================================
IF OBJECT_ID('dbo.IdempotencyRecords', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.IdempotencyRecords (
        [Key]               NVARCHAR(100)    NOT NULL,
        RequestHash         NVARCHAR(128)    NOT NULL,
        StatusCode          INT              NOT NULL,
        ResponseBody        NVARCHAR(MAX)    NOT NULL,
        CreatedAt           DATETIME2        NOT NULL CONSTRAINT DF_Idem_CreatedAt DEFAULT SYSUTCDATETIME(),
        ExpiresAt           DATETIME2        NOT NULL,

        CONSTRAINT PK_IdempotencyRecords PRIMARY KEY CLUSTERED ([Key])
    );

    CREATE NONCLUSTERED INDEX IX_IdempotencyRecords_ExpiresAt ON dbo.IdempotencyRecords(ExpiresAt);
    PRINT 'Tabel IdempotencyRecords berhasil dibuat.';
END
GO

-- ==============================================================================
-- 6. TABEL: OrderConfig (Konfigurasi Tambahan Sistem)
-- ==============================================================================
IF OBJECT_ID('dbo.OrderConfig', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderConfig (
        OrderConfigId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_OrderConfig_Id DEFAULT NEWID(),
        ConfigGroup         NVARCHAR(100)    NOT NULL,
        ConfigName          NVARCHAR(100)    NOT NULL,
        ConfigValue         NVARCHAR(500)    NOT NULL,
        Additional          NVARCHAR(500)    NULL,

        CreatedBy           NVARCHAR(100)    NOT NULL CONSTRAINT DF_OrderConfig_CreatedBy DEFAULT 'SYSTEM',
        CreatedDate         DATETIME2        NOT NULL CONSTRAINT DF_OrderConfig_CreatedDate DEFAULT SYSUTCDATETIME(),
        ModifiedBy          NVARCHAR(100)    NOT NULL CONSTRAINT DF_OrderConfig_ModifiedBy DEFAULT 'SYSTEM',
        ModifiedDate        DATETIME2        NULL,
        IsDeleted           BIT              NOT NULL CONSTRAINT DF_OrderConfig_IsDeleted DEFAULT 0,
        RowStatus           INT              NOT NULL CONSTRAINT DF_OrderConfig_RowStatus DEFAULT 0,

        CONSTRAINT PK_OrderConfig PRIMARY KEY CLUSTERED (OrderConfigId)
    );
    PRINT 'Tabel OrderConfig berhasil dibuat.';
END
GO

-- ==============================================================================
-- 7. SEED DATA PRODUK UNTUK PENGUJIAN KONKURENSI (SKENARIO A, B, C)
-- ==============================================================================
PRINT 'Menyiapkan Data Awal (Seed Data)...';

-- Product X: Stok 15 (Untuk pengujian Skenario A: 2 user serentak beli 10 unit)
IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = '11111111-1111-1111-1111-111111111111')
BEGIN
    INSERT INTO dbo.Products (Id, Name, StockQuantity, Price, CreatedBy, CreatedDate, ModifiedBy, IsDeleted)
    VALUES ('11111111-1111-1111-1111-111111111111', N'Product X (Distribution Item - Skenario A Test)', 15, 50000.00, 'SEEDER', SYSUTCDATETIME(), 'SEEDER', 0);
    PRINT 'Produk X (Stok 15) berhasil di-seed.';
END
ELSE
BEGIN
    UPDATE dbo.Products 
    SET StockQuantity = 15, Price = 50000.00 
    WHERE Id = '11111111-1111-1111-1111-111111111111';
    PRINT 'Produk X stok di-reset ke 15.';
END

-- Product Y: Stok 100 (Standard SKU)
IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = '22222222-2222-2222-2222-222222222222')
BEGIN
    INSERT INTO dbo.Products (Id, Name, StockQuantity, Price, CreatedBy, CreatedDate, ModifiedBy, IsDeleted)
    VALUES ('22222222-2222-2222-2222-222222222222', N'Product Y (Standard Distribution SKU)', 100, 25000.00, 'SEEDER', SYSUTCDATETIME(), 'SEEDER', 0);
    PRINT 'Produk Y (Stok 100) berhasil di-seed.';
END

-- Product Z: Stok 2 (Low Stock Item)
IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = '33333333-3333-3333-3333-333333333333')
BEGIN
    INSERT INTO dbo.Products (Id, Name, StockQuantity, Price, CreatedBy, CreatedDate, ModifiedBy, IsDeleted)
    VALUES ('33333333-3333-3333-3333-333333333333', N'Product Z (Limited Edition SKU)', 2, 150000.00, 'SEEDER', SYSUTCDATETIME(), 'SEEDER', 0);
    PRINT 'Produk Z (Stok 2) berhasil di-seed.';
END
GO

PRINT '=======================================================';
PRINT 'Inisialisasi Database dan Data Awal Selesai 100%!';
PRINT '=======================================================';
GO
