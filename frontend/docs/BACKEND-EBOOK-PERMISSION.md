# Backend — Phân quyền đọc/tải tài liệu số (OPAC)

Tài liệu này mô tả các thay đổi backend cần để Frontend OPAC kiểm tra quyền đọc/tải
tài liệu số. Quy ước hiện có trên `EbookItem`:

- `Free`: `2` = miễn phí (ai cũng đọc được, không cần đăng nhập); `1` hoặc `null` = cần đăng nhập (bạn đọc/Reader).
- `AllowDownload`: `2` = cho phép tải file.

Đối tượng đăng nhập ở OPAC là **bạn đọc (Reader)** — JWT trả từ `PublicReader/Login`, claim `Type = "Reader"`.

---

## 0. Đã có sẵn — không cần thay đổi

Response `GetEbookDetail` **đã** trả `isFree` và `allowDownload`:

```json
{ "allowDownload": 2, "isFree": 2, ... }
```

→ FE lấy 2 cờ này trực tiếp từ detail (gate nút Đọc/Tải, truyền sang reader). **Không** cần thêm field DTO,
**không** cần endpoint `CheckFilePermission` riêng. Quyết định cho đọc/tải nằm ở FE; backend chỉ enforce
khi thực sự phục vụ bytes file (mục dưới).

---

## 1. (Tùy chọn) Cho `GetFileMinio` / `GetFilePdf` chấp nhận token qua query string

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
| # | Việc | Bắt buộc | File backend |
|---|------|----------|--------------|
| 0 | `isFree` + `allowDownload` đã có trong detail | — (đã xong) | — |
| 1 | Nhận token qua query `?token=` cho `GetFileMinio`/`GetFilePdf` | Chỉ khi cần đọc tài liệu **không** miễn phí | `PublicEbookController.cs` / cấu hình JwtBearer |

> Tài liệu miễn phí (`isFree=2`) hoạt động hoàn toàn với backend hiện tại — không cần thay đổi gì.
