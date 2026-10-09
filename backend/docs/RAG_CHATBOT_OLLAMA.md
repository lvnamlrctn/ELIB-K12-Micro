# Kế hoạch: RAG Chatbot với Ollama + Elasticsearch kNN

## Context

Field `Embedding` đã khai báo trong `EbookChunkDocument` nhưng **chưa làm gì cả**: không có dense_vector mapping trong ES, không có service tạo embedding, không có kNN search, không có endpoint chat. Mục tiêu: tích hợp Ollama (local) để:
1. Tạo embedding khi index PDF chunk → lưu vào ES `dense_vector`
2. kNN search theo vector khi chatbot gửi câu hỏi
3. Endpoint `POST /api/public/ebook/chat` nhận câu hỏi → RAG → gọi Ollama LLM → trả lời + nguồn trích dẫn

---

## Cấu hình Ollama (appsettings.json)

```json
"OllamaSettings": {
  "BaseUrl": "http://localhost:11434",
  "EmbeddingModel": "nomic-embed-text",
  "ChatModel": "llama3.2",
  "EmbeddingDimensions": 768
}
```

> `nomic-embed-text` → 768 dims, multilingual, hỗ trợ tiếng Việt tốt.
> `ChatModel` thay bằng bất kỳ model nào đã pull về Ollama (qwen2.5, mistral, gemma3...).

---

## Phạm vi thay đổi — 8 bước

### Bước 1 — IEmbeddingService (Core)

**File mới:** `ELIBAPI.Core/Interfaces/IEmbeddingService.cs`

```csharp
public interface IEmbeddingService
{
    Task<float[]>   EmbedAsync(string text);
    Task<float[][]> EmbedBatchAsync(IEnumerable<string> texts);
}
```

**File mới:** `ELIBAPI.Infrastructure/Services/OllamaEmbeddingService.cs`

- POST `{BaseUrl}/api/embeddings` với `{"model": "nomic-embed-text", "prompt": "..."}`
- Response: `{"embedding": [float...]}`
- Batch: gọi tuần tự (Ollama không hỗ trợ batch native)

---

### Bước 2 — IOllamaChatService (Core)

**File mới:** `ELIBAPI.Core/Interfaces/IOllamaChatService.cs`

```csharp
public interface IOllamaChatService
{
    Task<string> ChatAsync(string systemPrompt, string userMessage);
}
```

**File mới:** `ELIBAPI.Infrastructure/Services/OllamaChatService.cs`

- POST `{BaseUrl}/api/chat` với messages array + `"stream": false`
- Response: `{"message": {"role": "assistant", "content": "..."}}`

---

### Bước 3 — Cập nhật ES Mapping (dense_vector)

**File:** `ELIBAPI.Infrastructure/Services/ElasticsearchService.cs`

Trong `CreateChunkIndexAsync`, thêm field `embedding` vào mapping:

```csharp
.DenseVector("embedding", dv => dv
    .Dims(768)
    .Index(true)
    .Similarity(DenseVectorSimilarity.Cosine))
```

> **Lưu ý deploy:** Index `ebook_chunks` hiện có phải **xóa và tạo lại** vì ES không cho phép thêm `dense_vector` field vào index đã tồn tại. Sau khi deploy, upload lại PDF để re-index với embedding.

---

### Bước 4 — Cập nhật EbookIndexingJob (gán Embedding)

**File:** `ELIBAPI.Infrastructure/Jobs/EbookIndexingJob.cs`

- Inject `IEmbeddingService`
- Sau bước map chunks → `EbookChunkDocument`, thêm:

```csharp
var contents = docChunks.Select(c => c.Content ?? "").ToList();
var vectors  = await _embedding.EmbedBatchAsync(contents);
for (int i = 0; i < docChunks.Count; i++)
    docChunks[i].Embedding = vectors[i];
```

---

### Bước 5 — kNN Search method

**File:** `ELIBAPI.Core/Interfaces/IElasticsearchService.cs` — thêm:

```csharp
Task<EbookSearchResponse> SearchChunksByVectorAsync(float[] queryVector, EbookChunkSearchRequest request);
```

**File:** `ELIBAPI.Infrastructure/Services/ElasticsearchService.cs` — implement kNN query dùng `KnnSearch` với collapse theo `ebook_id` và highlight `content`.

---

### Bước 6 — DTOs cho Chat

**File:** `ELIBAPI.Core/DTOs/Request/Requests.cs` — thêm:

```csharp
public class EbookChatRequest
{
    public string  Question  { get; set; } = "";
    public long?   TenantId  { get; set; }
    public int     TopK      { get; set; } = 5;
    public string? Language  { get; set; }
    public bool?   Free      { get; set; }
}
```

**File:** `ELIBAPI.Core/DTOs/Response/EbookSearchResponse.cs` — thêm:

