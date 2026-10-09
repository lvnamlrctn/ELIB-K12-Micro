# CLAUDE.md — Hệ thống Tìm kiếm Tài liệu số (Ebook Search System)

## Tổng quan kiến trúc

Hệ thống thư viện số với khả năng tìm kiếm full-text và sẵn sàng cho chatbot RAG.

```
PostgreSQL (Source of Truth)
  ├── Ebooks          → thông tin cơ bản
  └── EbookDetails    → Dublin Core metadata

MinIO                 → lưu trữ file PDF

Elasticsearch 8.14.3  → tìm kiếm (metadata + content chunks)

.NET 8 API            → backend
Angular               → frontend
Hangfire              → background jobs
```

---

## Cấu trúc Solution

```
EbookSearch/
├── EbookSearch.API/              ← ASP.NET Core 8 Web API
│   ├── Controllers/
│   │   ├── EbooksController.cs
│   │   └── SearchController.cs
│   ├── Program.cs
│   └── appsettings.json
│
├── EbookSearch.Application/      ← Business logic
│   ├── Services/
│   │   ├── EbookService.cs
│   │   ├── IndexingService.cs
│   │   └── SearchService.cs
│   ├── Jobs/
│   │   └── IndexingJob.cs
│   └── DTOs/
│
├── EbookSearch.Infrastructure/   ← Data access
│   ├── Elasticsearch/
│   │   ├── ElasticsearchService.cs
│   │   └── Mappings/
│   ├── Storage/
│   │   └── MinioStorageService.cs
│   ├── Pdf/
│   │   └── PdfExtractorService.cs
│   └── Repositories/
│
└── EbookSearch.Domain/           ← Entities, Enums
    ├── Entities/
    └── Enums/
```

---

## 1. Domain Entities

### File: `EbookSearch.Domain/Entities/Ebook.cs`

```csharp
namespace EbookSearch.Domain.Entities;

public class Ebook
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Thông tin cơ bản
    public string Title { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Publisher { get; set; }
    public DateTime? PublishDate { get; set; }

    // Phân loại
    public Guid? CollectionId { get; set; }
    public Guid? TopicId { get; set; }
    public Guid? SubjectId { get; set; }

    // Quyền truy cập
    public bool Free { get; set; } = true;
    public bool Share { get; set; } = true;

    // Từ khóa tìm kiếm
    public string? Keyword { get; set; }

    // Trạng thái file
    public EbookStatus Status { get; set; } = EbookStatus.Draft;
    public string? FileUrl { get; set; }         // ObjectName trong MinIO
    public int? TotalPages { get; set; }
    public int FileVersion { get; set; } = 1;
    public string? PreviousFileUrl { get; set; } // File cũ, xóa sau khi index xong

    // Tracking
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? IndexedAt { get; set; }
    public string? ErrorMessage { get; set; }

    // Navigation
    public EbookDetail? Detail { get; set; }
}
```

### File: `EbookSearch.Domain/Entities/EbookDetail.cs`

```csharp
namespace EbookSearch.Domain.Entities;

/// <summary>
/// Dublin Core Metadata 15 trường chuẩn thư viện
/// </summary>
public class EbookDetail
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EbookId { get; set; }

    public string? DC_Title { get; set; }        // Tên tài liệu (có thể có phụ đề)
    public string? DC_Creator { get; set; }       // Tác giả, đồng tác giả
    public string? DC_Subject { get; set; }       // Chủ đề, từ khóa
    public string? DC_Description { get; set; }   // Tóm tắt nội dung
    public string? DC_Publisher { get; set; }     // Nhà xuất bản
    public string? DC_Contributor { get; set; }   // Biên tập, dịch giả
    public string? DC_Date { get; set; }          // Ngày xuất bản (ISO 8601)
    public string? DC_Type { get; set; }          // Loại: Text, Image, Dataset...
    public string? DC_Format { get; set; }        // Định dạng: application/pdf
    public string? DC_Identifier { get; set; }    // ISBN, ISSN, DOI
    public string? DC_Source { get; set; }        // Nguồn gốc tài liệu
    public string? DC_Language { get; set; }      // Ngôn ngữ: vi, en
    public string? DC_Relation { get; set; }      // Liên quan tài liệu khác
    public string? DC_Coverage { get; set; }      // Phạm vi địa lý/thời gian
    public string? DC_Rights { get; set; }        // Bản quyền

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Ebook Ebook { get; set; } = null!;
}
```

