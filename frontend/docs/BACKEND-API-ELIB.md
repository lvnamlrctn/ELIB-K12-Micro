# Hướng dẫn API backend — Các module ELIB bổ sung cho admin-website

Tài liệu này đặc tả các API mà frontend admin-website gọi cho các chức năng được port từ ELIB (`Elib/Main.cs`).
Backend (.NET) cần hiện thực hóa các endpoint dưới đây. Mọi field/bảng gợi ý suy ra từ các lớp C# trong
`Entities/`, `Bussiness/`, `DataAccess/` của ELIB (đã ghi rõ lớp nguồn).

## Quy ước chung

- **Base URL**: tất cả dưới `/api/PrintBook/{Nhóm}/{Entity}`, Nhóm ∈ `Catalogue | Circulation | Magazine | Store | Receiption | Opac`.
- **Auth**: header `Authorization: Bearer <token>` (đã có sẵn qua AuthInterceptor) + `Accept-Language`.
- **Response chuẩn** (khuyến nghị):
  ```json
  { "success": true, "data": { "items": [ ... ], "totalCount": 123 } }
  ```
  hoặc với 1 bản ghi: `{ "success": true, "data": { ... } }`. Frontend cũng chấp nhận mảng trực tiếp hoặc `{data:[...]}`.
- **Tìm kiếm** (`POST /Search`): body tối thiểu `{ keyword, pageIndex, pageSize }` (pageIndex bắt đầu từ 1) + các bộ lọc riêng.
- **CRUD chuẩn**: `GET /GetById/{id}`, `POST /Add`, `PUT /Update/{id}`, `DELETE /Delete/{id}`. Một số entity dùng `POST /Save` (tự quyết thêm/sửa theo Id).
- **Export**: trả `application/octet-stream` (Excel/PDF), frontend nhận `Blob`.

---

# NHÓM 1 — Biên mục (Catalogue)  ✅ frontend đã xong

## 1.1 Loại biểu ghi — `/api/PrintBook/Catalogue/Dic/BibType`
> Nguồn: `Entities/PrintBook/Catalogue/Dic/BibType.cs`

| Method | Path | Mô tả |
|---|---|---|
| POST | `/Search` | Tìm kiếm phân trang. Body `{keyword,pageIndex,pageSize}` |
| POST | `/SearchAll` | Lấy toàn bộ (cho dropdown). Body `{}` |
| GET  | `/GetById/{id}` | Chi tiết |
| POST | `/Add` | Thêm |
| PUT  | `/Update/{id}` | Sửa |
| DELETE | `/Delete/{id}` | Xóa |

Body Add/Update:
```json
{ "name":"Sách", "code":"BK", "typeCode":"a", "bibLevelCode":"m", "materialTypeCode":"BK", "recordTypeCode":"a" }
```
**Bảng gợi ý** `bib_type`: `id` (PK, bigint), `name` (nvarchar), `code`, `type_code`, `bib_level_code`, `material_type_code`, `record_type_code`.

## 1.2 Khung phân loại — `/api/PrintBook/Catalogue/Dic/DicClass`
> Nguồn: `Entities/PrintBook/Catalogue/Dic/DClass.cs` (cấu trúc cây)

| Method | Path | Mô tả |
|---|---|---|
| POST | `/Search` | Tìm kiếm phân trang `{keyword,pageIndex,pageSize}` |
| POST | `/GetTree` | Trả cây (mỗi node có `children[]`) cho dropdown chọn cấp cha |
| GET  | `/GetById/{id}` · POST `/Add` · PUT `/Update/{id}` · DELETE `/Delete/{id}` | CRUD |

Body Add/Update:
```json
{ "code":"610", "description":"Medical sciences", "vnDescription":"Y học", "type":"DDC23", "parentId":12 }
```
**Bảng gợi ý** `dic_class`: `id`, `parent_id`, `code`, `description`, `vn_description`, `type`.

## 1.3 Phiếu nhập tin (worksheet) — `/api/PrintBook/Catalogue/WorkSheet`
> Nguồn: `Entities/PrintBook/Catalogue/WorkSheet.cs`, `Bussiness/PrintBook/Catalogue/WorkSheet.cs`

| Method | Path | Mô tả |
|---|---|---|
| POST | `/Search` | `{keyword,bibTypeId,pageIndex,pageSize}` |
| POST | `/SearchAll` | dropdown |
| GET  | `/GetByBibType/{bibTypeId}` | Lấy worksheet theo loại biểu ghi (để dựng form MARC) |
| GET  | `/GetById/{id}` · POST `/Add` · PUT `/Update/{id}` · DELETE `/Delete/{id}` | CRUD |

Body Add/Update:
```json
{ "name":"Phiếu sách", "bibTypeId":1, "usmarc":"245|a,b,c#260|a,b,c#700|a" }
```
> `usmarc`: chuỗi mô tả các trường mẫu — mỗi trường `tag|subfields` cách nhau bởi `#`.
> Có thể tách riêng bảng trường/trường con: tham khảo `SaveWorkSheetField(Id,WorkSheetId,Field,l1,l2)`,
> `GetInfoFieldInWorkSheet(WorkSheetId,Field)`, `GetInfoSubFieldInWorkSheet(WorkSheetFieldId,SubField)`.

**Bảng gợi ý**: `work_sheet`(`id`,`name`,`usmarc`,`bib_type_id`); `work_sheet_field`(`id`,`work_sheet_id`,`field`,`l1`,`l2`); `work_sheet_subfield`(`id`,`work_sheet_field_id`,`subfield`,`name`).

