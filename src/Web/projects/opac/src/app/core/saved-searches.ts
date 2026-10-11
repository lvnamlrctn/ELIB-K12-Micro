import { Injectable, signal } from '@angular/core';

/** Điều kiện tìm như trên URL trang /tim-kiem (q, nhande, tacgia…, trang bỏ đi). */
export type SearchParams = Record<string, string | string[]>;

/**
 * Một tìm kiếm đã lưu. total: số kết quả lần bạn đọc xem gần nhất — so với số hiện tại để báo "có N kết quả mới"
 * (monolith: ReaderSavedSearch + job cảnh báo kết quả mới).
 */
export interface SavedSearch { id: string; name: string; params: SearchParams; total: number; savedAt: string; }

const KEY = 'opac.savedSearches';
export const MAX_SAVED = 20;

/**
 * Tìm kiếm đã lưu của bạn đọc — giữ trên trình duyệt này (localStorage, mỗi thư viện một tên miền nên tách sẵn theo đơn vị).
 * Chưa có đăng nhập bạn đọc nên chưa đồng bộ giữa các máy; trình duyệt chặn lưu trữ thì danh sách chỉ sống trong phiên.
 */
@Injectable({ providedIn: 'root' })
export class SavedSearches {
  readonly items = signal<SavedSearch[]>(read());

  save(name: string, params: SearchParams, total: number): SavedSearch {
    const entry: SavedSearch = { id: newId(), name: name.trim().slice(0, 120) || 'Tìm kiếm', params, total, savedAt: new Date().toISOString() };
    const same = this.items().filter((s) => key(s.params) !== key(params));
    this.set([entry, ...same].slice(0, MAX_SAVED));
    return entry;
  }

  /** Bạn đọc đã xem kết quả hiện tại — mốc mới để đếm kết quả mới. */
  seen(id: string, total: number): void {
    this.set(this.items().map((s) => (s.id === id ? { ...s, total } : s)));
  }

  remove(id: string): void {
    this.set(this.items().filter((s) => s.id !== id));
  }

  find(params: SearchParams): SavedSearch | undefined {
    const k = key(params);
    return this.items().find((s) => key(s.params) === k);
  }

  private set(items: SavedSearch[]): void {
    this.items.set(items);
    try {
      localStorage.setItem(KEY, JSON.stringify(items));
    } catch {
      // Trình duyệt chặn lưu trữ (chế độ riêng tư…) — vẫn dùng được trong phiên.
    }
  }
}

/** Khoá so sánh: tham số sắp theo tên, giá trị nhiều lựa chọn sắp theo thứ tự. */
export function key(params: SearchParams): string {
  return JSON.stringify(Object.keys(params).sort().map((k) => [k, Array.isArray(params[k]) ? [...(params[k] as string[])].sort() : params[k]]));
}

function read(): SavedSearch[] {
  try {
    const parsed: unknown = JSON.parse(localStorage.getItem(KEY) ?? '[]');
    return Array.isArray(parsed)
      ? parsed.filter((s): s is SavedSearch => !!s && typeof s.id === 'string' && typeof s.name === 'string' && typeof s.params === 'object' && s.params !== null)
          .slice(0, MAX_SAVED)
      : [];
  } catch {
    return [];
  }
}

function newId(): string {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random().toString(36).slice(2)}`;
}
