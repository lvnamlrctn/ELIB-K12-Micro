# CHANGELOG — ELIB-K12

Các mục port từ ELIB-LRC ghi kèm ngày mục gốc bên LRC. Dòng **Tenant:** mô tả cách lọc theo đơn vị (LRC đã bỏ TenantId, K12 thì không).

## 2026-10-07

### Đợt 5e — Trang đặt phòng học nhóm theo sơ đồ tầng trên OPAC (port LRC 09-28, 09-29, 10-04)

- Trang mới **`/dat-phong-hoc-nhom`**: chọn tòa nhà / tầng / ngày; **sơ đồ tầng** tô màu từng phòng theo trạng thái (còn trống / đã có người đặt / đang sử dụng / không thể đặt; ngày hôm nay theo giờ hiện tại, ngày khác theo còn khung trống hay không); **bảng lịch** phòng × khung 30 phút với các khối lượt đặt (chờ duyệt / đã xác nhận / đang dùng, tên người khác đã che, "Lượt của bạn"). Chọn giờ: bấm ô bắt đầu rồi bấm ô giờ kết thúc (bấm lại ô bắt đầu = 1 khung; bấm khối lượt đặt = kết thúc đúng lúc lượt đó bắt đầu).
- Khung đặt: nội quy, thiết bị, số người hoặc thẻ thành viên nhóm (tra tên đã che, đủ số tối thiểu của phòng), ghi chú; phòng tạm ngưng / không thuộc đối tượng được đặt bị chặn kèm lý do. Đặt xong tải lại lịch, báo "đã duyệt" với phòng tự duyệt.
- Xem công khai, chưa đăng nhập vẫn xem lịch; gửi đặt thì chuyển tới đăng nhập. `GET api/public/RoomBooking/Rooms|Availability|FloorBoard` nay cho phép ẩn danh (Book/Check-in/Huỷ/… vẫn cần token bạn đọc).
- Lối vào: bấm phòng học nhóm trên **Sơ đồ thư viện** (mở sẵn đúng phòng, `?room=`), liên kết "Xem lịch theo sơ đồ tầng" ở Tài khoản → Đặt phòng học nhóm.

**Tenant:** người xem chưa đăng nhập dùng đơn vị của OPAC (tham số `tenantId` = APP_CONFIG, thiếu thì theo tên miền); đã đăng nhập thì luôn là đơn vị của bạn đọc. Lịch tầng chỉ có phòng/lượt đặt của đơn vị đó.

### Đợt 5d — Xác thực bạn đọc qua LDAP / API ngoài (port LRC 10-04)

- Đăng nhập OPAC (`ReaderAuthService`) xác thực được qua hệ thống của trường (`ExternalReaderAuth`): `Provider` = `Local` (mặc định, như cũ) / `Ldap` / `Api`; `FallbackToLocal` (mặc định bật) dùng mật khẩu nội bộ khi hệ thống ngoài lỗi hoặc sai mật khẩu; `MatchBy` = `Cardno` / `Email` để khớp tài khoản ngoài với hồ sơ bạn đọc.
- LDAP: tìm người dùng bằng tài khoản dịch vụ rồi bind lại bằng mật khẩu bạn đọc; giá trị bộ lọc thoát ký tự theo RFC 4515. API: POST JSON tên đăng nhập + mật khẩu, đọc trường lồng nhau (vd `data.cardNo`); 401/403 = sai mật khẩu, lỗi khác = không kết nối được.
- Kiểm tra cấu hình: `GET api/admin/reader-auth` (quyền `READERS` view), `POST api/admin/reader-auth/Test` (quyền `READERS` edit) — thử 1 tài khoản, không đăng nhập, không lưu gì.
- Gói mới `Novell.Directory.Ldap.NETStandard` 4.0.0 (thuần .NET, không cần thư viện hệ thống trong Docker).

**Tenant:** cấu hình theo từng đơn vị — tham số hệ thống `READER_AUTH_CONFIG` của đơn vị chứa JSON cùng cấu trúc mục `ReaderAuth` (có hoặc không bọc `"ReaderAuth"`); đơn vị không khai báo dùng `ReaderAuth` trong appsettings; JSON lỗi thì coi như Local. Hồ sơ bạn đọc chỉ khớp trong đơn vị của OPAC đang đăng nhập.

**Sửa lỗi:** đăng nhập bạn đọc tra số thẻ trên mọi đơn vị rồi mới kiểm tra đơn vị — 2 trường trùng số thẻ thì bạn đọc trường thứ hai có thể bị báo "Tài khoản không thuộc đơn vị này". Nay tra trong đúng đơn vị (ưu tiên bản ghi chưa xoá).

### Đợt 5c — Check-in phòng học nhóm bằng nhận diện khuôn mặt (port LRC 09-30, 10-04)

- **Kiosk/thủ thư** (hộp thoại "Quét QR check-in", nút mới **Nhận diện khuôn mặt**): camera tự chụp 2,5 giây/lần; ảnh chỉ so với bạn đọc (người đặt + thành viên nhóm) có lượt **đã duyệt đang trong khung check-in** (từ 15 phút trước giờ bắt đầu tới hết ân hạn của phòng), đúng phòng kiosk nếu có chọn. Nhận ra thì check-in lượt sớm nhất (ưu tiên đúng phòng) và hiện cùng thẻ kết quả như quét QR; 4 giây sau camera tự bật lại. API `PUT api/Map/RoomBookingAdmin/CheckInByFace` (quyền `STUDY_ROOM_BOOKING` edit).
- **OPAC** (Tài khoản → Đặt phòng học nhóm, nút **Check-in bằng khuôn mặt** cạnh Check-in): bạn đọc bấm "Chụp & xác minh" (không tự chụp — số lượt gọi AI có giới hạn); ảnh chỉ so với ảnh khuôn mặt của chính bạn đọc (`Reader.Photo` + ảnh bổ sung). Trạng thái/giờ kiểm tra trước nên lượt chưa tới giờ không tốn lượt gọi AI; tài khoản chưa có ảnh được báo rõ; không khớp thì hiện lý do và camera vẫn mở để thử lại. API `POST api/public/RoomBooking/CheckInByFace/{publicId}` (giới hạn 12 lần/phút/IP).
- `FaceRecognitionService.IdentifyReaderAsync` nhận thêm danh sách bạn đọc ứng viên (tuỳ chọn; rỗng thì không gọi AI) và `HasFacePhotoAsync`. Màn Vào/ra và Mượn/trả vẫn quét toàn bộ bạn đọc của đơn vị như cũ. Ảnh gửi lên thu nhỏ còn rộng tối đa 640px; chấp nhận cả dạng `data:image/...;base64,`.
- Component camera `app-face-capture` dùng chung thêm chế độ `submitFn`/`manual`; lớp phủ camera nằm trên hộp thoại quét check-in.

