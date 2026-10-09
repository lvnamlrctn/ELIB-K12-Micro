# Hướng dẫn triển khai trên server mới — Wildcard subdomain *.thuvientn.vn

**Tình huống:**
- Server mới có sẵn: `archive-web` (nginx, port 80), `docmanager-db` (postgres:16), `docmanager-api`, `document_web`
- Server cũ (`thuvientn.vn`) vẫn giữ nguyên, không đụng vào
- Cần triển khai ELIBAPI cho 5 đơn vị mới với subdomain riêng: `dv1.thuvientn.vn`, `dv2.thuvientn.vn`, ...
- Tái dụng `docmanager-db` postgres (tạo database `tvxa_moi` mới trong đó)
- Thêm nginx config cho `*.thuvientn.vn` vào `archive-web` (không deploy nginx mới)

---

## Files cần copy lên server mới

```
docker-compose.fresh.yml        ← compose cho API + Frontend (không có postgres, không có nginx)
nginx/nginx-wildcard.conf       ← server block wildcard *.thuvientn.vn
nginx/ssl/wildcard/             ← wildcard SSL cert (tạo ở Bước 2)
.env.new                        ← environment variables (điền PG_USER/PG_PASSWORD)
```

> **Không cần copy source code riêng** nếu đã có sẵn trong repo. Build từ code có sẵn.

---

## Bước 0 — Xác nhận thông tin

### Tên Docker network của docmanager-db

```bash
docker inspect docmanager-db | grep -A3 '"Networks"'
```

→ Kết quả: `document_default` (đã xác nhận). File `docker-compose.fresh.yml` đã có sẵn giá trị này.

### User/pass postgres của docmanager-db

```bash
docker inspect docmanager-db | grep -E "POSTGRES_USER|POSTGRES_PASSWORD"
```

→ Điền vào `.env.new`:
```env
PG_USER=<giá trị POSTGRES_USER>
PG_PASSWORD=<giá trị POSTGRES_PASSWORD>
```

---

## Bước 1 — Tạo database tvxa_moi trong docmanager-db

```bash
docker exec -it docmanager-db psql -U <PG_USER> -c "CREATE DATABASE tvxa_moi;"
```

---

## Bước 2 — SSL wildcard cert cho *.thuvientn.vn

Dùng DNS-01 challenge (port 80 đang bận bởi archive-web):

```bash
sudo certbot certonly --manual --preferred-challenges dns \
  -d "*.thuvientn.vn"
```

Certbot sẽ yêu cầu thêm TXT record `_acme-challenge.thuvientn.vn` vào DNS. Sau khi xác nhận, cert sẽ được tạo.

```bash
mkdir -p ./nginx/ssl/wildcard
sudo cp -L /etc/letsencrypt/live/thuvientn.vn/fullchain.pem ./nginx/ssl/wildcard/
sudo cp -L /etc/letsencrypt/live/thuvientn.vn/privkey.pem   ./nginx/ssl/wildcard/
```

---

## Bước 3 — Thêm nginx config + network vào archive-web

Tìm file docker-compose của `archive-web` (thường ở `/home/sohoatts/` hoặc `/opt/`):

```bash
docker inspect archive-web | grep '"Com\|Source"'
# Tìm thư mục chứa compose file
```

Thêm vào docker-compose của `archive-web`:

```yaml
services:
  archive-web:    # service đang có
    # ... (giữ nguyên config cũ)
    volumes:
      # ... (giữ nguyên volume cũ)
      - /path/to/elibapi/nginx/nginx-wildcard.conf:/etc/nginx/conf.d/wildcard.conf:ro
      - /path/to/elibapi/nginx/ssl/wildcard:/etc/nginx/ssl/wildcard:ro
    networks:
      # ... (giữ nguyên network cũ nếu có)
      - elibapi-net

# Thêm vào phần networks (cùng cấp với services):
networks:
  elibapi-net:
    external: true
```

Sau đó restart archive-web:

```bash
docker compose up -d archive-web
```

---

## Bước 4 — Build và Start ELIBAPI stack

```bash
cd /path/to/elibapi    # thư mục chứa source code

# Build image
docker compose -f docker-compose.fresh.yml --env-file .env.new build

# Start containers
docker compose -f docker-compose.fresh.yml --env-file .env.new up -d

# Reload nginx để apply config wildcard
docker exec archive-web nginx -s reload
```

