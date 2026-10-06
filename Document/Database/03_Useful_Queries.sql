-- ==============================================================================
-- USEFUL OPERATIONAL & AUDIT QUERIES
-- PT ADAYA SOLUSI TEKNOLOGI - ORDER MANAGEMENT SERVICE
-- ==============================================================================

USE OrdersDb;
GO

-- 1. CEK STATUS & SISA STOK PRODUK (BESERTA CONCURRENCY TOKEN ROWVERSION)
SELECT 
    Id,
    Name,
    StockQuantity,
    Price,
    CONVERT(VARCHAR(50), RowVersion, 1) AS RowVersion_Hex,
    CreatedDate,
    ModifiedDate
FROM dbo.Products
WHERE IsDeleted = 0
ORDER BY Name;

-- 2. AUDIT INVARIAN STOK NON-NEGATIF (HASIL WAJIB 0 ROWS)
-- Membuktikan bahwa Check Constraint berhasil mencegah stok minus dalam kondisi lonjakan konkurensi apapun
SELECT 
    Id, 
    Name, 
    StockQuantity 
FROM dbo.Products 
WHERE StockQuantity < 0;

-- 3. CEK DAFTAR PESANAN TERBARU (ORDER HEADER)
SELECT TOP 20
    OrderId,
    OrderNumber,
    CustomerId,
    CustomerName,
    ShippingAddress,
    TotalAmount,
    StatusCode,
    PaymentStatus,
    IdempotencyKey,
    CONVERT(VARCHAR(50), RowVersion, 1) AS RowVersion_Hex,
    OrderDate
FROM dbo.Orders
ORDER BY OrderDate DESC;

-- 4. CEK DETAIL ITEM UNTUK ORDER TERTENTU
SELECT 
    od.OrderDetailId,
    od.OrderId,
    o.OrderNumber,
    o.StatusCode AS OrderStatus,
    od.ProductId,
    od.ProductName,
    od.Quantity,
    od.UnitPrice,
    od.SubTotal
FROM dbo.OrderDetails od
INNER JOIN dbo.Orders o ON od.OrderId = o.OrderId
ORDER BY od.CreatedDate DESC;

-- 5. AUDIT CACHE IDEMPOTENCY RECORD
SELECT 
    [Key] AS IdempotencyKey,
    RequestHash,
    StatusCode,
    CreatedAt,
    ExpiresAt,
    LEFT(ResponseBody, 100) AS ResponsePreview
FROM dbo.IdempotencyRecords
ORDER BY CreatedAt DESC;

-- 6. RESET STOK KE AWAL (UNTUK PENGUJIAN ULANG SKENARIO A & B)
-- UPDATE dbo.Products SET StockQuantity = 15 WHERE Id = '11111111-1111-1111-1111-111111111111';
