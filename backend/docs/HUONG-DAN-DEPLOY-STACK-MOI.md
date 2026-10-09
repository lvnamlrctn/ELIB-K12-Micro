# Hướng dẫn triển khai stack mới — thuvientn.vn (5 đơn vị)

**Tình huống:** Server đã có stack cũ đang chạy (`elibapi-internal`, `admin-website`, `elibapi-nginx`, `elibapi-postgres`). Code đã có sẵn trên server. Cần triển khai stack mới với database `tvxa_moi` mà không ảnh hưởng stack cũ.

---

## Bước 0 — Copy 3 file config mới lên server

Từ máy local, copy 3 file vừa tạo lên server (vào cùng thư mục chứa code):

```bash
scp .env.new                  user@server:/opt/elibapi/
scp nginx/nginx-new.conf      user@server:/opt/elibapi/nginx/
scp docker-compose.new.yml    user@server:/opt/elibapi/
```

> **Không cần copy source code** — code đã có sẵn trên server từ lần build trước.

---

## Bước 1 — Copy database tvxa → tvxa_moi

```bash
# Tạo database mới
docker exec -it elibapi-postgres psql -U lvnam -c "CREATE DATABASE tvxa_moi;"

# Copy toàn bộ dữ liệu từ tvxa sang tvxa_moi
docker exec elibapi-postgres pg_dump -U lvnam tvxa \
  | docker exec -i elibapi-postgres psql -U lvnam tvxa_moi
```

---

## Bước 2 — Xóa dữ liệu tenant cũ trong tvxa_moi

```bash
docker exec -it elibapi-postgres psql -U lvnam -d tvxa_moi -c "
DELETE FROM cms.\"MenuType\"  WHERE \"TenantId\" IS NOT NULL;
DELETE FROM cms.\"Menu\"      WHERE \"TenantId\" IS NOT NULL;
DELETE FROM cms.\"LinkGroup\" WHERE \"TenantId\" IS NOT NULL;
DELETE FROM cms.\"Link\"      WHERE \"TenantId\" IS NOT NULL;
DELETE FROM public.systemparameter WHERE \"TenantId\" IS NOT NULL;
DELETE FROM public.readertype       WHERE \"TenantId\" IS NOT NULL;
DELETE FROM dbo.\"Tenants\";
"
```

---

## Bước 3 — Tạo 5 tenant mới

```bash
docker exec -it elibapi-postgres psql -U lvnam -d tvxa_moi -c "
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

---

## Bước 4 — Clone cấu hình mẫu cho 5 tenant

```bash
cd /opt/elibapi
python scripts/seed-clone-data.py \
  --pg-host localhost --pg-db tvxa_moi \
  --pg-user lvnam --pg-pass "Idt882013!" \
  --tenant-ids 1,2,3,4,5
```

> Seed: SystemParameter, MenuType, Menu, LinkGroup, Link, ReaderType cho từng tenant.

---

## Bước 5 — Build image mới + Start stack mới

```bash
cd /opt/elibapi

# Build image từ code hiện có (nginx cũ vẫn chạy, không bị ảnh hưởng)
docker compose -f docker-compose.new.yml --env-file .env.new build

# Start API + Frontend mới (port 8091 để test)
docker compose -f docker-compose.new.yml --env-file .env.new up -d elibapi-new admin-website-new
```

---

## Bước 6 — Kiểm tra API mới (port 8091)

```bash
curl -X POST http://localhost:8091/api/Auth/Login \
  -H "Content-Type: application/json" \
  -d '{"LoginName":"admin","Password":"123456"}'
```

API mới sẽ chạy EF migration tự động nếu cần.

---

## Bước 7 — SWITCH Nginx (downtime ~2 giây)

Khi API + Frontend mới đã xác nhận hoạt động tốt:

```bash
# Dừng nginx cũ
docker stop elibapi-nginx

# Start nginx mới (bind port 80/443)
docker compose -f docker-compose.new.yml --env-file .env.new \
  --profile switch up -d elibapi-nginx-new
```

---

## Rollback (nếu cần)

```bash
docker stop elibapi-nginx-new
docker start elibapi-nginx
```

---

## Sơ đồ sau khi switch

```
thuvientn.vn → [elibapi-nginx-new] → elibapi-new:8080 → DB: tvxa_moi
                                    → admin-website-new:4000

[Stack cũ dừng nhưng vẫn còn]
  elibapi-internal + admin-website + elibapi-nginx (stopped)
  DB: tvxa (giữ nguyên, không xóa)
```

---

## Checklist

| # | Việc | Done |
|---|------|------|
| 0 | Copy `.env.new`, `nginx/nginx-new.conf`, `docker-compose.new.yml` lên server | |
| 1 | `CREATE DATABASE tvxa_moi` + copy dữ liệu từ tvxa | |
| 2 | Xóa dữ liệu tenant cũ trong tvxa_moi | |
| 3 | INSERT 5 tenant mới | |
| 4 | `seed-clone-data.py` cho 5 tenant | |
| 5 | `docker compose build` + `up -d elibapi-new admin-website-new` | |
| 6 | Kiểm tra API qua port 8091 | |
| 7 | `docker stop elibapi-nginx` → start nginx-new | |
| 8 | Kiểm tra https://thuvientn.vn hoạt động | |
