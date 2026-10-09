#!/bin/sh
# Kiểm tra nhanh stack DEV — chạy TRÊN máy đích, trong thư mục bất kỳ.
#   sh ~/elib-dev/deploy/dev/smoke.sh
# Không in secret: chỉ báo đạt/không đạt.
set -u
cd "$(dirname "$0")"
set -a; . ./.env; set +a

GW="http://127.0.0.1:${GATEWAY_PORT:-8080}"
fail=0
ok()  { echo "  [OK]   $1"; }
bad() { echo "  [FAIL] $1"; fail=1; }
psql_q() { docker compose exec -T postgres psql -U postgres -d "$1" -tAc "$2" 2>/dev/null | tr -d '[:space:]'; }

echo "Gateway & identity"
code=$(curl -s -o /dev/null -w '%{http_code}' "$GW/healthz")
[ "$code" = 200 ] && ok "gateway /healthz" || bad "gateway /healthz → $code"

# Host gửi kèm cổng như trình duyệt — identity sinh URL endpoint từ host của request.
ID_HOST="id.$DEV_DOMAIN:${GATEWAY_PORT:-8080}"
json() { tr -d '\n' | sed -n "s/.*\"$1\": *\"\([^\"]*\)\".*/\1/p"; }

disco=$(curl -s -H "Host: $ID_HOST" "$GW/.well-known/openid-configuration")
issuer=$(printf '%s' "$disco" | json issuer)
jwks=$(printf '%s' "$disco" | json jwks_uri)
[ "$issuer" = "${IDENTITY_ISSUER:-http://$ID_HOST/}" ] && ok "OIDC discovery qua gateway (issuer $issuer)" || bad "OIDC discovery: issuer='$issuer'"
[ "$jwks" = "http://$ID_HOST/.well-known/jwks" ] && ok "jwks_uri đúng host công khai" || bad "jwks_uri='$jwks'"

token=$(curl -s -H "Host: $ID_HOST" "$GW/connect/token" \
  -d grant_type=client_credentials -d client_id=svc-gateway --data-urlencode "client_secret=$SVC_GATEWAY_SECRET" -d scope=elib-api \
  | json access_token)
[ -n "$token" ] && ok "client_credentials (svc-gateway) cấp được access token" || bad "client_credentials không cấp token"

# Token thật → gateway → tenant: tenant phải xác minh được chữ ký (JWKS) → 403 vì service không có quyền, KHÔNG phải 401.
code=$(curl -s -o /dev/null -w '%{http_code}' -H "Host: quantri.$DEV_DOMAIN:${GATEWAY_PORT:-8080}" -H "Authorization: Bearer $token" "$GW/api/system/tenant/tenants")
[ "$code" = 403 ] && ok "tenant xác minh JWT do identity ký (403 thiếu quyền, không phải 401)" || bad "token qua gateway → tenant: $code"

code=$(curl -s -o /dev/null -w '%{http_code}' -H "Host: $ID_HOST" "$GW/account/login")
[ "$code" = 200 ] && ok "trang đăng nhập" || bad "trang đăng nhập → $code"

admin=$(curl -s -H "Host: $ID_HOST" "$GW/admin/don-vi")
case "$admin" in
  *"<app-root>"*) ok "app Admin (route SPA trả index.html)" ;;
  *) bad "app Admin không phục vụ /admin/" ;;
esac
code=$(curl -s -o /dev/null -w '%{http_code}' -H "Host: $DEV_DOMAIN" "$GW/")
[ "$code" = 302 ] && ok "host hệ thống: / → /admin/" || bad "host hệ thống / → $code (mong 302)"
for h in $(docker compose exec -T postgres psql -U postgres -d elib_tenant -tAc "SELECT subdomain FROM tenants WHERE status = 1 LIMIT 1" 2>/dev/null); do
  opac=$(curl -s -H "Host: $h.$DEV_DOMAIN" "$GW/tim-kiem")
  case "$opac" in
    *"<opac-root>"*) ok "OPAC ở gốc host đơn vị $h (route SPA trả index.html)" ;;
    *) bad "OPAC không phục vụ trên $h.$DEV_DOMAIN" ;;
  esac
done

echo "Gateway → tenant"
body=$(curl -s -H "Host: khong-ton-tai.$DEV_DOMAIN" "$GW/api/opac/tenant/features")
case "$body" in
  *TENANT_NOT_FOUND*) ok "host lạ → TENANT_NOT_FOUND (gateway gọi được tenant bằng service token)" ;;
  *) bad "host lạ: $body" ;;