## 1.4 Biên mục biểu ghi (MARC) — `/api/PrintBook/Catalogue/Book`
> Nguồn: `Bussiness/PrintBook/Catalogue/Book.cs` + `Bib.cs` + `Marc21Ultil.cs`; `Entities/.../Bib.cs`

| Method | Path | Mô tả | Hàm ELIB tương ứng |
|---|---|---|---|
| POST | `/Search` | Tìm biểu ghi. Body `{keyword,title,author,publisher,mfnFrom,mfnTo,bibTypeId,pageIndex,pageSize}` | `SearchSimplePrintBook`, `SearchLikeOpac` |
| GET  | `/GetByMfn/{mfn}` | Lấy biểu ghi + danh sách trường MARC | `GetBookInfo`, `GetMarcFieldByMFN`, `GetISBDByMFN` |
| POST | `/Save` | Lưu biểu ghi MARC (thêm/sửa). Trả `{ mfn }` | `SaveBib` (Bib.cs) |
| DELETE | `/Delete/{mfn}` | Xóa biểu ghi | `DeleteBook(Mfn)` |
| GET  | `/Items/{bibId}` | Danh sách bản cá biệt (ĐKCB) của biểu ghi | `GetBookStatus`, `GetItemShip2` |

Body `/Save`:
```json
{
  "mfn": 1023, "bibId": 1023, "bibTypeId": 1,
  "fields": [
    { "tag":"245", "ind1":"0", "ind2":"0", "subFields":[{"code":"a","value":"Lập trình C#"},{"code":"c","value":"Nguyễn Văn A"}] },
    { "tag":"260", "ind1":" ", "ind2":" ", "subFields":[{"code":"a","value":"Hà Nội"},{"code":"b","value":"NXB Giáo dục"},{"code":"c","value":"2020"}] }
  ]
}
```
Response `/GetByMfn`: như body trên + `title,author,publisher,publishDate,isbd` (rút từ MARC để hiển thị nhanh).

**Bảng gợi ý**: `bib`(`bib_id` PK,`mfn`,`bib_type_id`,`bib_work_sheet_id`,`marc_status`,`status`,`images`,`url`,`created_by`,`created_time`,`updated_by`,`updated_time`); `marc_field`(`id`,`bib_id`,`tag`,`ind1`,`ind2`,`value`,`sort_order`); `marc_subfield`(`id`,`marc_field_id`,`code`,`value`,`sort_order`). Item/holdings xem Nhóm 4 (`book_item`).

---

# NHÓM 1 — Bổ sung: Đơn đặt / Phiếu nhập / Điều chuyển / Người giao  ✅ frontend đã xong

## 1.5 Đơn đặt bổ sung — `/api/PrintBook/Catalogue/Order`
> Nguồn: `Entities/.../Order.cs`, `Bussiness/.../Order.cs` (`SearchOrder, CreateOrder, SaveOrder, SaveOrderDetail, GetOrderDataTableById`)

| Method | Path | Mô tả |
|---|---|---|
| POST | `/Search` | `{orderName,supplierId,fundId,status,pageIndex,pageSize}` |
| GET  | `/GetById/{id}` | Chi tiết đơn (header) |
| POST | `/Save` | Lưu header (thêm/sửa, tự theo `id`) |
| GET  | `/Lines/{orderId}` | Danh sách dòng đặt |
| POST | `/SaveDetail` | Thêm/sửa 1 dòng đặt |
| DELETE | `/Delete/{id}` | Xóa đơn |

Body `/Save`: `{ id?, orderName, code, orderDate, dueDate, supplierId, fundId, budgetId?, sourceId?, status, note }` — `status`: 0 Nháp, 1 Đã đặt, 2 Đã nhận.
Body `/SaveDetail`: `{ id?, orderId, bibId?, title, author, publisher, amount, currency }`.
**Bảng**: `ab_order`(`id,code,order_name,order_date,due_date,supplier_id,fund_id,budget_id,source_id,payment_method_id,payment_status,status,note,created_by,created_date`); `ab_order_detail`(`id,order_id,bib_id,amount,currency`).

## 1.6 Phiếu nhập bổ sung — `/api/PrintBook/Catalogue/Receipt`
> Nguồn: `Receipt.cs` (`SearchReciept, CreateReceipt, SaveReceipt, SaveReceiptDetail, GetBarcodeByBibId, GetNumberBookRegister, GetInfoNhanMonLoai`)

Endpoint giống Order (`/Search` lọc theo `receiptName,supplierId,fundId,status`; `/GetById/{id}`, `/Save`, `/Lines/{receiptId}`, `/SaveDetail`, `/Delete/{id}`) **+**:

| Method | Path | Mô tả |
|---|---|---|
| GET | `/Barcodes/{receiptId}/{bibId}` | Lấy/sinh danh sách mã vạch (ĐKCB) cho 1 biểu ghi trong phiếu |

Body `/Save`: như Order + `storeId` (kho nhập). Khi lưu chi tiết, backend sinh số ĐKCB/barcode theo `GetNumberBookRegister`.
**Bảng**: `ab_receipt`(... như ab_order + `store_id`); `ab_receipt_detail`(`id,receipt_id,bib_id,amount,currency,register_count`); ĐKCB ghi vào `book_item` (xem Nhóm 4).

