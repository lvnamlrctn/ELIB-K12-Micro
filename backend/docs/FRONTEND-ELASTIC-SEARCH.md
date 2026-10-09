# Hướng dẫn Frontend sử dụng Elasticsearch Search API

## Tổng quan các endpoint tìm kiếm Ebook

| # | Method | URL | Mục đích |
|---|--------|-----|----------|
| 1 | `POST` | `/api/public/PublicEbook/Search` | Tìm database (phân trang) |
| 2 | `POST` | `/api/public/PublicEbook/SearchAll` | Tìm database (tất cả) |
| 3 | `GET` | `/api/public/ebook/search` | Tìm Elasticsearch (full-text cơ bản) |
| 4 | **`POST`** | **`/api/public/ebook/SearchElastic`** | **Tìm Elasticsearch (nâng cao, theo từng trường)** |
| 5 | `GET` | `/api/public/ebook/search/suggest` | Gợi ý autocomplete |
| 6 | `GET` | `/api/public/ebook/search/{ebookId}` | Tìm trong 1 ebook cụ thể |

---

## Endpoint: `POST /api/public/ebook/SearchElastic`

### Request Body

```typescript
interface PublicEbookElasticSearchRequest {
  q?: string;               // Full-text search tất cả trường (title, author, keyword, content...)
  page?: number;             // Trang hiện tại (default: 1)
  pageSize?: number;         // Số kết quả/trang (default: 10, max: 50)

  // ── Tìm theo từng trường (match - tìm gần đúng) ──
  title?: string;            // Tìm theo tên sách
  author?: string;           // Tìm theo tác giả
  publisher?: string;        // Tìm theo nhà xuất bản
  keyword?: string;          // Tìm theo từ khóa
  dcSubject?: string;        // Tìm theo chủ đề Dublin Core

  // ── Lọc chính xác (term filter) ──
  collectionId?: string;     // ID bộ sưu tập (long ID dạng string, vd: "217")
  topicId?: string;          // ID chủ đề (long ID dạng string, vd: "95")
  subjectId?: string;        // ID lĩnh vực (long ID dạng string, vd: "378")
  language?: string;         // Ngôn ngữ (vd: "vi", "en")
  dcType?: string;           // Loại tài liệu Dublin Core (vd: "Book", "Thesis")
  free?: boolean;            // true = miễn phí, false = có phí
  share?: boolean;           // true = chia sẻ, false = riêng tư
  tenantId?: number;         // ID thư viện/tenant

  // ── Lọc theo khoảng năm xuất bản ──
  publishDateFrom?: string;  // Năm bắt đầu (vd: "2020")
  publishDateTo?: string;    // Năm kết thúc (vd: "2025")
}
```

### Response

```typescript
interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
}

interface PublicEbookElasticResponse {
  total: number;             // Tổng số kết quả
  page: number;
  pageSize: number;
  items: PublicEbookElasticItem[];
  facets: EbookSearchFacets; // Thống kê để làm bộ lọc sidebar
}

interface PublicEbookElasticItem {
  ebookId: string;           // PublicId (GUID) của ebook — dùng để lấy chi tiết
  title?: string;
  author?: string;
  publisher?: string;
  publishDate?: string;
  keyword?: string;
  images?: string;           // Đường dẫn ảnh bìa
  collectionId?: string;     // ID bộ sưu tập
  collectionName?: string;   // Tên bộ sưu tập
  topicId?: string;
  topicName?: string;        // Tên chủ đề
  subjectId?: string;
  subjectName?: string;      // Tên lĩnh vực
  free: boolean;
  share: boolean;
  dcSubject?: string;        // Chủ đề Dublin Core
  dcDescription?: string;    // Mô tả Dublin Core
  dcLanguage?: string;       // Ngôn ngữ
  dcIdentifier?: string;     // ISBN/ISSN
  dcType?: string;           // Loại (Book, Thesis...)
  dcContributor?: string;    // Người đóng góp
  bestPageNumber?: number;   // Trang có nội dung liên quan nhất
  highlight?: string;        // Đoạn text highlight (HTML có <mark>)
  score?: number;            // Điểm relevance (cao = phù hợp hơn)
}

interface EbookSearchFacets {
  languages: FacetItem[];    // Thống kê theo ngôn ngữ
  topics: FacetItem[];       // Thống kê theo chủ đề
  collections: FacetItem[];  // Thống kê theo bộ sưu tập
  years: FacetItem[];        // Thống kê theo năm xuất bản
  freeCount: number;         // Số lượng sách miễn phí
}

interface FacetItem {
  key: string;
  count: number;
}
```

---

## Ví dụ sử dụng

### 1. Tìm kiếm cơ bản (full-text)

