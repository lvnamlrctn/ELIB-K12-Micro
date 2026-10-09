# Triển khai cho tỉnh mới (database riêng) với Docker

Hướng dẫn triển khai hệ thống ELIBAPI cho một tỉnh/thư viện mới với database PostgreSQL riêng, sử dụng Docker.

---

## Cách 1: Cùng server, thêm stack mới (khác port)

### Cấu trúc thư mục

```
/opt/elibapi-tinhmoi/
├── docker-compose.production.yml
├── .env.production             ← DB mới, port mới
├── nginx/
│   ├── nginx.conf              ← domain mới
│   └── ssl/
│       ├── fullchain.pem
│       └── privkey.pem
├── ELIBAPI.API/
│   └── Dockerfile
├── scripts/
│   ├── seed-admin-users.py
│   └── seed-clone-data.py
└── ...
```

### Bước 1 — Copy project lên server

```bash
mkdir -p /opt/elibapi-tinhmoi
# Copy source code (hoặc git clone)
cp -r /opt/elibapi/* /opt/elibapi-tinhmoi/
```

### Bước 2 — Sửa `.env.production`

```env
# ── Database (PostgreSQL Docker container) ───────────────────────────────────
PG_DATABASE=tvxa_tinhmoi
PG_USER=lvnam
PG_PASSWORD=MatKhauMoi!
PG_PORT=5433                    # port khác để không conflict với stack cũ

# ── JWT ──────────────────────────────────────────────────────────────────────
JWT_KEY=TINHMOI_JWT_SECRET_KEY_CHANGE_THIS_IN_PRODUCTION_MIN32CHARS
JWT_ISSUER=ELIBAPI
JWT_AUDIENCE=ELIBAPI_CLIENTS
JWT_EXPIRE_HOURS=8

# ── API Port ─────────────────────────────────────────────────────────────────
API_PORT=8091                   # port khác (stack cũ dùng 8090)

# ── Minio (Object Storage) ──────────────────────────────────────────────────
MINIO_ENDPOINT=103.124.95.249:9000
MINIO_ACCESS_KEY=minioadmin
MINIO_SECRET_KEY=<encrypted>
MINIO_BUCKET=elibapi-tinhmoi    # bucket riêng hoặc dùng chung
MINIO_PRIVATE_BUCKET=elibapi-private-tinhmoi
MINIO_USE_SSL=false
MINIO_PUBLIC_URL=https://thuvientinhmoi.vn/api/public/media

# ── Elasticsearch ────────────────────────────────────────────────────────────
ES_URI=http://103.124.95.249:9200
ES_USERNAME=elastic
ES_PASSWORD=<encrypted>

# ── Hangfire Dashboard ───────────────────────────────────────────────────────
HANGFIRE_USER=admin
HANGFIRE_PASSWORD=<password>

# ── Ebook Data Path ─────────────────────────────────────────────────────────
EBOOK_DATA_PATH=./data/ebook

# ── SSL Certificates ────────────────────────────────────────────────────────
SSL_CERT_PATH=./nginx/ssl
```

### Bước 3 — Sửa `docker-compose.production.yml`

Đổi `container_name` để không trùng stack cũ:

```yaml
services:
  postgres:
    container_name: elibapi-postgres-tinhmoi
    ports:
      - "${PG_PORT:-5433}:5432"

  elibapi-internal:
    container_name: elibapi-internal-tinhmoi
    ports:
      - "${API_PORT:-8091}:8080"

  admin-website:
    container_name: admin-website-tinhmoi

  nginx:
    container_name: elibapi-nginx-tinhmoi
    ports:
      - "8080:80"       # port khác, hoặc dùng IP khác
      - "8443:443"
```

> **Lưu ý:** Nếu server có nhiều IP, có thể bind port 80/443 vào IP riêng thay vì đổi port:
> ```yaml
> ports:
>   - "10.0.0.2:80:80"
>   - "10.0.0.2:443:443"
> ```

### Bước 4 — Sửa `nginx/nginx.conf`