## 1.7 Điều chuyển kho — `/api/PrintBook/Catalogue/Move`
> Nguồn: `Move.cs`. CRUD: `POST /Search {keyword,pageIndex,pageSize}`, `GET /GetById/{id}`, `POST /Save`, `DELETE /Delete/{id}`.
Body: `{ id?, code, storeDelivererId, storeReceiptId, delivererName, delivererAddress, receiptName, receiptAddress, delivererDate, note }`.
**Bảng** `ab_move`: `id,code,store_deliverer_id,store_receipt_id,deliverer_name,deliverer_address,receipt_name,receipt_address,deliverer_date,status,note,created_by`.

## 1.8 Người giao — `/api/PrintBook/Catalogue/Deliverer`
> Nguồn: `Deliverer.cs`. CRUD giống Move. Body thêm `storeId`, `receiptId`.
**Bảng** `ab_deliverer`: `id,code,receipt_id,store_id,deliverer_name,deliverer_address,receipt_name,receipt_address,deliverer_date,status,note,created_by`.

## 1.9 In mã vạch / In nhãn môn loại  (client-side)
Hai trang `/admin/print-barcode` và `/admin/print-spine-label` **render & in tại client** (jsbarcode + `window.print()`), không cần API riêng — dữ liệu mã vạch/nhãn có thể nhập tay hoặc lấy từ `/Catalogue/Receipt/Barcodes/...`.

---

# NHÓM 2 — Lưu thông `/api/PrintBook/Circulation`  ✅ frontend đã xong

> Nguồn: `Bussiness/PrintBook/Circulation/Loan/BorrowBook.cs`, `Fine.cs`; `DataAccess/.../HistoryBorrows.cs`.

## 2.1 Mượn / Trả / Gia hạn — `/api/PrintBook/Circulation/Loan`
| Method | Path | Mô tả | Hàm ELIB |
|---|---|---|---|
| POST | `/ReaderSnapshot` | `{cardNo,circPlaceId}` → thông tin bạn đọc + `currentLoans[]` | `SearchBookBorrowByCardNo` |
| POST | `/Checkout` | `{readerId,barcode,circPlaceId}` mượn 1 cuốn | `BorrowingBook`, `CheckMuonTrung`, `RemainBook` |
| POST | `/Return` | `{borrowId?,barcode?,cardNo?}` trả sách | `ReturnBook` |
| POST | `/Renew` | `{borrowId}` gia hạn | `RenewBook` |
| POST | `/Note` | `{borrowId,note}` ghi chú | `NoteBorrowBook` |

`ReaderSnapshot` response data: `{ readerId,cardNo,fullName,readerType,className,expireDate,maxItems,currentLoans:[{id,barcode,bibTitle,borrowDate,dueDate,status,statusName,ngayQH,fineValue}] }`.
Checkout/Return/Renew nên trả `{success, message}`; lỗi nghiệp vụ (hết hạn thẻ, quá số lượng, mượn trùng) trả `400 {message}` để UI hiển thị.
**Bảng**: `book_out`(`id,reader_id,barcode,bib_id,circ_place_id,borrow_date,due_date,return_date,renew_count,status,fine_value,created_by,note`).

## 2.2 Yêu cầu mượn — `/api/PrintBook/Circulation/Request`
`POST /Search {cardNo,status,pageIndex,pageSize}`, `POST /Approve {requestId}` (= `BorrowByRequest`), `POST /Reject {requestId}`, `DELETE /Delete/{id}`. Bảng `book_request`(`id,reader_id,barcode,bib_id,request_date,status`).

## 2.3 Lịch sử lưu thông — `/api/PrintBook/Circulation/History`
`POST /Search {cardNo,title,barcode,borrowDateFrom,borrowDateTo,status,pageIndex,pageSize}`; `POST /Export` (Blob Excel). Nguồn `SearchBorrowBook*`/`SearchReturnBook*`.

## 2.4 Phạt — `/api/PrintBook/Circulation/Fine`
`POST /Search {cardNo,status,fineDateFrom,fineDateTo,pageIndex,pageSize}` (`SearchFine`), `POST /Save` (`SaveFine` — body `{id?,readerId,reasonFineId,value,fineDate,note,borrowId?,status}`), `POST /Pay {id}`, `POST /Waive {id}`, `DELETE /Delete/{id}` (`DeleteFine`). Bảng `fine`(`id,reader_id,borrow_id,reason_fine_id,fine_date,return_date,value,fine_method_id,lan_phat,status,note,created_by`).

## 2.5 Chính sách lưu thông — `/api/PrintBook/Circulation/CircPolicy`
CRUD chuẩn (`/Search`,`/GetById/{id}`,`/Add`,`/Update/{id}`,`/Delete/{id}`). Body `{readerTypeId,storeId?,maxItems,loanDays,maxRenew,renewDays,finePerDay}`. Bảng `circ_policy`.

> **Còn lại (tùy chọn):** Sao chụp (`/Circulation/PhotoCopy`), Báo cáo lưu thông (`/Circulation/Report`), Thống kê giao dịch (`TransactionBook`) — chưa làm UI.

---

# NHÓM 3 — Ấn phẩm định kỳ (Serials) `/api/PrintBook/Magazine`

