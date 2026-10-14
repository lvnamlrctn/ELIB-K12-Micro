const PREFIX = 'elib.list.';

/**
 * Giữ bộ lọc + trang của màn danh sách khi sang màn sửa/chi tiết rồi quay lại (ELIB-LRC: quay về đúng trang đang xem).
 * Lưu theo tab (sessionStorage) — đóng tab hoặc đăng xuất thì về mặc định.
 */
export function rememberedSearch<T extends object>(key: string, defaults: T): T {
  try {
    const saved = JSON.parse(sessionStorage.getItem(PREFIX + key) ?? 'null') as Partial<T> | null;
    if (saved && typeof saved === 'object') {
      // Chỉ nhận các trường màn đang có — bản lưu từ phiên bản cũ không làm hỏng bộ lọc.
      const known = Object.fromEntries(Object.entries(saved).filter(([k]) => k in defaults));
      return { ...defaults, ...known };
    }
  } catch {
    /* trình duyệt chặn bộ nhớ hoặc giá trị hỏng */
  }
  return { ...defaults };
}

export function rememberSearch(key: string, search: object): void {
  try {
    sessionStorage.setItem(PREFIX + key, JSON.stringify(search));
  } catch {
    /* trình duyệt chặn bộ nhớ — quay lại về trang 1 */
  }
}

/**
 * Trang đã nhớ nằm ngoài kết quả (vừa xoá bản ghi cuối trang, dữ liệu đổi) → lùi về trang cuối có dữ liệu. Trả true khi đã đổi trang,
 * màn gọi tải lại.
 */
export function clampPage(search: { pageIndex?: number; pageSize?: number }, page: { items: unknown[]; totalCount: number }): boolean {
  const index = search.pageIndex ?? 1;
  if (page.items.length || index <= 1 || page.totalCount <= 0) return false;
  search.pageIndex = Math.max(1, Math.ceil(page.totalCount / (search.pageSize || 10)));
  return search.pageIndex !== index;
}

export function forgetListStates(): void {
  try {
    for (const k of Object.keys(sessionStorage)) if (k.startsWith(PREFIX)) sessionStorage.removeItem(k);
  } catch {
    /* trình duyệt chặn bộ nhớ */
  }
}
