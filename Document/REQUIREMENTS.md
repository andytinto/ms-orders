# Dokumen Spesifikasi Kebutuhan Sistem (Software Requirements Specification)
## Proyek: Prototype Order Management Service
**Perusahaan**: PT Adaya Solusi Teknologi  
**Alamat**: Perumahan Pesona Bali E7 No. 10, Kabupaten Bandung, Jawa Barat 40261  
**Email**: hrd@astek.id  
**Posisi**: Senior .NET Developer  
**Status Dokumen**: Final / Approved  
**Target Platform**: .NET 10 / ASP.NET Core Web API  

---

## 1. Latar Belakang & Masalah Bisnis

Klien (perusahaan distribusi) mengalami kendala kritis pada sistem *Order Management* yang sedang berjalan, antara lain:
1. **Double Order**: Terjadi duplikasi transaksi pemesanan akibat pengguna yang tidak sabar (*impatient user*) mengklik tombol **"Submit"** berkali-kali secara cepat.
2. **Stock Minus (Negative Inventory)**: Stok barang di gudang sering menjadi minus ketika dua pesanan masuk pada milidetik yang sama untuk produk yang sama karena sistem tidak memiliki mekanisme *concurrency control* yang tepat.
3. **Inkonsistensi Status Order**: Status pemesanan tidak konsisten saat terdapat beberapa administrator (*multiple admins*) yang mengedit atau memperbarui status order yang sama secara simultan.
4. **Minimnya Observabilitas & Logging**: Tim operasional kesulitan melakukan penelusuran (*tracing*) ketika terjadi insiden error di lingkungan *production* akibat ketiadaan logging terstruktur dan *Correlation ID*.

### 1.1 Tujuan Proyek
Membangun prototipe *service* baru berbasis **ASP.NET Core (.NET 10)** dengan arsitektur bersih (*Clean Architecture* & CQRS), tangguh terhadap *race condition*, menjamin *idempotency*, serta memiliki *concurrency handling* yang handal sebagai fondasi *rewrite* menyeluruh sistem order management klien.

---

## 2. Kebutuhan Fungsional (Functional Requirements)

### 2.1 Manajemen Order (Order Management API)

| Endpoint | Method | Deskripsi | Parameter / Request Body | Response Sukses |
| :--- | :--- | :--- | :--- | :--- |
| `/api/orders` | `POST` | Membuat order baru | `CustomerId`, `ShippingAddress`, `Items: [{ ProductId, Quantity }]`, Header: `Idempotency-Key` | `201 Created` / `200 OK` + Order Detail |
| `/api/orders/{id}` | `GET` | Mengambil detail order berdasarkan ID | Route `id` (GUID) | `200 OK` + Order Detail lengkap |
| `/api/orders` | `POST /list` | Mengambil daftar order dengan filter & pagination | Body: `Page`, `PageSize`, `Status`, `CustomerId`, `StartDate`, `EndDate`, `SortBy` | `200 OK` + Paged List of Orders |
| `/api/orders/{id}/status` | `PUT` | Memperbarui status order (State Machine) | Route `id`, Body: `NewStatus`, `RowVersion` (Concurrency Token) | `200 OK` + Status Updated |
| `/api/orders/{id}/cancel` | `POST` | Membatalkan order & mengembalikan stok | Route `id`, Body: `Reason`, `RowVersion` | `200 OK` + Order Cancelled |

---

### 2.2 Aturan Transisi Status Order (State Machine)

Status order wajib mengikuti diagram transisi status yang valid dan ketat (*finite state machine*):

