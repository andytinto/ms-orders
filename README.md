# Order Management Service - PT Adaya Solusi Teknologi

> **Production-Ready Order Management REST API built on .NET 10**  
> Mengatasi masalah konkurensi ekstrem, pencegahan double submit (*Idempotency*), integritas stok gudang, inkonsistensi multi-admin status update, dan observabilitas terstruktur.

---

## 1. Ringkasan Eksekutif & Masalah yang Diselesaikan

Layanan ini dirancang dan dibangun untuk menyelesaikan 5 masalah fundamental dalam operasional distribusi:
1. **Pencegahan Double Order (Double Submit)**: Akibat user yang mengklik tombol "Submit" berulang kali karena *network lag* atau ketidaksabaran. Diselesaikan menggunakan **Idempotency Key Header (`Idempotency-Key`)**, SHA-256 Request Payload Fingerprinting, dan DB Unique Index.
2. **Integritas Stok & Stok Minus (Race Condition)**: Akibat submit bersamaan pada milidetik yang sama untuk produk yang sama saat stok menipis. Diselesaikan dengan **Optimistic Concurrency Control (`RowVersion`)**, pengurutan item (*deadlock prevention*), dan **Database Check Constraint (`[StockQuantity] >= 0`)**.
3. **Inkonsistensi Update Status (Multi-Admin Concurrency)**: Akibat dua admin mengupdate order yang sama secara bersamaan (misal Admin 1 mengirim barang, Admin 2 membatalkan order). Diselesaikan dengan **Optimistic Concurrency Token (`RowVersion`)** dan **Finite State Machine yang ketat**.
4. **Kesulitan Tracing Error**: Minimnya log saat insiden terjadi. Diselesaikan menggunakan **Structured Logging (Serilog)** dengan korelasi end-to-end via **`X-Correlation-ID` Middleware**.
5. **Clean Architecture & Scalable Foundation**: Fondasi kokoh tanpa library proprietary eksternal yang usang, siap di-scale secara horizontal di atas kontainer Docker/Kubernetes.

---

## 2. Arsitektur & Teknologi

