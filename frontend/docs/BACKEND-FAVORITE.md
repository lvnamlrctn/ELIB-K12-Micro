# Hướng dẫn backend — Tài liệu yêu thích (lưu theo bạn đọc)

OPAC cho bạn đọc **lưu/bỏ lưu tài liệu số** và xem lại ở trang cá nhân. Trước đây FE lưu ở localStorage; nay chuyển sang **lưu DB theo từng bạn đọc**. Tài liệu này mô tả 3 endpoint công khai mà FE đã gọi ([favorite-api.service.ts](../src/app/opac/services/favorite-api.service.ts)).

## Ngữ cảnh & bảo mật
- Bạn đọc đăng nhập qua `POST /api/public/PublicReader/Login`; FE lưu `user.id = publicId` của bạn đọc → dùng làm **`readerId`**.
  - ⚠️ **BẮT BUỘC: `Login` phải trả `data.publicId`** (GUID bạn đọc). FE map `user.id = data.publicId` (fallback `readerPublicId`/`id`). Nếu response thiếu cả các trường này, `readerId` rỗng và chức năng favorite/review **không hoạt động** (FE chặn không gửi giá trị rác, ghi `console.warn`).
- OPAC **chưa có JWT bạn đọc** ⇒ các endpoint là **AllowAnonymous**, scope theo `readerId` + `tenantId` truyền trong body. (Khuyến nghị về sau gắn token bạn đọc để bảo vệ.)
- `tenantId` lấy theo đơn vị (đa tenant) — FE gửi `APP_CONFIG.TenantId` (đã resolve theo host).

## Bảng dữ liệu (đề xuất)
`EbookFavorite ( Id, ReaderId, ItemId, TenantId, CreatedDate )` — **unique theo (ReaderId, ItemId)** để không lưu trùng.

## 1) Danh sách yêu thích — `POST /api/public/Ebook/EbookFavorite/Search`
**Request:** `{ "readerId": "<GUID bạn đọc>", "tenantId": "<GUID đơn vị>" }`

**Response:** trả kèm **thông tin hiển thị** để FE render trực tiếp (không phải gọi lại chi tiết):
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "publicId": "94a9af93-1a68-f111-8a71-0050569d1ddc",
        "title": "Lập trình C# nâng cao",
        "author": "Nguyễn Văn A",
        "publisher": "NXB ...",
        "publishDate": "2023",
        "images": "http://.../media/.../cover.png",
        "totalView": 12,
        "totalDownload": 3
      }
    ]
  }
}
```
> FE map `publicId→id`, `images→imageUrl`, `publishDate→year`. Có thể trả mảng phẳng `data: [...]` cũng được (FE chấp nhận cả hai).

## 2) Lưu — `POST /api/public/Ebook/EbookFavorite/Add`
**Request:** `{ "readerId": "...", "itemId": "<publicId tài liệu>", "tenantId": "..." }`
**Response:** `{ "success": true }` — **idempotent** (đã tồn tại thì bỏ qua, vẫn `success`).

## 3) Bỏ lưu — `POST /api/public/Ebook/EbookFavorite/Remove`
**Request:** `{ "readerId": "...", "itemId": "...", "tenantId": "..." }`
**Response:** `{ "success": true }` — idempotent (không có cũng `success`).

## Tích hợp FE (đã làm)
- [favorite-api.service.ts](../src/app/opac/services/favorite-api.service.ts): `getFavorites/addFavorite/removeFavorite` (catchError an toàn).
- [auth.service.ts](../src/app/opac/services/auth.service.ts): `loadSavedBooks` gọi `getFavorites` khi đăng nhập/khôi phục phiên; `toggleSaveBook` cập nhật lạc quan + gọi Add/Remove, lỗi thì revert. Bỏ hẳn localStorage `saved_books_*`.
- Dùng ở [book-detail](../src/app/opac/pages/book-detail/book-detail.html) (nút Lưu/Bỏ lưu) và [profile](../src/app/opac/pages/profile/profile.html) (danh sách đã lưu).

## Checklist nghiệm thu
1. Bạn đọc A lưu tài liệu X → bản ghi `(A, X)` trong DB; đăng nhập lại vẫn thấy.
2. Bạn đọc B không thấy tài liệu của A (scope theo `readerId`).
3. `Search` trả đủ trường hiển thị; `Add`/`Remove` idempotent.
4. Lọc đúng theo `tenantId`.