```
       +------------+
       |  Pending   |
       +-----+------+
             |
      +------+------+
      |             |
      v             v
+-----------+ +-----------+
| Confirmed | | Cancelled | (Terminal State)
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

#### Aturan Transisi:
- **`Pending`** $\rightarrow$ Dapat berubah menjadi **`Confirmed`** atau **`Cancelled`**.
- **`Confirmed`** $\rightarrow$ Dapat berubah menjadi **`Shipped`** atau **`Cancelled`**.
- **`Shipped`** $\rightarrow$ Hanya dapat berubah menjadi **`Delivered`**.
- **`Delivered` & `Cancelled`** $\rightarrow$ **Terminal State** (status akhir permanen, tidak dapat diubah lagi ke status manapun).
- Setiap upaya perubahan status di luar alur di atas wajib ditolak dengan HTTP Status `400 Bad Request` atau `422 Unprocessable Entity` disertai pesan error deskriptif.

---

### 2.3 Manajemen Pembatalan Order (Cancellation Rules)
- Pembatalan order hanya diizinkan apabila status order saat ini adalah **`Pending`** atau **`Confirmed`**.
- Order dengan status **`Shipped`**, **`Delivered`**, atau yang sudah **`Cancelled`** **tidak dapat dibatalkan**.
- **Rollback Stok**: Saat pembatalan berhasil dikonfirmasi, stok semua item pada order tersebut **wajib dikembalikan** (*restocked*) ke tabel produk secara atomik dalam satu transaksi database.

---

### 2.4 Manajemen Stok & Inventory (Stock Management)
Untuk kebutuhan prototipe, inventory dikelola dalam tabel/entitas `Products`:
- **Atribut Produk**: `Id` (GUID/INT), `Name` (NVARCHAR), `StockQuantity` (INT), `Price` (DECIMAL), `RowVersion` (Concurrency Token).
- **Aturan Pengurangan Stok**:
  - Saat pesanan dibuat (*Create Order*), stok produk langsung dikurangi sesuai kuantitas yang dipesan.
  - Jika stok tidak mencukupi (`StockQuantity < Quantity requested`), pesanan **wajib ditolak** dengan error yang jelas (`400 Bad Request` / `422 Unprocessable Entity`).
- **Invarian Absolut**:
  - **Stok tidak boleh bernilai minus ($StockQuantity \ge 0$) dalam kondisi apapun**, termasuk saat terjadi lonjakan *concurrent requests* berbarengan dalam milidetik yang sama.

---

## 3. Kebutuhan Non-Fungsional (Non-Functional Requirements - Wajib)

### 3.1 Idempotency (Pencegahan Double Submit)
- **Tujuan**: Mencegah terciptanya order ganda ketika client mengirimkan request yang sama berulang kali (akibat *double-click*, *network retry*, atau *client timeout*).
- **Mekanisme**:
  - Client wajib menyertakan header unik: `Idempotency-Key` (misal UUID v4) pada request `POST /api/orders`.
  - Sistem mencatat kunci idempotensi, hash payload, dan hasil response transaksi ke dalam cache/database.
  - Jika request dengan `Idempotency-Key` yang sama diterima kembali:
    - Jika request pertama telah selesai $\rightarrow$ Kembalikan response yang sama tanpa membuat order baru di database.
    - Jika request pertama masih dalam proses $\rightarrow$ Kembalikan status `409 Conflict` atau tunggu proses selesai secara thread-safe (*distributed lock / atomic gatekeeper*).
- **Justifikasi Arsitektur**: Pendekatan header `Idempotency-Key` adalah standar industri (mirip Stripe/Shopify) yang memberikan kontrol penuh pada *client application* untuk melakukan retry transaksi dengan aman.

---

### 3.2 Penanganan Konkurensi (Concurrency Handling - Fokus Utama)

Sistem wajib lolos dan teruji pada 3 skenario konkurensi ekstrem berikut:

#### A. Skenario A: Concurrent Stock Deduction
- **Kasus**: Dua user melakukan submit order secara bersamaan pada produk yang sama ($Product\ X$). Masing-masing order membutuhkan 10 unit, sementara sisa stok gudang hanya **15 unit**.
- **Requirement**:
  - Hanya satu transaksi order yang berhasil memotong stok menjadi sisa 5 unit.
  - Transaksi order lainnya harus gagal secara elegan dengan pesan *"Insufficient stock"*.
  - Total stok yang terpotong tidak boleh melebihi 15 unit, dan stok akhir tidak boleh minus.
- **Strategi Implementasi**:
  - *Atomic SQL Update* dengan guard clause (`UPDATE Products SET StockQuantity = StockQuantity - @Qty WHERE Id = @Id AND StockQuantity >= @Qty`), atau
  - *Pessimistic Locking* via `SELECT ... WITH (UPDLOCK, ROWLOCK)`, atau
  - *Optimistic Concurrency Control* via EF Core `RowVersion / IsRowVersion`.

#### B. Skenario B: Concurrent Status Update
- **Kasus**: Dua administrator memperbarui status order yang sama pada detik yang sama (Admin 1 mengubah status menjadi `Shipped`, sementara Admin 2 membatalkan order menjadi `Cancelled`).
- **Requirement**:
  - Hanya satu update yang menang (*first-committer wins*).
  - Admin yang kalah harus menerima error `409 Conflict` atau `DbUpdateConcurrencyException` yang menginformasikan bahwa data telah berubah oleh pengguna lain.
  - Mencegah inkonsistensi stok (order dibatalkan padahal barang sudah dikirim).
- **Strategi Implementasi**:
  - Menggunakan **Optimistic Concurrency Control** dengan kolom `RowVersion / byte[]` pada entitas `Order`.

#### C. Skenario C: Idempotent Create Under Race
- **Kasus**: Dua request `POST /api/orders` dengan `Idempotency-Key` yang sama tiba di server pada milidetik yang persis sama (sebelum transaksi pertama sempat commit ke database).
- **Requirement**:
  - Sistem hanya boleh membuat **1 pesanan**.
  - Request kedua tidak boleh menghasilkan duplicate order dan tidak boleh throw unhandled database constraint exception.
- **Strategi Implementasi**:
  - *Unique Index Constraint* pada kolom `IdempotencyKey` di tabel database yang dibungkus *try-catch unique violation*, atau
  - *Distributed Lock (Redis / SemaphoreSlim)* berbasis kunci idempotensi selama eksekusi pembuatan order.

---

### 3.3 Pencegahan Race Condition Tambahan (Extra Race Conditions)

Dokumentasi wajib mengidentifikasi dan menangani minimal 2 potensi race condition lainnya:

1. **Race Condition 1: Pembatalan Bersamaan dengan Pengiriman (Cancel Order vs Ship Order)**
   - *Resiko*: Pelanggan membatalkan order (`POST /orders/{id}/cancel`) pada detik yang sama saat gudang menandai pesanan telah dikirim (`PUT /orders/{id}/status -> Shipped`). Jika keduanya lolos, stok dikembalikan padahal barang fisik telah keluar.
   - *Pencegahan*: Validasi status berbasis *Optimistic Concurrency Token* (`RowVersion`). Jika status telah beralih ke `Shipped`, transaksi pembatalan ditolak dan rollback stok dibatalkan.
2. **Race Condition 2: Admin Mengubah Stok Manual vs Pelanggan Checkout**
   - *Resiko*: Admin gudang melakukan penyesuaian stok (*stock adjustment*) bertepatan saat proses checkout pesanan pelanggan sedang menghitung kuantitas.
   - *Pencegahan*: Operasi mutasi stok produk wajib bersifat atomik atau menggunakan transaksi database dengan tingkat isolasi yang memadai (*Read Committed Snapshot Isolation / Serializable*).

---

### 3.4 Validasi & Penanganan Error Terpusat (Error Handling)
- Semua response API (sukses maupun gagal) wajib menggunakan format terstandarisasi yang konsisten:
  ```json
  {
    "success": false,
    "responseCode": "400",
    "responseMessage": "Validation failed",
    "data": null,
    "errors": [
      "Quantity must be greater than zero."
    ]
  }
  ```
- Penggunaan HTTP Status Code yang semantik:
  - `200 OK` / `201 Created`: Berhasil dieksekusi.
  - `400 Bad Request`: Input data tidak valid secara format.
  - `404 Not Found`: Entitas order/produk tidak ditemukan.
  - `409 Conflict`: Terjadi konflik konkurensi (optimistic lock conflict / duplicate idempotent key).
  - `422 Unprocessable Entity`: Validasi bisnis gagal (stok tidak cukup, transisi status invalid).
  - `500 Internal Server Error`: Kesalahan sistem yang tidak tertangani.

---

### 3.5 Logging & Tracing Terstruktur (Observability)
- Tidak menggunakan `Console.WriteLine`.
- Menggunakan library logging profesional (**Serilog**) dengan output JSON / Structured Console Logging.
- Setiap HTTP Request wajib memiliki **Correlation ID / Trace ID** unik yang diteruskan dari header `X-Correlation-ID` atau digenerate otomatis, serta disertakan pada seluruh baris log transaksi untuk pelacakan end-to-end.

---

### 3.6 Persistensi Data (Database & Schema)
- **Database Engine**: Microsoft SQL Server / PostgreSQL.
- **ORM**: Entity Framework Core 10 dengan Migrations otomatis.
- **Desain Skema Database**:
  - `Products`: `Id` (PK, Guid), `Name`, `StockQuantity`, `Price`, `RowVersion` (Timestamp/Concurrency Token).
  - `Orders`: `OrderId` (PK, Guid), `OrderNumber`, `CustomerId`, `ShippingAddress`, `Status`, `TotalAmount`, `RowVersion`, `IdempotencyKey` (Unique Index), `CreatedDate`, `ModifiedDate`.
  - `OrderDetails`: `OrderDetailId` (PK, Guid), `OrderId` (FK), `ProductId` (FK), `Quantity`, `UnitPrice`, `SubTotal`.
  - `IdempotencyRecords`: `Key` (PK, Varchar), `RequestHash`, `ResponseStatusCode`, `ResponseBody`, `CreatedAt`, `ExpiresAt`.

---

### 3.7 Pengujian (Automated Testing Requirements)
- Proyek pengujian menggunakan **xUnit**, **FluentAssertions**, dan **NSubstitute / MockQueryable**.
- Wajib memiliki pengujian otomatis bernilai tinggi:
  1. **Unit Test**: Validasi transisi status (*State Machine*), kalkulasi harga, dan validator FluentValidation.
  2. **Concurrency Test (Wajib)**: Minimal 1 pengujian multithreading simulasi konkurensi paralel (menggunakan `Task.WhenAll` dengan 2 atau lebih thread konkuren) untuk membuktikan integritas stok (Skenario A), status update (Skenario B), atau idempotency (Skenario C).

---

## 4. Format Pengumpulan (Submission)
- **Repositori Git**: GitHub / GitLab / Bitbucket.
- **Dokumentasi README**: Wajib memuat justifikasi pemilihan strategi Concurrency, Idempotency, Database Engine, dan instruksi menjalankan aplikasi & pengujian secara lokal maupun container (Docker).
- **Pengiriman**: Link repositori dikirimkan ke kontak WhatsApp HR PT Adaya Solusi Teknologi yang menghubungi.