```nginx
upstream api_internal { server elibapi-internal:8080; }
upstream ssr_app      { server admin-website:4000;    }

# HTTP server
server {
    listen 80;
    server_name *.thuvientinhmoi.vn thuvientinhmoi.vn;
    # ... (giữ nguyên proxy rules)
}

# HTTPS server
server {
    listen 443 ssl;
    server_name *.thuvientinhmoi.vn thuvientinhmoi.vn;
    ssl_certificate     /etc/nginx/ssl/fullchain.pem;
    ssl_certificate_key /etc/nginx/ssl/privkey.pem;
    # ... (giữ nguyên proxy rules)
}
```

### Bước 5 — SSL Certificate

```bash
# Tạo wildcard cert cho domain mới (DNS-01 challenge)
sudo certbot certonly --manual --preferred-challenges dns \
  -d "*.thuvientinhmoi.vn" -d "thuvientinhmoi.vn"

# Copy cert vào thư mục ssl
mkdir -p /opt/elibapi-tinhmoi/nginx/ssl
sudo cp -L /etc/letsencrypt/live/thuvientinhmoi.vn/fullchain.pem /opt/elibapi-tinhmoi/nginx/ssl/
sudo cp -L /etc/letsencrypt/live/thuvientinhmoi.vn/privkey.pem /opt/elibapi-tinhmoi/nginx/ssl/
```

### Bước 6 — Chạy

```bash
cd /opt/elibapi-tinhmoi
docker compose -f docker-compose.production.yml --env-file .env.production up -d
```

### Bước 7 — Seed data ban đầu

```bash
# Sửa connection string trong script trước khi chạy
cd /opt/elibapi-tinhmoi/scripts

# Tạo admin user
python seed-admin-users.py

# Nhân bản cấu hình mẫu (SystemParameter, MenuType, Menu, LinkGroup, Link, ReaderType)
python seed-clone-data.py
```

---

## Cách 2: Server riêng (đơn giản hơn)

Nếu tỉnh mới có server riêng thì không cần đổi port hay container_name — dùng y nguyên file gốc:

```bash
# Trên server mới
git clone <repo> /opt/elibapi
cd /opt/elibapi

# 1. Sửa .env.production  (database, JWT key, domain)
# 2. Sửa nginx/nginx.conf (server_name → domain mới)
# 3. Tạo SSL cert + copy vào nginx/ssl/
# 4. Chạy
docker compose -f docker-compose.production.yml --env-file .env.production up -d

# 5. Seed data
cd scripts && python seed-admin-users.py && python seed-clone-data.py
```

---

## Checklist triển khai

| # | Việc | Ghi chú |
|---|------|---------|
| 1 | Tạo thư mục / clone code | `/opt/elibapi-tinhmoi/` |
| 2 | Sửa `.env.production` | DB name, port, JWT key, domain |
| 3 | Sửa `docker-compose.production.yml` | container_name, port (nếu cùng server) |
| 4 | Sửa `nginx/nginx.conf` | server_name = domain mới |
| 5 | Tạo SSL wildcard cert | `certbot --manual --preferred-challenges dns` |
| 6 | Copy cert vào `nginx/ssl/` | `cp -L` để follow symlink |
| 7 | DNS trỏ domain → IP server | A record: `*.thuvientinhmoi.vn` → IP |
| 8 | `docker compose up -d` | Khởi chạy toàn bộ stack |
| 9 | Seed admin users | `python seed-admin-users.py` |
| 10 | Seed cấu hình mẫu | `python seed-clone-data.py` |
| 11 | Kiểm tra HTTP + HTTPS | `curl http://...` và `curl https://...` |
| 12 | Thêm tenant records | Qua admin UI hoặc SQL |

---

## Lưu ý quan trọng

- **Mỗi tỉnh = 1 bộ** `.env.production` + `nginx.conf` riêng
- **Cùng server:** phải đổi port expose + container_name để tránh conflict
- **Khác server:** giữ nguyên port mặc định, chỉ sửa domain + DB
- **Minio/Elasticsearch** có thể dùng chung nếu cùng hạ tầng, chỉ cần bucket/index riêng
- **SSL cert** cần renew mỗi 90 ngày (Let's Encrypt), nên cài cron tự động
- **Backup database** nên thiết lập `pg_dump` cron cho mỗi stack
