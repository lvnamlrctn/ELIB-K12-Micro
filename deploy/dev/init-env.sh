#!/bin/sh
# Sinh deploy/dev/.env với secret ngẫu nhiên. Không ghi đè file đã có (đổi secret sau khi Postgres đã khởi tạo
# sẽ lệch với mật khẩu role trong volume — muốn đổi thì xoá volume: docker compose down -v).
#   ./init-env.sh <DEV_DOMAIN> [GATEWAY_PORT]
#   ví dụ: ./init-env.sh 10.0.0.5.sslip.io 8080
#   cổng khác (máy dùng chung): POSTGRES_PORT=15432 RABBITMQ_UI_PORT=15673 ./init-env.sh ...
set -eu
cd "$(dirname "$0")"

if [ -e .env ]; then
  echo ".env đã tồn tại — giữ nguyên. Xoá file (và volume) nếu muốn sinh lại." >&2
  exit 1
fi
if [ $# -lt 1 ] || [ -z "$1" ]; then
  echo "Cách dùng: $0 <DEV_DOMAIN> [GATEWAY_PORT]" >&2
  exit 1
fi

secret() { od -An -N"$1" -tx1 /dev/urandom | tr -d ' \n'; }

umask 077
cat > .env <<EOF
DEV_DOMAIN=$1
GATEWAY_PORT=${2:-8080}
GATEWAY_BIND=${GATEWAY_BIND:-0.0.0.0}
POSTGRES_PORT=${POSTGRES_PORT:-5432}
RABBITMQ_UI_PORT=${RABBITMQ_UI_PORT:-15672}
MAILPIT_UI_PORT=${MAILPIT_UI_PORT:-8025}
GRAFANA_PORT=${GRAFANA_PORT:-3000}

POSTGRES_PASSWORD=$(secret 24)
IDENTITY_DB_PASSWORD=$(secret 24)
TENANT_DB_PASSWORD=$(secret 24)
NOTIFICATION_DB_PASSWORD=$(secret 24)
AUDIT_DB_PASSWORD=$(secret 24)
MEDIA_DB_PASSWORD=$(secret 24)
MINIO_ROOT_USER=elib
MINIO_ROOT_PASSWORD=$(secret 24)
RABBITMQ_USER=elib
RABBITMQ_PASSWORD=$(secret 24)
GATEWAY_SIGNING_KEY=$(secret 32)
SVC_GATEWAY_SECRET=$(secret 32)
SVC_TENANT_SECRET=$(secret 32)
SVC_NOTIFICATION_SECRET=$(secret 32)
SVC_IDENTITY_SECRET=$(secret 32)
SVC_AUDIT_SECRET=$(secret 32)
BOOTSTRAP_ADMIN_PASSWORD=Elib$(secret 10)9
EOF

echo "Đã tạo $(pwd)/.env (quyền 600). Mật khẩu sysadmin nằm ở BOOTSTRAP_ADMIN_PASSWORD — giữ kín, chỉ đọc trên máy này."