> Nguồn ELIB: `Entities/PrintBook/Magazine/{FrequencyMagazine,PatternMagazine}.cs`, `Entities/Magazine/MagaginzeType.cs`,
> `DataAccess/PrintBook/Magazine/{Serial,FrequencyMagazine,PatternMagazine,PartemMagazineDetail}.cs`,
> Forms `Elib/Magazine/{MagazineType,FrequencyMagazine,PatternMagazine,AddSerial,SerialReceipt,ClaimMagazine,SearchSerialReceipt}.cs`.
> Mô hình 3 cấp: **Loại ấn phẩm → Đăng ký (Serial subscription) → Kỳ (SerialItem)**, gắn Tần suất + Mẫu đánh số.
> Thiết kế theo **chuẩn ILS Serials Control** (Koha/MARC serials): đăng ký có kiểu thủ công/dự đoán, độ dài đăng ký, thời gian chờ khiếu nại (grace), vòng đời trạng thái; hỗ trợ **sinh kỳ dự kiến tự động** (issue prediction) và thống kê nhận kỳ.

## 3.1 Loại ấn phẩm — `/api/PrintBook/Magazine/MagazineType`
CRUD chuẩn (`/Search`,`/GetById/{id}`,`/Add`,`/Update/{id}`,`/Delete/{id}`). Body `{code,name,magazineTypeId,images}` (ILS: có mã + phân cấp loại cha).
**Bảng**: `magazine_type`(`id` PK,`code`,`name`,`magazine_type_id` (FK self),`images`). Nguồn `MagaginzeType`(Id, Name) — mở rộng theo chuẩn ILS.

## 3.2 Tần suất xuất bản — `/api/PrintBook/Magazine/Frequency`
CRUD chuẩn. Body `{name, dv, soTrenDV, dvTrenSo, ngayPhatHanh, order}`.
- `dv` = đơn vị thời gian (1 Ngày, 2 Tuần, 3 Tháng, 4 Quý, 5 Năm); `soTrenDV` số kỳ/đơn vị; `dvTrenSo` số đơn vị/kỳ.
**Bảng**: `frequency_magazine`(`id,name,dv,so_tren_dv,dv_tren_so,ngay_phat_hanh,order`). Nguồn `FrequencyMagazine`.

## 3.3 Mẫu đánh số kỳ — `/api/PrintBook/Magazine/Pattern`
CRUD chuẩn. Body header `{name, description, function, order}` + `detail:{x,y,z, stepX,stepY,stepZ, repeatX,repeatY,repeatZ, maxX,maxY,maxZ, resetX,resetY,resetZ}`.
- `function` = biểu thức sinh chuỗi số kỳ từ X/Y/Z (vd `"Tập {X}, Số {Y}"`). 3 cấp X/Y/Z với bước tăng/lặp/tối đa/mốc reset.
**Bảng**: `pattern_magazine`(`id,name,description,function,order`); `pattern_magazine_detail`(`id,pattern_id,x,y,z,step_x,step_y,step_z,repeat_x,repeat_y,repeat_z,max_x,max_y,max_z,reset_x,reset_y,reset_z`). Nguồn `PatternMagazine` + `PatternMagazineDetail`.

## 3.4 Đăng ký nhận ấn phẩm — `/api/PrintBook/Magazine/Serial`
| Method | Path | Mô tả | Hàm ELIB |
|---|---|---|---|
| POST | `/Search` | lọc `{title,author,publisher,issn,startTimeFrom,startTimeTo,endTimeFrom,endTimeTo,pageIndex,pageSize}` | `SearchSerialSubscription` (+`PageCount`) |
| GET | `/GetById/{id}` | chi tiết đăng ký | `GetDataTableSerialById` |
| POST | `/Add` / PUT `/Update/{id}` | lưu đăng ký | `SaveSerial`, `UpdateSerial` (cập nhật lastX/Y/Z) |
| DELETE | `/Delete/{id}` | xóa | `DeleteSerial` |

| PUT | `/ChangeStatus` | `{publicId,status}` đổi vòng đời (1 hiệu lực · 2 hết hạn · 3 hủy) | — |

Body (thiết kế **ILS Serials Control**): `{bibId,title,issn,magazineTypeId,frequencyId,patternId,storeId,supplierId,startTime,endTime,firstTime,startX,startY,startZ,subscriptionType,subscriptionLength,lengthUnit,graceDays,status,callNumber,locate,publicNote,internalNote,note}`.
- `subscriptionType`: 1 thủ công · 2 tự động dự đoán (predicted) — quyết định có sinh kỳ tự động (xem 3.5 `/PredictIssues`).
- `subscriptionLength` + `lengthUnit` (1 kỳ · 2 tuần · 3 tháng): độ dài đăng ký, dùng tính `endTime` & số kỳ dự kiến.
- `graceDays`: số ngày chờ sau `plannedDate` trước khi kỳ chuyển `late`/cho phép khiếu nại.
- `status`: vòng đời đăng ký (1/2/3).
Response item nên kèm `frequencyName,patternName,storeName,supplierName,magazineTypeName,lastX,lastY,lastZ` và thống kê `expectedCount,receivedCount,missingCount`.
**Bảng**: `serial_subscription`(`id,bib_id,issn,magazine_type_id,frequency_id,pattern_id,store_id,supplier_id,start_time,end_time,first_time,start_x,start_y,start_z,last_x,last_y,last_z,subscription_type,subscription_length,length_unit,grace_days,status,call_number,locate,public_note,internal_note,note,created_by`). Nguồn `Serial.SaveSerial` (mở rộng ILS).

