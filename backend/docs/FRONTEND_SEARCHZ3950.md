# Hướng dẫn Frontend — API SearchZ3950

API tìm kiếm liên thư viện theo chuẩn Z39.50. Hỗ trợ 3 loại thư viện: Internal (EF Core), Z3950SRU (HTTP/CQL), Z3950 (TCP/BER binary).

---

## Endpoints

| Method | URL | Mô tả |
|---|---|---|
| `POST` | `/api/PrintBook/SearchZ3950/SearchBrief` | Kiểm tra kết nối + đếm số biểu ghi |
| `POST` | `/api/PrintBook/SearchZ3950/SearchDetail` | Lấy danh sách biểu ghi chi tiết có phân trang |

**Auth:** `Authorization: Bearer <token>` (bắt buộc)

---

## SearchBrief

Dùng để kiểm tra nhanh xem các thư viện có kết nối được và có bao nhiêu biểu ghi thỏa mãn — không trả về nội dung chi tiết.

### Request

```http
POST /api/PrintBook/SearchZ3950/SearchBrief
Authorization: Bearer <token>
Content-Type: application/json
```

```json
{
  "fields": [
    { "field": "Title", "value": "Python" },
    { "field": "Author", "value": "Guido" }
  ],
  "operator": "AND",
  "libraryIds": [
    "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "7cb12a1e-9823-4abc-b1de-3c852d77bcf1"
  ]
}
```

| Tham số | Kiểu | Mô tả |
|---|---|---|
| `fields[].field` | string | Tên trường tìm kiếm (xem bảng Field bên dưới) |
| `fields[].value` | string | Giá trị cần tìm |
| `operator` | string | `"AND"` hoặc `"OR"` |
| `libraryIds` | `Guid[]` | Mảng `Z3950Config.PublicId` của các thư viện cần tìm |

### Response

```json
{
  "success": true,
  "data": [
    {
      "libraryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "libraryName": "Thư viện Quốc gia",
      "systax": "Z3950",
      "connected": true,
      "count": 142,
      "error": null
    },
    {
      "libraryId": "7cb12a1e-9823-4abc-b1de-3c852d77bcf1",
      "libraryName": "Thư viện nội bộ",
      "systax": "Internal",
      "connected": true,
      "count": 37,
      "error": null
    }
  ]
}
```

| Trường | Kiểu | Mô tả |
|---|---|---|
| `libraryId` | Guid | PublicId của thư viện |
| `libraryName` | string | Tên thư viện |
| `systax` | string | Loại kết nối: `Internal` / `Z3950` / `Z3950SRU` |
| `connected` | bool | `true` nếu kết nối thành công |
| `count` | int | Số biểu ghi tìm được (0 nếu không có hoặc lỗi) |
| `error` | string? | Mô tả lỗi nếu `connected = false` |

---

## SearchDetail

Lấy danh sách biểu ghi đầy đủ, có phân trang. Mỗi thư viện trong `libraryIds[]` trả về một khối kết quả riêng.

### Request

```http
POST /api/PrintBook/SearchZ3950/SearchDetail
Authorization: Bearer <token>
Content-Type: application/json
```

```json
{
  "fields": [
    { "field": "Title", "value": "Python" }
  ],
  "operator": "AND",
  "libraryIds": [
    "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  ],
  "pageIndex": 1,
  "pageSize": 10
}
```

| Tham số | Kiểu | Mặc định | Mô tả |
|---|---|---|---|
| `fields` | array | — | Danh sách điều kiện tìm kiếm |
| `operator` | string | `"AND"` | `"AND"` hoặc `"OR"` |
| `libraryIds` | `Guid[]` | — | Thư viện cần tìm |
| `pageIndex` | int | `1` | Trang hiện tại (bắt đầu từ 1) |
| `pageSize` | int | `10` | Số biểu ghi mỗi trang |

### Response

```json
{
  "success": true,
  "data": [
    {
      "libraryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "libraryName": "Thư viện Quốc gia",
      "systax": "Z3950",
      "connected": true,
      "totalCount": 142,
      "pageIndex": 1,
      "pageSize": 10,
      "records": [
        {
          "publicId": null,
          "title": "Learning Python",
          "author": "Mark Lutz",
          "publisher": "O'Reilly Media",
          "publishDate": "2013",
          "keyword": "Python programming",
          "otherTitle": null
        }
      ],
      "error": null
    }
  ]
}
```

