# 05 — Bảo mật

## 1. Identity service (OpenIddict)

| Hạng mục | Hiện tại (monolith) | Hệ mới |
|---|---|---|
| Phát token | JWT HS256 tự ký, khoá đối xứng dùng chung | **OpenIddict** trong `identity`, ký **RS256**; public key công bố qua `/.well-known/jwks`; xoay khoá 90 ngày (giữ khoá cũ để xác minh) |
| Thời hạn | `Jwt:ExpireMinutes` dài | Access token **15 phút**; refresh token **xoay vòng** (rotation + phát hiện tái sử dụng), 14 ngày cho nhân viên, 30 ngày cho bạn đọc |
| Truyền token | Header + `?token=` trong query | **Chỉ dùng header** `Authorization`. SSE và tải file dùng **ticket một lần** (`POST /api/.../ticket` → `?t=` hiệu lực 60s, một lần dùng) |
| Thu hồi | Đổi `PermissionStamp` | Blacklist `jti` trong Redis khi đăng xuất, khoá tài khoản hoặc đổi mật khẩu; thu hồi refresh token trong DB |
| Mật khẩu | BCrypt | Giữ BCrypt (cost 12); chính sách độ mạnh, khoá tạm sau N lần sai, captcha sau 3 lần sai (giữ captcha hiện có) |
| 2FA | OTP cho admin | Giữ OTP (email/SMS qua `notification`); bổ sung TOTP tuỳ chọn |

### Luồng (grant type)

- **Nhân viên (Admin SPA):** Authorization Code + PKCE, với trang đăng nhập do `identity` phục vụ. Không dùng password grant, để sau này có thể tích hợp SSO của Sở/Bộ (OIDC/SAML federation) mà không phải sửa SPA.
- **Bạn đọc (OPAC):** Authorization Code + PKCE. Trang đăng nhập hỗ trợ ba nguồn xác thực, cấu hình **theo đơn vị** (chuyển từ `READER_AUTH_CONFIG` + `ExternalReaderAuth.cs`):
  1. **Nội bộ:** số thẻ hoặc email + mật khẩu.
  2. **LDAP của đơn vị:** bind và đối chiếu thuộc tính sang số thẻ.
  3. **API ngoài của đơn vị**, ví dụ cổng SSO của trường.

  Endpoint "kiểm tra cấu hình" (`ReaderAuthTest`) giữ nguyên chức năng. Khi tra số thẻ, chỉ tìm trong đơn vị xác định theo host, đúng như bản sửa ở đợt 5d.