## 3.5 Nhận kỳ (SerialItem) — `/api/PrintBook/Magazine/Serial`
| Method | Path | Mô tả | Hàm ELIB |
|---|---|---|---|
| POST | `/SearchReceipt` | `{subscriptionId,pageIndex,pageSize}` → danh sách kỳ của đăng ký | `SearchSerialSubscriptionReceipt` (+`PageCount`) |
| POST | `/SaveItem` | nhận/cập nhật 1 kỳ | `SaveSerialItem` |
| POST | `/Claim` | `{id,note?}` khiếu nại kỳ thiếu (tăng `claim_count`, set `claim_date`, `status=2`) | (ClaimMagazine/ComplainSerial) |
| POST | `/PredictIssues` | `{subscriptionId,count}` **sinh kỳ dự kiến tự động** (ILS issue prediction) | (mới — từ pattern+frequency) |
| GET | `/Statistics/{subscriptionId}` | `{expected,received,claimed,missing,late}` thống kê nhận kỳ | `ThongKeSerialSubscriptionReceipt` |
| GET | `/LastPublishDate/{subscriptionId}` | ngày phát hành kỳ gần nhất | `GetLasPublishDate` |
| DELETE | `/DeleteItem/{id}` | xóa kỳ | — |

`SaveItem` body: `{id,subscriptionId,serialSeq,serialSeqX,serialSeqY,serialSeqZ,status,isSpecial,quantity,plannedDate,publishedDate,claimDate,claimCount,barcode,note}`.
- `status`: 0 dự kiến · 1 đã nhận · 2 khiếu nại · 3 thiếu · 4 trễ. Khi nhận kỳ set `status=1` + `publishedDate`.
- **`/PredictIssues` (đặc trưng ILS):** từ `subscription.patternId` (X/Y/Z + step/repeat/max/reset + `function`), `frequencyId` (đơn vị thời gian) và `firstTime` + `startX/Y/Z`, backend sinh `count` kỳ kế tiếp với `serialSeq`/`plannedDate` tính sẵn, `status=0`; đồng thời cập nhật `subscription.lastX/Y/Z`. Chỉ áp dụng khi `subscriptionType=2`.
**Bảng**: `PrintBook.SERIALITEM` → `serial_item`(`id,subscription_id,serial_seq,serial_seq_x,serial_seq_y,serial_seq_z,status,is_special,quantity,planned_date,published_date,claim_date,claim_count,barcode,note`). Nguồn `Serial.SaveSerialItem`, cột thực tế `SUBSCRIPTION_ID`,`PUBLISHED_DATE`...

## 3.6 Tìm phiếu nhận — `/api/PrintBook/Magazine/Serial`
Dùng lại `/Search` (đăng ký) với bộ lọc nhan đề/ISSN/ngày, từ kết quả mở `/SearchReceipt` để xem kỳ. Nguồn `SearchSerialReceipt` + `ThongKeSerialSubscriptionReceipt`.

> **Sinh kỳ tự động:** UI có nút *"Sinh kỳ dự kiến"* gọi `/PredictIssues`. Backend nên hỗ trợ cả 2 luồng: (a) đăng ký `subscriptionType=2` tự sinh kỳ khi lưu/đến hạn; (b) sinh thủ công `count` kỳ theo yêu cầu. Trang Nhận kỳ hiển thị thanh thống kê (`/Statistics`) và cho nhận/khiếu nại từng kỳ.

---

# NHÓM 4 — Kho nghiệp vụ (Store operations) `/api/PrintBook/Store`

> Nguồn ELIB: `Bussiness/PrintBook/Store/{Inventory,StatisticsDocument}.cs`, `Bussiness/PrintBook/Catalogue/Book.cs` (ProcessLossBook/LiquidateBook/SearchLostBook/SearchLiquidate),
> Forms `Elib/Store/{InventoryBarcode,SearchInventory,InventoryLostBook,ProcessLossBook,Liquidated,SearchLiquidated,ReRegisterBarcode,StatisticsDocument}.cs`.

## 4.1 Kiểm kê — `/api/PrintBook/Store/Inventory`
| Method | Path | Mô tả | Hàm ELIB |
|---|---|---|---|
| POST | `/Search` | `{inventoryName,inventoryDateFrom,inventoryDateTo,status,page}` | `SearchInventory` |
| GET | `/GetById/{id}` | chi tiết phiên | `GetDataTableInventoryById` |
| POST | `/Add` / PUT `/Update/{id}` | tạo/lưu phiên `{inventoryName,inventoryDate,status}` | `CreateInventory`,`SaveInventory` |
| DELETE | `/Delete/{id}` | xóa phiên | `DeleteInventory` |
| POST | `/ScanBarcode` | `{inventoryId,barcode,storeId}` quét 1 mã | `InsertBarcodeInventory` (+`CheckBarcodeExitsInventory`) |
| POST | `/SearchBarcode` | `{inventoryId,checkStoreStatus,checkBorrow,checkStatus,page}` | `SearchBarcodeInventory` |
| GET | `/Summary/{inventoryId}` | `{scanned,notRegister,notCorrectStore,lost}` | `CountBookNotRegister`+`CountBookNotCorrecStore`+`ProcessLostBook` |
| POST | `/ProcessLost` | `{inventoryId}` → DS sách trong kho không quét được | `ProcessLostBook` |
| GET | `/Report/{inventoryId}` | xuất báo cáo (Blob Excel) | `ReportInventory` |

**Bảng**: `inventory`(`id,inventory_name,inventory_date,status,user_id`); `inventory_barcode`(`id,inventory_id,barcode,store_id,check_store_status,check_borrow,check_status,scan_time`). `checkStoreStatus`: 0 sai kho · 1 đúng kho; `checkBorrow`: 1 đang mượn.