esac

echo "PostgreSQL: role service và RLS"
for role in identity_app tenant_app notification_app audit_app media_app; do
  r=$(psql_q postgres "SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname='$role'")
  [ "$r" = f ] && ok "$role không phải superuser, không BYPASSRLS" || bad "$role: superuser/bypassrls='$r'"
done
n=$(psql_q elib_identity "SELECT count(*) FROM pg_class c JOIN pg_policy p ON p.polrelid=c.oid WHERE c.relforcerowsecurity AND p.polname='elib_tenant_isolation'")
[ "${n:-0}" -ge 2 ] && ok "elib_identity: $n bảng có FORCE RLS" || bad "elib_identity: bảng có FORCE RLS = '${n:-?}'"
# Mọi bảng (schema public) có cột tenant_id phải có FORCE RLS + policy. Bỏ qua bảng bước khởi tạo của service tenant
# (tenant_provisioning_steps: tenant_id là khoá ngoại tới đơn vị đang tạo, chỉ ngữ cảnh hệ thống ghi).
for db in elib_tenant elib_notification elib_audit elib_media; do
  t=$(psql_q $db "SELECT count(*) FROM information_schema.columns col JOIN pg_class c ON c.relname=col.table_name AND c.relkind='r' LEFT JOIN pg_policy p ON p.polrelid=c.oid AND p.polname='elib_tenant_isolation' WHERE col.table_schema='public' AND col.column_name='tenant_id' AND col.table_name NOT LIKE '%provisioning%' AND (NOT c.relforcerowsecurity OR p.oid IS NULL)")
  [ "${t:-1}" = 0 ] && ok "$db: mọi bảng có tenant_id đều FORCE RLS" || bad "$db: $t bảng có tenant_id thiếu FORCE RLS"
  m=$(psql_q $db "SELECT count(*) FROM \"__EFMigrationsHistory\"")
  [ "${m:-0}" -ge 1 ] && ok "$db: đã migrate ($m migration)" || bad "$db chưa migrate"
done

echo "Notification"
code=$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:${MAILPIT_UI_PORT:-8025}/api/v1/info")
[ "$code" = 200 ] && ok "Mailpit (SMTP nền tảng của DEV) chạy" || bad "Mailpit API → $code"
for svc in notification audit media minio; do
  st=$(docker compose ps --format '{{.State}}' $svc 2>/dev/null)
  [ "$st" = running ] && ok "$svc đang chạy" || bad "$svc: '$st'"
done

echo "Media / MinIO (qua gateway /s3)"
# Bucket công khai: đọc từng file được, nhưng không ai liệt kê được (403) — bucket chưa tạo thì 404.
code=$(curl -s -o /dev/null -w '%{http_code}' -H "Host: $DEV_DOMAIN" "$GW/s3/media-public/")
[ "$code" = 403 ] && ok "media-public tồn tại, không liệt kê ẩn danh được" || bad "liệt kê media-public → $code (mong 403)"
code=$(curl -s -o /dev/null -w '%{http_code}' -H "Host: $DEV_DOMAIN" "$GW/s3/media-private/x")
[ "$code" = 403 ] && ok "media-private không đọc ẩn danh được" || bad "đọc media-private ẩn danh → $code (mong 403)"
hdr=$(curl -s -D - -o /dev/null -H "Host: $DEV_DOMAIN" "$GW/s3/media-public/khong-co.png" | tr -d '\r')
case "$hdr" in
  *"X-Content-Type-Options: nosniff"*"sandbox"*|*"sandbox"*"X-Content-Type-Options: nosniff"*) ok "/s3 trả kèm nosniff + CSP sandbox" ;;
  *) bad "/s3 thiếu header bảo vệ" ;;
esac

echo "RabbitMQ"
q=$(docker compose exec -T rabbitmq rabbitmqctl -q list_queues name 2>/dev/null | grep -c -E '^(identity|tenant|notification|audit)-' || true)
[ "${q:-0}" -ge 1 ] && ok "$q queue của identity/tenant/notification/audit đã khai báo" || bad "chưa thấy queue của service"

[ "$fail" = 0 ] && echo "TẤT CẢ ĐẠT" || { echo "CÓ MỤC KHÔNG ĐẠT — xem: docker compose logs identity tenant notification audit media gateway"; exit 1; }
