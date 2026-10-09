# Triển khai 5 đơn vị mới — 1 Database, 1 Stack mới, cùng server

## Context

Server hiện có đang chạy stack tỉnh cũ (port 8090, domain thuvientn.vn).  
Cần thêm 1 stack mới cho 5 đơn vị mới:
- 1 PostgreSQL database mới (chứa 5 tenant)
- 1 API container mới (port khác, VD 8091)
- 1 Frontend container mới
- Nginx thêm domain mới route đến stack mới
- 5 tenant records + seed data trong DB mới

---

## Bước 1 — Tạo database mới trên PostgreSQL container hiện có

```bash
docker exec -it elibapi-postgres psql -U lvnam -c "CREATE DATABASE tvxa_moi;"
```

---

## Bước 2 — Tạo file `.env.moi`

Copy từ `.env.production`, sửa 4 chỗ:

```env
PG_DATABASE=tvxa_moi
API_PORT=8091
MINIO_PUBLIC_URL=https://thuvien-moi.vn/api/public/media
JWT_KEY=MOI_JWT_SECRET_MIN32CHARS_CHANGE_THIS
```

---

## Bước 3 — Thêm service vào `docker-compose.production.yml`

Thêm 2 service mới (API + Frontend) với tên container khác:

```yaml
  elibapi-moi:
    image: elibapi-internal:latest   # dùng lại image đã build
    container_name: elibapi-moi
    restart: unless-stopped
    ports:
      - "8091:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - DatabaseProvider=PostgreSQL
      - ConnectionStrings__PostgreSQL=Host=postgres;Port=5432;Database=tvxa_moi;Username=${PG_USER:-lvnam};Password=${PG_PASSWORD};Timeout=30;Command Timeout=60
      - Jwt__Key=${JWT_KEY_MOI}
      - MinioSettings__Endpoint=${MINIO_ENDPOINT}
      - MinioSettings__AccessKey=${MINIO_ACCESS_KEY}
      - MinioSettings__SecretKey=${MINIO_SECRET_KEY}
      - MinioSettings__BucketName=${MINIO_BUCKET}
      - MinioSettings__PrivateBucketName=${MINIO_PRIVATE_BUCKET}
      - MinioSettings__UseSSL=${MINIO_USE_SSL:-false}
      - MinioSettings__PublicBaseUrl=https://thuvien-moi.vn/api/public/media
      - ElasticsearchSettings__Uri=${ES_URI}
      - ElasticsearchSettings__Username=${ES_USERNAME}
      - ElasticsearchSettings__Password=${ES_PASSWORD}
      - Hangfire__User=${HANGFIRE_USER:-admin}
      - Hangfire__Password=${HANGFIRE_PASSWORD}
    volumes:
      - ${EBOOK_DATA_PATH:-./data/ebook}:/app/ebook-data
    networks:
      - elibapi-network
    depends_on:
      postgres:
        condition: service_healthy

  admin-website-moi:
    image: admin-website:latest      # dùng lại image frontend đã build
    container_name: admin-website-moi
    restart: unless-stopped
    environment:
      - NODE_ENV=production
      - PORT=4001
    networks:
      - elibapi-network
    depends_on:
      - elibapi-moi
```

---

## Bước 4 — Cập nhật `nginx/nginx.conf`

Thêm vào cuối file (giữ nguyên server block cũ của tỉnh cũ):

```nginx
upstream api_moi  { server elibapi-moi:8080; }
upstream ssr_moi  { server admin-website-moi:4001; }

server {
    listen 80;
    server_name *.thuvien-moi.vn thuvien-moi.vn;
    client_max_body_size 50m;

    location /api/public {
        proxy_pass         http://api_moi;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 120s;
    }

    location /api {
        proxy_pass         http://api_moi;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 120s;
    }

    location / {
        proxy_pass          http://ssr_moi;
        proxy_http_version  1.1;
        proxy_set_header    Upgrade           $http_upgrade;
        proxy_set_header    Connection        'upgrade';
        proxy_set_header    Host              $host;
        proxy_set_header    X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header    X-Forwarded-Proto $scheme;
        proxy_cache_bypass  $http_upgrade;
        proxy_read_timeout  60s;
    }
}

server {
    listen 443 ssl;
    server_name *.thuvien-moi.vn thuvien-moi.vn;
    ssl_certificate     /etc/nginx/ssl/moi/fullchain.pem;
    ssl_certificate_key /etc/nginx/ssl/moi/privkey.pem;
    client_max_body_size 50m;

    location /api/public {
        proxy_pass         http://api_moi;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 120s;
    }

    location /api {
        proxy_pass         http://api_moi;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 120s;
    }

    location / {
        proxy_pass          http://ssr_moi;
        proxy_http_version  1.1;
        proxy_set_header    Upgrade           $http_upgrade;
        proxy_set_header    Connection        'upgrade';
        proxy_set_header    Host              $host;
        proxy_set_header    X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header    X-Forwarded-Proto $scheme;
        proxy_cache_bypass  $http_upgrade;
        proxy_read_timeout  60s;
    }
}
```

---

## Bước 5 — SSL Certificate