**Tenant:** ứng viên kiosk chỉ gồm lượt đặt thuộc đơn vị của thủ thư (lọc JWT), ân hạn đọc theo cấu hình phòng của đơn vị sở hữu lượt; bạn đọc tự check-in chỉ trong đơn vị của mình và ảnh so khớp giới hạn trong đơn vị đó.

Cần cấu hình Gemini (như nhận diện ở quầy vào/ra) và ảnh khuôn mặt của bạn đọc.

### Đợt 5b — Hỏi đáp AI trong trang đọc, trợ lý tìm tài liệu trên OPAC (port LRC 09-29)

**Hỏi đáp trong trang đọc** (panel "Hỏi đáp tài liệu này", `ChatService`):
- API mới `POST api/public/chat/ask-stream` (SSE: `sources` → `delta` → `done`/`error`). Panel hiện chữ dần, có nút Dừng, gợi ý câu hỏi, hỏi nối tiếp (gửi kèm lịch sử), nguồn theo số trang — bấm "Trang N" thì trình đọc lật tới trang đó.
- Trong 1 tài liệu chỉ còn 1 lượt gọi Gemini (trước là 2); yêu cầu tóm tắt nhận diện bằng quy tắc cục bộ. Tìm lai kNN + BM25 (`RetrieveKeywordChunksAsync`) gộp bằng Reciprocal Rank Fusion, bắt đúng tên riêng/con số.
- Máy chủ embedding lỗi: quá 5 giây thì bỏ qua embedding 1 phút, vẫn trả lời bằng tìm theo từ khóa. `EbookIndexingJob` dừng embedding từ chunk lỗi đầu tiên thay vì chờ timeout từng chunk.
- Tóm tắt giới hạn `RagSettings:SummaryMaxChars` (mặc định 60.000 ký tự, trang rải đều), cache 24 giờ theo tài liệu; câu trả lời lặp lại cache 30 phút; nguồn tối đa 5 trang khác nhau; tài liệu chưa lập chỉ mục nội dung thì báo rõ, không gọi Gemini.

**Trợ lý tìm tài liệu** (nút chat nổi OPAC, `DocumentFinderService`): nút chat nổi nay gọi `chat/find-documents` như các OPAC của LRC (trước đây gọi `chat/ask` — RAG nội dung toàn kho): câu dẫn ngắn + thẻ tài liệu (ảnh bìa, tác giả, năm, số bản còn của sách in) bấm vào trang chi tiết, "Xem tất cả" sang trang Tra cứu. Hiểu câu nối tiếp (4 lượt), cache tiêu chí 30 phút, cache danh mục chủ đề/môn học 10 phút, khớp danh mục ưu tiên tên đầy đủ/dài nhất, Gemini lỗi thì bỏ cụm rào đón khỏi từ khóa, tìm Elasticsearch chế độ nhẹ (`UnifiedSearchRequest.LightMode`: bỏ facet/highlight/dfs). Timeout 30 giây, báo riêng lỗi 429.

**Sửa lỗi:**
- `CHATBOT_API_BASE` để cứng `http://localhost:5101` — trên máy bạn đọc, chat trong trang đọc và chat nổi luôn lỗi kết nối. Nay gọi đường dẫn tương đối.
- Gợi ý theo hồ sơ/chủ đề của trợ lý tìm tài liệu không lọc gì: `TopicId`/`SubjectId` đã khớp nhưng không đưa xuống tìm kiếm. Nay lọc `topicId`/`subjectId` trên index gộp.

**Tenant:** chat thiếu `tenantId` trước đây truy xuất nội dung của mọi đơn vị — nay lấy đơn vị theo tên miền (`TenantContextMiddleware`) khi client không gửi. kNN, BM25 và tóm tắt chỉ lấy chunk của đơn vị hoặc dùng chung; khoá cache câu trả lời/tóm tắt/tiêu chí/danh mục có đơn vị.

### Đợt 5a — Thanh toán QR VietQR / VNPAY cho phí phạt, sao chụp, cấp lại thẻ (port LRC 09-25, 09-26)

**Thủ thư:** nút **Tạo mã QR thanh toán** ở chi tiết phiếu phạt và từng dòng phí sao chụp; nút **Thu phí cấp lại thẻ** ở danh sách bạn đọc (số tiền = tham số `PHI_CAP_LAI_THE` của đơn vị, chấp nhận "50.000"). Hộp thoại sinh QR, đếm ngược 15 phút, tự báo khi tiền về.

**Bạn đọc (OPAC):** tab **Phí / Nợ của tôi** ở trang tài khoản — liệt kê phiếu phạt và phí sao chụp chưa trả, thanh toán QR từng khoản. Phí cấp lại thẻ chỉ thu tại quầy.

**Đối soát tự động:** VNPAY qua IPN `GET api/public/payment-webhook/vnpay`; VietQR qua webhook Sepay `POST api/public/payment-webhook/sepay`. Tiền về thì phiếu phạt chuyển Đã hoàn thành (đủ tiền nộp, mọi dòng phạt có ngày thu), phí sao chụp chuyển Đã thanh toán. Job `payment-expiry` (5 phút) chuyển QR quá hạn sang Hết hạn. Bảng mới `payment.PaymentTransaction` tạo lúc khởi động (`PaymentSchema`, bản tay `backend/scripts/payment-postgresql.sql`).

**Tenant:** mỗi đơn vị một tài khoản nhận tiền.
- Giao dịch mang `TenantId` của bạn đọc. Cấu hình đọc theo đơn vị, thiếu thì dùng mục `VietQr` / `VnPay` / `Sepay` trong appsettings: `PAYMENT_VIETQR_BANK_BIN`, `PAYMENT_VIETQR_ACCOUNT_NO`, `PAYMENT_VIETQR_ACCOUNT_NAME`, `PAYMENT_VNPAY_TMN_CODE`, `PAYMENT_VNPAY_HASH_SECRET`, `PAYMENT_VNPAY_RETURN_URL`, `PAYMENT_SEPAY_APIKEY` (khai báo ở trang Tham số hệ thống của đơn vị). Mỗi nhóm lấy trọn từ 1 nguồn, không trộn tài khoản đơn vị với cấu hình chung.
- Hộp thoại chỉ hiện cổng đơn vị đã cấu hình; chưa cấu hình thì báo thanh toán tại quầy.
- Thủ thư chỉ tạo/xem giao dịch của bạn đọc cùng đơn vị. Quyền theo loại phí (`FINES`, `C_PHOTO`, `READERS`) thay cho mã `PAYMENT` riêng của LRC — không phải cấp thêm quyền.
- VNPAY IPN kiểm chữ ký bằng `HashSecret` của đơn vị sở hữu giao dịch. Sepay: `POST …/sepay` dùng khoá chung; `POST …/sepay/{tenantPublicId}` dùng khoá riêng của đơn vị và chỉ khớp giao dịch của đơn vị đó; tiền vào sai số tài khoản của đơn vị thì bỏ qua.

