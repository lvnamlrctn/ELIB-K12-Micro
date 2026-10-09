# ADR-007 — Bản sao cục bộ thay vì gọi đồng bộ trong nghiệp vụ lõi

- **Trạng thái:** Chấp nhận · 2026-10-07

## Bối cảnh
Mượn/trả cần dữ liệu bạn đọc (patron), bản sách (holdings) và nhan đề (catalog). Nếu gọi đồng bộ sang 2–3 service thì mượn/trả sẽ chậm và sập theo bất kỳ service nào. Điều này trái mục tiêu cô lập lỗi.

## Quyết định
- Service lõi giữ **bản sao chỉ đọc** của dữ liệu tham chiếu: `PatronReplica`, `ItemReplica`, `BibSnapshot`, `TenantReplica`. Bản sao được cập nhật bằng event mang đủ trạng thái và có `SourceVersion`.
- Trạng thái "đang mượn" do `circulation` sở hữu.
- Gọi đồng bộ (gRPC) chỉ cho danh sách hẹp ở [03 §4](../03-giao-tiep.md#4-truy-vấn-đồng-bộ-grpc-nội-bộ), tối đa 1 hop cho mỗi request.

## Hệ quả
- (+) Mượn/trả chỉ phụ thuộc DB của `circulation`, nhanh và chịu lỗi.
- (−) Nhất quán cuối (eventual consistency): bạn đọc vừa bị khoá có thể còn mượn được trong vài giây. Chấp nhận được về nghiệp vụ.
- (−) Dữ liệu cá nhân bị nhân bản. Chỉ sao field cần thiết và xoá theo `ReaderDeleted`.
- (−) Cần đối soát và công cụ dựng lại bản sao.
