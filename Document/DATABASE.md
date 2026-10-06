# Panduan Penggunaan & Script Database

**Layanan**: Order Management Service (.NET 10)  
**Database Engine**: Microsoft SQL Server 2019 / 2022  
**Nama Database**: `OrdersDb`  
**Lokasi File Script**: [`Document/Database/schema_and_seed.sql`](file:///c:/_DATA/Document/Pribadi/Pribadi/Adaya/Project/Orders/Document/Database/schema_and_seed.sql)

---

## 1. Ringkasan File Script Database

| File | Deskripsi |
| :--- | :--- |
| [`Document/Database/schema_and_seed.sql`](file:///c:/_DATA/Document/Pribadi/Pribadi/Adaya/Project/Orders/Document/Database/schema_and_seed.sql) | **Script Lengkap (All-in-One)**: DDL pembuatan database `OrdersDb`, pembuatan tabel (`Products`, `Orders`, `OrderDetails`, `IdempotencyRecords`, `OrderConfig`), pembuatan index & constraints, serta initial seed data produk. |
| [`Document/Database/03_Useful_Queries.sql`](file:///c:/_DATA/Document/Pribadi/Pribadi/Adaya/Project/Orders/Document/Database/03_Useful_Queries.sql) | **Query Audit & Operasional**: Query untuk memantau sisa stok produk, audit invarian stok non-negatif, detail pesanan, dan cache idempotensi. |

---

## 2. Cara Menjalankan Script Database

### Opsi A: Menggunakan Docker SQL Server (Paling Cepat)
Jika Anda menjalankan database menggunakan Docker Compose yang telah disediakan:

1. Pastikan container SQL Server telah berjalan:
   ```bash
   docker compose up -d sqlserver
   ```
2. Eksekusi script SQL langsung ke dalam container:
   ```bash
   docker exec -i orders-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "Your_strong_password123!" -C -i /dev/stdin < Document/Database/schema_and_seed.sql
   ```
   *(Atau sesuaikan nama container jika berbeda, misalnya `docker compose exec ...`)*

---

### Opsi B: Menggunakan SSMS / Azure Data Studio / DBeaver
1. Buka **SQL Server Management Studio (SSMS)** atau **Azure Data Studio**.
2. Hubungkan ke instance SQL Server Anda:
   - **Server**: `localhost,1433` (atau sesuai host database Anda)
   - **Authentication**: SQL Server Authentication
   - **Login**: `sa`
   - **Password**: `Your_strong_password123!`
3. Buka file [`Document/Database/schema_and_seed.sql`](file:///c:/_DATA/Document/Pribadi/Pribadi/Adaya/Project/Orders/Document/Database/schema_and_seed.sql).
4. Klik tombol **Execute (F5)**.

---

### Opsi C: Menggunakan Command Line (sqlcmd lokal)
```bash
sqlcmd -S localhost,1433 -U sa -P "Your_strong_password123!" -C -i "Document/Database/schema_and_seed.sql"
```

---

## 3. Struktur Skema & Fitur Keamanan Konkurensi

### A. Tabel `Products` (Inventory)
* `Id` (`UNIQUEIDENTIFIER`, Primary Key)
* `Name` (`NVARCHAR(200)`)
* `StockQuantity` (`INT`)
* `Price` (`DECIMAL(18,2)`)
* `RowVersion` (`ROWVERSION` / `TIMESTAMP`) $\rightarrow$ **Concurrency Token** untuk Optimistic Concurrency Control.
* **Kunci Keamanan**:
  ```sql
  CONSTRAINT CK_Product_StockQuantity_NonNegative CHECK ([StockQuantity] >= 0)
  ```
  Menjamin integritas tingkat database: **stok fisik tidak akan pernah minus ($< 0$)** meskipun terjadi ribuan request berbarengan dalam milidetik yang sama.

### B. Tabel `Orders` (Order Header)
* `OrderId` (`UNIQUEIDENTIFIER`, Primary Key)
* `OrderNumber` (`NVARCHAR(50)`)
* `CustomerId` (`NVARCHAR(100)`)
* `ShippingAddress` (`NVARCHAR(500)`)
* `TotalAmount` (`DECIMAL(18,2)`)
* `StatusCode` (`NVARCHAR(50)`) $\rightarrow$ `Pending`, `Confirmed`, `Shipped`, `Delivered`, `Cancelled`.
* `IdempotencyKey` (`NVARCHAR(100)`)
* `RowVersion` (`ROWVERSION` / `TIMESTAMP`) $\rightarrow$ **Concurrency Token** untuk mencegah tabrakan update antar admin (Skenario B).
* **Kunci Keamanan**:
  ```sql
  CREATE UNIQUE NONCLUSTERED INDEX UX_Orders_IdempotencyKey 
  ON dbo.Orders(IdempotencyKey) 
  WHERE IdempotencyKey IS NOT NULL;
  ```
  Menjamin **idempotensi mutlak**: pesanan dengan kunci idempotensi yang sama tidak akan terduplikasi di database.

### C. Tabel `OrderDetails` (Order Items)
* `OrderDetailId` (`UNIQUEIDENTIFIER`, Primary Key)
* `OrderId` (`UNIQUEIDENTIFIER`, Foreign Key ke `Orders`)
* `ProductId` (`NVARCHAR(100)`)
* `ProductName` (`NVARCHAR(200)`)
* `Quantity` (`INT`, dengan constraint `Quantity > 0`)
* `UnitPrice` (`DECIMAL(18,2)`)
* `SubTotal` (`DECIMAL(18,2)`)

### D. Tabel `IdempotencyRecords` (Response Cache)
* `[Key]` (`NVARCHAR(100)`, Primary Key)
* `RequestHash` (`NVARCHAR(128)`, SHA-256 hash dari payload request)
* `StatusCode` (`INT`)
* `ResponseBody` (`NVARCHAR(MAX)`)
* `CreatedAt` (`DATETIME2`)
* `ExpiresAt` (`DATETIME2`)

---

## 4. Initial Seed Data untuk Pengujian

Script telah memuat data awal produk untuk mempermudah validasi pengujian:

| Product Id | Nama Produk | Stok Awal | Harga Satuan | Tujuan Pengujian |
| :--- | :--- | :---: | :---: | :--- |
| `11111111-1111-1111-1111-111111111111` | **Product X** | **15** | Rp 50.000 | **Skenario A**: 2 user serentak beli 10 unit. Hanya 1 berhasil, stok sisa 5 (tidak minus). |
| `22222222-2222-2222-2222-222222222222` | **Product Y** | **100** | Rp 25.000 | Pengujian order kuantitas standar. |
| `33333333-3333-3333-3333-333333333333` | **Product Z** | **2** | Rp 150.000 | Pengujian batas stok minim (*low stock threshold*). |

---

## 5. Query Audit Cepat

Untuk memverifikasi kondisi data sewaktu-waktu:
```sql
USE OrdersDb;

-- Cek stok & RowVersion
SELECT Id, Name, StockQuantity, CONVERT(VARCHAR(50), RowVersion, 1) AS RowVersion_Hex 
FROM dbo.Products;

-- Pastikan tidak ada stok minus (hasil WAJIB 0 baris)
SELECT * FROM dbo.Products WHERE StockQuantity < 0;

-- Cek log idempotensi
SELECT [Key], RequestHash, StatusCode, CreatedAt FROM dbo.IdempotencyRecords;
```
