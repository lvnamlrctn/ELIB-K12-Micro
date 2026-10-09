# Biên mục theo ảnh bìa (Analyze Book Image)

Tự động trích xuất metadata sách từ **ảnh bìa** bằng AI để điền nhanh form Biên mục tài liệu số (EbookItem).

## 1. Mô tả tính năng & luồng xử lý

Người dùng chọn ảnh bìa trong form EbookItem → bấm **"Biên mục theo ảnh bìa"** → FE gửi ảnh lên API → backend gọi mô hình AI (LLM vision) đọc ảnh → trả JSON metadata → FE tự điền vào các trường form.

```
Frontend (chọn ảnh + bấm nút)
        │  multipart/form-data (field: images)
        ▼
POST /api/Ebook/EbookItem/AnalyzeBookImage   (Bearer JWT)
        │
        ▼
Backend → LLM vision (Ollama/Claude/OpenAI/Gemini)
        │  đọc bìa → trích title/author/publisher/year/isbn/language/description
        ▼
ApiResponse<BookMetadataResult> (JSON)
        │
        ▼
Frontend patch form (Title, Author[], Publisher, PublishDate, Isbn, Abstract...)
```

## 2. Endpoint (đã tồn tại — không sửa code)

| Thuộc tính | Giá trị |
|---|---|
| URL | `POST /api/Ebook/EbookItem/AnalyzeBookImage` |
| Content-Type | `multipart/form-data` |
| Auth | `Authorization: Bearer <JWT>` (yêu cầu đăng nhập) |
| Field name | `images` (multi-value — gửi 1 hoặc nhiều ảnh) |
| Response | `ApiResponse<BookMetadataResult>` |

**Response shape:**
```json
{
  "success": true,
  "message": "Phân tích thành công",
  "data": {
    "title":       "Lập trình C# nâng cao",
    "author":      "Nguyễn Văn A, Trần Thị B",
    "publisher":   "NXB Thông tin và Truyền thông",
    "publishYear": "2023",
    "isbn":        "978-604-80-1234-5",
    "language":    "vi",
    "description": "Giới thiệu ngắn về nội dung sách nếu có trên bìa"
  }
}
```
> Trường không đọc được trên ảnh → giá trị `null`.

## 3. Hướng dẫn cấu hình backend (`appsettings.json`)

Backend chọn nhà cung cấp LLM qua key `LLMSettings`. Ví dụ cho từng provider:

**Ollama (local, miễn phí — cần `ollama pull` model vision):**
```json
"LLMSettings": {
  "Provider": "Ollama",
  "BaseUrl": "http://localhost:11434",
  "Model": "llama3.2-vision",
  "TimeoutSeconds": 120
}
```

**Claude (Anthropic):**
```json
"LLMSettings": {
  "Provider": "Claude",
  "ApiKey": "sk-ant-...",
  "Model": "claude-3-5-sonnet-latest",
  "TimeoutSeconds": 60
}
```

**OpenAI:**
```json
"LLMSettings": {
  "Provider": "OpenAI",
  "ApiKey": "sk-...",
  "Model": "gpt-4o-mini",
  "TimeoutSeconds": 60
}
```

**Gemini (Google):**
```json
"LLMSettings": {
  "Provider": "Gemini",
  "ApiKey": "AIza...",
  "Model": "gemini-1.5-flash",
  "TimeoutSeconds": 60
}
```

## 4. Ví dụ gọi API

**curl (dev test nhanh):**
```bash
curl -X POST "http://localhost:5107/api/Ebook/EbookItem/AnalyzeBookImage" \
  -H "Authorization: Bearer <JWT>" \
  -F "images=@/path/bia-sach.jpg"
```

**Angular service** (`ebook-document.service.ts`):
```ts
analyzeBookImage(files: File[]): Observable<BookMetadataResult | null> {
  const form = new FormData();
  files.forEach(f => form.append('images', f));
  return this.http.post<any>(`${this.baseUrl}/AnalyzeBookImage`, form)
    .pipe(map(r => r?.data ?? null));
}
```

**Auto-fill form** (`ebook-document.ts` → `analyzeCover()` / `applyBookMetadata()`):
```ts
this.service.analyzeBookImage([file]).subscribe(meta => {
  if (!meta) return;
  if (meta.title)       this.docForm.patchValue({ title: meta.title });
  if (meta.publisher)   this.docForm.patchValue({ publisher: meta.publisher });
  if (meta.description) this.docForm.patchValue({ abstract: meta.description });
  if (meta.publishYear) this.docForm.patchValue({ publishYear: +meta.publishYear });
  // author: tách theo dấu phẩy → authors[]; isbn → numbers[] (type 'isbn')
});
```

## 5. Mapping `BookMetadataResult` → form EbookItem

| Trường AI trả về | Trường form EbookItem | Ghi chú |
|---|---|---|
| `title` | `title` | patch trực tiếp |
| `author` | `authors[]` (FormArray `{lastName, firstName}`) | tách theo dấu phẩy; quy ước VN: từ đầu = `lastName`, phần còn lại = `firstName` |
| `publisher` | `publisher` | patch trực tiếp |
| `publishYear` | `publishYear` | `parseInt` |
| `isbn` | `numbers[]` (FormArray, `type='isbn'`) | thêm 1 dòng số định danh |
| `language` | `languageId` | **KHÔNG auto-set**: AI trả mã `"vi"` nhưng form dùng FK `languageId` (số). Cần map thủ công, hoặc sau này tra dropdown ngôn ngữ theo mã/tên |
| `description` | `abstract` | patch trực tiếp |

> Chỉ điền field AI đọc được; field `null` được bỏ qua, không ghi đè dữ liệu đang nhập.

## 6. Xử lý lỗi phổ biến

| Hiện tượng | Nguyên nhân | Cách xử lý |
|---|---|---|
| Trả về toàn `null` / sai | Ảnh mờ, nghiêng, thiếu sáng | Chụp lại rõ nét, đủ sáng, thẳng góc; thử nhiều ảnh (`images` nhiều giá trị) |
| Lỗi timeout | Model chậm / ảnh lớn | Tăng `TimeoutSeconds`; nén ảnh trước khi gửi |
| Ollama lỗi "model not found" | Chưa tải model vision | Chạy `ollama pull llama3.2-vision` trên máy chạy Ollama |
| 401 Unauthorized | Thiếu/het hạn JWT | Đăng nhập lại, đảm bảo gửi header `Authorization` |
| FE hiện toast lỗi | `success=false` hoặc `data=null` | Kiểm tra log backend + cấu hình `LLMSettings` |

> FE hiển thị spinner khi đang phân tích (`isAnalyzing`) và toast thành công/lỗi (`EBOOK_DOC.ANALYZE_SUCCESS` / `EBOOK_DOC.ANALYZE_ERROR`).
