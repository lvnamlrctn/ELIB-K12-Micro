#!/bin/sh
# Tạo database + role riêng cho từng service (docs 04 §1). Role KHÔNG phải superuser và KHÔNG có BYPASSRLS —
# superuser luôn bỏ qua Row-Level Security, nên service tuyệt đối không được chạy bằng tài khoản postgres.
#
# Chạy được nhiều lần (bỏ qua role/database đã có, không đổi mật khẩu role cũ):
#   - lần đầu: docker-entrypoint-initdb.d khi volume Postgres còn trống;
#   - mỗi lần `docker compose up`: service db-init chạy lại để tạo DB cho service mới thêm vào stack.
set -eu

create_service_db() {
  db="$1"; role="$2"; password="$3"
  if [ -z "$password" ]; then
    echo "Thiếu mật khẩu cho role $role" >&2
    exit 1
  fi
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
       -v role="$role" -v password="$password" -v db="$db" <<'SQL'
SELECT format('CREATE ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS PASSWORD %L', :'role', :'password')
 WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'role') \gexec
SELECT format('CREATE DATABASE %I OWNER %I ENCODING %L TEMPLATE template0', :'db', :'role', 'UTF8')
 WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'db') \gexec
REVOKE ALL ON DATABASE :"db" FROM PUBLIC;
SQL
  # Schema public thuộc role của service (PG15+ mặc định không cho PUBLIC tạo bảng).
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$db" -v role="$role" <<'SQL'
ALTER SCHEMA public OWNER TO :"role";
SQL
}

create_service_db elib_identity     identity_app     "$IDENTITY_DB_PASSWORD"
create_service_db elib_tenant       tenant_app       "$TENANT_DB_PASSWORD"
create_service_db elib_notification notification_app "$NOTIFICATION_DB_PASSWORD"
create_service_db elib_audit        audit_app        "$AUDIT_DB_PASSWORD"
create_service_db elib_media        media_app        "$MEDIA_DB_PASSWORD"
create_service_db elib_patron       patron_app       "$PATRON_DB_PASSWORD"
create_service_db elib_catalog      catalog_app      "$CATALOG_DB_PASSWORD"
create_service_db elib_holdings     holdings_app     "$HOLDINGS_DB_PASSWORD"
create_service_db elib_circulation  circulation_app  "$CIRCULATION_DB_PASSWORD"
