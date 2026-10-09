# Hướng dẫn backend — Dữ liệu Dashboard admin

Trang Dashboard admin ([dashboard.ts](../src/app/pages/admin/dashboard.ts) + [dashboard service](../src/app/services/dashboard.ts)) hiện đang dùng **dữ liệu mock**. Tài liệu này đặc tả dữ liệu backend cần cung cấp để Dashboard hiển thị số liệu thật, gồm 4 thẻ thống kê + 3 biểu đồ (mượn/lượt khách, tỷ lệ sách, **lượt xem tài liệu số**).

## Endpoint đề xuất
Gộp 1 endpoint trả toàn bộ dữ liệu dashboard (đơn giản cho FE):

```
GET /api/Cms/Dashboard/Summary
```
- **Auth**: `Authorization: Bearer <token>` (như các API admin khác).
- **Response**: `{ "success": true, "data": <DashboardData> }` (FE đọc `res.data`).

> FE hiện gọi `DashboardService.getDashboardData()` trả mock. Khi có endpoint, chỉ cần đổi service sang `http.get('/api/Cms/Dashboard/Summary').pipe(map(r => r.data))` — shape giữ nguyên như dưới.

## Cấu trúc `DashboardData`

```json
{
  "stats": {
    "printBooks": 8240,
    "digitalBooks": 3150,
    "readers": 1580,
    "borrowed": 1245
  },
  "chartData": {
    "labels":   ["T1","T2","T3","T4","T5","T6"],
    "borrowed": [450, 600, 850, 700, 950, 1100],
    "visitors": [1200, 1500, 2200, 1800, 2500, 2800]
  },
  "pieData": {
    "labels": ["Sách in", "Sách số"],
    "data":   [8240, 3150]
  },
  "documentViewsData": {
    "labels": ["T1","T2","T3","T4","T5","T6"],
    "views":  [320, 480, 650, 520, 810, 920]
  }
}
```

## Giải thích từng phần

> **CẬP NHẬT (hiện trạng FE):** Dashboard đang **tạm ẩn** phần tài liệu in. FE chỉ hiển thị: thẻ **`stats.digitalBooks`**, **`stats.readers`** và biểu đồ **`documentViewsData`**. Các trường `stats.printBooks`, `stats.borrowed`, `chartData`, `pieData` hiện **KHÔNG hiển thị** (FE bọc sau cờ `showPrintInfo=false`) → backend **chưa cần trả cũng được**; vẫn để trong contract để bật lại sau (đổi cờ `true`).

| Trường | Ý nghĩa | Gợi ý nguồn dữ liệu backend | Trạng thái FE |
|---|---|---|---|
| `stats.digitalBooks` | Tổng số tài liệu số (EbookItem) | đếm EbookItem | **Đang hiển thị** |
| `stats.readers` | Tổng số độc giả | đếm Reader | **Đang hiển thị** |
| **`documentViewsData.labels`** | Nhãn kỳ | 6–12 kỳ gần nhất (tháng/tuần) | **Đang hiển thị** |
| **`documentViewsData.views`** | **Lượt xem tài liệu số theo từng kỳ** | tổng lượt xem EbookItem theo kỳ (vd cộng `totalView` theo tháng) | **Đang hiển thị** |
| `stats.printBooks` | Tổng số sách in | đếm bản ghi kho sách in | Đã ẩn (tùy chọn) |
| `stats.borrowed` | Số lượt đang mượn | đếm phiếu mượn chưa trả | Đã ẩn (tùy chọn) |
| `chartData.*` | Mượn/Lượt khách theo kỳ | thống kê mượn/visit theo kỳ | Đã ẩn (tùy chọn) |
| `pieData.*` | Tỷ lệ Sách in vs Sách số | 2 giá trị tương ứng tổng số | Đã ẩn (tùy chọn) |

## Lưu ý
- Mọi mảng trong cùng một biểu đồ phải **cùng độ dài** với `labels` tương ứng.
- Ưu tiên backend trả đúng **`documentViewsData`** (+ `digitalBooks`, `readers`) vì đó là phần đang hiển thị.
- Nếu backend tách nhiều endpoint thay vì 1 endpoint gộp, FE có thể `forkJoin` — nhưng khuyến nghị 1 endpoint `Summary` để giảm round-trip.
- Biểu đồ "Lượt xem tài liệu số" là biểu đồ đường (line), màu xanh lá; chỉ cần backend trả đúng `documentViewsData`.