**Sửa so với LRC:** IPN VNPAY đối chiếu `vnp_Amount` với giao dịch (LRC không kiểm số tiền) và trả `02` khi đã xử lý; so chữ ký/khoá bằng phép so thời gian cố định; một khoản phí chỉ có 1 QR đang chờ (tạo lại thì trả QR cũ còn hạn) — tránh 2 giao dịch cùng gạch 1 phiếu; nội dung chuyển khoản được bỏ dấu cách/ký tự lạ trước khi khớp mã; hạn QR trả về dạng UTC nên đếm ngược đúng ở mọi múi giờ; tạo QR từ OPAC giới hạn 10 lần/phút.

**Khác:** nút **Thu phạt** từng khoản dùng chung `FineSettlementService` với cổng thanh toán. `RoomBookingParams` chuyển thành `TenantParams` dùng chung (đọc tham số của đơn vị, thiếu thì dòng dùng chung).

### Đợt 4 — Đặt phòng học nhóm nâng cao (port LRC 09-28, 09-29, 10-04 đợt A–D)

**Sửa lỗi giờ:** giờ đặt lưu theo UTC (OPAC gửi ISO có "Z") nhưng đặt, check-in, huỷ và job hết hạn so với `DateTime.Now`. Máy chủ chạy UTC+7 thì mọi lượt trong 7 giờ tới bị báo "thời điểm đã qua", và job chuyển vắng mặt/từ chối lệch 7 giờ. Nay so bằng `DateTime.UtcNow`, giờ gửi lên quy về UTC (`RoomBookingHours.ToUtc`), ngày tính theo giờ thư viện. Email đặt phòng in giờ thư viện (trước đây in giờ UTC), giá trị chèn vào mẫu được mã hoá HTML.

**Quy định khi đặt** (`RoomBookingRepository`):
- Giờ đặt theo mốc 30 phút, trong cùng 1 ngày, trong giờ mở cửa.
- Một bạn đọc (kể cả thành viên nhóm) không giữ 2 phòng cùng khung giờ.
- Tham số mới, sửa ở tab **Tham số**: lượt/ngày, lượt sắp tới cùng lúc, thời lượng tối đa mỗi phiên. Mặc định không giới hạn.
- Cấu hình phòng thêm: loại phòng **Tự động duyệt**, đặt trước tối đa theo giờ, nội quy, đối tượng được đặt (loại bạn đọc), số người tối thiểu.
- Chỉ huỷ được trước giờ bắt đầu (trước đây huỷ được trong lúc chờ job, nên vắng mặt không bị tính).
- Check-in chỉ mở từ 15 phút trước giờ bắt đầu tới hết ân hạn của phòng (trước đây tới hết giờ đặt).

**Trả phòng** (bạn đọc ở OPAC, thủ thư ở danh sách): lượt đang dùng chuyển Hoàn tất, phòng mở lại ngay.

**Đặt theo nhóm:** bạn đọc nhập thẻ thành viên (số thẻ hoặc UID); số người = thành viên + người đặt. Thành viên phải còn hạn, không bị khoá đặt phòng, không đang giữ phòng khác cùng giờ. Bảng mới `map.RoomBookingMember`.

**Vắng mặt và danh sách chặn:**
- Job hết hạn chạy 5 phút/lần (trước đây mỗi giờ).
- Vắng mặt quá `ROOM_BOOKING_NOSHOW_LIMIT` lần trong N ngày thì tự khoá đặt phòng. Tab **Danh sách chặn**: xem, gỡ, khoá tay. Bảng mới `map.RoomBookingBan`.

**Giờ mở cửa** (tab mới): giờ theo thứ cho từng loại cơ sở (`map.RoomOpeningHour`) và ngày đặc biệt / đóng cửa đột xuất (`map.RoomSpecialDay`). Khi lưu, màn hình xem trước các lượt bị ảnh hưởng, có thể huỷ và báo bạn đọc.

**Tạm ngưng phòng** (nút ở trang Cấu hình phòng): xem trước lượt sắp tới, có thể huỷ và báo bạn đọc; phòng tạm ngưng không nhận đặt mới.

**Thẻ chip:** `Reader.CardUid` — ô "UID thẻ chip" trên form bạn đọc, cột ẩn ở danh sách, cột "UID thẻ" khi nhập Excel. Chuẩn hoá (bỏ khoảng trắng, `:`, `-`, viết hoa).

**Kiểm soát cửa** (tab mới; API cho phần mềm của hãng thiết bị):
- Khai báo thiết bị (mã cửa → phòng), khoá API riêng (chỉ lưu SHA-256), thẻ quản trị.
- `POST api/access/Verify` (header `X-Device-Code`, `X-Device-Key`): đối chiếu UID với lịch đặt; quẹt hợp lệ đầu tiên tự check-in.
- Thủ thư mở cửa từ web (lệnh lấy qua `GET api/access/Commands`, hết hạn sau 2 phút). Nhật ký quẹt thẻ.
- Bảng mới `map.AccessDevice`, `AccessStaffCard`, `AccessScanLog`, `AccessCommand`.

**Thủ thư (màn Đặt phòng học nhóm):**
- Danh sách thêm bộ lọc (bạn đọc/số thẻ gồm cả thành viên, khoảng ngày, phòng, loại cơ sở, trạng thái Vắng mặt), xuất Excel (tối đa 20.000 dòng), trả phòng, mở cửa.
- **Quét QR check-in:** máy quét USB, nhập tay hoặc camera (`jsqr`); chế độ kiosk theo phòng.
- Tab **Sơ đồ** (lịch tầng, không che tên), **Báo cáo** (theo khoảng ngày, xuất Excel: tỷ lệ vắng mặt/huỷ, tỷ lệ sử dụng theo phòng, giờ cao điểm, bạn đọc dùng nhiều), **Mẫu thông báo** (7 sự kiện, sửa/xem trước/gửi thử/khôi phục).

**OPAC (Hồ sơ → Đặt phòng):**
- Lưới giờ theo giờ mở cửa thật của phòng trong ngày; `Availability` trả thêm `openTime/closeTime/closed`.
- Hiện nội quy, loại duyệt, phòng tạm ngưng, đối tượng; nhập thẻ thành viên.
- Thêm nút Mã QR check-in (tự làm mới 5 giây/lần) và Trả phòng.
- Ẩn Huỷ sau giờ bắt đầu và Check-in sau ân hạn.

**Sửa kèm:** ô "Số CCCD" trên form bạn đọc trước đây không được lưu (chỉ nhập Excel mới lưu).

**CSDL:** chỉ thêm cột/bảng/index, chạy lại an toàn. Backend tự áp lúc khởi động (`RoomBookingSchema.PostgreSql`); bản áp tay ở `backend/scripts/room-booking-rules-postgresql.sql`.