```typescript
const res = await fetch('/api/public/ebook/SearchElastic', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    q: "Hồ Chí Minh",
    page: 1,
    pageSize: 10,
    tenantId: 1
  })
});
const { data } = await res.json();
// data.items — danh sách ebook
// data.total — tổng kết quả
// data.facets — thống kê cho sidebar filter
```

### 2. Tìm theo từng trường riêng

```typescript
// Tìm sách của tác giả "Nguyễn" có tiêu đề chứa "lịch sử"
const res = await fetch('/api/public/ebook/SearchElastic', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    title: "lịch sử",
    author: "Nguyễn",
    page: 1,
    pageSize: 10,
    tenantId: 1
  })
});
```

### 3. Kết hợp full-text + filter

```typescript
// Full-text "cách mạng" + lọc theo bộ sưu tập + chỉ sách miễn phí
const res = await fetch('/api/public/ebook/SearchElastic', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    q: "cách mạng",
    collectionId: "217",
    free: true,
    page: 1,
    pageSize: 10,
    tenantId: 1
  })
});
```

### 4. Lọc theo khoảng năm xuất bản

```typescript
// Sách xuất bản từ 2020 đến 2025
const res = await fetch('/api/public/ebook/SearchElastic', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    publishDateFrom: "2020",
    publishDateTo: "2025",
    page: 1,
    pageSize: 20,
    tenantId: 1
  })
});
```

### 5. Tìm nâng cao — nhiều điều kiện

```typescript
// Tìm luận văn tiếng Việt về kinh tế, chia sẻ miễn phí
const res = await fetch('/api/public/ebook/SearchElastic', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    dcType: "Thesis",
    dcSubject: "kinh tế",
    language: "vi",
    free: true,
    share: true,
    page: 1,
    pageSize: 10,
    tenantId: 1
  })
});
```

---

## Sử dụng Facets cho Sidebar Filter

Response trả về `facets` giúp xây dựng bộ lọc sidebar động:

```typescript
const { data } = await res.json();

// Hiển thị filter theo ngôn ngữ
data.facets.languages.forEach(f => {
  console.log(`${f.key}: ${f.count} sách`);
  // "vi": 150 sách, "en": 42 sách
});

// Hiển thị filter theo bộ sưu tập (collectionId)
data.facets.collections.forEach(f => {
  console.log(`Collection ${f.key}: ${f.count} sách`);
  // Collection 217: 30 sách
});

// Hiển thị filter theo năm
data.facets.years.forEach(f => {
  console.log(`${f.key}: ${f.count} sách`);
});

// Số sách miễn phí
console.log(`Miễn phí: ${data.facets.freeCount}`);
```

---

## Hiển thị Highlight

Trường `highlight` chứa HTML với tag `<mark>` cho phần text khớp:

```typescript
// item.highlight = "...sự kiện <mark>Hồ Chí Minh</mark> đi tìm đường..."

// Render trong Angular
<p [innerHTML]="item.highlight"></p>

// Render trong React
<p dangerouslySetInnerHTML={{ __html: item.highlight }} />
```

---

## Phân biệt `q` vs các trường riêng

| Param | Loại tìm kiếm | Ví dụ |
|-------|---------------|-------|
| `q` | Full-text — tìm trên TẤT CẢ trường (title, author, keyword, content, dc_subject...) với boosting | `q: "Hồ Chí Minh"` tìm ở mọi nơi |
| `title` | Match — chỉ tìm trên trường `title` | `title: "sự kiện"` chỉ khớp tiêu đề |
| `author` | Match — chỉ tìm trên trường `author` | `author: "Nguyễn"` chỉ khớp tác giả |
| `collectionId` | Term — lọc chính xác | `collectionId: "217"` chỉ lấy đúng bộ sưu tập |

**Có thể kết hợp:** `q` + các trường riêng + filter sẽ AND với nhau. Ví dụ: `q: "cách mạng"` + `author: "Nguyễn"` → tìm full-text "cách mạng" AND tác giả chứa "Nguyễn".

---

## Lưu ý quan trọng

- **`collectionId`, `topicId`, `subjectId`** là **long ID** (database ID) dạng string, KHÔNG phải GUID PublicId
- **`tenantId`** luôn truyền để lọc theo thư viện
- **`pageSize`** tối đa 50
- **Không cần JWT** — endpoint public, không cần header Authorization
- **Kết quả deduplicate** — mỗi ebook chỉ xuất hiện 1 lần (collapse by ebook_id), lấy chunk có score cao nhất
- **`ebookId`** trong response là GUID (PublicId) — dùng để gọi `GET /api/public/PublicEbook/{ebookId}` lấy chi tiết