- **Kiosk, máy SIP2, thiết bị ra vào:** Client Credentials, mỗi thiết bị một client gắn `tenant_id` và scope hẹp (ví dụ `circulation.selfcheck`, `space.access`). Cấp và thu hồi trong Admin.
- **Service-to-service:** Client Credentials ([§4](#4-service-to-service)).

### Claim trong access token

```
sub, sub_type (staff|reader|device|service), tenant_id, tenant_scope (tuỳ chọn, cấp Sở),
roles[], pstamp (permission stamp), modules[] (license tại thời điểm phát — chỉ dùng cho UI; gateway vẫn kiểm theo cache license mới nhất),
scope, jti, exp
```

Danh sách quyền chi tiết **không** nằm trong token, để token nhỏ và đổi quyền có hiệu lực ngay ([§2](#2-phân-quyền)).

## 2. Phân quyền

Giữ nguyên ngữ nghĩa của `[Permission("MODULE","action")]` (xem `backend/docs/AUTH_PERMISSION.md`) để việc port controller có tính cơ học.

- **Nguồn sự thật:** `identity` (Modules, Roles, Permissions, quyền theo user/nhóm).
- Building block `Elib.BuildingBlocks.Authorization` cung cấp `[Permission]` / `[PermissionAny]` và `IPermissionChecker`. Thứ tự kiểm tra quyền:
  1. L1 cache trong process.
  2. Redis `perm:{userId}:{pstamp}`.
  3. gRPC `identity.Permissions.GetEffective` (khi cache miss).
- `pstamp` đổi khi quyền của user, vai trò hoặc module thay đổi. `identity` phát `PermissionChanged`, nên token cũ mang stamp cũ sẽ buộc tải lại quyền.
- **Quyền theo dữ liệu** vẫn do service nghiệp vụ kiểm tra:
  - **Kho / điểm lưu thông được phép thao tác** (hiện là `PbPrivate`/`PbRoles`): `identity` lưu phân công; `circulation`/`holdings` kiểm tra qua claim `assignments` hoặc cache.
  - **Bộ sưu tập số được phép** (`CollectionPermistionUser`): ở lại `digital`.
- Quyền bạn đọc (OPAC) dựa trên `sub_type=reader` + chính sách nghiệp vụ, như chính sách lưu thông và chính sách số. Không dùng ma trận quyền.

## 3. Bảo vệ ứng dụng

- **Input:**
  - FluentValidation cho mọi command.
  - Giới hạn kích thước upload theo loại; kiểm tra MIME và magic bytes ở `media`.
  - Quét virus (ClamAV sidecar) cho tài liệu bạn đọc nộp (`DocumentSubmission`).
- **SSRF:** mọi URL do người dùng cấu hình (LDAP, API ngoài, webhook, Z39.50) chỉ được gọi qua `HttpClient` có allowlist và chặn dải IP nội bộ.
- **Webhook thanh toán:**
  - Xác minh chữ ký VNPAY (HMAC SHA512) và Sepay (API key/HMAC).
  - Idempotent theo mã giao dịch.
  - Đối chiếu số tiền với giao dịch đang chờ.
  - Ghi log nguyên văn request để đối soát.
- **Rate limit:** tại gateway, và thêm trong `identity` (đăng nhập, OTP) và `ai` (theo tenant/bạn đọc).
- **Header bảo mật** (HSTS, CSP cho OPAC/Admin, `X-Content-Type-Options`) cấu hình ở ingress.
- **Dữ liệu cá nhân (học sinh là trẻ vị thành niên):**
  - Mã hoá cột nhạy cảm (số CCCD, điện thoại) bằng Data Protection API, khoá trong secret store.
  - Ảnh khuôn mặt chỉ lưu ở bucket private.
  - Gửi ảnh sang Gemini phải được đơn vị **bật rõ ràng** trong cấu hình và có ghi chú đồng ý.
  - Log không ghi PII (Serilog/OTel processor che field).

## 4. Service-to-service

- **Mạng:** Kubernetes **NetworkPolicy** mặc định deny. Mỗi service chỉ nhận traffic từ gateway và từ các service được liệt kê trong chart. RabbitMQ, Redis và Postgres chỉ nhận từ namespace ứng dụng.
- **Cấu hình client OIDC:** dạng dictionary theo ClientId (`Identity:Clients:svc-gateway:ClientSecret` ↔ biến môi trường `Identity__Clients__svc-gateway__ClientSecret`), không dùng mảng — mảng gộp cấu hình theo chỉ số, dễ ghi đè nhầm secret.
- **Danh tính:**
  - Mỗi service là một OpenIddict client (Client Credentials, scope theo gRPC service được phép gọi).
  - Lời gọi nhân danh người dùng thì chuyển tiếp token gốc (token exchange đơn giản: giữ `sub` và `tenant_id`, thêm `act=service`).
- **mTLS (tuỳ chọn, profile SaaS):** Linkerd tự cấp chứng chỉ giữa pod. Không bắt buộc ở profile tỉnh để giảm vận hành.
- **Header nội bộ** (`X-Tenant-Id` cho route anonymous) được gateway ký HMAC (`X-Gw-Signature`). Service từ chối nếu chữ ký sai.

## 5. Secret

- **Lưu trữ:** External Secrets Operator, nguồn là Vault hoặc secret store của cloud. Profile tỉnh dùng Sealed Secrets. **Không** để secret trong `appsettings*.json` hay repo.
- **Dọn hiện trạng (khi dựng hệ mới):**
  - Các file `.env.*` và `appsettings.json` có secret thật trong repo monolith → không mang sang repo mới, và đổi toàn bộ credential.
  - AES key/IV **hard-code** dùng chung giữa `AesEncryptionHelper.cs` và `ebook-indexer` → hệ mới dùng Data Protection / KMS, mỗi service một khoá, xoay được.
- **Khoá ký JWT** (RSA) nằm trong secret và xoay theo lịch. Khoá công khai được JWKS phân phối tự động.

## 6. Audit

- Mọi thao tác ghi quan trọng phát `AuditRecorded` (actor, tenant, entity, hành động, diff rút gọn, IP, correlationId). `audit` lưu bản ghi append-only, có partition theo tháng và retention theo cấu hình đơn vị.
- Các việc sau **bắt buộc** ghi audit: đăng nhập thất bại, "đóng vai đơn vị", thay đổi quyền, thay đổi license, hoàn/huỷ phí.