```csharp
public class EbookChatResponse
{
    public string                Answer  { get; set; } = "";
    public List<EbookChatSource> Sources { get; set; } = [];
}

public class EbookChatSource
{
    public string  EbookId    { get; set; } = "";
    public string? Title      { get; set; }
    public string? Author     { get; set; }
    public int?    PageNumber { get; set; }
    public string? Highlight  { get; set; }
}
```

---

### Bước 7 — Chat endpoint (PublicEbookSearchController)

**File:** `ELIBAPI.API.Public/Controllers/Ebook/PublicEbookSearchController.cs`

Thêm `POST /api/public/ebook/chat`:

Flow:
1. `EmbeddingService.EmbedAsync(request.Question)` → `float[] queryVector`
2. `SearchChunksByVectorAsync(queryVector, ...)` → top-K chunks
3. Build RAG system prompt với context từ các chunks (title + trang + nội dung)
4. `OllamaChatService.ChatAsync(systemPrompt, request.Question)` → answer
5. Return `EbookChatResponse { Answer, Sources }`

---

### Bước 8 — DI + appsettings

**File:** `ELIBAPI.Infrastructure/DependencyInjection.cs`

```csharp
services.AddHttpClient<IEmbeddingService,  OllamaEmbeddingService>();
services.AddHttpClient<IOllamaChatService, OllamaChatService>();
```

**File:** `ELIBAPI.API/appsettings.json` và `ELIBAPI.API.Public/appsettings.json` — thêm `OllamaSettings`.

---

## Sơ đồ luồng Chat

```
POST /api/public/ebook/chat  {"question": "sách nào nói về..."}
        │
        ▼
OllamaEmbeddingService.EmbedAsync(question)  → float[768]
        │
        ▼
ElasticsearchService.SearchChunksByVectorAsync(vector, filters)
        │  → top-K EbookSearchItem với content highlight
        ▼
Build RAG prompt:
  "Dựa trên tài liệu sau, hãy trả lời câu hỏi..."
  [Chunk 1 — {title}, trang {page}]: {content}
  ...
        │
        ▼
OllamaChatService.ChatAsync(systemPrompt, question)  → string answer
        │
        ▼
EbookChatResponse { Answer, Sources[] }
```

---

## Thứ tự file cần tạo/sửa

| # | File | Loại |
|---|------|------|
| 1 | `ELIBAPI.Core/Interfaces/IEmbeddingService.cs` | Mới |
| 2 | `ELIBAPI.Core/Interfaces/IOllamaChatService.cs` | Mới |
| 3 | `ELIBAPI.Infrastructure/Services/OllamaEmbeddingService.cs` | Mới |
| 4 | `ELIBAPI.Infrastructure/Services/OllamaChatService.cs` | Mới |
| 5 | `ELIBAPI.Infrastructure/Services/ElasticsearchService.cs` | Sửa (dense_vector mapping + kNN method) |
| 6 | `ELIBAPI.Core/Interfaces/IElasticsearchService.cs` | Sửa (thêm SearchChunksByVectorAsync) |
| 7 | `ELIBAPI.Infrastructure/Jobs/EbookIndexingJob.cs` | Sửa (inject IEmbeddingService + gán Embedding) |
| 8 | `ELIBAPI.Core/DTOs/Request/Requests.cs` | Sửa (EbookChatRequest) |
| 9 | `ELIBAPI.Core/DTOs/Response/EbookSearchResponse.cs` | Sửa (EbookChatResponse + EbookChatSource) |
| 10 | `ELIBAPI.API.Public/Controllers/Ebook/PublicEbookSearchController.cs` | Sửa (thêm /chat endpoint) |
| 11 | `ELIBAPI.Infrastructure/DependencyInjection.cs` | Sửa (AddHttpClient cho 2 services mới) |
| 12 | `ELIBAPI.API/appsettings.json` + `ELIBAPI.API.Public/appsettings.json` | Sửa (thêm OllamaSettings) |

---

## Kiểm tra sau triển khai

```bash
# 1. Cài model Ollama (trên server chạy Ollama)
ollama pull nomic-embed-text
ollama pull llama3.2

# 2. Build
dotnet build ELIBAPI.sln   # 0 Errors

# 3. Drop index ebook_chunks cũ (ES mapping change)
# DELETE http://{ES_HOST}/ebook_chunks

# 4. Khởi động API → index tự tạo lại với dense_vector field

# 5. Upload lại PDF (tạo embedding)
POST /api/Ebook/EbookFile/Upload  (PDF + ebookId)
→ Hangfire job: extract → embed → bulk index với vector

# 6. Test chat
POST /api/public/ebook/chat
{"question": "tìm sách về lịch sử Việt Nam", "tenantId": 1, "topK": 5}
→ {"answer": "Dựa trên tài liệu...", "sources": [...]}
```
