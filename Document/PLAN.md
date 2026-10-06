# Rencana Implementasi Sistem (Technical Implementation Plan)
## Proyek: Prototype Order Management Service (.NET 10)
**Perusahaan**: PT Adaya Solusi Teknologi  
**Lokasi Dokumen**: `Document/PLAN.md`  
**Referensi Kebutuhan**: [REQUIREMENTS.md](file:///c:/_DATA/Document/Pribadi/Pribadi/Adaya/Project/Orders/Document/REQUIREMENTS.md)  
**Status**: Completed (All Phases 1 to 5 Passed)  

---

## 1. Ringkasan Eksekutif & Sasaran Teknis

Rencana kerja ini menjabarkan langkah-langkah implementasi prototipe **Order Management Service** menggunakan **Clean Architecture**, **CQRS (MediatR)**, dan **Entity Framework Core 10**. Sasaran utama adalah menyelesaikan 4 masalah inti klien:
1. **Idempotency**: Mencegah *double order* saat submit berulang.
2. **Concurrency Control**: Mencegah *stock inventory minus* dan *inconsistent order status* saat diakses simultan.
3. **Order State Machine**: Menjaga integritas transisi status dan *stock rollback* saat pembatalan order.
4. **Structured Logging & Correlation ID**: Memudahkan *error tracing* di *production*.

---

## 2. Rencana Arsitektur & Strategi Solusi

```
                             [ Client Requests ]
                                      │
                         (Header: Idempotency-Key,
                           Header: X-Correlation-ID)
                                      ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Orders.Api (Presentation Layer)                                        │
│  ├─ CorrelationIdMiddleware (Inject & propagate Trace ID)              │
│  ├─ IdempotencyMiddleware (Atomic check & cached response gatekeeper)  │
│  ├─ ExceptionMiddleware (Standardized ApiResponse format)              │
│  └─ OrderController (REST Endpoints)                                   │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ MediatR (ISender)
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Orders.Application (Business Logic & CQRS)                             │
│  ├─ ValidationBehavior (FluentValidation Pipeline)                     │
│  ├─ Commands: CreateOrder, UpdateOrderStatus, CancelOrder              │
│  ├─ Queries: GetOrderById, OrderList                                   │
│  ├─ Services: OrderStateMachine, IdempotencyService                    │
│  └─ Interfaces: IOrdersDbContext, ICurrentUserService                  │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Orders.Domain (Core Entities)                                          │
│  ├─ Order (with RowVersion, ShippingAddress, State Machine Rules)      │
│  ├─ OrderDetail (Product details & snapshot prices)                    │
│  ├─ Product (StockQuantity, Price, RowVersion)                         │
│  └─ IdempotencyRecord (Key, RequestHash, ResponseCache)                │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Orders.Persistance (Data Access)                                       │
│  ├─ OrdersContext (EF Core 10 DbContext)                               │
│  ├─ Configurations (Unique Index on IdempotencyKey, RowVersion token)  │
│  └─ Concurrency Guards (Atomic SQL / Optimistic Concurrency Control)   │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Rincian Fase Implementasi (Phase-by-Phase Roadmap)

### Fase 1: Domain & Persistance Enhancement (Fondasi Model & Konkurensi)
- [x] **Langkah 1.1**: Menambahkan entitas [Product.cs](file:///c:/_DATA/Document/Pribadi/Pribadi/Adaya/Project/Orders/Orders.Domain/Models/Product.cs)
  - Properti: `Id` (Guid), `Name` (string), `StockQuantity` (int), `Price` (decimal), `RowVersion` (`byte[]`).
- [x] **Langkah 1.2**: Memperbarui entitas [Order.cs](file:///c:/_DATA/Document/Pribadi/Pribadi/Adaya/Project/Orders/Orders.Domain/Models/Order.cs)
  - Menambahkan: `ShippingAddress` (string), `IdempotencyKey` (string?), `RowVersion` (`byte[]` - *Optimistic Concurrency Token*), enum/string `Status`.
- [x] **Langkah 1.3**: Menambahkan entitas [IdempotencyRecord.cs](file:///c:/_DATA/Document/Pribadi/Pribadi/Adaya/Project/Orders/Orders.Domain/Models/IdempotencyRecord.cs)
  - Properti: `Key` (string PK), `RequestHash` (string), `StatusCode` (int), `ResponseBody` (string), `CreatedAt` (DateTime), `ExpiresAt` (DateTime).
- [x] **Langkah 1.4**: Memperbarui [IOrdersDbContext.cs](file:///c:/_DATA/Document/Pribadi/Pribadi/Adaya/Project/Orders/Orders.Application/Common/Interfaces/IOrdersDbContext.cs) dan [OrdersContext.cs](file:///c:/_DATA/Document/Pribadi/Pribadi/Adaya/Project/Orders/Orders.Persistance/DbContext/OrdersContext.cs)
  - Mendaftarkan `DbSet<Product>` dan `DbSet<IdempotencyRecord>`.
  - Menerapkan Entity Configuration:
    - `Product`: Check constraint `StockQuantity >= 0`, `IsRowVersion()` pada `RowVersion`.
    - `Order`: `IsRowVersion()` pada `RowVersion`, Unique Index pada `IdempotencyKey` (`IsUnique(true)` filter not null).
    - `IdempotencyRecord`: Clustered PK pada `Key`.

---

### Fase 2: Application Layer (State Machine, Commands, & Concurrency Handlers)
- [x] **Langkah 2.1**: Implementasi Order State Machine
  - Membuat validator transisi status:
    - `Pending` $\rightarrow$ `Confirmed` atau `Cancelled`
    - `Confirmed` $\rightarrow$ `Shipped` atau `Cancelled`
    - `Shipped` $\rightarrow$ `Delivered`
    - `Delivered` & `Cancelled` $\rightarrow$ Terminal (Throw `InvalidOperationException` / `422 Unprocessable Entity`).
- [x] **Langkah 2.2**: Implementasi `CreateOrderCommand` & Handler
  - Input: `CustomerId`, `ShippingAddress`, `List<OrderItemRequest>`, `IdempotencyKey`.
  - **Penanganan Stok Atomik (Skenario A)**:
    - Mengurangi stok produk menggunakan *Atomic Execution* atau *Optimistic/Pessimistic Concurrency*:
      ```sql
      UPDATE Products 
      SET StockQuantity = StockQuantity - @Qty 
      WHERE Id = @ProductId AND StockQuantity >= @Qty
      ```
    - Jika baris terpengaruh = 0 $\rightarrow$ Tolak transaksi (*Insufficient Stock*), batalkan pembuatan order.
- [x] **Langkah 2.3**: Implementasi `UpdateOrderStatusCommand` & Handler (Skenario B)
  - Input: `OrderId`, `NewStatus`, `RowVersion`.
  - Menggunakan *Optimistic Concurrency Control*:
    - Load order, validasi `RowVersion`.
    - Jika versi database berbeda (sudah diubah admin lain secara bersamaan) $\rightarrow$ Tangkap `DbUpdateConcurrencyException` dan kembalikan HTTP `409 Conflict` dengan pesan yang jelas.
- [x] **Langkah 2.4**: Implementasi `CancelOrderCommand` & Handler
  - Input: `OrderId`, `Reason`, `RowVersion`.
  - Validasi status saat ini wajib `Pending` atau `Confirmed`.
  - Kembalikan stok item pesanan (*Restock*) ke tabel `Products` secara atomik dalam satu transaksi.
- [x] **Langkah 2.5**: Implementasi `GetOrderByIdQuery` & `OrderListQuery`
  - Memperbarui `OrderListQuery` untuk mendukung filter `CustomerId`, `Status`, dan `DateRange (StartDate, EndDate)`.

---

### Fase 3: Idempotency & Observability Middleware (API Layer)
- [x] **Langkah 3.1**: Implementasi `CorrelationIdMiddleware`
  - Membaca header `X-Correlation-ID` dari request atau generate `Guid.NewGuid()`.
  - Mendorong Correlation ID ke Serilog `LogContext` dan header response HTTP `X-Correlation-ID`.
- [x] **Langkah 3.2**: Implementasi `IdempotencyMiddleware` / Action Filter (Skenario C)
  - Mengekstrak header `Idempotency-Key` pada endpoint `POST /api/orders`.
  - Menghitung SHA-256 hash dari request body.
  - Cek apakah key sudah ada di `IdempotencyRecords`:
    - **Kasus Request Selesai**: Langsung return cached response body dan status code sebelumnya tanpa mengeksekusi handler lagi.
    - **Kasus Concurrent Request Berbarengan (Race)**: Memanfaatkan *Atomic Gatekeeper* (transaksi database dengan *Unique Constraint* atau *Distributed Lock Redis/Semaphore*) sehingga hanya 1 request yang memproses, sementara request kedua menunggu atau mendapat respon yang sama.
- [x] **Langkah 3.3**: Pembaruan [OrderController.cs](file:///c:/_DATA/Document/Pribadi/Pribadi/Adaya/Project/Orders/Orders.Api/Controllers/OrderController.cs)
  - `POST /api/orders`
  - `GET /api/orders/{id}`
  - `POST /api/orders/list`
  - `PUT /api/orders/{id}/status`
  - `POST /api/orders/{id}/cancel`

---

### Fase 4: Pengujian Otomatis (Automated & Concurrency Testing)
- [x] **Langkah 4.1**: Unit Test State Machine & Validasi
  - Uji transisi status valid vs invalid.
  - Uji pembatalan order saat status `Shipped` (harus ditolak).
- [x] **Langkah 4.2**: **Concurrency Test - Skenario A (Concurrent Stock Deduction)**
  - Inisialisasi produk dengan stok 15.
  - Luncurkan 2 thread paralel (`Task.WhenAll`) yang sama-sama memesan 10 unit.
  - Verifikasi: Tepat 1 transaksi berhasil, 1 transaksi gagal dengan error stok, sisa stok akhir harus 5 (tidak boleh minus).
- [x] **Langkah 4.3**: **Concurrency Test - Skenario B (Concurrent Status Update)**
  - Luncurkan 2 thread paralel (Admin 1: `Shipped`, Admin 2: `Cancelled`) pada order yang sama.
  - Verifikasi: 1 berhasil, 1 gagal dengan error `409 Conflict`.
- [x] **Langkah 4.4**: **Concurrency Test - Skenario C (Idempotent Create Under Race)**
  - Luncurkan 2 thread paralel `POST /orders` dengan `Idempotency-Key` yang identik.
  - Verifikasi: Hanya 1 order yang terbuat di database.

---

### Fase 5: Dokumentasi README & Delivery
- [x] **Langkah 5.1**: Penulisan [README.md](file:///c:/_DATA/Document/Pribadi/Pribadi/Adaya/Project/Orders/README.md)
  - Justifikasi strategi Concurrency (*Optimistic Concurrency vs Atomic Update*).
  - Justifikasi strategi Idempotency (*Header Idempotency-Key + Database Constraint*).
  - Pembahasan 2 race condition tambahan dan mitigasinya.
  - Panduan menjalankan service secara lokal dan via Docker.
- [x] **Langkah 5.2**: Verifikasi Solusi End-to-End (`dotnet test`, `dotnet build`).

---

## 4. Jadwal & Estimasi Waktu Pengerjaan

| Fase | Deskripsi Kerja | Estimasi | Output Utama |
| :---: | :--- | :---: | :--- |
| **Fase 1** | Domain & Persistance Enhancement | 2 - 3 Jam | Entitas `Product`, `Order`, `IdempotencyRecord` & DbContext |
| **Fase 2** | CQRS Commands, State Machine, & Handlers | 3 - 4 Jam | Logic Create, Update Status, Cancel, Restock |
| **Fase 3** | Idempotency & CorrelationId Middleware | 2 - 3 Jam | Idempotency Gatekeeper, Structured Logging |
| **Fase 4** | Automated Concurrency Testing | 2 - 3 Jam | Test Suite Skenario A, B, C lolos 100% |
| **Fase 5** | Final Review, README Justification & Packaging | 1 - 2 Jam | README lengkap, Docker siap jalan |

---

## 5. Matriks Risiko & Mitigasi Teknis

| Risiko Teknis | Dampak | Strategi Mitigasi |
| :--- | :--- | :--- |
| **Deadlock Database** saat concurrent stock update | Transaksi timeout / 500 error | Urutkan `ProductId` saat melakukan locking/update dalam multi-item order (*consistent lock ordering*). |
| **Client Retry Timeout** sebelum order selesai diproses | Double debit / duplicate order | Simpan status `In_Progress` pada `IdempotencyRecord` sebelum transaksi commit. |
| **False-positive Idempotency** (Payload beda dengan key sama) | Data korup | Simpan hash dari request body; jika key sama namun payload beda, tolak dengan `422 Unprocessable Entity / 400 Bad Request`. |