API sẽ tự chạy EF migration và tạo schema cho `tvxa_moi`.

---

## Bước 5 — Kiểm tra (trước khi đổi DNS)

Test trực tiếp bằng cách thêm vào `/etc/hosts` trên máy local:

```
<IP_SERVER_MOI>   dv1.thuvientn.vn
```

Rồi truy cập `https://dv1.thuvientn.vn` — phải thấy giao diện ELIBAPI.

---

## Bước 6 — Seed dữ liệu

### Tạo 5 tenant

```bash
docker exec -it docmanager-db psql -U <PG_USER> -d tvxa_moi -c "
INSERT INTO dbo.\"Tenants\" (\"Name\",\"Code\",\"PublicId\",\"IsDelete\",\"Status\",\"CreatedRowDate\")
VALUES
  ('Đơn vị 1','DV1',gen_random_uuid(),1,2,NOW()),
  ('Đơn vị 2','DV2',gen_random_uuid(),1,2,NOW()),
  ('Đơn vị 3','DV3',gen_random_uuid(),1,2,NOW()),
  ('Đơn vị 4','DV4',gen_random_uuid(),1,2,NOW()),
  ('Đơn vị 5','DV5',gen_random_uuid(),1,2,NOW());

SELECT \"Id\",\"Name\",\"Code\" FROM dbo.\"Tenants\" ORDER BY \"Id\";
"
```

### Clone cấu hình mẫu (SystemParameter, Menu, ReaderType...)

```bash
python scripts/seed-clone-data.py \
  --pg-host localhost --pg-db tvxa_moi \
  --pg-user <PG_USER> --pg-pass "<PG_PASSWORD>" \
  --tenant-ids 1,2,3,4,5
```

---

## Bước 7 — DNS

Thêm **wildcard A record** vào DNS của `thuvientn.vn`:

| Record | Type | Value |
|--------|------|-------|
| `*.thuvientn.vn` | A | `<IP server mới>` |

> Record `thuvientn.vn` (apex) **không đổi** — vẫn trỏ về server cũ.  
> Wildcard `*.thuvientn.vn` chỉ match subdomain, không match apex domain.

---

## Rollback (nếu cần)

Xóa wildcard A record → traffic subdomain ngừng đến server mới.  
`archive-web` nginx trên server mới không ảnh hưởng server cũ.

---

## Sơ đồ tổng thể sau khi hoàn thành

```
DNS:
  thuvientn.vn          → IP server cũ  (tỉnh cũ, giữ nguyên)
  *.thuvientn.vn        → IP server mới (5 đơn vị)

Server cũ:
  [elibapi-nginx] → elibapi-internal (DB: tvxa)

Server mới:
  [archive-web nginx]
    ├─ dv1.thuvientn.vn ─┐
    ├─ dv2.thuvientn.vn  ├──► elibapi-new:8080 ──► docmanager-db (DB: tvxa_moi)
    ├─ dv3.thuvientn.vn  │                          ├─ TenantId=1 (DV1)
    ├─ dv4.thuvientn.vn  │         ──► admin-website-new:4000
    └─ dv5.thuvientn.vn ─┘
    └─ (domain khác)     → archive-web app (giữ nguyên)
```

---

## Checklist

| # | Việc | Done |
|---|------|------|
| 0a | Xác nhận network docmanager-db = `document_default` | ✓ |
| 0b | Lấy PG_USER/PG_PASSWORD từ docker inspect docmanager-db | |
| 0c | Điền PG_USER/PG_PASSWORD vào `.env.new` | |
| 1 | `CREATE DATABASE tvxa_moi` trong docmanager-db | |
| 2 | Wildcard SSL cert `*.thuvientn.vn` (DNS-01) | |
| 3 | Copy cert vào `nginx/ssl/wildcard/` | |
| 4 | Thêm volume + network vào docker-compose của archive-web | |
| 5 | `docker compose up -d archive-web` | |
| 6 | `docker compose -f docker-compose.fresh.yml --env-file .env.new build && up -d` | |
| 7 | `docker exec archive-web nginx -s reload` | |
| 8 | Test qua `/etc/hosts` trước khi đổi DNS | |
| 9 | Seed 5 tenant + clone cấu hình | |
| 10 | Thêm A record `*.thuvientn.vn` → IP server mới | |
| 11 | Verify `https://dv1.thuvientn.vn` hoạt động | |
