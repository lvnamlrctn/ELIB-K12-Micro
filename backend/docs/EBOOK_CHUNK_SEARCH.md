# Hướng dẫn: Tìm kiếm Ebook theo Chunks (Elasticsearch + Hangfire)

## Tổng quan

Kiến trúc tìm kiếm Ebook dựa trên **chunk-based indexing**: mỗi file PDF được tách thành các đoạn ~500 từ, mỗi đoạn là 1 document trong Elasticsearch index `ebook_chunks`. Khi tìm kiếm, kết quả được **collapse theo `ebook_id`** để trả về 1 ebook/item, kèm highlight đoạn nội dung liên quan nhất.

---

## Kiến trúc tổng thể

```
Upload PDF
    │
    ▼
EbookFileController.Upload
    │
    ├─► MinIO (lưu file)
    ├─► EbookFile (DB)
    └─► BackgroundJob.Enqueue<EbookIndexingJob>(publicId)
                            │
                            ▼
                    Hangfire Worker
                            │
                    ┌───────┴────────┐
                    │               │
                    ▼               ▼
            PdfExtractorService  ELIBAPIDbContext
            (PdfPig: extract     (load metadata:
            text per page,       EbookItem +
            split ~500w chunks)  MetaDataValue +
                    │            EbookItemXml)
                    └───────┬────────┘
                            │
                            ▼
                  Map → EbookChunkDocument
                  (denormalize metadata)
                            │
                            ▼
               ElasticsearchService.UpsertChunksAsync
               (bulk upsert → ebook_chunks index)
                            │
                            ▼
               UPDATE Ebook.Item SET
                 IndexedAt=Now,
                 FileVersion++,
                 ErrorMessage=null
```

---

## 3 Luồng xử lý chính

### Luồng 1 — Thêm mới Ebook

```
POST /api/Ebook/EbookItem/Add
  1. INSERT Ebook.Item (FileVersion=null, IndexedAt=null)
  2. INSERT Ebook.MetaDataValue (Dublin Core fields)
  3. INSERT Ebook.ItemXml (denorm: title, author, publisher, ...)
  4. Sync → ebook_items (ES index cũ, metadata only)
  ✗ KHÔNG enqueue IndexingJob — chưa có file PDF
```

### Luồng 2 — Cập nhật metadata

```
PUT /api/Ebook/EbookItem/Update/{publicId}
  1. UPDATE Ebook.Item
  2. UPDATE/INSERT Ebook.MetaDataValue
  3. Rebuild Ebook.ItemXml
  4. Sync → ebook_items (ES cũ)
  5. [Điều kiện] Nếu EbookItem.IndexedAt != null:
       → ElasticsearchService.UpdateChunkMetadataAsync(ebookId)
         (UpdateByQuery Painless script: cập nhật title, author,
          keyword, dc_subject, dc_description, dc_language, dc_type
          KHÔNG re-extract PDF)
```

### Luồng 3 — Upload file PDF

```
POST /api/Ebook/EbookFile/Upload  (multipart: file + ebookId)
  1. Upload file → MinIO (private bucket)
  2. INSERT Ebook.EbookFile
  3. [Điều kiện] Nếu extension == "pdf" && ebookId != null:
       a. Load EbookItem.PublicId WHERE Id = ebookId
       b. BackgroundJob.Enqueue<EbookIndexingJob>(x => x.RunAsync(publicId))

--- Hangfire Job (async, retry 3×, 5 phút/lần) ---
  4. Load EbookItem + EbookItemXml + MetaDataValues + EbookFile (PDF đầu tiên)
  5. Nếu FileVersion > 1: DeleteChunksByEbookIdAsync → xóa chunks cũ
  6. MinioService.GetPrivateObjectStreamAsync(objectName)
  7. PdfExtractorService.ExtractChunksAsync(stream, chunkSize=500, overlap=50)
  8. Map List<PageChunk> → List<EbookChunkDocument> (denormalize metadata)
  9. BulkUpsertChunksAsync(chunks, batchSize=200)
 10. UPDATE Ebook.Item: IndexedAt=UtcNow, FileVersion++, ErrorMessage=null
 11. Catch Exception: UPDATE Ebook.Item SET ErrorMessage=ex.Message → rethrow
```

---

## Cấu trúc file mới/sửa đổi

### Core (ELIBAPI.Core)

| File | Loại | Mô tả |
|------|------|-------|
| `Entities/Ebook/EbookItem.cs` | Sửa | Thêm 3 trường: `FileVersion`, `IndexedAt`, `ErrorMessage` |
| `DTOs/Elasticsearch/EbookChunkDocument.cs` | Mới | Document ES cho mỗi chunk |
| `DTOs/Response/EbookSearchResponse.cs` | Mới | Response cho search API public |
| `DTOs/Request/Requests.cs` | Sửa | Thêm `EbookChunkSearchRequest` |
| `Interfaces/IElasticsearchService.cs` | Sửa | Thêm 6 method chunk mới |