## 4.2 Xử lý sách mất — `/api/PrintBook/Store/LostBook`
| Method | Path | Mô tả | Hàm ELIB |
|---|---|---|---|
| POST | `/Search` | `{storeId,createdDateFrom,createdDateTo,page}` | `SearchLostBook` |
| POST | `/MarkLost` | `{barcode,lossDate,reason,command:'loss'}` đánh dấu mất | `ProcessLossBook` |
| POST | `/UndoLost` | `{barcode,command:'undo'}` bỏ đánh dấu | `ProcessLossBook` (command undo) |

**Bảng**: cập nhật `book_item.status` = mất + ghi `book_loss`(`id,barcode_id,loss_date,reason,created_by`).

## 4.3 Thanh lý — `/api/PrintBook/Store/Liquidate`
| Method | Path | Mô tả | Hàm ELIB |
|---|---|---|---|
| POST | `/Search` | `{title,author,publisher,barcode,storeId,bibTypeId,liquidateFrom,liquidateTo,page}` | `SearchLiquidate` |
| POST | `/Liquidate` | `{barcode,reason,liquidateDate}` thanh lý | `LiquidateBook` |
| POST | `/ReLiquidate` | `{barcode}` hủy thanh lý | `ReLiquidateBook` |

**Bảng**: `book_item` thêm `is_liquidated,liquidate_date,liquidate_reason,liquidate_by`. (Dùng chung danh sách bản cá biệt với `SearchBib(IsLiquidated=true)`.)

## 4.4 Đăng ký lại mã vạch — `/api/PrintBook/Store/ReRegisterBarcode`
| Method | Path | Mô tả | Hàm ELIB |
|---|---|---|---|
| GET | `/Lookup/{barcode}` | tra cứu bản cá biệt theo mã vạch hiện tại → `{barcode,bibTitle,author,storeId,storeName,status}` | (ReRegisterBarcode form) |
| POST | `/ReRegister` | `{oldBarcode,newBarcode,storeId}` gán mã vạch mới | (cập nhật `book_item.barcode`) |

## 4.5 Thống kê tài liệu — `/api/PrintBook/Store/Statistics`
| Method | Path | Mô tả | Hàm ELIB |
|---|---|---|---|
| POST | `/Document` | `{criterion,receiptDateFrom,receiptDateTo,storeId}` → `[{key,label,count,detail}]` | `StatisticsBy{BibType,Status,Language,DDC}` / `StatisticBookInStore` |
| POST | `/DocumentExport` | như trên, trả Blob Excel | `StatisticsDetailBy*` |

`criterion` ∈ `bibType|status|language|ddc|store`. `count` = số biểu ghi (title), `detail` = số bản cá biệt (item). Tham số ELIB gốc: `(CriterionId, ReceiptDateFrom, ReceiptDateTo, StoreId)`.

> **Còn lại (tùy chọn):** Xuất biểu ghi MARC/ISO2709 (`/Store/ExportRecord` — `Elib/Store/ExportRecord`), Báo cáo sách trong kho/thư viện (`ReportBookInStore`/`BookInLibrary`) — chưa làm UI.

---

# NHÓM 5 — Vào ra (Receiption) `/api/PrintBook/Receiption`

> Nguồn ELIB: `Bussiness/Common/Reader.cs` (ReaderCheckIn/Out, SearchReaderCheckIn, CheckReaderCheckIn), `Bussiness/PrintBook/Receiption/StatisticsCheckn.cs`,
> `Bussiness/PrintBook/Circulation/Loan/BorrowKey.cs` (BorrowingKey/ReturnKey/SearchKeyBorrowByCardNo/SearchBorrowKey), Forms `Elib/Receiption/{CheckIn,CheckOut,HistoryCheckIn,StatisticsCheckIn,ReportReceiption,BorrowKey,SearchBorrowKey}.cs`.

## 5.1 Check-in / Check-out — `/api/PrintBook/Receiption/Check`
| Method | Path | Mô tả | Hàm ELIB |
|---|---|---|---|
| POST | `/Status` | `{cardNo,circPlaceId}` → `{readerId,cardNo,fullName,readerType,className,avatar,checkedIn,checkInId,checkInTime,found}` | `CheckReaderCheckIn` |
| POST | `/CheckIn` | `{cardNo,circPlaceId}` ghi nhận vào | `ReaderCheckIn` |
| POST | `/CheckOut` | `{cardNo,checkInId,circPlaceId}` ghi nhận ra | `ReaderCheckOut` |
| POST | `/SearchHistory` | `{cardNumber,firstName,lastName,checkInTimeFrom,checkInTimeTo,checkOutTimeFrom,checkOutTimeTo,tenantId,readerTypeId,classId,courseId,storeId,page}` | `SearchReaderCheckIn` |
| POST | `/ExportHistory` | như trên → Blob Excel | — |
| DELETE | `/Delete/{id}` | xóa 1 bản ghi vào/ra | `DeleteCheckIn` |

`checkedIn=true` ⇒ chỉ cho Check-out; `false` ⇒ chỉ cho Check-in. `checkType` trong DB: 1 vào · 2 ra.
**Bảng**: `reader_checkin`(`id,reader_id,store_id,circ_place_id,check_in_time,check_out_time,check_type,user_id`).

