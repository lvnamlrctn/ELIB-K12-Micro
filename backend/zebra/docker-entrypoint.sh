#!/bin/bash
# Entrypoint container Zebra (Index Data) — server Z39.50 của ELIB.
#
# Phục vụ cùng lúc:
#   - 1 database GỘP        : toàn bộ biểu ghi mọi đơn vị   → mặc định "ELIB"
#   - 1 database MỖI ĐƠN VỊ : chỉ biểu ghi của đơn vị đó     → "ELIB_<mã đơn vị>"
#
# Nguồn dữ liệu là thư mục /export do ELIBAPI (ZebraExportJob) ghi ra, KHÔNG đọc CSDL:
#   /export/<mã đơn vị>/<bibid>.iso   — biểu ghi của 1 đơn vị (mã lấy từ Tenant.Code)
#   /export/_system/<bibid>.iso       — biểu ghi không thuộc đơn vị nào (tài khoản hệ thống tạo)
# Thêm đơn vị mới KHÔNG cần sửa cấu hình hay khởi động lại: thư mục mới xuất hiện là vòng lặp đánh chỉ
# mục kế tiếp tự tạo database tương ứng.
#
# VÌ SAO CÓ /export/_all:
# Zebra định danh bản ghi theo ĐƯỜNG DẪN FILE, nên không đánh chỉ mục cùng một file vào 2 database khác
# nhau được. Ta dựng /export/_all chứa HARDLINK tới mọi file (đặt tên "<mã>-<bibid>.iso"): khác đường dẫn
# nên Zebra coi là bản ghi riêng, nhưng dùng chung inode nên KHÔNG tốn thêm dung lượng và nội dung luôn
# khớp bản mới nhất (ZebraExportJob ghi đè tại chỗ, không đổi inode). Database gộp đánh chỉ mục /export/_all,
# mỗi database đơn vị đánh chỉ mục /export/<mã>.

set -euo pipefail

CFG=/zebra/zebra.cfg
EXPORT_DIR=/export
ALL_DIR="$EXPORT_DIR/_all"
COMBINED_DB="${ZEBRA_COMBINED_DB:-ELIB}"
DB_PREFIX="${ZEBRA_DB_PREFIX:-ELIB_}"
REINDEX_INTERVAL="${ZEBRA_REINDEX_INTERVAL:-60}"
INIT_MARKER=/var/lib/zebra/.initialized

mkdir -p /var/lib/zebra/reg /var/lib/zebra/shadow /var/lib/zebra/lock \
         /var/lib/zebra/tmp /var/lib/zebra/key "$EXPORT_DIR" "$ALL_DIR"

if [ ! -f "$INIT_MARKER" ]; then
  echo "[zebra] Khởi tạo register lần đầu..."
  zebraidx -c "$CFG" init
  touch "$INIT_MARKER"
fi

# Đồng bộ thư mục hardlink dùng cho database gộp.
sync_all_dir() {
  # 1) Thêm/cập nhật hardlink cho biểu ghi mới. Dùng phép so sánh -nt của bash (không sinh tiến trình con)
  #    để bắt cả trường hợp hiếm là file gốc bị thay inode — khi đó hardlink cũ giữ nội dung cũ.
  find "$EXPORT_DIR" -mindepth 2 -maxdepth 2 -name '*.iso' -type f ! -path "$ALL_DIR/*" -print0 |
  while IFS= read -r -d '' src; do
    rel=${src#"$EXPORT_DIR"/}
    code=${rel%%/*}
    file=${rel##*/}
    dest="$ALL_DIR/${code}-${file}"
    if [ ! -e "$dest" ] || [ "$src" -nt "$dest" ]; then
      ln -f "$src" "$dest" 2>/dev/null || cp -f "$src" "$dest"
    fi
  done

  # 2) Gỡ hardlink mồ côi: biểu ghi gốc đã bị xóa nên link count tụt về 1.
  find "$ALL_DIR" -name '*.iso' -type f -links 1 -delete
}

reindex() {
  sync_all_dir

  # Database gộp — MỌI biểu ghi tìm thấy dưới /export, kể cả _system và cả thư mục có tên không hợp lệ
  # (xem vòng lặp dưới): database gộp là "toàn bộ kho", không bỏ sót biểu ghi nào.
  zebraidx -c "$CFG" -d "$COMBINED_DB" update "$ALL_DIR" \
    || echo "[zebra] Lỗi đánh chỉ mục database gộp $COMBINED_DB" >&2

  # Mỗi đơn vị 1 database. Thư mục bắt đầu bằng "_" (_all, _system) không có database riêng.
  # Lỗi ở 1 đơn vị không được chặn các đơn vị còn lại → luôn "|| echo".
  for dir in "$EXPORT_DIR"/*/; do
    [ -d "$dir" ] || continue
    code=${dir%/}
    code=${code##*/}
    case "$code" in
      _*) continue ;;
      # Tên thư mục = tên database Z39.50, chỉ nhận [A-Za-z0-9_-]. ZebraExportJob đã lọc sẵn (SafeCode)
      # nên trường hợp này chỉ xảy ra khi có người chép file vào /export bằng tay.
      *[!A-Za-z0-9_-]*) echo "[zebra] Bỏ qua thư mục có tên không hợp lệ: $code" >&2; continue ;;
    esac
    zebraidx -c "$CFG" -d "${DB_PREFIX}${code}" update "$dir" \
      || echo "[zebra] Lỗi đánh chỉ mục đơn vị $code" >&2
  done
}

echo "[zebra] Đánh chỉ mục lần đầu (database gộp: $COMBINED_DB, tiền tố đơn vị: $DB_PREFIX)..."
reindex || echo "[zebra] Đánh chỉ mục lần đầu lỗi — bỏ qua, sẽ thử lại sau ${REINDEX_INTERVAL}s." >&2

# Vòng lặp đánh chỉ mục định kỳ chạy nền: bắt biểu ghi mới/sửa/xóa do ELIBAPI ghi ra giữa 2 lần chạy.
(
  while true; do
    sleep "$REINDEX_INTERVAL"
    reindex >/tmp/zebraidx-reindex.log 2>&1 \
      || echo "[zebra] Đánh chỉ mục định kỳ lỗi, xem /tmp/zebraidx-reindex.log" >&2
  done
) &

echo "[zebra] zebrasrv lắng nghe Z39.50 trên cổng 2100 trong container."
exec zebrasrv -c "$CFG" tcp:@:2100
