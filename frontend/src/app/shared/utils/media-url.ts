import { environment } from '../../../environments/environment';

// Ảnh/media do backend lưu trên MinIO chỉ trả về path tương đối (vd "2026/07/xxx.png").
// Ghép vào route serving media hiện có (/api/public/media/<path>, cùng origin với mọi API khác)
// để tránh mixed-content khi FE chạy HTTPS. Nếu backend trả sẵn URL tuyệt đối thì giữ nguyên.
export function resolveMediaUrl(raw: string | null | undefined): string {
  if (!raw) return '';
  if (/^https?:\/\//i.test(raw)) return raw;
  return `${environment.baseApiUrl}/api/public/media/${raw.replace(/^\/+/, '')}`;
}