## 5.2 Thống kê vào ra — `/api/PrintBook/Receiption/Statistics`
| Method | Path | Mô tả | Hàm ELIB |
|---|---|---|---|
| POST | `/CheckIn` | `{criterion,receiptDateFrom,receiptDateTo,circPlaceId}` → `[{key,label,count}]` | `StatisticsBy{ReaderType,Class,Course,Department}` |
| POST | `/CheckInExport` | như trên → Blob Excel | `StatisticsDetailBy*` |

`criterion` ∈ `readerType|class|course|department`. `count` = số lượt vào.

## 5.3 Báo cáo vào ra — `/api/PrintBook/Receiption/Report`
| Method | Path | Mô tả | Hàm ELIB |
|---|---|---|---|
| POST | `/ReaderCount` | `{classId,courseId,tenantId,readerTypeId,fromDate,toDate,circPlaceId,top,page}` → `[{cardNo,fullName,readerType,className,checkInCount}]` | `ReportReaderCheckInCount` |
| POST | `/ReaderCountExport` | như trên → Blob Excel | — |

(`top=0` = không giới hạn; `top=N` = N bạn đọc vào nhiều nhất.)

## 5.4 Mượn chìa khóa / tủ — `/api/PrintBook/Receiption/BorrowKey`
| Method | Path | Mô tả | Hàm ELIB |
|---|---|---|---|
| POST | `/Snapshot` | `{cardNo,circPlaceId}` → `{readerId,cardNo,fullName,readerType,className,currentKeys:[{id,cabinetName,cabinetBarcode,borrowDate}]}` | `SearchKeyBorrowByCardNo` |
| POST | `/Borrow` | `{readerId,cabinetBarcode,circPlaceId}` mượn 1 tủ/khóa | `BorrowingKey` |
| POST | `/Return` | `{borrowKeyId?,cabinetBarcode?,cardNo?}` trả khóa | `ReturnKey` |
| POST | `/Search` | `{cardNumber,firstName,lastName,borrowDateFrom,borrowDateTo,returnDateFrom,returnDateTo,tenantId,readerTypeId,classId,courseId,storeId,onlyReturned,page}` | `SearchBorrowKey`/`SearchReturnKey` |

**Bảng**: `borrow_key`(`id,reader_id,cabinet_id,circ_place_id,borrow_date,return_date,status,user_id`). `status`: 1 đang mượn · 2 đã trả. Tủ dùng chung `cabinet` (đã có ở admin). `onlyReturned=true` ⇒ `SearchReturnKey`.

> **Lưu ý:** Cấu hình vào ra (`/admin/config-receiption`) và Tủ đựng đồ (`/admin/cabinets`) đã có sẵn trong admin (nhóm tham số), không thuộc phạm vi nhóm 5.

---

# NHÓM 6 — Z39.50 (Biên mục từ xa) `/api/PrintBook/Opac`

> Nguồn ELIB: `Entities/PrintBook/Opac/{Z3950Config,Z3950Group,Z3950Result}.cs`, `Bussiness/PrintBook/Opac/Z3950Config.cs`,
> Forms `Elib/Cataloging/Z3950SearchImport.cs` (BuilQuery + `Zoom.Net.YazSharp.Connection.Search` + Import vào Bib), `Elib/Cataloging/Acquisitions/Z3950Config.cs`.
> **Quan trọng:** kết nối Z39.50 (giao thức YAZ/ZOOM) chạy ở **BACKEND**; trình duyệt không nói chuyện trực tiếp với server Z39.50. UI chỉ gửi điều kiện, nhận MARC và yêu cầu nhập.

## 6.1 Cấu hình Z39.50 — `/api/PrintBook/Opac/Z3950Config`
CRUD chuẩn (`/Search` lọc `{keyword,groupId,pageIndex,pageSize}`, `/GetById/{id}`, `/Add`, `/Update/{id}`, `/Delete/{id}`).
Body: `{name,host,port,databaseName,systax,userName,password,url,language,groupId}` (giữ nguyên tên `systax` theo entity ELIB).
**Bảng**: `z3950_config`(`id,name,host,port,database_name,systax,user_name,password,url,language,group_id,portal_id`). Nguồn `Z3950Config`. Nhóm dùng `z3950_group` (đã có ở admin `/admin/z3950-groups`).

## 6.2 Tra cứu & nhập biểu ghi — `/api/PrintBook/Opac/Z3950`
| Method | Path | Mô tả | Hàm ELIB |
|---|---|---|---|
| POST | `/Search` | `{title,author,isbn,issn,publisher,keyword,configId?,groupId?,page}` → brief `[{id,configId,configName,title,author,publisher,publishDate,isbn,issn,language}]` | `BuilQuery`+`Connection.Search` (PrefixQuery/PQF) trên các server theo `SearchListZ3950Config` |
| POST | `/Marc` | `{resultId,configId}` → `{marc}` (bản MARC dạng văn bản) | present record từ `IResultSet` |
| POST | `/Import` | `{resultId,configId,bibTypeId,worksheetId?,receiptId?}` → `{mfn}` tạo biểu ghi mới | `objBBib` Save từ MARC Z3950 (Z3950SearchImport) |

- `groupId`/`configId` rỗng ⇒ backend tra trên **toàn bộ** server đã cấu hình (gộp kết quả), gắn `configName` để biết nguồn.
- Backend dựng PQF/PrefixQuery từ các trường (Bib-1 attset: title=4, author=1003/1004, isbn=7, issn=8, publisher=1018, keyword=1016).
- `/Import`: backend lấy MARC của `resultId`, tạo `bib` + `marc_field`/`marc_subfield` theo `bibTypeId`/`worksheetId`, trả `mfn` để UI điều hướng tới `/admin/catalog-bibs/edit/:mfn`.

