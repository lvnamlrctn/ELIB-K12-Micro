# Hướng dẫn backend — Dữ liệu Dashboard admin

Trang Dashboard admin ([dashboard.ts](../src/app/pages/admin/dashboard.ts) + [dashboard service](../src/app/services/dashboard.ts)) hiển thị 4 thẻ thống kê + 1 biểu đồ xu hướng mượn sách + 1 danh sách phân bố theo loại tài liệu + 1 danh sách Top 5 tài liệu mượn nhiều nhất.

> **TRẠNG THÁI FE:** `DashboardService` đã đấu nối `GET /api/Cms/Dashboard/Summary` (đọc `res.data`, chuẩn hóa về `DashboardData`), và **fallback dữ liệu mock** khi endpoint lỗi/không truy cập được. Backend đã triển khai đầy đủ (`ELIBAPI.API/Controllers/Cms/DashboardController.cs`), không còn field nào bị ẩn/hardcode `0`.

## Endpoint

```
GET /api/Cms/Dashboard/Summary
```
- **Auth**: `Authorization: Bearer <token>` (như các API admin khác), quyền `[Permission("DASHBOARD","view")]`.
- **Response**: `{ "success": true, "data": <DashboardData> }` (FE đọc `res.data`).
- **Tenant scoping**: theo user hiện tại (`TenantId` claim); nếu `RoleCode`/`TenantCode` khớp
  `ReadOnlyPolicy:AdminRoleCodes`/`ReadOnlyPolicy:TenantCodes` trong config thì bỏ lọc tenant (xem
  toàn hệ thống).

## Cấu trúc `DashboardData`

```json
{
  "stats": {
    "totalDocs": 8626,
    "digitalBooks": 3426,
    "printTitles": 5200,
    "printBooksInStock": 8838,
    "digitizationRate": 40
  },
  "borrowTrend": {
    "labels": ["Th9","Th10","Th11","Th12","Th1","Th2","Th3","Th4","Th5","Th6","Th7","Th8"],
    "data":   [40, 45, 60, 55, 130, 65, 70, 75, 80, 95, 65, 731]
  },
  "typeDistribution": [
    { "label": "Hoạt cảnh", "count": 57, "percentage": 2 },
    { "label": "Technical Report", "count": 3, "percentage": 0 }
  ],
  "top5Borrowed": [
    { "bibId": 1234, "title": "...", "author": "...", "ddc": "349.597", "count": 174 }
  ]
}
```

## Giải thích từng phần / công thức

| Trường | Ý nghĩa | Nguồn dữ liệu / công thức |
|---|---|---|
| `stats.digitalBooks` | Tổng số tài liệu số | `COUNT(Ebook.Item WHERE Status==2 && IsDelete!=2 [&& tenant])` |
| `stats.printTitles` | Số đầu tài liệu in (biểu ghi) | `COUNT(PrintBook.Bib WHERE IsDelete!=2 [&& tenant])` — title-level, không lọc `Status` (khác nghĩa MARC status, không phải published/active) |
| `stats.printBooksInStock` | Số bản sách in đang lưu kho | `COUNT(PrintBook.Barcode WHERE Status=="R" && IsDelete!=2 [&& tenant])` — copy-level, mã `"R"` = "Trong kho sẵn sàng cho mượn" theo `PrintBook.Barcode_Status` (khác `"I"` = trong kho nhưng chưa sẵn sàng — không tính) |
| `stats.totalDocs` | Tổng học liệu | `digitalBooks + printTitles` (title-level cả 2 vế — **không** cộng `printBooksInStock` vì đó là số bản, không phải số đầu tài liệu) |
| `stats.digitizationRate` | Tỷ lệ số hóa (%) | `round(digitalBooks * 100 / totalDocs)`, `0` nếu `totalDocs == 0` |
| `borrowTrend.labels` / `.data` | Xu hướng mượn sách in 12 tháng gần nhất | group `PrintBook.BookOut` (`IsDelete!=2 [&& tenant]`) theo `BorrowDate` năm/tháng, cửa sổ trượt 12 tháng, nhãn `"Th{tháng}"` |
| `typeDistribution` | Phân bố học liệu số theo loại (`Ebook.DigType`) | group `Ebook.Item` (digital, cùng filter `digitalBooks`) theo `TypeId`, tên lấy từ `DigType.DescriptionVn`, sắp giảm dần theo count (không giới hạn số dòng — FE tự cuộn), `percentage = round(count*100/digitalBooks)` |
| `top5Borrowed` | Top 5 tài liệu (sách in) được mượn nhiều nhất, tích lũy toàn thời gian | `BookOut` join `Barcode` (theo `Barcode == BarcodeValue`) join `BibXml` (theo `BibId`) lấy `Title`/`Author`/`DDC`, group theo `BibId`, đếm, sắp giảm dần, lấy 5 dòng đầu — không lọc theo khoảng ngày |

## Lưu ý
- `borrowTrend.labels`/`borrowTrend.data` phải cùng độ dài (12 phần tử).
- `typeDistribution`/`top5Borrowed` đã sắp giảm dần theo `count` từ backend — FE dùng phần tử đầu mảng
  làm mốc `max` để tính độ rộng thanh progress tương đối (`item.count / list[0].count * 100`), không
  dùng trực tiếp `percentage` cho độ rộng thanh (để mục nhỏ vẫn còn thấy được).
- `top5Borrowed` chỉ gồm sách in (nguồn từ `BookOut`/`Barcode`/`BibXml`), FE gắn tag tĩnh "SÁCH IN" cho
  mọi dòng, không cần backend trả thêm field loại tài liệu.
