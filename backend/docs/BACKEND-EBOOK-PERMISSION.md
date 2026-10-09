# Backend — Phân quyền đọc/tải tài liệu số (OPAC)

Tài liệu này mô tả các thay đổi backend cần để Frontend OPAC kiểm tra quyền đọc/tải
tài liệu số. Quy ước hiện có trên `EbookItem`:

- `Free`: `2` = miễn phí (ai cũng đọc được, không cần đăng nhập); `1` hoặc `null` = cần đăng nhập (bạn đọc/Reader).
- `AllowDownload`: `2` = cho phép tải file.

Đối tượng đăng nhập ở OPAC là **bạn đọc (Reader)** — JWT trả từ `PublicReader/Login`, claim `Type = "Reader"`.

---

## 1. Trả `Free` trong `PublicEbookResponse`

FE cần biết tài liệu có miễn phí không để hiển thị nút "Đọc" hay dòng "Yêu cầu đăng nhập để đọc tài liệu".
Hiện `PublicEbookResponse` mới có `AllowDownload`, **thiếu `Free`**.

`ELIBAPI.Core/DTOs/Response/PublicEbookResponse.cs`:

```csharp
public int? AllowDownload { get; set; }
public int? Free { get; set; }   // THÊM: 2 = miễn phí; 1/null = cần đăng nhập
```

Đảm bảo repository (`GetEbookByPublicIdAsync`, `GetLatestEbooksAsync`, `Search`...) map `EbookItem.Free` sang DTO.

---

## 2. Endpoint mới: kiểm tra quyền theo file

`GET /api/public/PublicEbook/CheckFilePermission/{fileId}`

- **Không** gắn `[Authorize]` (cho phép gọi cả khi chưa đăng nhập, để biết tài liệu miễn phí).
- Đọc token tùy chọn từ header `Authorization: Bearer <jwt>` (FE gửi khi bạn đọc đã đăng nhập).
- Dựa trên `GetEbookFileWithItemAsync(fileId)` để lấy `EbookItem` tương ứng.

Logic:

```csharp
[HttpGet("CheckFilePermission/{fileId}")]
public async Task<IActionResult> CheckFilePermission(Guid fileId)
{
    var (file, book) = await repo.GetEbookFileWithItemAsync(fileId);
    if (file == null || book == null)
        return NotFound(ApiResponse<object>.Fail("Không tìm thấy tài liệu"));

    bool isFree  = book.Free == 2;
    bool allowed = isFree || User.Identity!.IsAuthenticated; // miễn phí HOẶC đã đăng nhập

    return Ok(ApiResponse<object>.Ok(new {
        allowed,
        allowDownload = book.AllowDownload == 2,
        free          = book.Free
    }));
}
```

Response mẫu:

```json
{
  "success": true,
  "data": { "allowed": true, "allowDownload": true, "free": 2 },
  "statusCode": 200
}
```

FE chỉ nạp file khi `allowed = true`; hiện nút "Tải xuống" khi `allowDownload = true`.

---

## 3. Cho `GetFileMinio` / `GetFilePdf` chấp nhận token qua query string

Trình đọc PDF (dFlip/PDF.js) và thẻ `<audio>/<video>/<iframe>` **không thể gắn header `Authorization`**.
Vì vậy với tài liệu không miễn phí, FE truyền token qua query: `?token=<jwt>`.

Backend cần: nếu request **chưa** được xác thực qua header, thử đọc & validate JWT từ query `token`
**trước khi** kiểm tra `Free`. Có thể làm bằng middleware nhỏ hoặc ngay đầu action:

```csharp
// Trước check Free trong GetFileMinio / GetFilePdf:
if (!User.Identity!.IsAuthenticated)
{
    var qToken = Request.Query["token"].FirstOrDefault();
    if (!string.IsNullOrEmpty(qToken))
    {
        var principal = jwt.ValidateToken(qToken); // dùng cùng TokenValidationParameters với Reader
        if (principal != null) HttpContext.User = principal;
    }
}

// Giữ nguyên check sẵn có:
if ((book.Free == 1 || book.Free == null) && !User.Identity!.IsAuthenticated)
    return Unauthorized(ApiResponse<string>.Fail(loc["Unauthorized"]));
```

> Gợi ý cấu hình `JwtBearerEvents.OnMessageReceived` để tự lấy token từ query cho 2 route file này,
> tránh lặp code. Lưu ý chỉ áp dụng cho route file public để hạn chế lộ token qua URL.

---

## Tóm tắt thay đổi
| # | Việc | File backend |
|---|------|--------------|
| 1 | Thêm `Free` vào DTO + map | `PublicEbookResponse.cs` + repository |
| 2 | Endpoint `CheckFilePermission/{fileId}` | `PublicEbookController.cs` |
| 3 | Nhận token qua query cho file endpoint | `PublicEbookController.cs` / cấu hình JwtBearer |
