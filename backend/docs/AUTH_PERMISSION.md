# Xác thực & Phân quyền

## Tổng quan luồng

```
1. Client gọi POST /api/Auth/Login  →  Nhận JWT token
2. Client gửi token trong header: Authorization: Bearer {token}
3. JWT Middleware xác thực token  →  Gán ClaimsPrincipal vào HttpContext.User
4. [PermissionAttribute] đọc UserId từ Claim; quyết định từ claim "perm" nếu "pstamp" còn khớp, không thì tra cms.Permission
5. Nếu có quyền → vào Controller; nếu không → 403 Forbidden
```

---

## JWT Token

### Cấu hình (`appsettings.json`)

```json
{
  "Jwt": {
    "Key": "ELIBAPI_JWT_SECRET_KEY_CHANGE_THIS_IN_PRODUCTION_MIN32CHARS",
    "Issuer": "ELIBAPI",
    "Audience": "ELIBAPI_CLIENTS",
    "ExpireMinutes": 60
  }
}
```

- Key phải tối thiểu **32 ký tự** (HMAC-SHA256)
- Token hiệu lực **8 tiếng**, `ClockSkew = TimeSpan.Zero` (không có grace period)

### Claims trong token

```
ClaimTypes.NameIdentifier  →  UserId (long)
ClaimTypes.Name            →  LoginName
"FullName"                 →  Họ tên đầy đủ
"PublicId"                 →  Users.PublicId
"TenantId" / "TenantCode"  →  Đơn vị của user (không có = tài khoản hệ thống)
"RoleCode"                 →  Mã role
"perm"                     →  Kết quả phân quyền lúc đăng nhập: "*" (role toàn quyền) hoặc "MODCODE:view,add;MODCODE2:view"
"pstamp"                   →  Users.PermissionStamp lúc đăng nhập
"ro"                       →  "1" nếu tài khoản bị chặn ghi (không đơn vị, đơn vị/role chỉ-xem)
JwtRegisteredClaimNames.Jti → GUID (unique per token)
```

#### Claim quyền và PermissionStamp (Đợt 2, port ELIB-LRC 09-27)

- `AuthService.BuildLoginResponseAsync` nhúng `perm`/`pstamp`/`ro`. User cũ chưa có `PermissionStamp` thì được sinh ở lần đăng nhập đó.
- `PermissionAttribute`/`PermissionAnyAttribute` gọi `PermissionClaimEvaluator.TryEvaluateAsync` trước: 1 query theo khoá chính lấy `Users.PermissionStamp`; khớp `pstamp` thì quyết định từ claim, **bất kỳ trường hợp không chắc** (thiếu claim, stamp lệch, mã quyền không có trong claim, lỗi) đều trả `null` và rơi về `HasPermissionAsync` như trước.
- Claim bỏ qua mã quyền có nhiều dòng `cms.Module` hoặc nhiều dòng `cms.Permission` (kết quả DB không xác định) — các mã đó luôn đi đường DB.
- Stamp đổi khi: lưu quyền của user (`UsersRepository.SavePermissionsAsync`), đổi role của user. Sửa/xoá/ẩn module hoặc sửa/xoá role (`[InvalidatePermissionStamps]` trên `ModuleController`/`RolesController`) đặt stamp mọi user về `null` → mọi token đang lưu hành đi đường DB tới lần đăng nhập sau.
- Cột `Users.PermissionStamp`: Postgres tự thêm khi khởi động (`Program.cs`); SQL Server chạy `backend/scripts/add-user-permission-stamp-sqlserver.sql`.
- Tra quyền hộ user khác (AdminWork, AdminTask…) vẫn gọi thẳng `HasPermissionAsync`.

#### Frontend

Sidebar và `*appCan` tra theo **mã quyền**: bảng route → mã ở `frontend/src/app/services/system/admin-perm-codes.ts`; quyền của user lấy từ `Users/MyPermission` (chỉ cần đăng nhập). Route chưa khai trong bảng vẫn khớp theo `cms.Module.Link` (trả kèm trong `MyPermission`). `*appCan="'edit'; code: 'READERS'"` (`appCanCode`) và `perm` của mục menu chỉ định thẳng mã.

### Lấy UserId trong controller

```csharp
// Dùng helper từ BaseApiController
long userId = GetCurrentUserId();

// Hoặc thủ công
var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
long userId = long.Parse(claim!);
```

---

## Mật khẩu

`AuthService` dùng **MD5** để hash mật khẩu trước khi so sánh với database:

```csharp
private static string HashPassword(string password)
{
    using var md5 = MD5.Create();
    var bytes = Encoding.UTF8.GetBytes(password);
    var hash = md5.ComputeHash(bytes);
    return Convert.ToHexString(hash).ToLower();
    // Ví dụ: "admin123" → "0192023a7bbd73250516f069df18b500"
}
```

**Nếu hệ thống cũ dùng cách hash khác** (SHA256, bcrypt, plain text...), sửa phương thức `HashPassword` trong `ELIBAPI.Infrastructure/Services/AuthService.cs`.

---

## PermissionAttribute

### Cách hoạt động

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class PermissionAttribute : Attribute, IAsyncAuthorizationFilter
```

1. Đọc `UserId` từ `ClaimTypes.NameIdentifier`
2. Thử `PermissionClaimEvaluator.TryEvaluateAsync` (xem mục claim quyền ở trên); `null` thì gọi `IPermissionService.HasPermissionAsync(userId, moduleCode, action)`
3. `PermissionService` tra bảng `cms.Module` với `WHERE ModuleCode = ? AND IsDelete != 2` → lấy `ModuleId`
4. Tra bảng `cms.Permission` với `WHERE UserId = ? AND ModuleId = ? AND IsDelete != 2`
5. So sánh cột tương ứng theo `action`:

| action | Cột kiểm tra |
|--------|-------------|
| `"add"` | `Can_Add = 2` |
| `"edit"` | `Can_Edit = 2` |
| `"delete"` | `Can_Delete = 2` |
| `"view"` | `Can_View = 2` |
| `"access"` | `Can_Access = 2` |

### Cách sử dụng

```csharp
// Gắn trực tiếp trên action method — dùng ModuleCode (string) từ cms.Module.ModuleCode
[HttpPost("Add")]
[Permission("NEWS", "add")]
public new async Task<IActionResult> Add([FromBody] NewsRequest request) => await base.Add(request);
```

### ModuleCode

**Không dùng `ModuleIds` hardcoded constant nữa.** Dùng trực tiếp chuỗi `ModuleCode` từ cột `cms.Module.ModuleCode`.

```
// Ví dụ các ModuleCode thông dụng (lấy từ cms.Module.ModuleCode trong database):
"ATTACH_FILE"   → cms.AttachFile
"CATEGORY"      → cms.Category
"MENU"          → cms.Menu
"MENU_TYPE"     → cms.MenuType
"MODULE"        → cms.Module
"MODULE_ROLES"  → cms.ModuleRoles
"NEWS"          → cms.news
"PHOTO"         → cms.Photo
"PHOTO_ALBUM"   → cms.PhotoAlbum
```

**Quan trọng**: Giá trị `ModuleCode` phải khớp chính xác với cột `ModuleCode` trong bảng `cms.Module`. Nếu chưa có, thêm vào database trước khi dùng.

---

## Response khi không có quyền

Message trả về theo ngôn ngữ client (`Accept-Language` header). Base language là **tiếng Anh**, dùng `IStringLocalizer` để chuyển sang tiếng Việt.

### Chưa đăng nhập (401)
```json
// Accept-Language: en (default)
{ "success": false, "message": "You are not logged in or your token has expired.", "statusCode": 401 }

// Accept-Language: vi-VN
{ "success": false, "message": "Bạn chưa đăng nhập hoặc token đã hết hạn.", "statusCode": 401 }
```

### Không có quyền (403)
```json
// Accept-Language: en (default)
{ "success": false, "message": "You do not have permission to perform this action.", "statusCode": 403 }

// Accept-Language: vi-VN
{ "success": false, "message": "Bạn không có quyền thực hiện thao tác này.", "statusCode": 403 }
```

Cả hai được cấu hình trong `Program.cs` qua `JwtBearerEvents.OnChallenge` và `OnForbidden`, có inject `IStringLocalizer`.

---

## Endpoint không cần đăng nhập

Chỉ có `AuthController` có `[AllowAnonymous]`:

```csharp
[AllowAnonymous]
public class AuthController : ControllerBase
{
    [HttpPost("Login")] // POST /api/Auth/Login — public
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
```

Tất cả controllers khác kế thừa `BaseApiController` có `[Authorize]` ở class level.

---

## Ví dụ sử dụng API từ client

```javascript
// 1. Đăng nhập
const login = await fetch('/api/Auth/Login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ loginName: 'admin', password: 'admin123' })
});
const { data: { token } } = await login.json();

// 2. Gọi API với token
const news = await fetch('/api/News/Search', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${token}`
  },
  body: JSON.stringify({ pageIndex: 1, pageSize: 10 })
});
```