> **Tùy chọn (để sau):** công cụ chuyển đổi MARCXML ⇄ MARC, ISO2709 → MARC (`Elib/Tool/Convert*`).

---

## Bảng tổng hợp route ↔ endpoint ↔ lớp ELIB (Nhóm 1–6 — hoàn tất)

| Route admin | Endpoint base | Lớp ELIB nguồn |
|---|---|---|
| `/admin/bib-types` | `/api/PrintBook/Catalogue/Dic/BibType` | `Dic/BibType.cs` |
| `/admin/dic-classes` | `/api/PrintBook/Catalogue/Dic/DicClass` | `Dic/DClass.cs` |
| `/admin/worksheets` | `/api/PrintBook/Catalogue/WorkSheet` | `Catalogue/WorkSheet.cs` |
| `/admin/catalog-bibs`, `/catalog-bibs/edit/:mfn` | `/api/PrintBook/Catalogue/Book` | `Catalogue/Book.cs`, `Bib.cs` |
| `/admin/ab-orders` | `/api/PrintBook/Catalogue/Order` | `Catalogue/Order.cs` |
| `/admin/ab-receipts` | `/api/PrintBook/Catalogue/Receipt` | `Catalogue/Receipt.cs` |
| `/admin/ab-moves` | `/api/PrintBook/Catalogue/Move` | `Catalogue/Move.cs` |
| `/admin/ab-deliverers` | `/api/PrintBook/Catalogue/Deliverer` | `Catalogue/Deliverer.cs` |
| `/admin/print-barcode`, `/admin/print-spine-label` | (client-side) | `ParaPrintBarcode`, `PrintInNhanMonLoai` |
| `/admin/borrow` | `/api/PrintBook/Circulation/Loan` | `Circulation/Loan/BorrowBook.cs` |
| `/admin/request-books` | `/api/PrintBook/Circulation/Request` | `BorrowBook.cs` (RequestBook) |
| `/admin/loan-history` | `/api/PrintBook/Circulation/History` | `Circulation/HistoryBorrows.cs` |
| `/admin/fines` | `/api/PrintBook/Circulation/Fine` | `Circulation/Loan/Fine.cs` |
| `/admin/circ-policies` | `/api/PrintBook/Circulation/CircPolicy` | ELIB CircPolicy |
| `/admin/magazine-types` | `/api/PrintBook/Magazine/MagazineType` | `Magazine/MagaginzeType.cs` |
| `/admin/magazine-frequencies` | `/api/PrintBook/Magazine/Frequency` | `PrintBook/Magazine/FrequencyMagazine.cs` |
| `/admin/magazine-patterns` | `/api/PrintBook/Magazine/Pattern` | `PrintBook/Magazine/PatternMagazine.cs` |
| `/admin/serial-subscriptions` | `/api/PrintBook/Magazine/Serial` | `PrintBook/Magazine/Serial.cs` (SaveSerial) |
| `/admin/serial-issues` | `/api/PrintBook/Magazine/Serial` (`/SaveItem`,`/SearchReceipt`,`/Claim`) | `Serial.cs` (SaveSerialItem) |
| `/admin/serial-receipts` | `/api/PrintBook/Magazine/Serial` (`/Search`) | `SearchSerialReceipt.cs` |
| `/admin/inventory` | `/api/PrintBook/Store/Inventory` | `Store/Inventory.cs`, `InventoryBarcode.cs` |
| `/admin/lost-books` | `/api/PrintBook/Store/LostBook` | `Catalogue/Book.cs` (ProcessLossBook/SearchLostBook) |
| `/admin/liquidates` | `/api/PrintBook/Store/Liquidate` | `Catalogue/Book.cs` (LiquidateBook/SearchLiquidate) |
| `/admin/re-register-barcode` | `/api/PrintBook/Store/ReRegisterBarcode` | `Store/ReRegisterBarcode.cs` |
| `/admin/statistics-document` | `/api/PrintBook/Store/Statistics` | `Store/StatisticsDocument.cs` |
| `/admin/check-in-out` | `/api/PrintBook/Receiption/Check` | `Common/Reader.cs` (ReaderCheckIn/Out) |
| `/admin/checkin-history` | `/api/PrintBook/Receiption/Check` (`/SearchHistory`) | `Common/Reader.cs` (SearchReaderCheckIn) |
| `/admin/checkin-statistics` | `/api/PrintBook/Receiption/Statistics` | `Receiption/StatisticsCheckn.cs` |
| `/admin/receiption-report` | `/api/PrintBook/Receiption/Report` | `StatisticsCheckn.cs` (ReportReaderCheckInCount) |
| `/admin/borrow-keys` | `/api/PrintBook/Receiption/BorrowKey` | `Circulation/Loan/BorrowKey.cs` |
| `/admin/search-borrow-keys` | `/api/PrintBook/Receiption/BorrowKey` (`/Search`) | `BorrowKey.cs` (SearchBorrowKey) |
| `/admin/z3950-configs` | `/api/PrintBook/Opac/Z3950Config` | `Opac/Z3950Config.cs` |
| `/admin/z3950-search` | `/api/PrintBook/Opac/Z3950` | `Cataloging/Z3950SearchImport.cs` |
