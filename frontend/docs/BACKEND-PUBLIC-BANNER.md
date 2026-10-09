# Hướng dẫn backend — API Banner công khai (ảnh nền trang chủ OPAC)

OPAC dùng banner để làm **ảnh nền hero** (section tìm kiếm đầu trang chủ). Frontend gọi endpoint công khai lấy danh sách banner theo `tenantId`, dùng **phần tử đầu tiên** làm ảnh nền; nếu rỗng/lỗi → fallback ảnh tĩnh `assets/images/banner.png`.

## Endpoint

| Thuộc tính | Giá trị |
|---|---|
| Method/URL | `POST /api/public/PublicBanner/SearchAll` |
| Auth | **AllowAnonymous** (không cần đăng nhập) |
| Content-Type | `application/json` |

> Đặt cùng nhóm `/api/public/...` như các API công khai khác (`PublicHyperLink/SearchAll`, `PublicNews`, `PublicZ3950Config/Search`). FE (browser) gọi qua proxy `/api/public`; SSR gọi `${BackendBase}/api/public`.

### Request
```json
{ "tenantId": "04503B89-CD40-F111-8A6B-0050569D1DDC" }
```
| Tham số | Kiểu | Mô tả |
|---|---|---|
| `tenantId` | Guid (string) | Đơn vị/thư viện cần lấy banner |

### Response
```json
{
  "success": true,
  "data": [
    {
      "url": "http://.../media/2026/06/banner-home.png",
      "link": "https://thuvien.example.gov.vn",
      "name": "Banner trang chủ",
      "sortOrder": 1,
      "status": 2
    }
  ]
}
```

| Trường | Kiểu | Mô tả |
|---|---|---|
| `url` | string | **URL ảnh tuyệt đối** — FE dùng thẳng cho `background-image`, KHÔNG ghép tiền tố |
| `link` | string? | Link khi bấm banner (FE hero chưa dùng, để dành) |
| `name` | string? | Tên banner |
| `sortOrder` | int? | Thứ tự hiển thị |
| `status` | int | 2 = hiển thị |

## Yêu cầu xử lý ở backend
- Lọc theo `tenantId`.
- Chỉ trả banner **đang hiển thị**: `status = 2` và **chưa xoá** (`isDelete != 2`).
- **Sắp xếp theo `sortOrder` tăng dần** — FE lấy `data[0]` làm ảnh nền hero.
- `url` trả về phải là **đường dẫn tuyệt đối** tới ảnh (giống ảnh bìa/ảnh tin tức công khai). Nếu DB lưu đường dẫn tương đối, backend tự ghép base media URL trước khi trả.
- Có thể tái dùng đúng logic của `PublicHyperLink/SearchAll` (cùng pattern lọc tenant + status).

## Tích hợp phía Frontend (đã làm)
- [system-api.service.ts](../src/app/opac/services/system-api.service.ts) → `getBanners()` POST `PublicBanner/SearchAll` `{ tenantId }`, `catchError → []`, cache `shareReplay(1)`.
- [home.ts](../src/app/opac/pages/home/home.ts) → `bannerUrl` (mặc định `assets/images/banner.png`); khi có banner đầu có `url` thì dùng URL đó; `heroBgStyle` ghép overlay tối + ảnh.
- [home.html](../src/app/opac/pages/home/home.html) → hero dùng `[style.background-image]="heroBgStyle()"`.

> Khi endpoint chưa sẵn sàng, FE tự fallback ảnh tĩnh — trang chủ vẫn hoạt động bình thường.
