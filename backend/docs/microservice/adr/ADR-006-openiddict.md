# ADR-006 — OpenIddict, token RS256 + JWKS

- **Trạng thái:** Chấp nhận · 2026-10-07

## Bối cảnh
Monolith dùng JWT HS256 với khoá đối xứng. Nếu áp dụng cho nhiều service, mọi service xác minh token sẽ phải giữ khoá có khả năng **ký** token. Token còn được nhận qua query string. Hệ thống cần hỗ trợ bạn đọc đăng nhập qua LDAP/API ngoài theo đơn vị, và về lâu dài là SSO cấp Sở/Bộ.

## Quyết định
- `identity` dùng OpenIddict (mã nguồn mở, chạy trong .NET) làm OIDC server.
- Token ký RS256, phân phối khoá công khai qua JWKS.
- Access token 15 phút, refresh token xoay vòng.
- Authorization Code + PKCE cho Admin/OPAC; Client Credentials cho thiết bị và service.
- Bỏ token qua query string, thay bằng ticket một lần cho SSE/tải file.

## Hệ quả
- (+) Service chỉ cần khoá công khai. Xoay khoá không phải deploy lại service.
- (+) Chuẩn OIDC giúp mở đường cho federation sau này.
- (−) Luồng đăng nhập SPA phức tạp hơn password grant. Thư viện OIDC phía Angular xử lý phần này.
- (−) Phải tự vận hành OpenIddict (không có bản managed). Đổi lại không tốn phí license như Duende.