### Infrastructure (ELIBAPI.Infrastructure)

| File | Loại | Mô tả |
|------|------|-------|
| `Services/ElasticsearchService.cs` | Sửa | Thêm index `ebook_chunks` + chunk methods |
| `Services/PdfExtractorService.cs` | Mới | Extract text từ PDF, split chunks |
| `Jobs/EbookIndexingJob.cs` | Mới | Hangfire job: extract → index |
| `DependencyInjection.cs` | Sửa | Đăng ký PdfExtractor + Hangfire |
| `ELIBAPI.Infrastructure.csproj` | Sửa | Thêm UglyToad.PdfPig + Hangfire packages |

### API nội bộ (ELIBAPI.API)

| File | Loại | Mô tả |
|------|------|-------|
| `Controllers/Ebook/EbookFileController.cs` | Sửa | Enqueue job sau upload PDF |
| `Program.cs` | Sửa | `app.UseHangfireDashboard("/hangfire")` |
| `appsettings.json` | Sửa | Thêm `ChunkIndexName`, `Chunking` config |
| `ELIBAPI.API.csproj` | Sửa | Thêm `Hangfire.AspNetCore` |

### API public (ELIBAPI.API.Public)

| File | Loại | Mô tả |
|------|------|-------|
| `Controllers/Ebook/PublicEbookSearchController.cs` | Mới | 3 endpoint search public |

---

## DB Migration

Chạy SQL sau trước khi khởi động API:

```sql
ALTER TABLE Ebook.Item ADD FileVersion   INT           NULL;
ALTER TABLE Ebook.Item ADD IndexedAt     DATETIME      NULL;
ALTER TABLE Ebook.Item ADD ErrorMessage  NVARCHAR(MAX) NULL;
```

Bảng Hangfire được tự tạo bởi `UseSqlServerStorage` khi khởi động lần đầu.

---

## Cấu hình (appsettings.json)

```json
{
  "ElasticsearchSettings": {
    "Uri": "http://...",
    "IndexName": "ebook_items",
    "ChunkIndexName": "ebook_chunks",
    "Username": "elastic",
    "Password": "..."
  },
  "Chunking": {
    "ChunkSize": 500,
    "Overlap":   50
  }
}
```

Hangfire dùng `ConnectionStrings:Hangfire` nếu có, fallback sang `ConnectionStrings:DefaultConnection`.

---

## Elasticsearch Index `ebook_chunks`

### Mapping chính

| Field | Type | Boost | Ghi chú |
|-------|------|-------|---------|
| `ebook_id` | keyword | — | collapse field |
| `title` | text + keyword + search_as_you_type | ^4 | vn_analyzer |
| `author` | text + keyword | ^3 | vn_analyzer |
| `keyword`, `dc_subject` | text | ^2 | vn_analyzer |
| `content`, `dc_description` | text | ^1 | term_vector: with_positions_offsets |
| `collection_id`, `topic_id`, `subject_id` | keyword | — | filter |
| `dc_language`, `dc_type` | keyword | — | facet |
| `free` | boolean | — | filter |
| `publish_date` | date | — | facet (year histogram) |
| `tenant_id` | long | — | filter |
| `embedding` | dense_vector dim=384, index=false | — | dùng cho chatbot sau |

### ChunkId format

```
"{ebookPublicId}_p{pageNumber:D4}_c{chunkIndex:D2}"
// Ví dụ: "a1b2c3d4-..._p0012_c03"
```

---

## Public Search API

Base route: `GET /api/public/ebook`

### 1. Tìm kiếm toàn văn

```
GET /api/public/ebook/search
```

**Query params:**

| Param | Type | Mô tả |
|-------|------|-------|
| `q` | string | Từ khóa tìm kiếm |
| `page` | int | Trang (default: 1) |
| `pageSize` | int | Số kết quả/trang (default: 10) |
| `collectionId` | string | Lọc theo bộ sưu tập |
| `topicId` | string | Lọc theo chủ đề |
| `subjectId` | string | Lọc theo môn học |
| `language` | string | Lọc theo ngôn ngữ (`dc_language`) |
| `free` | bool | Lọc tài liệu miễn phí |
| `fromYear` | int | Năm xuất bản từ |
| `toYear` | int | Năm xuất bản đến |
| `tenantId` | long | Lọc theo đơn vị (optional) |

**Response:**