```bash
# Tạo cert (server phải trống port 80 hoặc dùng --dns-01)
sudo certbot certonly --standalone -d thuvien-moi.vn -d "*.thuvien-moi.vn"

# Copy vào thư mục nginx
mkdir -p ./nginx/ssl/moi
sudo cp -L /etc/letsencrypt/live/thuvien-moi.vn/fullchain.pem ./nginx/ssl/moi/
sudo cp -L /etc/letsencrypt/live/thuvien-moi.vn/privkey.pem   ./nginx/ssl/moi/
```

---

## Bước 6 — Cập nhật DNS

Tạo A record trỏ về IP server hiện có:
- `thuvien-moi.vn` → IP server
- `*.thuvien-moi.vn` → IP server

---

## Bước 7 — Khởi động

```bash
# Nếu có thay đổi code → build lại image trước
docker compose -f docker-compose.production.yml build elibapi-internal

# Chạy 2 container mới (không restart container cũ)
docker compose -f docker-compose.production.yml up -d elibapi-moi admin-website-moi

# Apply nginx config mới
docker exec elibapi-nginx nginx -s reload
```

API khởi động sẽ **tự chạy EF migration** tạo toàn bộ schema cho `tvxa_moi`.

---

## Bước 8 — Seed dữ liệu ban đầu

### 8a. Tạo tài khoản SYSADMIN

Truy cập `https://thuvien-moi.vn/api/Auth/Login` sau đó vào Hangfire hoặc dùng script:

```bash
python scripts/seed-admin-users.py --pg-host 103.124.95.249 --pg-db tvxa_moi --pg-user lvnam --pg-pass "Idt882013!"
```

### 8b. Tạo 5 Tenant records

Kết nối vào DB mới và chạy:

```sql
-- Kết nối vào tvxa_moi
INSERT INTO dbo."Tenants" ("Name","Code","PublicId","IsDelete","Status","CreatedRowDate")
VALUES
  ('Đơn vị 1','DV1',gen_random_uuid(),1,2,NOW()),
  ('Đơn vị 2','DV2',gen_random_uuid(),1,2,NOW()),
  ('Đơn vị 3','DV3',gen_random_uuid(),1,2,NOW()),
  ('Đơn vị 4','DV4',gen_random_uuid(),1,2,NOW()),
  ('Đơn vị 5','DV5',gen_random_uuid(),1,2,NOW());
```

### 8c. Clone cấu hình mẫu cho 5 tenant

```bash
# Chỉnh connection string trỏ về tvxa_moi
python scripts/seed-clone-data.py \
  --pg-host 103.124.95.249 --pg-db tvxa_moi \
  --pg-user lvnam --pg-pass "Idt882013!" \
  --tenant-ids 1,2,3,4,5
```

Script sẽ clone: SystemParameter, MenuType, Menu, LinkGroup, Link, ReaderType cho từng tenant.

---

## Sơ đồ tổng thể

```
Internet
  │
  ├─ *.thuvientn.vn ─────► [Nginx]──► elibapi-internal:8080 ──► DB: tvxa (tỉnh cũ)
  │  (stack cũ, giữ nguyên)       └──► admin-website:4000
  │
  └─ *.thuvien-moi.vn ───► [Nginx]──► elibapi-moi:8080 ──► DB: tvxa_moi
     (stack mới)                   └──► admin-website-moi:4001   ├─ TenantId=1 (DV1)
                                                                  ├─ TenantId=2 (DV2)
                                                                  ├─ TenantId=3 (DV3)
                                                                  ├─ TenantId=4 (DV4)
                                                                  └─ TenantId=5 (DV5)

[1 PostgreSQL container]
  ├─ DB: tvxa        (tỉnh cũ)
  └─ DB: tvxa_moi    (5 đơn vị mới)
```

---

## Checklist triển khai

| # | Việc | Trạng thái |
|---|------|------------|
| 1 | `CREATE DATABASE tvxa_moi` trên PostgreSQL | |
| 2 | Tạo `.env.moi` | |
| 3 | Thêm `elibapi-moi` + `admin-website-moi` vào docker-compose | |
| 4 | Thêm server block vào `nginx/nginx.conf` | |
| 5 | Tạo SSL cert + copy vào `nginx/ssl/moi/` | |
| 6 | DNS A record domain mới → IP server | |
| 7 | `docker compose up -d elibapi-moi admin-website-moi` | |
| 8 | `nginx -s reload` | |
| 9 | Seed SYSADMIN account | |
| 10 | INSERT 5 Tenant records | |
| 11 | `seed-clone-data.py` clone cấu hình cho 5 tenant | |
| 12 | Tạo tài khoản user cho từng đơn vị | |
| 13 | Kiểm tra đăng nhập từng đơn vị | |

---

## Lưu ý

- **Không restart stack cũ** khi thêm stack mới — `docker compose up -d elibapi-moi` chỉ tạo container mới.
- **Nginx reload** (`nginx -s reload`) khác với restart — không làm gián đoạn kết nối đang có.
- **JWT Key** nên khác với tỉnh cũ để token không dùng chéo được.
- **MinIO** có thể dùng chung endpoint, chỉ cần bucket khác nếu cần tách storage.
- **SSL cert** Let's Encrypt: nếu Nginx đang chạy port 80, dùng `--webroot` hoặc `--dns-01` thay vì `--standalone`.
