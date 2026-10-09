#!/bin/sh
# Đẩy mã nguồn lên máy chỉ định và dựng stack DEV ở đó (build image ngay trên máy đích).
# Máy chạy lệnh chỉ cần ssh + tar (Git Bash trên Windows là đủ), không cần Docker.
#
#   ./deploy/dev/deploy.sh <user@host> [thư-mục-trên-máy-đích]   (mặc định ~/elib-dev)
#   ELIB_SSH_OPTS="-p 2222 -i ~/.ssh/elib" ./deploy/dev/deploy.sh dev@10.0.0.5
#
# Lần đầu: script dừng lại và nhắc chạy init-env.sh TRÊN máy đích — secret sinh và nằm ở máy đó, không đi qua máy này.
set -eu

if [ $# -lt 1 ]; then
  echo "Cách dùng: $0 <user@host> [thư-mục-trên-máy-đích]" >&2
  exit 1
fi
TARGET="$1"
REMOTE_DIR="${2:-elib-dev}"
SSH_OPTS="${ELIB_SSH_OPTS:-}"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"

echo "==> Kiểm tra Docker trên $TARGET"
# shellcheck disable=SC2086
ssh $SSH_OPTS "$TARGET" 'docker version --format "Docker {{.Server.Version}}" && docker compose version' \
  || { echo "Máy đích chưa có Docker Engine + compose plugin, hoặc user chưa thuộc nhóm docker." >&2; exit 1; }

echo "==> Đồng bộ mã nguồn sang $TARGET:$REMOTE_DIR"
# Chỉ gửi những gì image cần; .env và secret trên máy đích không bị đụng tới.
# shellcheck disable=SC2086
tar -C "$ROOT" -czf - \
    --exclude='bin' --exclude='obj' --exclude='TestResults' \
    --exclude='node_modules' --exclude='dist' --exclude='.angular' \
    --exclude='.env' --exclude='.env.*' --exclude='*.pfx' --exclude='*.pem' --exclude='*.key' \
    global.json .dockerignore src deploy \
  | ssh $SSH_OPTS "$TARGET" "rm -rf '$REMOTE_DIR/src' && mkdir -p '$REMOTE_DIR' && tar -xzf - -C '$REMOTE_DIR'"

# shellcheck disable=SC2086
if ! ssh $SSH_OPTS "$TARGET" "test -f '$REMOTE_DIR/deploy/dev/.env'"; then
  cat >&2 <<EOF

Máy đích chưa có .env. Tạo secret NGAY TRÊN máy đích rồi chạy lại script này:
  ssh $TARGET
  cd $REMOTE_DIR/deploy/dev && sh init-env.sh <DEV_DOMAIN> [GATEWAY_PORT]
(DEV_DOMAIN: tên miền wildcard trỏ về máy đó, hoặc <IP>.sslip.io)
EOF
  exit 2
fi

echo "==> Build và khởi động stack"
# shellcheck disable=SC2086
ssh $SSH_OPTS "$TARGET" "cd '$REMOTE_DIR/deploy/dev' && docker compose up -d --build --remove-orphans && docker compose ps"

echo "==> Xong. Kiểm tra nhanh: ssh $TARGET 'sh $REMOTE_DIR/deploy/dev/smoke.sh'"
