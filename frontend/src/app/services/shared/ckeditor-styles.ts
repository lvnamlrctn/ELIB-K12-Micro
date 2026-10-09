/**
 * Nạp CSS của CKEditor theo yêu cầu, đúng 1 lần.
 *
 * File `ckeditor5.css` nặng ~229KB nhưng chỉ 2 trang admin cần (Tin tức, Tham số hệ thống).
 * Trước đây nó nằm trong mảng `styles` toàn cục của angular.json nên MỌI trang — kể cả trang
 * tra cứu công khai — đều phải tải. Nay file được copy sang `/vendor/ckeditor5.css` qua mục
 * `assets` (đường dẫn cố định, không bị outputHashing đổi tên) và chỉ được chèn vào đúng lúc
 * khởi tạo trình soạn thảo, ngay cạnh chỗ `await import('ckeditor5')`.
 */
const HREF = '/vendor/ckeditor5.css';
let injected = false;

export function ensureCkeditorStyles(): void {
  // Guard cho SSR (khu vực admin render phía client nên thực tế không chạy trên server).
  if (injected || typeof document === 'undefined') return;
  injected = true;

  const link = document.createElement('link');
  link.rel = 'stylesheet';
  link.href = HREF;
  document.head.appendChild(link);
}
