import { Injectable, Signal, computed, inject, signal } from '@angular/core';
import { Api } from './api';

const LOCAL_PREFIX = 'elib.pref.';

/**
 * Tuỳ chọn giao diện theo tài khoản (ELIB-LRC: AdminPreference) — lưu ở identity (/api/me/preferences) để đổi máy vẫn giữ.
 * Bản sao ở localStorage dùng ngay khi mở màn và khi không gọi được server (mất mạng, đóng vai đơn vị chỉ đọc).
 */
@Injectable({ providedIn: 'root' })
export class Preferences {
  private readonly api = inject(Api);
  private server: Promise<Record<string, string>> | null = null;

  /** Giá trị đã lưu (đã parse JSON) — đọc localStorage trước, server trả về thì gọi lại `apply` nếu khác. */
  watch<T>(key: string, apply: (value: T) => void): void {
    const local = readLocal(key);
    if (local != null) apply(parse<T>(local) as T);
    this.server ??= this.api.preferences().catch(() => ({}) as Record<string, string>);
    void this.server.then((all) => {
      const remote = all[key];
      if (remote == null || remote === local) return;
      writeLocal(key, remote);
      const value = parse<T>(remote);
      if (value != null) apply(value);
    });
  }

  set(key: string, value: unknown): void {
    const json = value == null ? null : JSON.stringify(value);
    writeLocal(key, json);
    void this.server?.then((all) => {
      if (json == null) delete all[key];
      else all[key] = json;
    });
    this.api.setPreference(key, json).catch(() => {
      /* chỉ đọc hoặc mất mạng — vẫn nhớ trên trình duyệt này */
    });
  }

}

/** Đăng xuất: lần đăng nhập sau (có thể là tài khoản khác) đọc lại từ server, không thấy cột của người trước. */
export function forgetLocalPreferences(): void {
  try {
    for (const k of Object.keys(localStorage)) if (k.startsWith(LOCAL_PREFIX)) localStorage.removeItem(k);
  } catch {
    /* trình duyệt chặn bộ nhớ */
  }
}

export interface ColumnDef<K extends string> { key: K; label: string; shown: boolean; }

export interface ColumnPrefs<K extends string> {
  readonly columns: readonly ColumnDef<K>[];
  readonly shown: Signal<Set<K>>;
  readonly visible: Signal<ColumnDef<K>[]>;
  has(key: K): boolean;
  toggle(key: K): void;
  reset(): void;
}

/**
 * Cột hiện/ẩn của một bảng, nhớ theo tài khoản (gọi trong field initializer của component). `legacyKey`: khoá localStorage
 * cũ của màn (trước khi lưu theo tài khoản) — đọc một lần để không mất lựa chọn đã có.
 */
export function columnPrefs<K extends string>(key: string, columns: readonly ColumnDef<K>[], legacyKey?: string): ColumnPrefs<K> {
  const prefs = inject(Preferences);
  const prefKey = `columns.${key}`;
  const defaults = () => new Set(columns.filter((c) => c.shown).map((c) => c.key));
  const valid = (keys: unknown): Set<K> | null => {
    const list = Array.isArray(keys) ? keys.filter((k): k is K => columns.some((c) => c.key === k)) : [];
    return list.length ? new Set(list) : null;
  };
  const shown = signal<Set<K>>(defaults());
  if (legacyKey) {
    const legacy = valid(parse(readRaw(legacyKey)));
    if (legacy) shown.set(legacy);
  }
  prefs.watch<K[]>(prefKey, (keys) => shown.set(valid(keys) ?? defaults()));
  return {
    columns,
    shown,
    visible: computed(() => columns.filter((c) => shown().has(c.key))),
    has: (k) => shown().has(k),
    toggle: (k) => {
      const next = new Set(shown());
      if (next.has(k)) next.delete(k);
      else next.add(k);
      if (!next.size) return;
      shown.set(next);
      prefs.set(prefKey, columns.filter((c) => next.has(c.key)).map((c) => c.key));
    },
    reset: () => {
      shown.set(defaults());
      prefs.set(prefKey, null);
    },
  };
}

function parse<T>(json: string | null): T | null {
  if (json == null) return null;
  try {
    return JSON.parse(json) as T;
  } catch {
    return null;
  }
}

function readRaw(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function readLocal(key: string): string | null {
  return readRaw(LOCAL_PREFIX + key);
}

function writeLocal(key: string, json: string | null): void {
  try {
    if (json == null) localStorage.removeItem(LOCAL_PREFIX + key);
    else localStorage.setItem(LOCAL_PREFIX + key, json);
  } catch {
    /* chế độ riêng tư — chỉ nhớ trong phiên */
  }
}