### File: `EbookSearch.Domain/Enums/EbookStatus.cs`

```csharp
namespace EbookSearch.Domain.Enums;

public enum EbookStatus
{
    Draft,       // Metadata đã tạo, chưa có file
    Processing,  // Đang extract + index ES
    Published,   // Hoàn thành, có thể tìm kiếm
    Failed       // Lỗi trong quá trình xử lý
}
```

---

## 2. Elasticsearch Document Model

### File: `EbookSearch.Infrastructure/Elasticsearch/Documents/EbookChunkDocument.cs`

```csharp
namespace EbookSearch.Infrastructure.Elasticsearch.Documents;

/// <summary>
/// Document lưu trong Elasticsearch.
/// Mỗi document = 1 chunk (đoạn văn bản) của 1 trang.
/// Metadata được denormalize để tìm kiếm không cần JOIN.
/// </summary>
public class EbookChunkDocument
{
    // --- Identity ---
    public string ChunkId { get; set; } = string.Empty;     // "{ebookId}_p{page}_c{chunk}"
    public string EbookId { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public int ChunkIndex { get; set; }                      // Thứ tự chunk trong trang
    public string Content { get; set; } = string.Empty;     // Nội dung chunk (~500 từ)
    public int FileVersion { get; set; }
    public DateTime IndexedAt { get; set; }

    // --- Metadata cơ bản (từ Ebooks) ---
    public string Title { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Publisher { get; set; }
    public DateTime? PublishDate { get; set; }
    public string? Keyword { get; set; }

    // --- Phân loại ---
    public string? CollectionId { get; set; }
    public string? TopicId { get; set; }
    public string? SubjectId { get; set; }

    // --- Quyền truy cập ---
    public bool Free { get; set; }
    public bool Share { get; set; }

    // --- Dublin Core (từ EbookDetails, chỉ các trường hay search) ---
    public string? DcSubject { get; set; }
    public string? DcDescription { get; set; }
    public string? DcLanguage { get; set; }
    public string? DcIdentifier { get; set; }     // ISBN, DOI
    public string? DcType { get; set; }
    public string? DcRights { get; set; }

    // --- Vector (để trống khi index, bật sau cho chatbot) ---
    public float[]? Embedding { get; set; }
}
```

---

## 3. Elasticsearch Index Setup

### File: `EbookSearch.Infrastructure/Elasticsearch/IndexSetup.cs`

Tạo method `CreateIndexAsync` với mapping sau:

**Index name:** `ebook_chunks`

**Settings:**
```json
{
  "number_of_shards": 5,
  "number_of_replicas": 1,
  "index.refresh_interval": "30s",
  "analysis": {
    "analyzer": {
      "vi_analyzer": {
        "type": "custom",
        "tokenizer": "icu_tokenizer",
        "filter": ["icu_folding", "lowercase", "vi_stop"]
      },
      "vi_search_analyzer": {
        "type": "custom",
        "tokenizer": "icu_tokenizer",
        "filter": ["icu_folding", "lowercase"]
      }
    },
    "filter": {
      "vi_stop": {
        "type": "stop",
        "stopwords": ["và", "của", "là", "các", "có", "được", "trong",
                      "để", "với", "cho", "không", "này", "đó", "từ", "một"]
      }
    }
  }
}
```

**Mappings:**