* **Framework**: .NET 10 (C# 14)
* **Pola Desain**: Clean Architecture (Domain, Application, Infrastructure, Persistance, Api, Test) & CQRS (Command Query Responsibility Segregation via MediatR)
* **Validasi**: FluentValidation terpusat pada pipeline MediatR
* **ORM & Database**: Entity Framework Core 10, Microsoft SQL Server 2022 / In-Memory Test Provider
* **Distributed Caching**: Redis (`StackExchange.Redis` / `IDistributedCache`)
* **Search & APM**: Elasticsearch 8.15.5 & Elastic APM
* **Logging & Observability**: Serilog dengan JSON structured console output & `CorrelationId` context enrichment (`X-Correlation-ID`)
* **Testing**: xUnit, FluentAssertions, MockQueryable, dan Multithreaded Concurrency Testing (`Task.WhenAll` dengan `Barrier`)

---

## 3. Justifikasi Solusi Teknis & Concurrency Handling

### 3.1 Penanganan Stok Konkuren (Skenario A)
* **Masalah**: Dua order masuk serentak masing-masing meminta 10 unit, sementara sisa stok gudang hanya 15 unit.
* **Strategi Pertahanan Berlapis (Defense-in-Depth)**:
  1. **Consistent Lock Ordering**: Item dalam order selalu diurutkan secara ascending berdasarkan `ProductId` sebelum dievaluasi. Hal ini mencegah *database deadlock* saat dua pesanan memesan kombinasi produk yang sama dalam urutan terbalik.
  2. **Application Level Guard**: Memeriksa ketersediaan kuantitas (`StockQuantity >= requested`). Jika tidak cukup, langsung menolak dengan `422 Unprocessable Entity` ("Insufficient stock").
  3. **Optimistic Concurrency Token (`RowVersion`)**: Kolom `RowVersion` (Timestamp) pada tabel `Products` memicu `DbUpdateConcurrencyException` apabila ada transaksi lain yang mendahului memotong stok produk tersebut. Handler menangkap konflik ini dan mengembalikan `409 Conflict`.
  4. **Database Invariant Check Constraint**: Tabel `Products` dilengkapi constraint SQL permanen:
     ```sql
     ALTER TABLE Products ADD CONSTRAINT CK_Product_StockQuantity_NonNegative CHECK ([StockQuantity] >= 0);
     ```
     Menjamin 100% secara matematis bahwa stok tidak akan pernah bernilai negatif dalam kondisi beban ekstrem apapun.

### 3.2 Penanganan Konflik Update Status Multi-Admin (Skenario B)
* **Masalah**: Admin 1 mengubah status menjadi `Shipped`, sementara di milidetik yang sama Admin 2 membatalkan pesanan menjadi `Cancelled`.
* **Strategi Implementasi**:
  1. **Optimistic Concurrency Token**: Client wajib mengirimkan token `RowVersion` terkini (Base64 string) saat memanggil endpoint `PUT /api/orders/{id}/status` atau `POST /api/orders/{id}/cancel`.
  2. **First-Committer Wins**: Admin 1 yang melakukan commit pertama akan mengubah status dan memperbarui `RowVersion` di database.
  3. Saat Admin 2 mengirimkan request dengan token `RowVersion` lama yang sudah basi (*stale*), sistem mendeteksi perbedaan tersebut dan melempar `409 Conflict` dengan pesan deskriptif.
  4. Barang fisik yang sudah dalam proses pengiriman tidak akan terbatalkan secara keliru, dan stok tidak mengalami pengembalian dobel (*double refund*).

### 3.3 Penanganan Idempotency & Race Condition (Skenario C)
* **Masalah**: Client mengirim dua request pembuatan order dengan `Idempotency-Key` yang sama persis secara simultan sebelum request pertama selesai commit.
* **Strategi Implementasi**:
  1. **Header `Idempotency-Key`**: Diwajibkan pada setiap request `POST /api/orders`.
  2. **Payload Hash Check**: Middleware menghitung SHA-256 dari request body. Jika key yang sama digunakan dengan payload yang berbeda, request ditolak dengan `422 Unprocessable Entity`.
  3. **Thread-Safe Key Gatekeeper (`SemaphoreSlim`)**: Request dengan `Idempotency-Key` yang sama dikunci sementara per-kunci. Request kedua akan menunggu hingga request pertama commit, lalu membaca dan mengembalikan hasil transaksi yang telah dibuat tanpa menduplikasi pesanan.
  4. **Unique Index di Database**: Kolom `IdempotencyKey` pada tabel `Orders` memiliki unique constraint, melindungi dari replikasi jika dijalankan di lingkungan multi-instance/multi-pod.
  5. **Response Caching**: Tabel `IdempotencyRecords` menyimpan status code dan response body selama 24 jam sehingga replay request di kemudian hari langsung dilayani secara instan (*idempotent*).

### 3.4 Skenario Race Condition Tambahan yang Ditangani
1. **Pembatalan Bersamaan dengan Pengiriman (Cancel Order vs Ship Order)**:
   - *Resiko*: Pelanggan membatalkan order saat barang sedang dipindai gudang untuk dikirim.
   - *Solusi*: State machine melarang transisi dari `Shipped` ke `Cancelled`. Validasi token `RowVersion` memastikan jika order telah beralih ke `Shipped`, pembatalan ditolak dan pengembalian stok digagalkan.
2. **Penyesuaian Stok Manual oleh Gudang vs Checkout Pelanggan**:
   - *Resiko*: Admin gudang mengoreksi stok fisik bersamaan dengan checkout pelanggan.
   - *Solusi*: Mutasi stok menggunakan token konkurensi dan constraint non-negatif yang atomik. Jika admin gudang mengurangi stok, checkout pelanggan yang sedang berjalan akan mendeteksi `DbUpdateConcurrencyException` atau `Insufficient stock` secara elegan.

---

## 4. Finite State Machine (Alur Transisi Status Order)

Status pesanan dikontrol secara ketat melalui diagram status berikut:

```
       +------------+
       |  Pending   |
       +-----+------+
             |
      +------+------+
      |             |
      v             v
+-----------+ +-----------+
| Confirmed | | Cancelled | (Terminal State - Restock Items)
+-----+-----+ +-----------+
      |             ^
      +------+------+
      |      |
      v      |
+-----------+
|  Shipped  |
+-----+-----+
      |
      v
+-----------+
| Delivered | (Terminal State)
+-----------+
```

* **`Pending`** $\rightarrow$ `Confirmed`, `Cancelled`
* **`Confirmed`** $\rightarrow$ `Shipped`, `Cancelled`
* **`Shipped`** $\rightarrow$ `Delivered` (Tidak dapat dibatalkan!)
* **`Delivered` & `Cancelled`** $\rightarrow$ **Terminal State** (Permanen, tidak dapat diubah lagi).
* Pada saat order beralih menjadi `Cancelled`, seluruh kuantitas item produk yang dipesan secara otomatis **dikembalikan ke stok gudang (*restocked*)**.

---

## 5. Dokumentasi API Endpoints

Semua endpoint mengembalikan format response standar yang konsisten:

```json
{
  "success": true,
  "responseCode": "00",
  "responseMessage": "Success",
  "data": { ... },
  "errors": null
}
```

| Method | Endpoint | Deskripsi | Header Wajib / Parameter | Status Code |
| :--- | :--- | :--- | :--- | :--- |
| `POST` | `/api/orders` | Membuat order baru | `Idempotency-Key: <UUID>` | `201 Created` / `400` / `409` / `422` |
| `GET` | `/api/orders/{id}` | Detail order | Route parameter `{id:guid}` | `200 OK` / `404 Not Found` |
| `POST` | `/api/orders/list` | Filter & paginasi order | Body: `Page`, `PageSize`, `Status`, dll. | `200 OK` |
| `PUT` | `/api/orders/{id}/status` | Update status order | Body: `NewStatus`, `RowVersion` | `200 OK` / `409 Conflict` / `422` |
| `POST` | `/api/orders/{id}/cancel` | Batalkan order & restock | Body: `Reason`, `RowVersion` | `200 OK` / `409 Conflict` / `422` |
| `GET` | `/api/products` | Cek list & stok produk | - | `200 OK` |
| `POST` | `/api/products` | Tambah/seed data produk | Body: `Name`, `StockQuantity`, `Price` | `200 OK` |

---

## 6. Pengujian Otomatis (Automated & Concurrency Tests)

Proyek dilengkapi dengan 108 automated test cases, termasuk pengujian konkurensi multithreading:

1. **`SkenarioA_ConcurrentStockDeduction_TwoConcurrentOrders_OnlyOneSucceeds_StockNeverNegative`**:
   - Mensimulasikan 2 thread konkuren yang masuk bersamaan menggunakan `Barrier` dan `Task.WhenAll`.
   - Menguji produk dengan stok 15 yang diperebutkan oleh dua order masing-masing 10 unit.
   - **Hasil**: Tepat 1 order berhasil, 1 order ditolak, sisa stok akhir tepat 5 unit (tidak pernah minus).
2. **`SkenarioB_ConcurrentStatusUpdate_ConflictingUpdates_SecondAdminRejectedWithConflictException`**:
   - Mensimulasikan Admin 1 (`Shipped`) dan Admin 2 (`Cancelled`) yang mengupdate order bersamaan.
   - **Hasil**: Admin 1 berhasil, Admin 2 ditolak dengan HTTP 409 Conflict, integritas status dan stok terjamin.
3. **`SkenarioC_IdempotentCreateUnderRace_TwoParallelRequests_CreatesOnlyOneOrder`**:
   - Mensimulasikan dua request order paralel dengan `Idempotency-Key` yang sama persis.
   - **Hasil**: Tepat 1 order tersimpan di database dan stok hanya terpotong satu kali.
4. **`ExtraRaceCondition_CancelOrder_WhenAlreadyShipped_ThrowsUnprocessableEntityException`**:
   - Memastikan order yang telah berstatus `Shipped` tidak dapat dibatalkan.
5. **State Machine & Validation Tests**:
   - Memvalidasi seluruh kombinasi transisi status valid/invalid dan aturan FluentValidation.

### Menjalankan Pengujian:
```bash
dotnet test Orders.Test/Orders.Test.csproj
```

---

## 7. Script & Inisialisasi Database

Script SQL lengkap (DDL tabel, index, constraints, dan data awal produk) tersedia di folder `Document/Database/`:
* 📜 **Script Skema & Seed Lengkap**: [`Document/Database/schema_and_seed.sql`](file:///c:/_DATA/Document/Pribadi/Pribadi/Adaya/Project/Orders/Document/Database/schema_and_seed.sql)
* 📜 **Query Audit & Operasional**: [`Document/Database/03_Useful_Queries.sql`](file:///c:/_DATA/Document/Pribadi/Pribadi/Adaya/Project/Orders/Document/Database/03_Useful_Queries.sql)
* 📖 **Panduan Penggunaan Database**: [`Document/DATABASE.md`](file:///c:/_DATA/Document/Pribadi/Pribadi/Adaya/Project/Orders/Document/DATABASE.md)

### Eksekusi ke dalam Kontainer SQL Server Docker:
```bash
# Melalui PowerShell (Windows):
Get-Content "Document/Database/schema_and_seed.sql" -Raw | docker exec -i orders-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "Password123!" -C

# Melalui Bash (Linux / macOS / WSL):
docker exec -i orders-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "Password123!" -C -i /dev/stdin < Document/Database/schema_and_seed.sql
```

*(Atau via tool GUI seperti SSMS / DBeaver dengan connect ke `localhost,1433`, User: `sa`, Password: `Password123!`)*

---

## 8. Cara Menjalankan Aplikasi

### Opsi A: Menjalankan Secara Lokal (CLI)
1. **Prasyarat**: .NET 10 SDK terpasang di komputer.
2. **Restore & Build**:
   ```bash
   dotnet restore Orders.slnx
   dotnet build Orders.slnx
   ```
3. **Jalankan Service**:
   ```bash
   dotnet run --project Orders.Api/Orders.Api.csproj
   ```
4. **Buka Swagger UI**:
   Kunjungi browser: `http://localhost:5000/swagger`

---

### Opsi B: Menjalankan Menggunakan Docker Compose (Full Stack)
1. **Prasyarat**: Docker Desktop terpasang dan aktif.
2. **Jalankan Seluruh Layanan**:
   ```bash
   docker compose up --build -d
   ```
3. **Inisialisasi Database**:
   ```bash
   Get-Content "Document/Database/schema_and_seed.sql" -Raw | docker exec -i orders-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "Password123!" -C
   ```
4. Seluruh kontainer infrastruktur dan API aktif:
   * **Orders API**: [`http://localhost:5000/swagger`](http://localhost:5000/swagger)
   * **SQL Server 2022**: `localhost:1433` (`orders-sqlserver`)
   * **Redis Cache**: `localhost:6379` (`orders-redis`)
   * **Elasticsearch**: [`http://localhost:9200`](http://localhost:9200) (`orders-elasticsearch`)
5. 📖 **Panduan Detail Setup & Arsitektur Infrastruktur**: [`Document/SETUP_AND_INFRASTRUCTURE.md`](Document/SETUP_AND_INFRASTRUCTURE.md)

---

## 9. Verifikasi Kebutuhan Proyek

| Kebutuhan Teknis | Status | Catatan Implementasi |
| :--- | :---: | :--- |
| **Idempotency (Double Submit)** | ✅ Selesai | Header `Idempotency-Key`, caching response body, dan DB unique index |
| **Skenario A (Stock Race)** | ✅ Selesai | Optimistic token + SQL check constraint `[StockQuantity] >= 0` |
| **Skenario B (Status Conflict)** | ✅ Selesai | Concurrency token `RowVersion` + First-committer wins |
| **Skenario C (Idempotent Race)** | ✅ Selesai | SemaphoreSlim gatekeeper + DB unique index |
| **State Machine & Restock** | ✅ Selesai | Validasi transisi status ketat & otomatis restock pada pembatalan |
| **Observability (Logging & Trace)** | ✅ Selesai | Serilog JSON Logging + `CorrelationIdMiddleware` (`X-Correlation-ID`) |
| **Multithreading Concurrency Tests** | ✅ Selesai | 108 tests passing via `Task.WhenAll` |
| **Pembersihan Library CPD** | ✅ Selesai | Seluruh referensi proprietary `CPD_MasterLibrary` telah dilepas |
