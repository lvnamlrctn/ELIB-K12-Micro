# Hướng dẫn backend — Quản lý bình luận tài liệu (admin EbookReview)

Màn admin **Quản lý bình luận tài liệu** ([/admin/ebook-review]) liệt kê bình luận/đánh giá bạn đọc gửi từ OPAC và cho phép **Duyệt / Bỏ duyệt / Xóa**. FE đã đấu nối ([ebook-review.service.ts](../src/app/services/ebook/ebook-review.service.ts)) và **fallback dữ liệu mock** khi backend chưa có — chỉ cần dựng đúng 3 endpoint dưới là chạy số thật, không phải sửa FE.

> Văn phong & response giống các API admin khác: `{ "success": true, "data": ... }`. Auth: `Authorization: Bearer <token>` (admin), scope theo **tenant của user** đăng nhập.

## Bối cảnh
- OPAC gửi review qua `POST /api/public/Ebook/EbookReview/Add` → tạo bản ghi **`status = 1` (chờ duyệt)**.
- OPAC hiển thị công khai qua `SearchAll` chỉ trả **`status = 2` (đã duyệt)**.
- Quy ước trạng thái: **`1` = chờ duyệt**, **`2` = đã duyệt**.

## 1) Danh sách (phân trang) — `POST /api/Ebook/EbookReview/Search`
**Request:**
```json
{ "keyword": "", "status": null, "itemId": null, "pageIndex": 1, "pageSize": 10 }
```
- `keyword`: lọc theo người gửi (`displayName`) / nội dung / **tên tài liệu**.
- `status`: `null` = tất cả; `1` = chờ duyệt; `2` = đã duyệt.
- `itemId`: (tùy chọn) lọc theo 1 tài liệu (publicId).

**Response:** mỗi item kèm **`itemTitle`** (tên tài liệu) để FE hiển thị trực tiếp:
```json
{
  "success": true,
  "data": {
    "items": [
      { "id": 1, "itemId": "<publicId tài liệu>", "itemTitle": "Lập trình C# nâng cao",
        "displayName": "Nguyễn Văn A", "rating": 5,
        "content": "Tài liệu rất hữu ích.", "status": 1, "createdRowDate": "2026-06-20T09:15:00" }
    ],
    "recordsTotal": 42
  }
}
```
> FE chấp nhận `data.items` hoặc mảng phẳng `data: [...]`; tổng số đọc `recordsTotal` hoặc `totalCount`. Nhận cả PascalCase lẫn camelCase.

## 2) Duyệt / Bỏ duyệt — `PUT /api/Ebook/EbookReview/ChangeStatus`
**Request:** `{ "id": <id review>, "status": 2 }` (duyệt) hoặc `{ "id": ..., "status": 1 }` (bỏ duyệt).
**Response:** `{ "success": true }`.

## 3) Xóa — `DELETE /api/Ebook/EbookReview/Delete/{id}`
**Response:** `{ "success": true }`.

## Checklist nghiệm thu
1. `Search` trả đúng phân trang + `itemTitle`; lọc `status`/`keyword` hoạt động; scope theo tenant.
2. `ChangeStatus` đổi `1↔2`; sau khi duyệt (`2`), review xuất hiện ở OPAC (`SearchAll`); bỏ duyệt thì biến mất.
3. `Delete` xóa bản ghi; OPAC không còn hiển thị.
4. Backend chưa sẵn → FE vẫn render bằng mock, không vỡ.
