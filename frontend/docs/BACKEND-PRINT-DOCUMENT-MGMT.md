# Hướng dẫn backend — Quản lý tài liệu in ấn: Số KCB & Xếp giá (Phase 1)

Tài liệu này mô tả các endpoint mới cần backend triển khai để hoàn thiện "xương sống" Số KCB (đăng ký cá biệt) cho tài liệu in, theo đúng quy trình mô tả trong `docs/09.Tai-lieu-cam-nang-su-dung.pdf` (mục II.3, II.6, IV.1). FE đã build sẵn UI gọi các endpoint dưới — khi backend triển khai xong, chức năng hoạt động ngay không cần đổi FE.

## Bối cảnh

- **Phiếu nhập** (`AbReceipt`, bảng `PrintBook.Catalogue.Receipt`) chứa nhiều **dòng phiếu nhập** (`AbReceiptLine`) — mỗi dòng ứng với 1 biểu ghi (`bibId`) với số lượng bản (`amount`) cần nhập.
- Mỗi dòng, sau khi biên mục xong, cần **đăng ký số KCB** (đăng ký cá biệt / accession number) cho từng bản — số lượng số KCB đăng ký cho 1 dòng có thể ít hơn `amount` (đăng ký dần).
- Sau khi có số KCB, tài liệu ở trạng thái **"chưa sẵn sàng"** (chưa xếp giá) — cần qua bước **Xếp giá** mới chuyển sang **"sẵn sàng cho mượn"**.

## 1) Đăng ký KCB theo lô

| | |
|---|---|
| Method/URL | `POST /api/PrintBook/Catalogue/Receipt/RegisterBarcodes` |
| Mô tả | Sinh và lưu một dải số KCB liên tiếp cho 1 dòng phiếu nhập, gán vào 1 kho lưu giữ. |

**Request:**
```json
{
  "receiptLineId": 123,
  "prefix": "VN",
  "digitLength": 6,
  "quantity": 5,
  "storeId": 4
}
```
| Trường | Mô tả |
|---|---|
| `receiptLineId` | ID dòng phiếu nhập (`AbReceiptLine.id`) cần đăng ký KCB |
| `prefix` | Tiền tố chuỗi số KCB (có thể rỗng) |
| `digitLength` | Độ dài phần số (đệm số 0 phía trước), vd `digitLength=6` → `000001` |
| `quantity` | Số lượng số KCB cần sinh (sinh liên tiếp từ số hiện có lớn nhất + 1, theo tiền tố) |
| `storeId` | Kho lưu giữ tài liệu (bảng `PrintBook.Store`) |

**Response:** trả về danh sách số KCB vừa đăng ký (dùng lại ngay để hiển thị):
```json
{
  "success": true,
  "data": [
    { "id": 501, "barcode": "VN000001", "storeId": 4 },
    { "id": 502, "barcode": "VN000002", "storeId": 4 }
  ]
}
```

**Quy tắc nghiệp vụ:**
- Sau khi đăng ký, mỗi số KCB tạo 1 bản ghi bản cá biệt (item) gắn với `bibId` của dòng, trạng thái mặc định **"chưa sẵn sàng"** (chưa xếp giá).
- `AbReceiptLine.registerCount` (số đã đăng ký) cần tăng theo số lượng vừa đăng ký — trả về trong response của `GetLines`/`Lines/{receiptId}` để FE hiển thị.
- Không giới hạn cứng `registerCount ≤ amount` ở tầng validate bắt buộc (thư viện đôi khi đăng ký dư), nhưng nên cảnh báo (không chặn) nếu vượt.

## 2) Lấy danh sách số KCB đã đăng ký cho 1 dòng

| | |
|---|---|
| Method/URL | `GET /api/PrintBook/Catalogue/Receipt/Barcodes/{receiptLineId}` |
| Response | `{ "data": [ { "id": 501, "barcode": "VN000001", "storeId": 4 }, ... ] }` |

(Endpoint `GET .../Barcodes/{receiptId}/{bibId}` cũ — nếu đã tồn tại — đổi tên tham số đường dẫn thành `{receiptLineId}` để khớp đúng 1 dòng cụ thể, tránh nhầm giữa nhiều dòng cùng `bibId` trong sách bộ/tập.)