**Tenant:**
- Mọi bảng mới có `TenantId`.
- Lượt đặt, thành viên, lệnh khoá mang đơn vị của bạn đọc. Phía OPAC lấy đơn vị từ `Reader.TenantId`.
- Thành viên nhóm và tra thẻ chỉ trong cùng đơn vị. `Rooms`/`FloorBoard`/`Member` vẫn bắt đăng nhập bạn đọc như K12 cũ (LRC cho xem ẩn danh).
- Tham số, giờ theo thứ, ngày đặc biệt, mẫu thông báo là **cấu hình theo đơn vị**: dòng của đơn vị thắng dòng dùng chung (`TenantId` null). Tài khoản hệ thống không chọn đơn vị thì sửa bản dùng chung. LRC chỉ có 1 bộ cấu hình toàn hệ thống.
- Job hết hạn xử lý theo từng đơn vị (cờ `ROOM_BOOKING_ENABLED`, ân hạn, ngưỡng khoá); trước đây đọc 1 cờ chung.
- Thiết bị cửa thuộc đơn vị của phòng nó mở. API thiết bị suy ra đơn vị từ thiết bị đã xác thực, chỉ đối chiếu bạn đọc, lượt đặt, thẻ quản trị của đơn vị đó. Thẻ quản trị dùng chung (do tài khoản hệ thống tạo) mở mọi cửa.
- UID thẻ chỉ cần duy nhất trong 1 đơn vị (bạn đọc, thẻ quản trị). Mã thiết bị duy nhất toàn hệ thống.
- Màn thủ thư: danh sách, duyệt, check-in QR, trả phòng, xuất Excel lọc theo đơn vị JWT. Các tab còn lại nhận `tenantId` (Guid) cho tài khoản đặc quyền; ô "Đơn vị" đầu trang áp cho mọi tab. Lịch tầng chỉ cho tầng thuộc phạm vi. Báo cáo tính giờ mở cửa theo đơn vị của từng phòng.

**Gộp với K12:** giữ trang Cấu hình phòng riêng (`/admin/room-booking-config`, thêm trường mới và nút tạm ngưng) thay cho tab "Cấu hình phòng" của LRC; thêm `admin/room-booking-config → STUDY_ROOM_BOOKING` vào bảng mã quyền.

**Không port:** check-in bằng khuôn mặt, xác thực bạn đọc qua LDAP/API, trang đặt phòng theo sơ đồ của opac-lrc.

**Gói mới (frontend):** `jsqr`, `qrcode` (+ `@types/qrcode`).

### Đợt 3c — AI chat: Gemini client, chat thống kê admin (port LRC 09-29, 09-30)

**`GeminiClient`** (dùng chung cho mọi chat)
- **Bảo mật:** trước đây mỗi lượt tạo `HttpClient` mới, **tắt kiểm tra chứng chỉ TLS** và gửi API key trên URL. Nay dùng 1 named client (`IHttpClientFactory`), bật lại kiểm tra TLS (chỉ tắt được bằng `LLMSettings:SkipCertificateValidation=true`), key gửi qua header `x-goog-api-key`.
- Thử lại 1 lần khi 429/503; log độ trễ + số token; lấy đúng part `functionCall` dù đứng sau text, bỏ phần "thought". Thêm `StreamTextAsync` (SSE) và `GeminiGeneration.Config`.

**Chat thống kê admin** (`AdminStatChatService`)
- **Sửa số liệu sai:** "lượt mượn sách in" trước đây đếm bảng vào/ra thư viện (`CheckOuts`); bản "sẵn sàng" lọc `"1"/"Available"` nên luôn ra 0; lượt mượn đọc tài liệu số "đang hiệu lực" tính cả lượt hết hạn. Nay theo `BookOuts`/`BookIns` như báo cáo lưu thông, mã `R`, loại lượt hết hạn.
- **Số liệu mới:** đang mượn, quá hạn, tiền phạt, lượt vào thư viện, thẻ hết hạn; theo kỳ: lượt trả, trả quá hạn, phiếu/tiền phạt, lượt vào.
- **Công cụ `get_list`:** liệt kê tài liệu đang mượn, quá hạn, bạn đọc quá hạn, sách in mượn nhiều, tài liệu số đọc nhiều (mặc định 20, tối đa 50 dòng). Bảng dựng thẳng từ CSDL, không qua Gemini lượt 2.
- Ngày "hôm nay/tuần này…" theo giờ thư viện; cache số liệu tổng 5 phút, quyền 2 phút; phân hệ lỗi truy vấn vẫn được nêu tên; controller không còn trả `ex.Message`.
- Hỏi nối tiếp (gửi kèm tối đa 6 lượt trước); **API mới** `POST api/Cms/AdminStatChat/ask-stream` (SSE). Giao diện: chữ hiện dần, Markdown, nút Dừng; nút chat chỉ hiện khi có quyền `STAT_CHAT`.
- **Rate limit:** chat admin 30 câu/phút/tài khoản; chat công khai (`PublicChat/ask`, `find-documents`) 12 câu/phút/IP — trước đây không giới hạn, ai cũng gọi được và tốn phí Gemini.
- **Tenant:** mọi số liệu và danh sách chỉ trong đơn vị của người hỏi (JWT; tài khoản hệ thống xem toàn bộ) — qua accessor có phạm vi trên từng bảng; ĐKCB join theo (mã, đơn vị); cache tách theo đơn vị. Model không tự chọn đơn vị. Kiểm tra quyền qua `HasPermissionAsync` (giữ bypass role toàn quyền).
- **Chưa port:** tối ưu chat tìm tài liệu OPAC (`DocumentFinderService`) và chat hỏi đáp trong trang đọc (`ChatService` — RAG lai, SSE, tóm tắt) — đều thuộc OPAC.

### Đợt 3b — Menu CMS: liên kết an toàn, menu di trú sửa được (port LRC 10-04)
- Liên kết menu chỉ nhận `http(s)://`, đường dẫn bắt đầu bằng `/` (không phải `//host`) hoặc từ khoá trang. Trước đây nhận mọi chuỗi, kể cả `javascript:…`, mà cổng OPAC hiển thị làm `href`.
- **Sửa bất kỳ menu di trú nào cũng bị báo "LinkType không hợp lệ"** (dữ liệu cũ dùng `OuterLink`/`CategoryLink`/`LinkPage`): `OuterLink` hiểu là `external`; `CategoryLink`/`LinkPage` giữ nguyên liên kết cũ.
- Liên kết chuyên mục / bộ sưu tập nay kiểm tra tồn tại (như trang).
- **Tenant:** chuyên mục / bộ sưu tập / trang được trỏ tới phải thuộc đơn vị người sửa hoặc dùng chung (trước đây trỏ được tới dữ liệu đơn vị khác).

### Đợt 3a — Tin tức: quản lý file đính kèm (port LRC 10-04)

**Không đổi schema** (`cms.AttachFile`): `Name` = tên hiển thị, `Url` = tên object trong kho riêng tư MinIO, `FileSize` = KB, `CreatedDate` = ngày tải lên.