```json
{
  "total": 128,
  "page": 1,
  "pageSize": 10,
  "items": [
    {
      "ebookId": "a1b2c3d4-...",
      "title": "Lịch sử Việt Nam",
      "author": "Nguyễn Văn A",
      "publisher": "NXB Giáo dục",
      "publishDate": "2020",
      "dcLanguage": "vi",
      "dcIdentifier": "978-...",
      "images": "cover.jpg",
      "collectionName": "Lịch sử",
      "free": true,
      "bestPageNumber": 42,
      "highlight": "...đoạn văn bản <mark>từ khóa</mark> được tìm thấy...",
      "score": 12.5
    }
  ],
  "facets": {
    "languages":   [{ "key": "vi", "count": 85 }, { "key": "en", "count": 43 }],
    "topics":      [{ "key": "topic-guid-...", "count": 30 }],
    "collections": [{ "key": "col-guid-...", "count": 20 }],
    "years":       [{ "key": "2020", "count": 15 }],
    "freeCount":   67
  }
}
```

### 2. Autocomplete / Suggest

```
GET /api/public/ebook/search/suggest?q=lịch sử&tenantId=1
```

**Response:** `["Lịch sử Việt Nam", "Lịch sử thế giới", ...]`

Dùng `search_as_you_type` field `title` với `multi_match type: bool_prefix`.

### 3. Tìm kiếm trong sách (In-book search)

```
GET /api/public/ebook/search/{ebookId}?q=từ khóa
```

Tìm tất cả chunks của ebook đó khớp với từ khóa, trả về từng đoạn nội dung với highlight. Dùng để hiển thị "Tìm trong sách".

---

## NuGet packages thêm mới

| Package | Version | Project |
|---------|---------|---------|
| `UglyToad.PdfPig` | 0.1.9-alpha001-patch1 | Infrastructure |
| `Hangfire.Core` | 1.8.17 | Infrastructure |
| `Hangfire.SqlServer` | 1.8.17 | Infrastructure |
| `Hangfire.AspNetCore` | 1.8.17 | Infrastructure + API |

---

## Namespace aliases (ElasticsearchService.cs)

Dùng alias để tránh conflict kiểu giữa các namespace Elastic:

```csharp
using EsAgg        = Elastic.Clients.Elasticsearch.Aggregations;
using EsCoreSearch = Elastic.Clients.Elasticsearch.Core.Search;
using EsQdsl       = Elastic.Clients.Elasticsearch.QueryDsl;
```

**Lý do:** `Elastic.Clients.Elasticsearch.QueryDsl` có kiểu `Fields` xung đột với `Elastic.Clients.Elasticsearch.Fields`. `HighlightField` nằm trong `Core.Search`, `CalendarInterval` nằm trong `Aggregations` — dùng alias để phân biệt rõ ràng.

---

## Lưu ý triển khai

### Highlight
`HighlightDescriptor<T>` trong Elastic.Clients.Elasticsearch 8.x không có method `Fields()`. Phải dùng object overload:

```csharp
s.Highlight(new EsCoreSearch.Highlight
{
    Fields = new Dictionary<Field, EsCoreSearch.HighlightField>
    {
        ["content"] = new EsCoreSearch.HighlightField
        {
            FragmentSize      = 200,
            NumberOfFragments = 1,
            PreTags           = new[] { "<mark>" },
            PostTags          = new[] { "</mark>" }
        }
    }
});
```

### InnerHits + Highlight
`InnerHitsDescriptor<T>` trong 8.x **không có** method `Highlight`. Đặt highlight ở cấp search, không đặt trong `InnerHits`.

### CalendarInterval
Dùng enum, không dùng string:
```csharp
.CalendarInterval(EsAgg.CalendarInterval.Year)  // ✓
.CalendarInterval("year")                        // ✗ CS1503
```

### Hangfire Dashboard
Chỉ expose trong môi trường nội bộ. Truy cập tại `/hangfire` sau khi API khởi động.

---

## Kiểm tra sau triển khai

```bash
# 1. Build không lỗi
dotnet build ELIBAPI.sln   # 0 Errors

# 2. Luồng 1 — Thêm mới (chưa có chunks)
POST /api/Ebook/EbookItem/Add
→ Ebook.Item.IndexedAt IS NULL

# 3. Luồng 3 — Upload PDF
POST /api/Ebook/EbookFile/Upload (PDF + ebookId)
→ Hangfire dashboard /hangfire: job Enqueued → Processing → Succeeded
→ Ebook.Item.IndexedAt NOT NULL
→ GET /api/public/ebook/search?q=... → trả về chunks

# 4. Luồng 2 — Cập nhật metadata
PUT /api/Ebook/EbookItem/Update/{publicId}
→ ES: title/author thay đổi trong ebook_chunks (nếu đã indexed)

# 5. Search API
GET /api/public/ebook/search?q=lịch sử&tenantId=1
GET /api/public/ebook/search/suggest?q=lịch
GET /api/public/ebook/search/{ebookPublicId}?q=từ khóa
```
