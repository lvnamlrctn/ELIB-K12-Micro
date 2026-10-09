# Server Z39.50 (Zebra / Index Data)

Cho phép **thư viện bên ngoài tra cứu biểu ghi của ELIB theo chuẩn Z39.50** — chuẩn liên thư viện mà các
phần mềm thư viện khác (Koha, Libol, Ilib, VTLS…) dùng để lấy biểu ghi biên mục về dùng lại.

Mỗi lần triển khai phục vụ đồng thời:

| Database | Nội dung | Ai dùng |
|---|---|---|
| `ELIB` | **Toàn bộ** biểu ghi của mọi đơn vị (kể cả biểu ghi do tài khoản hệ thống tạo) | Thư viện muốn tra cứu cả hệ thống trong 1 lần |
| `ELIB_<mã đơn vị>` | Chỉ biểu ghi của đúng đơn vị đó, VD `ELIB_PDP`, `ELIB_LS` | Thư viện chỉ quan tâm 1 đơn vị |

Chuỗi kết nối cho thư viện bạn có dạng `<địa chỉ máy chủ>:<cổng>/<tên database>`, ví dụ
`thuvientn.vn:2100/ELIB_PDP`. Trang quản trị **Biên mục → Server Z39.50** hiển thị sẵn chuỗi này cho từng
đơn vị để gửi cho đối tác.

---

## 1. Kiến trúc

```
┌────────────────────┐   ghi file MARC        ┌──────────────────────────────┐
│  ELIBAPI (.NET)    │  ISO2709 (.iso)        │  container elib-zebra        │
│  ZebraExportJob    ├───────────────────────►│  zebraidx  (đánh chỉ mục)    │
└────────────────────┘   volume dùng chung    │  zebrasrv  (phục vụ Z39.50)  │
                            /export           └──────────────┬───────────────┘
                                                             │ TCP cổng 2100
                                                      thư viện bên ngoài
```

**ELIBAPI không nói chuyện trực tiếp với Zebra.** Toàn bộ liên kết chỉ là thư mục `/export` dùng chung:

- ELIBAPI **ghi** file: `/export/<mã đơn vị>/<bibid>.iso`, mỗi biểu ghi 1 file.
  Biểu ghi không thuộc đơn vị nào (tài khoản hệ thống tạo) rơi vào `/export/_system/`.
- Zebra **đọc** thư mục đó, tự đánh chỉ mục định kỳ (mặc định 60 giây/lần) và phục vụ truy vấn.

Chỉ biểu ghi **đã duyệt** (`Bib.Status = 'f'`, chưa xóa mềm) mới được xuất. Khi biểu ghi bị xóa hoặc rút
duyệt, ELIBAPI xóa file tương ứng và Zebra gỡ khỏi chỉ mục ở chu kỳ kế tiếp.

### Thư mục `_all` dùng để làm gì

Zebra định danh bản ghi theo **đường dẫn file**, nên không thể đánh chỉ mục cùng một file vào 2 database
khác nhau. Vì vậy container tự dựng `/export/_all/` chứa **hardlink** tới mọi file (đặt tên
`<mã đơn vị>-<bibid>.iso`): khác đường dẫn nên Zebra coi là bản ghi riêng, nhưng dùng chung inode nên
**không tốn thêm dung lượng đĩa** và nội dung luôn khớp bản mới nhất.

- database gộp `ELIB` ← đánh chỉ mục `/export/_all`
- database `ELIB_<mã>` ← đánh chỉ mục `/export/<mã>`

Thư mục `_all` do container tự quản lý (tự thêm hardlink mới, tự dọn hardlink mồ côi khi biểu ghi gốc bị
xóa). **Không sửa tay** thư mục này.

---

## 2. Khi nào dữ liệu được xuất

| Thời điểm | Cơ chế |
|---|---|
| Thêm / sửa 1 biểu ghi | `CatalogueBookController.Save` → enqueue `ZebraExportJob.RunAsync(bibId)` |
| Xóa 1 biểu ghi | `CatalogueBookController.DeleteByMfn` → enqueue `ZebraExportJob.RemoveAsync(bibId)` |
| Hằng đêm 3:00 | Recurring job `zebra-export-nightly-rebuild` xuất lại toàn bộ (lưới an toàn) |
| Bấm tay | Trang **Biên mục → Server Z39.50** → nút «Xuất lại dữ liệu» |

Tài khoản thuộc 1 đơn vị chỉ xuất lại được đơn vị mình; tài khoản hệ thống xuất lại được toàn bộ.

---

## 3. Triển khai

Zebra chạy như một service trong `docker-compose.yml` (dev) và `docker-compose.production.yml` (thật).

```bash
cd backend
docker compose -f docker-compose.production.yml up -d --build elib-zebra
docker compose -f docker-compose.production.yml logs -f elib-zebra
```