**Admin:** danh sách tin có nút "File đính kèm" → trang `/admin/news-attachments/:publicId`.
- Tải lên nhiều file, có tiến độ; tối đa 50 MB/file; chỉ nhận định dạng thông dụng (pdf, office, odf, txt/csv/rtf, zip/rar/7z, ảnh, mp3/mp4) — chặn exe, html, svg, js, chm…
- Xem/tải về qua API (không lộ URL kho), đổi tên (giữ đuôi), xoá (xoá mềm dòng + xoá object).
- File của hệ thống cũ (`Upload/...`) hiện "Chưa đồng bộ"; nút "Đồng bộ file cũ" (`POST api/Cms/AttachFile/SyncFiles`) chép từ `PathSettings:PathAttachment` lên kho. **Chưa chạy**; chưa cấu hình thư mục thì trả 400; đường dẫn chứa `../` không đọc ra ngoài thư mục cấu hình.
- API `api/Cms/AttachFile`: `Upload`, `Download/{id}`, `Rename/{id}`, `Delete/{id}`, `SyncFiles`; `SearchAll` lọc được theo `newsPublicId`.

**OPAC:** chi tiết tin có khối "Tệp đính kèm" (`GET api/public/PublicNews/Attachments?newsId=`, `GET .../Attachment/{id}` — chỉ tin đã xuất bản, giới hạn tần suất như tra cứu OPAC).

**Tenant:**
- Chỉ đính kèm vào tin của đơn vị mình; file gắn đơn vị của tin (kể cả khi tài khoản hệ thống tải lên). Xem/đổi tên/xoá theo phạm vi đơn vị của repository.
- API công khai nhận đơn vị theo tên miền (`PublicHostTenantFilter`): chỉ file của tin thuộc đơn vị đó hoặc tin dùng chung.
- "Đồng bộ file cũ" chỉ xử lý file của đơn vị người bấm (tài khoản hệ thống: mọi đơn vị). LRC đồng bộ toàn bộ.
- Thêm `admin/news-attachments → NEWS_MANAGE` vào bảng mã quyền (Đợt 2).

### Đợt 2 — Phân quyền theo mã quyền, JWT mang claim quyền (port LRC 09-26, 09-27)

**Giai đoạn 1 — frontend tra quyền theo mã quyền**
- Sidebar và `*appCan` tra theo **mã quyền** (`ModuleCode`, đúng mã backend kiểm tra qua `[Permission]`) thay cho `cms.Module.Link`. Bảng route → mã ở `frontend/src/app/services/system/admin-perm-codes.ts` (111 route: các mục có link trong `ModuleCatalog.cs` + 17 mục đã đối chiếu với controller K12).
- **Khác LRC:** route chưa khai trong bảng vẫn khớp theo `cms.Module.Link` như cũ (`MyPermission` trả kèm `link`). K12 có module riêng (đơn vị, cài đặt, kênh/nhật ký thông báo, cấu hình đặt phòng, quản lý mượn tài liệu số, tra cứu đơn nhận) và 4 trang mà mã trong DB không trùng mã controller kiểm tra (`Z3950_SEARCH`, `SERIAL_RECEIPTS`, `CHECKIN_HISTORY`, `SEARCH_BORROW_KEYS`) — chưa đối chiếu được với `cms.Module` thật nên giữ nguyên hành vi.
- **Sửa lỗi:** quyền của chính user trước đây tải qua `Users/GetPermission/{id}` — endpoint đòi quyền module `USERS`, nên cán bộ không có quyền đó không tải được quyền của mình. Nay dùng `Users/MyPermission` (chỉ cần đăng nhập). Bỏ lời gọi `Module/GetTree` lúc tải quyền.
- Thêm `appCanCode` (`*appCan="'edit'; code: 'READERS'"`) và `perm` cho mục menu để chỉ định thẳng mã quyền.

**Giai đoạn 2 — JWT mang claim quyền + `PermissionStamp`**
- Lúc đăng nhập, JWT có thêm `perm` (`*` nếu role toàn quyền, hoặc `MÃ:view,add,...;...`), `pstamp` (= `Users.PermissionStamp`) và `ro` (tài khoản bị chặn ghi).
- `PermissionAttribute`/`PermissionAnyAttribute` quyết định từ claim bằng 1 query theo khoá chính khi `pstamp` còn khớp; mọi trường hợp không chắc chắn rơi về `HasPermissionAsync` như trước (trước đây 3–5 query có join mỗi request).
- **Khác LRC (an toàn hơn):** mã quyền có nhiều dòng `cms.Module` hoặc nhiều dòng `cms.Permission` không đưa vào claim; mã không có trong claim đi đường DB (LRC trả "không có quyền"). So mã phân biệt hoa thường như truy vấn DB.
- Stamp đổi khi lưu quyền hoặc đổi role của user — quyền mới có hiệu lực ngay ở request kế tiếp. **Thêm so với LRC:** sửa/xoá/ẩn module, đồng bộ module, sửa/xoá role (`[InvalidatePermissionStamps]`) đặt stamp mọi user về `null`, mọi token đang lưu hành đi đường DB tới lần đăng nhập sau.
- Cột mới `Users.PermissionStamp` (nullable): Postgres tự thêm khi khởi động (`ADD COLUMN IF NOT EXISTS`); SQL Server chạy `backend/scripts/add-user-permission-stamp-sqlserver.sql` trước khi deploy.
- **Tenant:** claim `TenantId`/`TenantCode`/`RoleCode` giữ nguyên; tài khoản đặc quyền vẫn bỏ qua lọc đơn vị như cũ. Cập nhật `backend/CLAUDE.md` (còn ghi `DepartmentId`) và `docs/AUTH_PERMISSION.md`.

## 2026-10-06

### Vào/ra thư viện, mượn chìa khoá tủ (port LRC 10-04)

Sửa trong controller hiện có (đã lọc đơn vị); phần tra thẻ dùng chung `ReaderCards`, lượt vào cửa dùng chung `ReaderVisits`, khoảng ngày dùng `DateRange`.

**Vào/ra thư viện**
- **Lịch sử bỏ sót người chưa quét ra:** trước đây chỉ đọc lượt đã ra. Nay gồm cả lượt đang mở, cột Ra hiện "Đang ở trong" (Excel ghi "(đang ở trong)"); xoá được cả lượt đang mở.
- Lọc "đến ngày" tính trọn ngày cuối.
- Bộ lọc "Kho" ở trang lịch sử thành "Điểm lưu thông" (dữ liệu vào/ra lưu mã điểm lưu thông). Backend nhận `circPlaceId`, vẫn nhận `storeId`.
- Quét vào 2 lần không còn tạo 2 lượt đang mở; tra thẻ lấy lượt mới nhất.
- Tài liệu đang mượn khi tra thẻ theo `PrintLoans.Open` (không tính phiếu đã trả kiểu cũ); nhan đề lấy trong 1 truy vấn.
- Thẻ rỗng trả 400 thay vì 500.

