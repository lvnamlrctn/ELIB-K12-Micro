/** Nội dung mã QR check-in phòng học nhóm: "ELIB-RB:{publicId của lượt đặt}". */
const PREFIX = 'ELIB-RB:';
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function toRoomBookingQr(publicId: string): string {
  return PREFIX + publicId;
}

/** Nhận chuỗi từ máy quét/camera/nhập tay: chấp nhận "ELIB-RB:{guid}" hoặc guid trần; sai định dạng trả null. */
export function parseRoomBookingQr(text: string | null | undefined): string | null {
  let s = (text ?? '').trim();
  if (s.toUpperCase().startsWith(PREFIX)) s = s.slice(PREFIX.length).trim();
  return GUID.test(s) ? s.toLowerCase() : null;
}
