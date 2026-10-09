# ADR-005 — YARP gateway, kiểm tra license module tại gateway

- **Trạng thái:** Chấp nhận · 2026-10-07

## Bối cảnh
Cần một điểm vào duy nhất để làm các việc: xác định tenant theo subdomain, xác thực, rate limit, và **chặn module chưa mua**. Một số màn hình OPAC (MyLibrary) cần ghép dữ liệu từ nhiều service.

## Quyết định
- Gateway viết bằng YARP (.NET).
- Route được nạp động từ ConfigMap do Helm chart của từng service khai báo (path, module, auth).
- Gateway kiểm license qua cache Redis và trả 403 `MODULE_NOT_LICENSED`.
- BFF composition chỉ dùng cho một số màn hình OPAC, có trả về từng phần khi một service lỗi.
- Service vẫn tự xác thực token và kiểm `[RequiresModule]` cho consumer/job.

## Hệ quả
- (+) Bán theo module được thực thi ở mức kỹ thuật, không chỉ ẩn menu.
- (+) Cùng stack .NET, dễ viết logic tuỳ biến (tenant, license, ký header).
- (−) Gateway là điểm nghẽn tiềm năng. Giảm thiểu bằng HPA, canary, và giữ gateway không chứa nghiệp vụ.