| Field | Type | Analyzer | Notes |
|---|---|---|---|
| `chunk_id` | keyword | — | ID duy nhất |
| `ebook_id` | keyword | — | Dùng collapse |
| `page_number` | integer | — | |
| `chunk_index` | integer | — | |
| `content` | text | vi_analyzer | `term_vector: with_positions_offsets` |
| `file_version` | integer | — | |
| `indexed_at` | date | — | |
| `title` | text + keyword + search_as_you_type | vi_analyzer | `^4` boost |
| `author` | text + keyword | vi_analyzer | `^3` boost |
| `publisher` | keyword | — | Dùng filter/facet |
| `publish_date` | date | — | |
| `keyword` | text | vi_analyzer | `^2` boost |
| `collection_id` | keyword | — | Filter |
| `topic_id` | keyword | — | Filter |
| `subject_id` | keyword | — | Filter |
| `free` | boolean | — | Filter |
| `share` | boolean | — | Filter |
| `dc_subject` | text | vi_analyzer | `^2` boost |
| `dc_description` | text | vi_analyzer | `^1` boost |
| `dc_language` | keyword | — | Facet |
| `dc_identifier` | keyword | — | Tìm theo ISBN/DOI |
| `dc_type` | keyword | — | Filter |
| `dc_rights` | keyword | — | |
| `embedding` | dense_vector | — | `dims: 384`, `index: false` — BẬT SAU cho chatbot |

**Quan trọng:** `embedding` phải có `"index": false` — lưu sẵn trường này nhưng chưa build kNN index để tiết kiệm RAM. Khi cần chatbot chỉ cần PUT mapping đổi sang `"index": true`.

---

## 4. Chunk Strategy

### File: `EbookSearch.Infrastructure/Pdf/PdfExtractorService.cs`

**Logic chunking:**
- Extract text từng trang bằng `PdfPig` (NuGet: `UglyToad.PdfPig`)
- Mỗi trang chia thành chunks ~500 từ, overlap 50 từ giữa các chunk
- Nếu trang < 100 từ: 1 chunk = cả trang
- Làm sạch text: bỏ ký tự đặc biệt thừa, normalize whitespace

```csharp
public class PageChunk
{
    public int PageNumber { get; set; }
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;
}

public interface IPdfExtractorService
{
    Task<List<PageChunk>> ExtractChunksAsync(Stream pdfStream, 
                                              int chunkSize = 500, 
                                              int overlap = 50);
}
```

**Cài đặt NuGet cần thiết:**
```
UglyToad.PdfPig
```

---

## 5. MinIO Storage Service

### File: `EbookSearch.Infrastructure/Storage/MinioStorageService.cs`

**Interface:**
```csharp
public interface IStorageService
{
    Task<string> UploadAsync(Guid ebookId, IFormFile file);    // Trả về objectName
    Task<Stream> GetStreamAsync(string objectName);
    Task<string> GetPresignedUrlAsync(string objectName, int expirySeconds = 3600);
    Task DeleteAsync(string objectName);
}
```

**Quy tắc objectName:** `books/{ebookId}/{yyyyMMddHHmmss}.pdf`

**Bucket name:** `library-books` (tạo tự động nếu chưa có)

**NuGet cần thiết:**
```
Minio
```

---

## 6. Indexing Job

### File: `EbookSearch.Application/Jobs/IndexingJob.cs`

**Logic thực thi khi được Hangfire gọi:**

```
1. Lấy Ebook + EbookDetail từ PostgreSQL (Include Detail)
2. Nếu FileVersion > 1:
   a. Xóa toàn bộ chunks cũ trong ES (delete_by_query theo ebook_id)
   b. Xóa file cũ trên MinIO (PreviousFileUrl)
3. GetStreamAsync từ MinIO
4. ExtractChunksAsync → List<PageChunk>
5. Map sang List<EbookChunkDocument> (denormalize metadata vào từng chunk)
6. BulkIndex vào ES theo batch 200 chunks
7. Cập nhật PostgreSQL: Status = Published, TotalPages, IndexedAt
8. Nếu lỗi: Status = Failed, ErrorMessage = ex.Message → throw (Hangfire tự retry)
```

**Hangfire retry policy:** 3 lần, delay 5 phút mỗi lần.

---

## 7. Search Service

### File: `EbookSearch.Application/Services/SearchService.cs`