## 3) Xóa dòng phiếu nhập (có ràng buộc)

| | |
|---|---|
| Method/URL | `DELETE /api/PrintBook/Catalogue/Receipt/DeleteLine/{lineId}` |

**Quy tắc nghiệp vụ (cẩm nang mục II.6):** từ chối xóa (HTTP 400) nếu dòng còn số KCB đã đăng ký. Trả về message rõ ràng để FE hiển thị trực tiếp cho người dùng:
```json
{ "success": false, "message": "Không thể xóa vì biểu ghi còn số KCB. Vui lòng xóa số KCB trước." }
```
FE đã code sẵn: hiển thị `err.error.message` nếu có, fallback về text cố định tương tự nếu backend không trả `message`.

## 4) Xếp giá

### 4.1 Tìm bản cá biệt chưa xếp giá

| | |
|---|---|
| Method/URL | `POST /api/PrintBook/Store/SearchUnshelved` |
| Body | `{ "receiptCode": "", "barcodeFrom": null, "barcodeTo": null, "storeId": null, "pageIndex": 1, "pageSize": 10 }` |
| Response | `{ "data": [ { "id": 501, "barcode": "VN000001", "bibTitle": "…", "receiptCode": "PN2026-001", "storeId": 4, "storeName": "Kho A" } ], "recordsTotal": N }` |

Lọc theo: mã đơn nhận (`receiptCode`, chứa/khớp), khoảng số KCB (`barcodeFrom`/`barcodeTo`, so sánh chuỗi hoặc phần số tùy thiết kế lưu trữ), kho (`storeId`). **Chỉ trả về bản cá biệt đang ở trạng thái "chưa sẵn sàng"** (chưa xếp giá) — đây là điều kiện lọc mặc định, không cần FE gửi thêm flag trạng thái.

### 4.2 Xếp giá (đánh dấu sẵn sàng)

| | |
|---|---|
| Method/URL | `POST /api/PrintBook/Store/Shelve` |
| Body | `{ "barcodeIds": [501, 502, 503] }` |
| Response | `{ "success": true }` |

Chuyển tất cả bản cá biệt trong `barcodeIds` sang trạng thái **"sẵn sàng cho mượn"**.

## Checklist nghiệm thu (Phase 1)
1. `RegisterBarcodes` sinh đúng số lượng, đúng định dạng (tiền tố + đệm số 0), không trùng với số đã tồn tại.
2. `Barcodes/{receiptLineId}` trả đúng danh sách vừa đăng ký; `registerCount` ở `Lines/{receiptId}` phản ánh đúng.
3. `DeleteLine` từ chối đúng khi còn số KCB, xóa được khi dòng không còn số KCB nào.
4. `SearchUnshelved` chỉ trả bản cá biệt "chưa sẵn sàng", lọc đúng theo mã đơn nhận/khoảng KCB/kho.
5. `Shelve` chuyển đúng các ID được chọn sang "sẵn sàng cho mượn"; sau khi xếp giá, bản ghi không còn xuất hiện lại trong `SearchUnshelved`.

## Phase 2 — Hoàn thiện Bổ sung: biên mục MARC ở đơn nhận, báo cáo bổ sung

### 5) `Catalogue/Book/Save` — bổ sung yêu cầu response (endpoint đã tồn tại)
FE giờ gọi endpoint này ngay khi "Thêm sách lẻ" ở đơn nhận (trước khi tạo dòng phiếu nhập), để lấy `bibId` gắn vào dòng. Yêu cầu **response phải trả kèm** (không chỉ `mfn`):
```json
{ "success": true, "data": { "mfn": 1024, "bibId": 501, "title": "…", "author": "…", "publisher": "…" } }
```
Nếu hiện tại response chỉ có `mfn`, cần bổ sung `bibId`/`title`/`author`/`publisher` (trích từ MARC 245/100/260) để FE không phải gọi lại `GetByMfn`.