**Mượn chìa khoá tủ**
- Một ngăn không còn cho 2 bạn đọc mượn cùng lúc: báo ngăn đang do thẻ nào giữ. Quét lặp trả lại lượt hiện có.
- Mã ngăn không tồn tại trả 404 (trước đây tạo lượt mượn không gắn ngăn và báo thành công); thiếu mã ngăn trả 400.
- Trả khoá theo mã ngăn ưu tiên lượt của thẻ đang tra; giữ ghi chú lúc mượn.
- Trang tra cứu mượn/trả lọc được theo số thẻ, tên, từ ngày, đến ngày (trước đây bỏ qua mọi bộ lọc) và trả đủ số thẻ, họ tên, trạng thái.
- Frontend hiện đúng thông báo lỗi của máy chủ khi mượn/trả.

**Tenant:** lượt vào/ra và lượt mượn khoá gắn đơn vị của bạn đọc (trước đây gắn đơn vị JWT — tài khoản hệ thống ghi `null`); lượt ra/trả theo đơn vị của lượt vào/mượn. Ngăn tủ phải cùng đơn vị với bạn đọc. Lịch sử theo phạm vi đơn vị gồm cả lượt đang mở.

### Báo tạp chí: kỳ ấn phẩm, đóng tập, báo cáo, mẫu kỳ (port LRC 10-03, 10-04)

**Kỳ ấn phẩm** (`ISerialIssueService`; `MagazineSerialController` còn khoảng 110 dòng, đường dẫn giữ nguyên)
- **Màn "Nhận kỳ" không khớp tên trường với API:** giao diện gửi `subscriptionId`, `serialSeqX`, `plannedDate`…, API nhận `SUBSCRIPTION_ID`… nên danh sách kỳ trống số kỳ/ngày, còn nhận kỳ/sửa/ghép số tạo **bản ghi mới không gắn đăng ký**. Nay `SaveItem` nhận `SerialIssueSaveRequest`; có `id`/`publicId` thì sửa đúng kỳ đó; nhập đúng số của kỳ dự kiến thì cập nhật kỳ đó. Các API kỳ trả `SerialIssueView` camelCase.
- Xoá kỳ theo `DeleteItem/{publicId}` (trước đây chỉ có route theo Id số nên luôn 404).
- Không khiếu nại được kỳ đã nhận.
- **Sinh kỳ dự kiến:** tần suất "n số mỗi tháng/quý/năm" không còn làm mọi kỳ trùng một ngày; tính thẳng từ mốc (không trôi ngày cuối tháng); kỳ đầu đúng "ngày phát hành kỳ đầu"; tối đa 366 kỳ.
- Ngày nhận gửi lên luôn được lưu; thống kê "trễ" so theo ngày; "kỳ nhận gần nhất" bỏ kỳ không có ngày nhận.
- **Đăng ký:** số bắt đầu (`StartX/Y/Z`) nay được lưu (thêm cột `PrintBook.Serial`, Postgres tự thêm khi khởi động bằng `ADD COLUMN IF NOT EXISTS`; SQL Server cần thêm cột tay); sửa đăng ký không còn xoá `LastX/Y/Z` (đánh số lại từ đầu); đầu báo (`BibId`) được lưu.

**Đóng tập** (`ISerialBindingService`)
- Mở sửa một tập không còn thấy form trống / lưu lại xoá trắng dữ liệu (`GetById` trả dạng phẳng).
- Nhãn gáy tập và cột Kho có dữ liệu (kết quả kèm danh sách số và tên kho).
- Một số báo chỉ đóng được vào một tập; số ĐKCB tập không trùng; chỉ đóng số đã nhận, cùng một đơn đặt; nhãn kỳ/ngày nhận lấy từ CSDL. Thêm tập trong 1 transaction.
- Tìm kiếm giữ thứ tự sắp xếp, không phân biệt hoa thường, tìm cả nhan đề đơn đặt. Lỗi lưu/xoá hiện thông báo của máy chủ.

**Báo cáo** (`ISerialReportService`)
- Các cột trên màn báo cáo không còn trống (API trả đúng tên trường); ISSN lấy từ MARC 022$a; tổng hợp tách "Số kỳ nhận" và "Số lượng".
- Ô "Mã hoặc nhan đề đơn đặt" lọc thật (số = mã, chữ = nhan đề).
- Báo cáo "nhận" lọc theo ngày nhận; ngày "đến" tính trọn ngày.
- Danh mục đòi số thiếu: bỏ số đã nhận về, thêm số quá hạn chưa về và số đánh dấu "Thiếu"; hiện ngày dự kiến và trạng thái.

**Mẫu kỳ:** `SaveDetail` với mẫu không tồn tại/đã xoá trả 404 (trước đây tạo dòng chi tiết mồ côi).

**Tenant:**
- Trước đây chỉ danh sách đăng ký/kỳ/tập lọc đơn vị. Duyệt, nhận/sửa, khiếu nại, sinh kỳ, thống kê, xoá kỳ, xem/sửa/xoá tập thao tác được trên dữ liệu đơn vị khác theo Id; **3 báo cáo gồm mọi đơn vị**. Nay đều theo phạm vi đơn vị.
- Kỳ mới và tập mới gắn đơn vị của đăng ký (kể cả khi tài khoản hệ thống thao tác). Số ĐKCB tập duy nhất trong đơn vị.
- Mẫu kỳ: đơn vị thấy mẫu của mình và mẫu dùng chung; chỉ sửa chi tiết đánh số của mẫu thuộc đơn vị mình.

### Bổ sung: báo cáo bổ sung, đơn phân bổ (port LRC 10-03)

**Báo cáo bổ sung**
- Ngày "đến" (ngày nhập, ngày tạo đơn, thư mục sách mới) tính trọn ngày. Giao diện gửi `yyyy-MM-dd` (0 giờ) nên trước đây bản ghi có giờ trong chính ngày "đến" bị loại.
- Danh sách bổ sung trên màn hình lọc theo kho đã chọn (trước đây bỏ qua).
- Thư mục sách mới sắp theo ngày biên mục (`CreatedTime`, thiếu thì `CreatedRowDate`); biểu ghi không có ngày xếp cuối.
- Phân trang: `PageSize` 1–500, thêm tiêu chí phụ theo Id. Xuất Excel tối đa 50.000 dòng.
- **Tenant — lỗ hổng riêng của K12:** trước đây chỉ Sổ ĐKCB lọc đơn vị. Danh sách bổ sung, phân bổ kho, thư mục sách mới và 4 bản in lấy đơn nhận / ĐKCB / biểu ghi của **mọi đơn vị**. Nay mọi báo cáo theo phạm vi đơn vị (`TenantScopeHelper.ResolveScopeAsync`; tài khoản đặc quyền chọn đơn vị qua `tenantId`).

**Đơn phân bổ**
- Thêm sách vào đơn (`AddLine`/`AddLines` dùng chung logic, lưu 1 lần):
  - từ chối cả lô khi có ĐKCB đã nằm ở đơn phân bổ khác (nêu số ĐKCB). Trước đây 2 người cùng chọn 1 cuốn thì 1 ĐKCB nằm ở 2 đơn;
  - từ chối ĐKCB không tồn tại, đã xoá hoặc khác đơn vị (trước đây `AddLines` bỏ qua im lặng).
