# ADR-010 — Tách OPAC/Admin trong Nx workspace

- **Trạng thái:** Chấp nhận · 2026-10-07

## Bối cảnh
App Angular hiện tại gộp OPAC (cần SSR, public, tải lớn) và Admin (nội bộ, không cần SSR). Vì vậy không scale và deploy riêng được, và bundle admin bị kéo vào server SSR. Micro-frontend (Module Federation) đã được cân nhắc nhưng quá phức tạp so với nhu cầu.

## Quyết định
- Hai app trong một Nx workspace: `opac` (Angular SSR) và `admin` (SPA tĩnh).
- Lib dùng chung (`shared/ui`, `shared/auth`, `shared/tenant`, `api/<service>` sinh từ OpenAPI). Feature lib lazy-load theo gói bán.
- Ranh giới phụ thuộc được ép bằng Nx lint.

## Hệ quả
- (+) OPAC scale riêng. Admin deploy dạng tĩnh, rẻ.
- (+) Tái dùng code hiện có. Build theo `nx affected`.
- (−) Admin vẫn là một bundle deploy chung cho mọi gói. Chấp nhận được vì lazy route và license đã ẩn phần chưa mua. Có thể nâng cấp lên Module Federation sau nếu cần deploy feature độc lập.