### 6) `Catalogue/Receipt/SaveDetail` — đổi cấu trúc dòng phiếu nhập
| | |
|---|---|
| Method/URL | `POST /api/PrintBook/Catalogue/Receipt/SaveDetail` |

**Request (mới):**
```json
{ "id": 0, "receiptId": 10, "bibId": 501, "title": "…", "author": "…", "publisher": "…", "amount": 3, "currency": "VND", "price": 150000 }
```
`bibId` là bắt buộc (tạo trước qua `Book/Save`, xem mục 5). `title`/`author`/`publisher` do FE gửi kèm (copy từ response `Book/Save`) để không phải join lại khi hiển thị danh sách — nhưng backend nên ưu tiên nguồn `Bib` làm chính, các trường này chỉ là convenience cache.
`GET /Lines/{receiptId}` cần trả thêm `price` trong mỗi dòng.

### 7) `Catalogue/Order/SaveDetail` — chỉ thêm `price`
Đơn đặt **không đổi cấu trúc** (vẫn nhận `title`/`author`/`publisher` tự do, không có `bibId`, không tạo `Bib`) — chỉ bổ sung nhận/trả thêm trường `price` (đơn giá dự kiến):
```json
{ "id": 0, "orderId": 7, "title": "…", "author": "…", "publisher": "…", "amount": 5, "currency": "VND", "price": 120000 }
```
`GET /Lines/{orderId}` trả thêm `price`.

### 8) Báo cáo bổ sung (4 báo cáo dạng bảng)
| | |
|---|---|
| Method/URL | `POST /api/Acquisition/Report/{Action}` với `Action` ∈ `AcquisitionList`, `StoreAllocation`, `AccessionRegister`, `NewBookCatalog` |
| Export | `POST /api/Acquisition/Report/{Action}Export` → trả file Excel (blob) |

**Request chung:**
```json
{ "fromDate": "2026-01-01", "toDate": "2026-12-31", "storeId": null, "supplierId": null, "pageIndex": 1, "pageSize": 20 }
```

**Response chung:**
```json
{ "success": true, "data": { "items": [ { /* tuỳ loại báo cáo, xem dưới */ } ], "totalCount": 42 } }
```

Nghiệp vụ từng loại (cẩm nang mục II):
- **AcquisitionList** (Danh sách tài liệu bổ sung): liệt kê các dòng `Receipt/AbReceiptLine` trong khoảng ngày, có `receiptCode`, `title`, `author`, `amount`, `price`.
- **StoreAllocation** (Danh sách phân bổ kho): tổng hợp số lượng bản đã nhập theo từng kho (`storeName`, `title`, `amount` — số lượng gộp theo kho).
- **AccessionRegister** (Sổ ĐKCB/KTQ): danh sách số KCB đã đăng ký (join bảng barcode/item với `Bib` qua `bibId`) — `barcode`, `title`, `isbn` (trích từ MARC 020$a), `storeName`.
- **NewBookCatalog** (Thư mục sách mới): danh sách biểu ghi (`Bib`) mới tạo trong khoảng ngày — `title`, `author`, `isbn`, `publishDate`.

### 9) Tìm dữ liệu in nhãn môn loại
| | |
|---|---|
| Method/URL | `POST /api/PrintBook/Catalogue/ClassLabel/Search` |
| Request | `{ "receiptCode": "PN2026-001", "barcodeFrom": null, "barcodeTo": null }` |
| Response | `{ "data": [ { "id": 501, "barcode": "VN000001", "classSymbol": "610.7", "authorMark": "NG-527", "title": "…" } ] }` |

Lọc theo mã đơn nhận hoặc khoảng số KCB; `classSymbol` lấy từ `DicClass` gắn với `Bib` (qua `bibId`), `authorMark` là ký hiệu tác giả (Cutter) nếu hệ thống có lưu, nếu chưa có thể để trống.

