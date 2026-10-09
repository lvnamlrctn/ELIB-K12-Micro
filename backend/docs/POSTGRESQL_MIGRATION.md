# Hướng dẫn: Chuyển sang PostgreSQL

## Tổng quan

Hầu hết hạ tầng đã hỗ trợ PostgreSQL sẵn. Chỉ cần **4 bước**:

| Bước | Nội dung | File thay đổi |
|------|----------|---------------|
| 1 | Thêm NuGet `Hangfire.PostgreSql` | `ELIBAPI.Infrastructure.csproj` |
| 2 | Hangfire conditional provider | `DependencyInjection.cs` |
| 3 | Cấu hình connection string | `appsettings.json` (cả 2 API) |
| 4 | Chạy migration SQL | Database PostgreSQL |

---

## Bước 1 — NuGet

**`ELIBAPI.Infrastructure/ELIBAPI.Infrastructure.csproj`** — thêm:

```xml
<PackageReference Include="Hangfire.PostgreSql" Version="1.20.9" />
```

> Tương thích với `Hangfire.Core 1.8.17` (yêu cầu >= 1.6.0).

---

## Bước 2 — DependencyInjection.cs

**File:** `ELIBAPI.Infrastructure/DependencyInjection.cs`

### Thêm using

```csharp
using Hangfire.PostgreSql;
```

### Sửa khối Hangfire (~dòng 291–306)

```csharp
// Trước:
services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(Hangfire.CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(hangfireConn));

// Sau:
services.AddHangfire(cfg =>
{
    cfg.SetDataCompatibilityLevel(Hangfire.CompatibilityLevel.Version_180)
       .UseSimpleAssemblyNameTypeSerializer()
       .UseRecommendedSerializerSettings();

    if (dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        cfg.UsePostgreSqlStorage(o => o.UseNpgsqlConnection(hangfireConn));
    else
        cfg.UseSqlServerStorage(hangfireConn);
});
```

> Biến `dbProvider` đã được khai báo ở đầu method (`configuration["DatabaseProvider"] ?? "SqlServer"`).

---

## Bước 3 — appsettings.json

Áp dụng cho **cả 2 file**:
- `ELIBAPI.API/appsettings.json`
- `ELIBAPI.API.Public/appsettings.json`

```json
{
  "DatabaseProvider": "PostgreSQL",
  "ConnectionStrings": {
    "PostgreSQL": "Host=<HOST>;Port=5432;Database=ELIBAPI;Username=<USER>;Password=<PASS>;TrustServerCertificate=True",
    "SqlServer":  "..."
  }
}
```

> Nếu muốn Hangfire dùng database riêng, thêm key `"Hangfire"` trong `ConnectionStrings`. Nếu không có, code tự fallback về `DefaultConnection`.

---

## Bước 4 — Migration SQL (chạy trên PostgreSQL)

### Tạo schemas

```sql
CREATE SCHEMA IF NOT EXISTS dbo;
CREATE SCHEMA IF NOT EXISTS cms;
CREATE SCHEMA IF NOT EXISTS "Ebook";
CREATE SCHEMA IF NOT EXISTS "EOffice";
CREATE SCHEMA IF NOT EXISTS "Evaluate";
CREATE SCHEMA IF NOT EXISTS "PrintBook";
```

> Schema có chữ hoa (`Ebook`, `EOffice`, `Evaluate`, `PrintBook`) phải để trong dấu ngoặc kép vì PostgreSQL case-sensitive.

### 3 cột mới cho Ebook chunking

```sql
ALTER TABLE "Ebook"."Item" ADD "FileVersion"   INTEGER   NULL;
ALTER TABLE "Ebook"."Item" ADD "IndexedAt"     TIMESTAMP NULL;
ALTER TABLE "Ebook"."Item" ADD "ErrorMessage"  TEXT      NULL;
```

### Bảng Hangfire

Tự động tạo khi API khởi động lần đầu (schema `hangfire` trong PostgreSQL database).

---

## Mapping kiểu dữ liệu SQL Server → PostgreSQL

| SQL Server | PostgreSQL | Ghi chú |
|-----------|-----------|---------|
| `NVARCHAR(n)` | `VARCHAR(n)` | EF Core tự map |
| `NVARCHAR(MAX)` | `TEXT` | EF Core tự map |
| `DATETIME` / `DATETIME2` | `TIMESTAMP` | EF Core tự map |
| `INT` | `INTEGER` | Giống nhau |
| `BIGINT IDENTITY(1,1)` | `BIGSERIAL` hoặc `BIGINT` + sequence | EF Core tự xử lý |
| `UNIQUEIDENTIFIER` | `UUID` | EF Core tự map |
| `newsequentialid()` | `gen_random_uuid()` | Đã xử lý trong `ELIBAPIDbContext.cs` |
| `BIT` | `BOOLEAN` | EF Core tự map |

---

## Những gì ĐÃ sẵn sàng (không cần sửa)

| Thành phần | Trạng thái |
|-----------|-----------|
| `ELIBAPIDbContext.cs` — provider switch | Đã có `UseNpgsql` / `UseSqlServer` conditional |
| `ELIBAPIDbContext.cs` — UUID default | Đã có `gen_random_uuid()` cho PostgreSQL |
| `Npgsql.EntityFrameworkCore.PostgreSQL` package | Đã có trong `Infrastructure.csproj` |
| `Program.cs` — timestamp behavior | Đã có `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true)` |
| Repository / Service code | Không dùng raw SQL, EF Core thuần |
| Connection string placeholder | Đã có `"PostgreSQL": "Host=REPLACE_ME..."` trong appsettings |

---

## Kiểm tra sau khi thực hiện

```bash
# 1. Build
dotnet build ELIBAPI.sln   # 0 Errors

# 2. Chạy API
cd ELIBAPI.API && dotnet run
# → Swagger mở được → GET /api/... trả về data từ PostgreSQL

# 3. Kiểm tra Hangfire
# → POST /api/Ebook/EbookFile/Upload (file PDF + ebookId)
# → /hangfire dashboard: job Enqueued → Processing → Succeeded
# → PostgreSQL: schema "hangfire" tự tạo với các bảng Hangfire
```