| Trường | Kiểu | Mô tả |
|---|---|---|
| `totalCount` | int | Tổng số biểu ghi tìm được của thư viện này |
| `pageIndex` | int | Trang hiện tại |
| `pageSize` | int | Số records/trang đã yêu cầu |
| `records` | array | Danh sách biểu ghi |
| `records[].publicId` | Guid? | Chỉ có giá trị khi `systax = "Internal"`, null với Z3950 bên ngoài |
| `records[].title` | string? | Nhan đề |
| `records[].author` | string? | Tác giả |
| `records[].publisher` | string? | Nhà xuất bản |
| `records[].publishDate` | string? | Năm xuất bản |
| `records[].keyword` | string? | Từ khóa / chủ đề |
| `records[].otherTitle` | string? | Nhan đề khác (thường chỉ có ở Internal) |

---

## Các giá trị hợp lệ cho `field`

| `field` | Tìm theo | Ghi chú |
|---|---|---|
| `Title` | Nhan đề | |
| `Author` | Tác giả | |
| `Publisher` | Nhà xuất bản | |
| `Keyword` | Từ khóa | |
| `Subject` | Chủ đề | Alias của `Keyword` |
| `ISBN` | Số ISBN | |
| `OtherTitle` | Nhan đề khác | Chỉ hoạt động với `systax = "Internal"` |

---

## Ví dụ TypeScript

```typescript
const BASE_URL = 'https://localhost:7001';
const token = localStorage.getItem('access_token');

const authHeaders = {
  'Content-Type': 'application/json',
  'Authorization': `Bearer ${token}`,
};

// SearchBrief — kiểm tra nhanh tất cả thư viện
async function searchBrief(
  libraryIds: string[],
  fields: { field: string; value: string }[],
  operator: 'AND' | 'OR' = 'AND'
) {
  const res = await fetch(`${BASE_URL}/api/PrintBook/SearchZ3950/SearchBrief`, {
    method: 'POST',
    headers: authHeaders,
    body: JSON.stringify({ fields, operator, libraryIds }),
  });
  return res.json();
}

// SearchDetail — lấy records của một hoặc nhiều thư viện
async function searchDetail(
  libraryIds: string[],
  fields: { field: string; value: string }[],
  operator: 'AND' | 'OR' = 'AND',
  pageIndex = 1,
  pageSize = 10
) {
  const res = await fetch(`${BASE_URL}/api/PrintBook/SearchZ3950/SearchDetail`, {
    method: 'POST',
    headers: authHeaders,
    body: JSON.stringify({ fields, operator, libraryIds, pageIndex, pageSize }),
  });
  return res.json();
}
```

### Workflow gợi ý: Brief trước, Detail sau

```typescript
async function federatedSearch(query: string) {
  const allLibraryIds = ['lib-guid-1', 'lib-guid-2', 'lib-guid-3'];
  const fields = [{ field: 'Title', value: query }];

  // Bước 1: Kiểm tra song song tất cả thư viện (nhanh)
  const brief = await searchBrief(allLibraryIds, fields);

  // Bước 2: Lọc ra thư viện có kết quả
  const connectedLibs = brief.data.filter(r => r.connected && r.count > 0);

  if (connectedLibs.length === 0) return [];

  // Bước 3: Lấy detail chỉ từ thư viện có kết quả
  const connectedIds = connectedLibs.map(r => r.libraryId);
  const detail = await searchDetail(connectedIds, fields, 'AND', 1, 10);

  return detail.data;
}
```

---

## Xử lý lỗi

Mỗi thư viện trả về kết quả độc lập — một thư viện lỗi không ảnh hưởng đến thư viện khác.

```typescript
brief.data.forEach(lib => {
  if (!lib.connected) {
    // Lỗi mạng, timeout, sai credentials, hoặc Systax không hợp lệ
    console.warn(`[${lib.libraryName}] ${lib.error}`);
  } else if (lib.count === 0) {
    // Kết nối OK nhưng không có biểu ghi thỏa mãn
    console.info(`[${lib.libraryName}] Không có kết quả`);
  }
});
```

| Trường hợp | `connected` | `count` / `error` |
|---|---|---|
| Kết nối OK, có kết quả | `true` | `count > 0` |
| Kết nối OK, không có kết quả | `true` | `count = 0` |
| Timeout / lỗi mạng | `false` | `error`: mô tả lỗi |
| Sai credentials | `false` | `error`: mô tả lỗi |
| `Systax` không hợp lệ | `false` | `error: "Unknown Systax: ..."` |

---

## Lưu ý

- Các thư viện trong `libraryIds[]` được tìm **song song** ở server — thời gian chờ bằng thư viện chậm nhất (timeout mặc định: Z3950 BER = 10 giây, SRU = 15 giây)
- `pageIndex` và `pageSize` áp dụng **riêng cho từng thư viện** — không phải tổng hợp toàn cục
- Để phân trang kết quả tổng hợp nhiều thư viện, frontend cần tự merge và sort danh sách records
- `publicId` trong records chỉ có giá trị khi `systax = "Internal"` — có thể dùng để link đến trang chi tiết Ebook