## Checklist nghiệm thu (Phase 2)
1. `Book/Save` trả `bibId` + `title`/`author`/`publisher` ngay trong response.
2. `Receipt/SaveDetail` tạo dòng đúng với `bibId` liên kết tới biểu ghi MARC vừa tạo; `Lines/{receiptId}` trả đúng `price`.
3. `Order/SaveDetail`/`Lines/{orderId}` nhận/trả đúng `price`, các trường khác không đổi hành vi.
4. 4 report action trả đúng dữ liệu theo bộ lọc ngày/kho/NCC; export trả file Excel hợp lệ (không rỗng khi có dữ liệu).
5. `ClassLabel/Search` trả đúng danh sách theo mã đơn nhận hoặc khoảng KCB, có `classSymbol` để in nhãn.

## Phase 3 — Hoàn thiện Biên mục in: Tạo chỉ mục, Bộ sưu tập tài liệu in

### 10) Tạo lại chỉ mục tìm kiếm
| | |
|---|---|
| Method/URL | `POST /api/PrintBook/Catalogue/Book/RebuildIndex` |
| Request | không tham số (body rỗng `{}`) |
| Response | `{ "success": true }` |

Rebuild chỉ mục tìm kiếm (full-text/search index) cho toàn bộ biểu ghi in hiện có. Không cần trả dữ liệu, chỉ cần `success` để FE hiện thông báo. Nếu quá trình chạy lâu, có thể chạy bất đồng bộ (background job) và trả `success: true` ngay khi nhận yêu cầu — FE hiện tại không chờ polling kết quả.

### 11) `Book/Save` / `Book/Search` / `Book/GetByMfn` — bổ sung `collectionId`
Cả 3 endpoint (đã tồn tại) cần nhận/trả thêm trường `collectionId` (number, optional — liên kết tới `PrintCollection.id`):
```json
{ "mfn": 1024, "bibId": 501, "bibTypeId": 2, "collectionId": 7, "fields": [ /* MARC */ ] }
```

### 12) CRUD cây "Bộ sưu tập tài liệu in" (PrintCollection)
Copy đúng contract của `api/Ebook/EBookCollection` đã có (dùng cho tài liệu số), đổi base path, bỏ các trường SEO (`link`, `pageTitle`, `metaDescription`, `keyword`, `language`) vì không áp dụng cho tài liệu in vật lý.

| | |
|---|---|
| Base | `api/PrintBook/Catalogue/PrintCollection` |
| `POST /GetTree` | body `{ keyword, status: 0, pageIndex: 0, pageSize: 0, parentId: 0, level: 0 }` → `{ "data": [ { "id": 1, "publicId": "...", "parentId": null, "name": "Sách văn học", "status": 2, "order": 0, "description": "...", "children": [...] } ] }` |
| `GET /GetById/{publicId}` | → `{ "data": { ...1 node } }` |
| `POST /Add` | body `{ "Name": "...", "ParentId": 0, "Status": 2, "Order": 0, "Description": "..." }` (0 = ẩn, 2 = hoạt động — giữ đúng quy ước `status` như `EBookCollection`) |
| `PUT /Update/{publicId}` | body giống `/Add` |
| `DELETE /Delete/{publicId}` | xóa 1 node (cân nhắc chặn nếu còn node con hoặc còn `Bib` gắn `collectionId`, tuỳ nghiệp vụ) |

## Checklist nghiệm thu (Phase 3)
1. `Book/RebuildIndex` trả `success: true`, không lỗi 500 khi gọi lặp lại nhiều lần.
2. `Book/Save`/`Book/Search`/`Book/GetByMfn` nhận/trả đúng `collectionId`.
3. `PrintCollection` CRUD tree hoạt động đúng — thêm node gốc, thêm node con, sửa, xóa, cây trả về đúng cấu trúc `children` lồng nhau.
4. Sau khi gán `collectionId` cho 1 `Bib` và xóa `PrintCollection` tương ứng, hành vi phía backend nhất quán (không để `Bib` trỏ tới `collectionId` không tồn tại, hoặc set về `null`).

## Phase 4 — Hoàn thiện Lưu thông: Trả ngược

### 13) `Circulation/Loan/Return` — hỗ trợ "Trả ngược" (chỉ gửi `barcode`)
| | |
|---|---|
| Method/URL | `POST /api/PrintBook/Circulation/Loan/Return` (endpoint đã tồn tại, dùng cho cả "Trả xuôi" lẫn "Trả ngược") |