- **Ký nhận** chỉ chuyển ĐKCB "chưa xếp giá" (`I` hoặc trống) sang `R`. Trước đây mọi ĐKCB khác `R` trong đơn đều bị đặt `R`, kể cả bản đang mượn, mất, thanh lý.
- **Tenant:** ký nhận đổi ĐKCB theo đơn vị của đơn phân bổ (không theo người ký — tài khoản hệ thống trước đây đổi được ĐKCB mọi đơn vị trùng Id). Tìm sách để phân bổ chỉ lấy ĐKCB cùng đơn vị với đơn nhận.

### Z39.50 (port LRC 09-29, 10-04)
- **Giới hạn 1.000 kết quả** mỗi thư viện (`Z3950SearchService.MaxResults`), cho cả Internal/SRU/BER. Kết quả có cờ `truncated`; trang nằm ngoài giới hạn trả rỗng mà không kéo dữ liệu.
- **Phân trang BER rơi bản ghi** (mỗi trang chỉ 2–9 bản ghi): `ReadPdu` dừng khi `DataAvailable` tạm bằng false. Nay đọc trọn 1 PDU theo độ dài BER (`MeasureTlv`, hỗ trợ độ dài bất định).
- Máy chủ từ chối trả bản ghi (vd LOC mã Bib-1 13) thì báo lỗi rõ (`PresentDiagnostic`) thay vì "0 bản ghi"; thử lại tối đa 2 lần.
- **Nhập biểu ghi từ Z39.50 không còn "mồ côi":** trước đây không có Mfn, không có BibXml (không hiện nhan đề/tác giả), mất trường điều khiển 001–008, không được lập chỉ mục. Nay dựng như biên mục tay, trong 1 transaction.
- Tra cứu Z39.50 công khai: tối đa 10 thư viện và 50 bản ghi/trang mỗi yêu cầu.
- Bỏ log `[DEBUG Z3950]` còn sót (đếm toàn bảng `EbookItems` mỗi lần tìm).
- **Tenant:** nguồn Internal vẫn chỉ tìm tài liệu của đơn vị sở hữu cấu hình. Biểu ghi nhập về thuộc đơn vị người nhập; tài khoản hệ thống thì theo đơn vị của cấu hình Z39.50.

### Tra cứu tài liệu `/admin/books` (port LRC 10-03)
- "Xuất chi tiết" / "Xuất tóm tắt" luôn 403: API đòi quyền `export` mà hệ thống phân quyền không có. Nay dùng quyền xem (cả nút trên giao diện).
- Bật Elasticsearch thì tìm theo đầu sách bỏ qua ô ĐKCB/ký hiệu (trả mọi đầu sách). Nay lọc thêm theo ĐKCB.
- Cột "Đơn nhận" cho ĐKCB cũ (không có `Receipt_Id`): suy từ các dòng đơn nhận chứa biểu ghi; nhiều đơn thì liệt kê "799, 829". `donNhan` đổi sang chuỗi. Áp dụng cả Excel chi tiết.
- `pageSize` tối đa 500.
- **Tenant:** ĐKCB khớp theo (mã, đơn vị của biểu ghi); đơn nhận suy ra phải cùng đơn vị với ĐKCB.

### Biên mục, đơn nhận, đơn đặt (port LRC 09-30, 10-03)

**Nhận từ đơn đặt (`PromoteToBib`)**
- Một dòng đơn đặt nhận nhiều đợt (hoặc bấm 2 lần) không còn sinh biểu ghi trùng: nếu đã có dòng đơn nhận trỏ về dòng đơn đặt đó và Bib còn sống thì dùng lại Bib đó.
- Bib mới được xếp lập chỉ mục (ES + Zebra); trước đây không tìm thấy ở `/admin/books`, OPAC, Z39.50 cho tới lần dựng lại toàn bộ.
- Ghi trong 1 transaction.

**Đánh số ĐKCB theo lô** (đơn nhận và biên mục dùng chung `BarcodeNumbering`)
- Số lớn nhất lấy bằng `MAX` trên CSDL (trước đây tải mọi số về bộ nhớ); lưu 1 lần cho cả lô.
- Kiểm tra trùng không phân biệt hoa/thường; tối đa 5.000 ĐKCB/lần; số lượng ≤ 0 trả 400 (biên mục trước đây không chặn).

**Hangfire tắt:** lưu/xoá biểu ghi và tải file tài liệu số không còn trả 500 sau khi đã lưu. Job xếp qua `IBackgroundJobClient` (`BackgroundJobs.TryEnqueue`), không có thì bỏ qua và ghi cảnh báo. "Dựng lại chỉ mục" trả 400 rõ ràng.

**Chi tiết đơn nhận:** ô tìm nhanh (nhan đề, tác giả, NXB, năm, MFN; không phân biệt dấu), phân trang 10/20/50/100, cột MFN. "Chọn tất cả" áp dụng cho mọi dòng khớp bộ lọc.

**Tenant:**
- `Catalogue/Book/Save` với `BibId` của đơn vị khác trả 404 (trước đây ghi đè được). `Catalogue/Book/Delete/{mfn}` lọc đơn vị (trước đây xoá được biểu ghi đơn vị khác).
- `BookOrder/Save` chỉ sửa biểu ghi nháp của đơn vị mình; dòng con gắn đơn vị của biểu ghi nháp.
- `Receipt/SaveDetail` kiểm tra đơn nhận thuộc đơn vị người gọi và biểu ghi cùng đơn vị (hoặc dùng chung); dòng mới gắn đơn vị của đơn nhận.
- Bib từ đơn đặt thuộc đơn vị của biểu ghi nháp; Bib dùng lại phải cùng đơn vị. ĐKCB đánh số trong đơn vị của dòng đơn nhận / biểu ghi.

### Kho: ra/vào kho, sách mất, thanh lý, điều chuyển, kiểm kê, đánh lại mã, xếp giá (port LRC 10-03, 10-04)

**Ra/vào kho**
- Chặn xuất kho với bản đã thanh lý (`S`) và bản đang `R` nhưng còn phiếu mượn mở.
- ĐKCB `X` bị kẹt (không có giao dịch xuất kho) nay quét Nhập kho được.
- Xoá giao dịch xuất kho còn mở thì trả ĐKCB về kho.
- Bộ lọc không phân biệt hoa/thường.

**Sách mất**
- Danh sách gồm cả ĐKCB `L` chưa có bản ghi báo mất (dữ liệu cũ, mất qua phiếu phạt).
- Chặn báo mất bản đang mượn hoặc đã thanh lý.
- Khôi phục được bản `L` không có bản ghi, và chỉ đổi `L` → `R`.

**Thanh lý**
- Danh sách trả đủ các trường. Trước đây mọi cột trống và nút "Huỷ" không chạy.
- Chặn thanh lý bản đang mượn hoặc đang ra kho.
- Huỷ thanh lý trả về `L` nếu bản sách còn bản ghi báo mất.

