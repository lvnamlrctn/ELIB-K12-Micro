# Hướng dẫn backend — Module "Theo dõi đọc tài liệu"

Module admin theo dõi việc **đọc tài liệu số** (EbookItem) của bạn đọc. Gồm 2 tab:
- **Thống kê**: biểu đồ lượt đọc theo kỳ (ngày/tháng) + 3 chỉ số tổng.
- **Chi tiết**: danh sách từng lượt đọc (bạn đọc, tài liệu, thời điểm, thời lượng) có lọc + phân trang.

Frontend đã sẵn sàng ([reading-tracking.service.ts](../src/app/services/ebook/reading-tracking.service.ts), [reading-tracking.ts](../src/app/pages/admin/ebook/reading-tracking.ts)); khi backend chưa có endpoint, FE hiển thị **dữ liệu mẫu**. Tài liệu này mô tả 2 endpoint cần xây.

## Nguồn dữ liệu
Cần bảng log mỗi lượt đọc tài liệu số, tối thiểu: `ReaderId`, `ItemId` (EbookItem), `ReadAt` (datetime), `Duration` (phút), `TenantId`. Ghi log khi bạn đọc mở/đọc tài liệu ở OPAC.

## Auth & scope
- `Authorization: Bearer <token>` (admin).
- Scope theo **tenant của user** đăng nhập (đa đơn vị).

## 1) Thống kê — `POST /api/Ebook/ReadingTracking/Statistics`
**Request:**
```json
{ "fromDate": "2026-01-01", "toDate": "2026-06-30", "groupBy": "month" }
```
| Tham số | Mô tả |
|---|---|
| `fromDate`/`toDate` | Khoảng thời gian (null = tất cả) |
| `groupBy` | `"day"` hoặc `"month"` — gom nhóm trục thời gian |

**Response:**
```json
{
  "success": true,
  "data": {
    "labels": ["T1","T2","T3","T4","T5","T6"],
    "counts": [120, 210, 180, 260, 320, 410],
    "totalReads": 1500,
    "totalDocuments": 84,
    "totalReaders": 53
  }
}
```
| Trường | Mô tả |
|---|---|
| `labels` | Nhãn kỳ theo `groupBy` |
| `counts` | Số lượt đọc mỗi kỳ (cùng độ dài `labels`) |
| `totalReads` | Tổng lượt đọc trong khoảng |
| `totalDocuments` | Số tài liệu **phân biệt** được đọc |
| `totalReaders` | Số bạn đọc **phân biệt** |

## 2) Chi tiết — `POST /api/Ebook/ReadingTracking/Search`
**Request:**
```json
{ "keyword": "", "fromDate": null, "toDate": null, "pageIndex": 1, "pageSize": 10 }
```
| Tham số | Mô tả |
|---|---|
| `keyword` | Tìm theo tên bạn đọc hoặc tên tài liệu |
| `fromDate`/`toDate` | Lọc thời gian |
| `pageIndex` | Trang (bắt đầu từ 1) |
| `pageSize` | Số dòng/trang |

**Response:**
```json
{
  "success": true,
  "data": {
    "items": [
      { "readerName": "Nguyễn Văn A", "itemTitle": "Lập trình C# nâng cao", "readAt": "2026-06-20T09:15:00", "duration": 35 }
    ],
    "recordsTotal": 152
  }
}
```
| Trường | Mô tả |
|---|---|
| `items[].readerName` | Tên bạn đọc |
| `items[].itemTitle` | Tên tài liệu |
| `items[].readAt` | Thời điểm đọc (ISO datetime) |
| `items[].duration` | Thời lượng đọc (phút) |
| `recordsTotal` | Tổng số bản ghi (để phân trang) |

## Lưu ý
- `labels`/`counts` phải cùng độ dài.
- Biểu đồ là đường (line) màu xanh lá (emerald), giống "Lượt xem tài liệu số" ở Dashboard.
- FE gửi `pageIndex` bắt đầu từ 1.
- Khi backend sẵn sàng, FE tự gọi thật (bỏ dữ liệu mẫu nhờ `catchError`).