Endpoint này đã nhận `{ borrowId?, barcode?, cardNo? }` — tất cả đều optional. Cẩm nang mục V định nghĩa 2 luồng trả:
- **Trả xuôi**: FE gửi `{ borrowId, barcode, cardNo }` (đã có bạn đọc/lượt mượn cụ thể) — **hành vi giữ nguyên như hiện tại**.
- **Trả ngược**: FE gửi **chỉ** `{ barcode }` (không `borrowId`, không `cardNo`). Backend cần: tự tra bản ghi mượn (`Loan`) đang ở trạng thái "đang mượn" theo `barcode`/số KCB, thực hiện trả (chuyển trạng thái tài liệu về "sẵn sàng cho mượn"), và trả về response kèm thông tin để FE hiện xác nhận:
```json
{ "success": true, "data": { "readerName": "Nguyễn Văn A", "bibTitle": "…", "cardNo": "TV000123" } }
```
Nếu không tìm thấy lượt mượn đang active khớp `barcode`, trả lỗi rõ ràng (vd `{ "success": false, "message": "Không tìm thấy lượt mượn đang hoạt động cho mã KCB này." }`) để FE hiển thị qua toastr.

## Checklist nghiệm thu (Phase 4)
1. Gửi `Return` với đủ `{ borrowId, barcode, cardNo }` (Trả xuôi) — hành vi không đổi so với trước.
2. Gửi `Return` chỉ với `{ barcode }` (Trả ngược) — trả đúng lượt mượn đang active khớp barcode, chuyển trạng thái tài liệu đúng, response có `readerName`/`bibTitle`.
3. Gửi `Return` với `{ barcode }` không khớp lượt mượn nào đang active — trả lỗi rõ ràng, không lỗi 500.

## Phase 5 — Hoàn thiện Báo tạp chí (VII)

### 14) Duyệt/khóa đơn đặt — `Serial/Approve`
| | |
|---|---|
| Method/URL | `PUT /api/PrintBook/Magazine/Serial/Approve` |
| Body | `{ "publicId": "..." }` |
| Response | `{ "success": true, "data": { "publicId": "...", "approved": true, "approvedDate": "2026-07-10" } }` |

Thêm 2 field vào entity `Subscription` (bảng Serial hiện có): `Approved` (bool, mặc định `false`), `ApprovedDate` (datetime, null cho tới khi duyệt). Đây là quyết định **1 chiều** (cẩm nang mục V.3: *"chuyển trạng thái từ chưa duyệt thành duyệt"*, không có nghiệp vụ "un-duyệt"). **Quan trọng**: `Serial/Update` và `Serial/Delete` phải tự kiểm tra `Approved=true` và từ chối (trả lỗi rõ ràng) — FE chỉ ẩn nút trên UI, không thay thế việc kiểm tra phía server.

### 15) Ghép số phát hành — field mới trên `SerialIssue` + tra cứu mẫu theo id số
FE thực hiện "Ghép số" (cẩm nang mục VII.4) bằng cách **tạo 1 bản ghi `SerialIssue` mới + xóa N bản ghi nguồn**, dùng lại các endpoint `SaveItem`/`DeleteItem` đã có — không cần endpoint ghép chuyên biệt, nhưng cần:
- Thêm 2 field vào entity `SerialIssue`: `IsMerged` (bool), `SortOrder` (int, vị trí hiển thị mong muốn trong danh sách số phát hành — tương ứng "STT" trong cẩm nang).
- Bản ghi gộp chỉ set `SerialSeq` (string, vd `"Số 6+7"`) — **không** set `SerialSeqX/Y/Z` (các field này là số nguyên, không chứa được giá trị ghép dạng chuỗi).
- Endpoint mới `GET /api/PrintBook/Magazine/Pattern/GetByNumericId/{id}` — tra cứu `PatternMagazine` (kèm `detail`) theo `id` số (khác `GetById` hiện tại dùng `publicId`), phục vụ modal Ghép số hiển thị đúng nhãn cấp X/Y/Z (vd "Tập"/"Số") từ mẫu phát hành của đăng ký. Response giống `GetById`.
- Khuyến nghị (không bắt buộc): backend nên xử lý tạo+xóa trong 1 transaction để tránh trạng thái nửa vời nếu có lỗi giữa chừng.