**Tìm kiếm chính — trả về 1 hit/sách (collapse theo ebook_id):**

Query logic:
```
bool query:
  must: multi_match trên các fields với boost:
    - title^4 (vi_analyzer)
    - author^3
    - keyword^2
    - dc_subject^2
    - dc_description^1
    - content^1
  filter (nếu có):
    - collection_id = ?
    - topic_id = ?
    - subject_id = ?
    - free = true/false
    - dc_language = ?
    - publish_date range

collapse: ebook_id
  inner_hits: best_chunk (size=1, highlight content)

highlight: content (fragment_size=200, pre_tag=<mark>, post_tag=</mark>)

aggregations:
  - by_language: terms dc_language
  - by_topic: terms topic_id
  - by_collection: terms collection_id
  - by_year: date_histogram publish_date (interval=year)
  - free_only: terms free

size: 10, from: (page-1)*10
```

**Autocomplete — gợi ý tên sách:**
```
multi_match bool_prefix trên title.suggest
collapse: ebook_id
_source: ebook_id, title, author
size: 8
```

**Tìm nội dung trong 1 sách:**
```
bool:
  filter: ebook_id = ?
  must: match content = keyword
sort: page_number asc
size: 20
```

**DTOs trả về:**

```csharp
public class SearchResponse
{
    public long Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<SearchItem> Items { get; set; } = [];
    public SearchFacets Facets { get; set; } = new();
}

public class SearchItem
{
    public string EbookId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Publisher { get; set; }
    public DateTime? PublishDate { get; set; }
    public string? DcLanguage { get; set; }
    public string? DcIdentifier { get; set; }
    public bool Free { get; set; }
    public bool Share { get; set; }
    public int? TotalPages { get; set; }

    // Chunk match tốt nhất
    public int? BestPageNumber { get; set; }
    public string? Highlight { get; set; }
    public double? Score { get; set; }
}

public class SearchFacets
{
    public List<FacetItem> Languages { get; set; } = [];
    public List<FacetItem> Topics { get; set; } = [];
    public List<FacetItem> Collections { get; set; } = [];
    public List<FacetItem> Years { get; set; } = [];
    public long FreeCount { get; set; }
}

public record FacetItem(string Key, long Count);
```

---

## 8. API Controllers

### File: `EbookSearch.API/Controllers/EbooksController.cs`

| Method | Endpoint | Mô tả |
|---|---|---|
| POST | `/api/ebooks` | Tạo metadata (Status = Draft) |
| GET | `/api/ebooks/{id}` | Lấy chi tiết 1 ebook (từ PostgreSQL) |
| PUT | `/api/ebooks/{id}` | Cập nhật metadata cơ bản + sync ES |
| PUT | `/api/ebooks/{id}/detail` | Cập nhật Dublin Core + sync ES |
| POST | `/api/ebooks/{id}/upload` | Upload file PDF → enqueue IndexingJob |
| GET | `/api/ebooks/{id}/view-url` | Lấy presigned URL xem file (1 giờ) |
| DELETE | `/api/ebooks/{id}` | Xóa ebook (soft delete SQL + delete ES) |

**Upload endpoint logic:**
```
1. Validate: file phải là PDF, size < 500MB
2. Upload lên MinIO → objectName
3. Cập nhật SQL:
   - PreviousFileUrl = FileUrl (cũ)
   - FileUrl = objectName (mới)
   - FileVersion++
   - Status = Processing
4. BackgroundJob.Enqueue<IndexingJob>(id)
5. Return 202 Accepted
```

**Update metadata logic:**
```
1. Cập nhật SQL
2. Nếu Status = Published: gọi ES UpdateByQuery
   - Cập nhật title, author, publisher, publish_date,
     keyword, free, share vào tất cả chunks của ebook
3. Return 200
```

### File: `EbookSearch.API/Controllers/SearchController.cs`

| Method | Endpoint | Mô tả |
|---|---|---|
| GET | `/api/search?q=...&page=1` | Tìm kiếm chính |
| GET | `/api/search/suggest?q=...` | Autocomplete |
| GET | `/api/search/ebook/{id}?q=...` | Tìm trong 1 sách |

