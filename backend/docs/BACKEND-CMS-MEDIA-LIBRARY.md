# Hướng dẫn backend — Thư viện ảnh CKEditor (module Tin tức)

Trong CKEditor của module Tin tức (`/admin/news`), ngoài nút **upload ảnh mới** (đã có, gọi `POST /api/Cms/News/UploadImage`) FE vừa thêm nút **"Chọn từ thư viện"** — mở modal liệt kê các ảnh **đã upload trước đó**, lưu trên MinIO (bucket `public`), để chọn lại thay vì upload trùng. Tài liệu này mô tả 1 endpoint mới FE cần backend triển khai ([media-library.service.ts](../src/app/services/cms/media-library.service.ts)).

## Ngữ cảnh
- Đây **không phải** tích hợp CKFinder — đã cân nhắc và quyết định không dùng CKFinder chính hãng vì nó đòi hỏi mua license + backend cài đặt "connector" theo đúng giao thức riêng của CKFinder (không tương thích với việc chỉ liệt kê file MinIO tự viết). Thay vào đó FE tự xây UI picker, chỉ cần 1 API list-file thông thường.
- FE resolve `path` tương đối trả về từ mọi API media (`UploadImage` lẫn `Media/List`) theo **cùng 1 quy tắc**: nếu `path` đã là URL tuyệt đối (`http(s)://...`) thì giữ nguyên; nếu là path tương đối (vd `"2026/07/xxx.png"`) thì ghép thành `<origin>/api/public/media/<path>` — xem [media-url.ts](../src/app/shared/utils/media-url.ts). **Endpoint mới phải trả `path` theo đúng định dạng này** để nhất quán và tránh lặp lại lỗi mixed-content đã gặp trước đây (khi backend từng trả URL tuyệt đối trỏ thẳng vào host nội bộ).
- Yêu cầu Bearer token như mọi endpoint `/api/Cms/**` khác (đã có `AuthInterceptor` tự đính kèm).

## Endpoint cần làm — `GET /api/Cms/Media/List`

**Query params:**
| Tên | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `keyword` | string | không | lọc theo tên file (chứa, không phân biệt hoa/thường) |
| `page` | number | có | bắt đầu từ `1` |
| `pageSize` | number | có | FE hiện gửi `24` |

**Nguồn dữ liệu:** liệt kê object trong bucket `public` của MinIO, **chỉ ảnh** (lọc theo phần mở rộng `.jpg/.jpeg/.png/.gif/.webp`), sắp xếp mới nhất trước.

**Response mẫu:**
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "path": "2026/07/42f69d07e3ee44289ab292264470a3eb.png",
        "fileName": "42f69d07e3ee44289ab292264470a3eb.png",
        "size": 251272,
        "uploadedDate": "2026-07-10T12:00:00Z"
      }
    ],
    "totalCount": 42
  }
}
```
> FE chấp nhận cả `data.items` lẫn mảng phẳng `data: [...]` hoặc `items`/`total` ở gốc (xem `MediaLibraryService.list()` — parse khoan dung theo nhiều dạng response giống các service khác trong repo).

**Trường hợp API chưa tồn tại / lỗi:** FE tự bắt lỗi (`catchError`) và hiển thị trạng thái "Chưa có ảnh nào trong thư viện" — không crash trang, nên có thể deploy FE trước khi backend xong endpoint này.

## Tích hợp FE (đã làm)
- [media-library.service.ts](../src/app/services/cms/media-library.service.ts): gọi `GET /api/Cms/Media/List`.
- [media-library-picker.ts](../src/app/shared/components/media-library-picker/media-library-picker.ts) + `.html`: modal lưới thumbnail, tìm kiếm theo tên, phân trang Trước/Sau.
- [news.ts](../src/app/pages/admin/cms/news.ts): thêm nút toolbar "Chọn từ thư viện" trong CKEditor (cạnh nút upload ảnh) — mở picker, khi chọn ảnh sẽ `editor.execute('insertImage', { source: url })` chèn vào đúng vị trí con trỏ của đúng ô đang thao tác (tóm tắt hoặc nội dung).

## Checklist nghiệm thu
1. Upload 1 ảnh mới qua `UploadImage` → gọi `Media/List` (không keyword) trang 1 → thấy ảnh vừa upload xuất hiện đầu danh sách.
2. `keyword` lọc đúng theo tên file (vd nhập 1 phần tên file đã biết).
3. Phân trang đúng theo `totalCount` (tổng số ảnh trong bucket `public`).
4. `path` trả về đúng định dạng tương đối `<yyyy>/<mm>/<filename>` giống response của `UploadImage` — không trả URL tuyệt đối trỏ vào host nội bộ MinIO.