### 16) Đóng tập báo tạp chí — module mới hoàn toàn (`Magazine/Binding`)
Cẩm nang mục VII.5: tìm các số đã nhận, đóng thành tập, đăng ký cá biệt cho tập. Module hoàn toàn chưa tồn tại trong ELIB, cần xây mới:

| | |
|---|---|
| Base | `api/PrintBook/Magazine/Binding` |
| `POST /Search` | body `{ accessionNo, volumeTitle, storeId, pageIndex, pageSize }` → `{ "data": { "items": [ {...SerialBinding} ], "totalCount": N } }` |
| `GET /GetById/{publicId}` | → `{ "data": {...SerialBinding, "items": [...] } }` |
| `POST /Add` | body `SerialBinding` (xem shape bên dưới) |
| `PUT /Update/{publicId}` | body giống `/Add` |
| `DELETE /Delete/{publicId}` | xóa 1 tập |

Shape `SerialBinding`:
```json
{
  "accessionNo": "DKCB-2026-001",
  "storeId": 3,
  "volumeTitle": "Tạp chí Khoa học — Tập 2024",
  "subscriptionId": 12,
  "subscriptionTitle": "Tạp chí Khoa học",
  "bindingDate": "2026-07-10",
  "note": "...",
  "items": [
    { "issueId": 101, "serialSeq": "Số 6", "publishedDate": "2026-06-01" },
    { "issueId": 102, "serialSeq": "Số 7", "publishedDate": "2026-07-01" }
  ]
}
```
`items[].issueId` liên kết tới `SerialIssue.id`. `itemCount` (số lượng `items`) nên trả kèm ở `Search` để FE hiển thị cột mà không cần load chi tiết từng dòng.

Khuyến nghị (không bắt buộc): thêm field `IsBound` (bool) trên `SerialIssue`, set `true` khi số phát hành đã thuộc 1 tập — giúp FE loại các số đã đóng tập khỏi danh sách chọn ở modal "Thêm số phát hành". Nếu chưa có, FE vẫn hoạt động (best-effort, không chặn cứng).

### 17) Báo cáo báo tạp chí — 3 endpoint mới (`Magazine/Report`)
Cẩm nang mục VII.6 nêu 5 hành động: 2 icon in nhanh (dùng lại `Serial/Search` và `Serial/SearchReceipt` đã có, không cần endpoint mới) + 3 báo cáo tổng hợp trong phân hệ Báo cáo:

| | |
|---|---|
| Base | `api/PrintBook/Magazine/Report` |
| `POST /ReceivedSummary` + `/ReceivedSummaryExport` | Tổng hợp báo tạp chí nhận |
| `POST /ReceivedDetail` + `/ReceivedDetailExport` | Chi tiết báo tạp chí nhận |
| `POST /MissingClaimList` + `/MissingClaimListExport` | Danh mục yêu cầu đòi số thiếu |

Request body (3 endpoint đầu, giống nhau): `{ subscriptionCode, dateFrom, dateTo, pageIndex, pageSize }` → response mirror `AcquisitionReportService` (`{ "data": { "items": [...], "totalCount": N } }`). Export (3 endpoint `...Export`) nhận cùng filter (không kèm `pageIndex`/`pageSize`), trả về file Excel dạng blob.

## Checklist nghiệm thu (Phase 5)
1. `Serial/Approve` chuyển đúng `Approved=true`; `Serial/Update`/`Serial/Delete` từ chối khi `Approved=true`.
2. Ghép số: tạo mới `SerialIssue` với `IsMerged=true`/`SortOrder` thành công; `Pattern/GetByNumericId/{id}` trả đúng `detail.x/y/z`.
3. Module `Magazine/Binding` CRUD hoạt động đầy đủ, `items[]` lưu/trả đúng liên kết `issueId`.
4. 3 endpoint `Magazine/Report/*` trả đúng dữ liệu lọc theo `subscriptionCode`/`dateFrom`/`dateTo`; các endpoint `...Export` trả file Excel hợp lệ.