**Điều chuyển kho**
- Thêm **"Hoàn thành điều chuyển"** (`POST Catalogue/Move/Complete/{id}`): chuyển ĐKCB sang kho nhận và bỏ qua bản đang mượn, đã mất, đã thanh lý hoặc khác đơn vị. Trước đây `Barcode.Store` không bao giờ đổi.
- Phiếu đã hoàn thành bị khoá.
- Chỉ thêm được ĐKCB đang ở kho nguồn.
- Kho nhận phải khác kho nguồn.

**Kiểm kê**
- Cờ đọc đúng mã: 1 = bình thường, 2 = có vấn đề.
- Bộ lọc "Không" gửi 2.
- Mã chưa đăng ký vẫn được ghi nhận.
- Không chọn kho thì không đối chiếu kho.
- "Nghi mất" chỉ tính các kho đã quét, không tính bản đang mượn, đã mất hoặc đã thanh lý, và chạy trong CSDL.
- Đợt đã kết thúc không nhận thêm mã.
- Tìm đợt theo tên/ngày và lưu ngày kiểm kê đã chạy được.

**Đánh lại mã**
- Đổi mã chỉ khác hoa/thường không còn bị báo trùng.
- Mã cũ trùng nhiều bản trong đơn vị thì báo lỗi thay vì đổi bừa một bản.
- Tệp có mã lặp lại bị từ chối.

**Xếp giá theo sơ đồ**
- Chỉ bản `I` hoặc trống mới chuyển sang `R`; bản `B`, `L`, `X` chỉ đổi vị trí.
- Ngăn phải thuộc đúng giá đã chọn.
- Gợi ý DDC tính cả phân mục con của mốc cuối.

**Tenant:**
- Service mới nhận phạm vi đơn vị đã phân giải (`TenantScopeHelper.ResolveScopeAsync`).
- ĐKCB tra theo (mã, đơn vị); phiếu mượn mở so theo (mã, đơn vị).
- Bản ghi mất, thanh lý và ra kho gắn đơn vị của bản sách.
- Hai kho của phiếu điều chuyển phải thuộc đơn vị của phiếu.
- `Place` (xếp giá) trước đây không kiểm tra đơn vị: nhận mọi ĐKCB và giá client gửi. Nay đã kiểm tra.

### Lưu thông: phiếu phạt, báo cáo, lịch sử (port LRC 09-30, 10-03, 10-04)

**Phiếu phạt**
- Nghiệp vụ chuyển từ `CFineTicketController` (432 dòng) sang `IFineTicketService`/`FineTicketService`; đường dẫn và JSON giữ nguyên.
- "Gom phiếu quá hạn" chỉ phạt sách **đang mượn** (`Status` khác `"R"`). Trước đây lọc `Status == "O"` nên bỏ sót phiếu cũ lưu `"1"`.
- Mượn/Trả: nút "Cập nhật phạt" bật cả khi chỉ có tài liệu được tích chọn (vd mất sách chưa đến hạn). Dòng chưa quá hạn mặc định lý do "Mất tài liệu" (`MATTL`), 0đ.
- Chi tiết phiếu:
  - lý do phạt theo từng dòng (cùng danh mục với đầu phiếu);
  - lý do gắn trạng thái (vd Mất → `L`) thì khi lưu sẽ đổi trạng thái ĐKCB và đóng phiếu mượn (ghi 1 lượt trả);
  - thêm/xoá dòng phạt (theo ĐKCB hoặc tự do) khi phiếu chưa hoàn thành;
  - ô "Lý do phạt" ở đầu phiếu chỉ khoá khi phiếu đã hoàn thành.
- Danh mục lý do phạt:
  - cột "Trạng thái" trước đây luôn trống, và sửa một lý do làm mất trạng thái đã gán (API dùng `status_Reg_Id`);
  - nay lưu mã trạng thái thay cho GUID.
- **Tenant:**
  - phiếu, bạn đọc và chi tiết khác đơn vị JWT trả 404;
  - phiếu mới gắn đơn vị của bạn đọc; dòng phạt theo đơn vị phiếu; lượt trả theo đơn vị phiếu mượn;
  - ĐKCB tra theo (mã, đơn vị) qua `BarcodeTenantLookup`;
  - lý do phạt, trạng thái ĐKCB và chính sách lưu thông lấy của đơn vị hoặc dùng chung, ưu tiên của đơn vị.

**Báo cáo lưu thông**
- Đang mượn, quá hạn, bạn đọc quá hạn, hết hạn thẻ còn giữ sách (báo cáo 2, 3, 4, 6, 7): đếm `Status` khác `"R"`, không còn chỉ đếm `"O"`.
- Hoạt động theo ngày (1) và mượn nhiều (8): lượt mượn = phiếu đang mượn + lượt đã trả (`BookIn`), mỗi lượt đếm 1 lần.
- Chưa từng mượn (9): loại cả ĐKCB chỉ còn lịch sử trả kiểu cũ trong `BookIn`.
- Báo cáo 1 có dòng **Tổng cộng** (màn hình, bản in, Excel, email định kỳ), tính trên toàn bộ khoảng ngày.
- Màn hình báo cáo có phân trang. Trước đây chỉ hiện 20 dòng đầu.
- **Tenant:** giữ lọc `TenantId` ở mọi nhánh; join ĐKCB và kiểm tra "chưa từng mượn" theo (mã, đơn vị).

**Lịch sử lưu thông**
- Thêm cột "Người thực hiện": cán bộ nhận trả; chưa trả, hoặc lượt trả không ghi người, thì lấy cán bộ cho mượn. Có trong Excel và PDF.
- Bộ lọc người thao tác lọc theo đúng giá trị này.
- Bộ lọc "Đang mượn"/"Quá hạn" dùng `Status` khác `"R"`.

**Nền tảng:** thêm `ServiceResult<T>` (Core) và `FromServiceResult` (API) cho các service tách từ controller.

### Bảo mật và lỗ hổng đơn vị (port LRC 10-03, 09-28, 09-29)
- Token đọc tài liệu số không còn ký bằng khoá giữ chỗ công khai: khoá suy từ `Jwt:Key` nếu `EbookFileSettings:TokenSecret` là placeholder.
- Đóng dấu PDF nay chạy được: chuẩn hoá đuôi `".pdf"` và dùng font DejaVu (Dockerfile).
- **Tenant:**
  - `GetById`/`GetByPublicId` của mọi controller chỉ trả bản ghi của đơn vị mình hoặc dùng chung;
  - liên kết tài liệu in ↔ số lọc theo Bib/EbookItem;
  - lọc đơn vị cho DicClass `GetTree`, WorkSheet, WorksheetField/Subfield;
  - `PublicNews/GetNewsById` lọc theo đơn vị suy từ tên miền.
- `pageIndex <= 0` không còn gây lỗi 500.
- Nhập bạn đọc khoá theo đơn vị trước khi tra danh mục, không còn tạo trùng "K24".
- Xoá project `ELIBAPI.API.Public`: không deploy, trùng `Controllers/Public`, thiếu bộ lọc đơn vị.