**Query params cho search chính:**
- `q` — từ khóa
- `page` — trang (default 1)
- `pageSize` — kích thước trang (default 10, max 50)
- `collectionId`, `topicId`, `subjectId` — filter phân loại
- `language` — filter ngôn ngữ
- `free` — filter miễn phí (true/false)
- `fromYear`, `toYear` — filter năm xuất bản

---

## 9. PostgreSQL Setup

### File: `EbookSearch.Infrastructure/Persistence/AppDbContext.cs`

Dùng EF Core 8 với PostgreSQL (NuGet: `Npgsql.EntityFrameworkCore.PostgreSQL`).

**Migration SQL cần tạo index:**

```sql
-- Ebooks
CREATE INDEX idx_ebooks_status      ON "Ebooks"("Status");
CREATE INDEX idx_ebooks_collection  ON "Ebooks"("CollectionId");
CREATE INDEX idx_ebooks_topic       ON "Ebooks"("TopicId");
CREATE INDEX idx_ebooks_subject     ON "Ebooks"("SubjectId");
CREATE INDEX idx_ebooks_free        ON "Ebooks"("Free");
CREATE INDEX idx_ebooks_created     ON "Ebooks"("CreatedAt" DESC);

CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE INDEX idx_ebooks_title_trgm  ON "Ebooks" USING GIN("Title" gin_trgm_ops);
CREATE INDEX idx_ebooks_author_trgm ON "Ebooks" USING GIN("Author" gin_trgm_ops);

-- EbookDetails
CREATE UNIQUE INDEX idx_detail_ebook      ON "EbookDetails"("EbookId");
CREATE INDEX idx_detail_identifier        ON "EbookDetails"("DC_Identifier");
CREATE INDEX idx_detail_language          ON "EbookDetails"("DC_Language");
```

---

## 10. Configuration

### `appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=EbookSearch;Username=postgres;Password=xxx"
  },
  "Elasticsearch": {
    "Url": "http://localhost:9200",
    "Username": "elastic",
    "Password": "xxx",
    "IndexName": "ebook_chunks"
  },
  "Minio": {
    "Endpoint": "localhost:9000",
    "AccessKey": "minioadmin",
    "SecretKey": "xxx",
    "BucketName": "library-books",
    "UseSSL": false
  },
  "Hangfire": {
    "ConnectionString": "Host=localhost;Database=HangfireDb;Username=postgres;Password=xxx"
  },
  "Chunking": {
    "ChunkSize": 500,
    "Overlap": 50
  }
}
```

### `Program.cs` — đăng ký services

```csharp
// PostgreSQL + EF Core
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

// Elasticsearch 8.x
builder.Services.AddSingleton<ElasticsearchClient>(sp => {
    var settings = new ElasticsearchClientSettings(new Uri(esUrl))
        .Authentication(new BasicAuthentication(user, pass))
        .DefaultIndex("ebook_chunks");
    return new ElasticsearchClient(settings);
});

// MinIO
builder.Services.AddSingleton<IMinioClient>(sp =>
    new MinioClient()
        .WithEndpoint(endpoint)
        .WithCredentials(accessKey, secretKey)
        .Build());

// Services
builder.Services.AddScoped<IStorageService, MinioStorageService>();
builder.Services.AddScoped<IPdfExtractorService, PdfExtractorService>();
builder.Services.AddScoped<IElasticsearchService, ElasticsearchService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IEbookService, EbookService>();

// Hangfire với PostgreSQL
builder.Services.AddHangfire(config => config
    .UsePostgreSqlStorage(hangfireConn));
builder.Services.AddHangfireServer(options => {
    options.WorkerCount = 4;  // 4 workers song song
    options.Queues = ["indexing", "default"];
});

// Index ES khi startup
var app = builder.Build();
using var scope = app.Services.CreateScope();
await scope.ServiceProvider
    .GetRequiredService<IElasticsearchService>()
    .EnsureIndexExistsAsync();