Log lần chạy đầu phải thấy:

```
[zebra] Khởi tạo register lần đầu...
[zebra] Đánh chỉ mục lần đầu (database gộp: ELIB, tiền tố đơn vị: ELIB_)...
[zebra] zebrasrv lắng nghe Z39.50 trên cổng 2100 trong container.
```

### Biến môi trường (đặt trong `.env` cạnh file compose)

| Biến | Mặc định | Ý nghĩa |
|---|---|---|
| `ZEBRA_PORT` | `2100` | Cổng Z39.50 mở ra host. Đặt `210` nếu muốn dùng cổng chuẩn quốc tế |
| `ZEBRA_EXPORT_PATH` | `./data/zebra-export` | Thư mục trên host chứa file MARC, dùng chung giữa API và Zebra |
| `ZEBRA_REINDEX_INTERVAL` | `60` | Số giây giữa 2 lần đánh chỉ mục |
| `ZEBRA_COMBINED_DB` | `ELIB` | Tên database gộp |
| `ZEBRA_DB_PREFIX` | `ELIB_` | Tiền tố tên database của từng đơn vị |
| `ZEBRA_PUBLIC_HOST` | `localhost` | **Chỉ để hiển thị** trên trang quản trị — đặt đúng tên miền công khai để chuỗi kết nối gửi đối tác là đúng |

> `ZEBRA_COMBINED_DB` và `ZEBRA_DB_PREFIX` phải đặt **giống nhau** ở cả service `elibapi-internal` và
> `elib-zebra` (compose đã tham chiếu cùng biến nên chỉ cần khai 1 lần trong `.env`).

---

## 4. Kiểm thử

Dùng `yaz-client` (bộ công cụ YAZ của chính Index Data — máy trạm Windows đã cài sẵn ở
`C:\Program Files\YAZ\bin`, trên Linux là gói `yaz`).

**Database gộp:**

```
yaz-client tcp:localhost:2100/ELIB
Z> find @attr 1=4 "giáo trình"      # 1=4 là chỉ số BIB-1 cho Nhan đề
Z> show 1
Z> quit
```

**Database của 1 đơn vị** (chỉ ra biểu ghi của đơn vị đó — đây là phép kiểm quan trọng nhất của thiết kế
nhiều database):

```
yaz-client tcp:localhost:2100/ELIB_PDP
Z> find @attr 1=4 "giáo trình"
```

Kỳ vọng: số kết quả của `ELIB` **lớn hơn hoặc bằng** tổng các `ELIB_<mã>`, và mỗi `ELIB_<mã>` chỉ trả về
biểu ghi của đúng đơn vị đó.

Các chỉ số BIB-1 đã ánh xạ trong `zebra/tab/elib.abs`: `1=4` Nhan đề, `1=1003` Tác giả, `1=1018` Nhà xuất
bản, `1=21` Chủ đề, `1=7` ISBN, `1=8` ISSN, `1=1016` Bất kỳ.

**Kiểm tra tiếng Việt có dấu**: file `.iso` do `Iso2709Reader.Write` xuất luôn là UTF-8 và tự đặt ký tự
thứ 09 của leader = `a` (Unicode), nên `show` phải hiện đúng dấu. Nếu ra ký tự lạ, kiểm tra locale của
terminal trước khi nghi ngờ server.

---

## 5. Thêm đơn vị mới

**Không cần làm gì với Zebra.** Quy trình vẫn như cũ (thêm bản ghi `dbo.Tenant` + subdomain). Ngay khi đơn
vị đó có biểu ghi đã duyệt đầu tiên, ELIBAPI tạo thư mục `/export/<mã>/` và chu kỳ đánh chỉ mục kế tiếp
của Zebra tự sinh database `ELIB_<mã>`. Không sửa `zebra.cfg`, không khởi động lại container.

Mã đơn vị (`Tenant.Code`) trở thành tên thư mục **và** tên database, nên chỉ dùng chữ/số ASCII, `_`, `-`
(quy ước hiện tại là 2-3 ký tự hoa, VD `PDP`, `LS`, `QT2`). `ZebraExportJob.SafeCode` đã tự lọc ký tự lạ,
và container bỏ qua thư mục có tên không hợp lệ (ghi cảnh báo ra log).

---

## 6. Bảo mật

⚠️ **Z39.50 là TCP thuần, KHÔNG đi qua nginx được** — cổng này mở thẳng từ container ra host, là cổng duy
nhất trong stack cần bên ngoài kết nối vào. Giao thức này **không có mã hóa và không xác thực** trong cấu
hình hiện tại, và mọi database đều đọc được nếu biết tên.

Vì vậy phải chặn ở tầng mạng, không phải ở tầng ứng dụng:

```bash
# Chỉ cho các dải IP của đơn vị đối tác kết nối cổng Z39.50
sudo ufw allow from 203.0.113.0/24 to any port 2100 proto tcp
sudo ufw deny 2100/tcp
```

Cân nhắc trước khi mở công khai: dữ liệu chia sẻ ở đây là **biểu ghi biên mục** (nhan đề, tác giả, ISBN…),
không chứa thông tin bạn đọc hay dữ liệu mượn trả — nhưng vẫn nên giới hạn theo danh sách đối tác.

---

## 7. Xử lý sự cố

**Trang quản trị báo "Chưa thấy thư mục xuất dữ liệu"**
→ Service `elibapi-internal` chưa mount volume `/export`, hoặc `ZebraExport__Directory` sai. Kiểm tra:
`docker compose exec elibapi-internal ls -la /export`

**Job xuất chạy nhưng báo lỗi quyền ghi (`UnauthorizedAccessException` / `Permission denied`)**
→ Container ELIBAPI chạy bằng user **không phải root** (`appuser`, tạo bằng `useradd --system` nên UID nằm
ở dải hệ thống, thường 997–999 — **không** phải 1000), trong khi Docker tạo thư mục bind-mount mới với chủ
sở hữu `root`. Lấy đúng UID/GID rồi cấp quyền cho thư mục trên host:
```bash
# 1) Hỏi chính ảnh API xem appuser có uid/gid bao nhiêu
docker compose -f docker-compose.production.yml run --rm --no-deps --entrypoint id elibapi-internal
# → ví dụ: uid=999(appuser) gid=999(appgroup)

# 2) Cấp quyền theo đúng số vừa lấy
sudo mkdir -p ./data/zebra-export
sudo chown -R 999:999 ./data/zebra-export
```
(Container Zebra chạy bằng root nên đọc và tạo hardlink được bình thường.)

**Thư mục `/export` rỗng dù đã có biểu ghi**
→ Kiểm tra job có chạy không tại Hangfire dashboard `/hangfire` (mục **Failed**). Chỉ biểu ghi
`Status = 'f'` (đã duyệt) mới được xuất — biểu ghi nháp sẽ không xuất.

**Zebra chạy nhưng `find` không ra kết quả**
→ Xem log đánh chỉ mục trong container:
```bash
docker compose exec elib-zebra cat /tmp/zebraidx-reindex.log
docker compose exec elib-zebra zebraidx -c /zebra/zebra.cfg -d ELIB update /export/_all
```

**Đánh chỉ mục lại từ đầu** (khi register hỏng):
```bash
docker compose stop elib-zebra
docker volume rm backend_zebra-register-data
docker compose up -d elib-zebra      # entrypoint tự chạy `zebraidx init` lại
```
Register dựng lại hoàn toàn từ `/export` nên thao tác này **không mất dữ liệu** và không cần sao lưu volume
register.

**Database của 1 đơn vị bị thiếu biểu ghi**
→ Vào trang **Server Z39.50**, đối chiếu cột «Số biểu ghi đã xuất» với số biểu ghi đã duyệt của đơn vị;
nếu lệch thì bấm «Xuất lại dữ liệu».

---

## 8. File liên quan

| File | Vai trò |
|---|---|
| `backend/zebra/Dockerfile` | Ảnh container Zebra (Ubuntu + idzebra từ kho Index Data) |
| `backend/zebra/zebra.cfg` | Cấu hình Zebra: register, profile, storeData |
| `backend/zebra/tab/elib.abs` | Ánh xạ trường MARC → chỉ số tra cứu BIB-1 |
| `backend/zebra/docker-entrypoint.sh` | Khởi tạo, vòng lặp đánh chỉ mục nhiều database, chạy `zebrasrv` |
| `ELIBAPI.Infrastructure/Jobs/ZebraExportJob.cs` | Xuất MARC ISO2709 theo từng đơn vị |
| `ELIBAPI.Infrastructure/Services/MarcRecordBuilder.cs` | Dựng bản ghi MARC từ CSDL (dùng chung với OAI-PMH) |
| `ELIBAPI.Core/Common/Iso2709Reader.cs` | Đọc/ghi định dạng ISO2709 |
| `ELIBAPI.API/Controllers/Dbo/AdminJobController.cs` | 3 endpoint `rebuild-zebra-export*`, `zebra-export-status` |
| `frontend/src/app/pages/admin/cataloging/z3950-server.*` | Trang quản trị theo dõi |

> **Phân biệt với "Tra cứu Z3950"** (`/admin/z3950-search`): trang đó là **client** — ELIB đi lấy biểu ghi
> *từ* thư viện khác về. Trang này là **server** — thư viện khác lấy biểu ghi *từ* ELIB. Hai chiều ngược
> nhau, không liên quan về mã nguồn.