```

---

## 11. NuGet Packages

### `EbookSearch.API`
```
Hangfire.AspNetCore
Hangfire.PostgreSql
```

### `EbookSearch.Application`
```
Hangfire.Core
```

### `EbookSearch.Infrastructure`
```
Elastic.Clients.Elasticsearch        (v8.14.x)
Npgsql.EntityFrameworkCore.PostgreSQL
Minio
UglyToad.PdfPig
```

### `EbookSearch.Domain`
```
(không có dependency ngoài)
```

---

## 12. Docker Compose

### `docker-compose.yml`

```yaml
version: '3.8'

services:
  elasticsearch:
    image: elasticsearch:8.14.3
    container_name: es01
    environment:
      - discovery.type=single-node
      - ES_JAVA_OPTS=-Xms16g -Xmx16g
      - xpack.security.enabled=true
      - xpack.security.http.ssl.enabled=false
      - ELASTIC_PASSWORD=${ES_PASSWORD}
      - bootstrap.memory_lock=true
    ulimits:
      memlock: { soft: -1, hard: -1 }
      nofile:  { soft: 65536, hard: 65536 }
    mem_limit: 30g
    volumes:
      - es_data:/usr/share/elasticsearch/data
    ports:
      - "9200:9200"
    restart: always
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://elastic:${ES_PASSWORD}@localhost:9200/_cluster/health || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 5

  minio:
    image: minio/minio:latest
    container_name: minio01
    command: server /data --console-address ":9001"
    environment:
      - MINIO_ROOT_USER=${MINIO_USER}
      - MINIO_ROOT_PASSWORD=${MINIO_PASSWORD}
    ports:
      - "9000:9000"
      - "9001:9001"
    mem_limit: 4g
    volumes:
      - minio_data:/data
    restart: always
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:9000/minio/health/live"]
      interval: 30s
      timeout: 10s
      retries: 3

  api:
    build: .
    container_name: api01
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=${DB_CONNECTION}
      - Elasticsearch__Url=http://elasticsearch:9200
      - Elasticsearch__Password=${ES_PASSWORD}
      - Minio__Endpoint=minio:9000
      - Minio__AccessKey=${MINIO_USER}
      - Minio__SecretKey=${MINIO_PASSWORD}
    mem_limit: 3g
    ports:
      - "8080:8080"
    restart: always
    depends_on:
      elasticsearch:
        condition: service_healthy
      minio:
        condition: service_healthy

volumes:
  es_data:
  minio_data:
```

---

## 13. Kế hoạch mở rộng Chatbot (KHÔNG làm ngay)

Khi cần thêm chatbot RAG, chỉ cần 2 bước — **không migration dữ liệu:**

**Bước 1:** Bật kNN index trên field embedding
```json
PUT /ebook_chunks/_mapping
{
  "properties": {
    "embedding": {
      "type": "dense_vector",
      "dims": 384,
      "index": true,
      "similarity": "cosine",
      "index_options": { "type": "hnsw", "m": 16, "ef_construction": 100 }
    }
  }
}
```

**Bước 2:** Chạy Hangfire job điền embedding
- Model: `paraphrase-multilingual-MiniLM-L6-v2` (384 dims, tiếng Việt tốt)
- Chạy batch 100 chunks/lần vào ban đêm
- Sau khi có embedding: dùng RRF hybrid search (BM25 + kNN)

---

## Quy tắc code

1. **Tất cả async/await** — không dùng `.Result` hay `.Wait()`
2. **Cancellation token** — truyền xuống tất cả các layer
3. **Logging** — dùng `ILogger<T>`, log đầy đủ lỗi trong IndexingJob
4. **Validation** — FluentValidation cho tất cả request DTOs
5. **Error handling** — Global exception middleware, trả về ProblemDetails
6. **Bulk size** — ES bulk index tối đa 200 chunks/batch
7. **Chunk ID format** — `{ebookId}_p{pageNumber:D4}_c{chunkIndex:D2}`
   Ví dụ: `550e8400-e29b-41d4-a716-446655440000_p0001_c00`
