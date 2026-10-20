import { HttpInterceptorFn } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Preferences } from './preferences';
import { Session } from './session';

/** Khoá tuỳ chọn theo tài khoản: cơ quan chủ quản / tên thư viện của bản in (cùng khoá với bản in nhãn) và địa danh ghi ở chân báo cáo. */
export const REPORT_HEADER_PREF = 'labels.header';
export const REPORT_PLACE_PREF = 'report.place';

/**
 * Thông tin ghi ở đầu/cuối mọi báo cáo in và xuất Excel (mẫu ELIB-LRC: cơ quan chủ quản, tên thư viện, "Địa danh, ngày … tháng … năm …",
 * khối chữ ký người lập báo cáo / thủ trưởng đơn vị). Cơ quan chủ quản và địa danh nhớ theo tài khoản; người lập báo cáo là người đang đăng nhập.
 */
@Injectable({ providedIn: 'root' })
export class ReportMeta {
  private readonly prefs = inject(Preferences);
  private readonly session = inject(Session);

  readonly parent = signal('');
  readonly place = signal('');

  constructor() {
    this.prefs.watch<{ parent?: string }>(REPORT_HEADER_PREF, (h) => this.parent.set(h?.parent ?? ''));
    this.prefs.watch<string>(REPORT_PLACE_PREF, (p) => this.place.set(typeof p === 'string' ? p : ''));
  }

  /** Họ tên người đang đăng nhập — ghi dưới "Người lập báo cáo". */
  maker(): string {
    return this.session.me()?.fullName || this.session.me()?.userName || '';
  }

  setPlace(place: string): void {
    this.place.set(place);
    this.prefs.set(REPORT_PLACE_PREF, place.trim() || null);
  }

  /** Header gửi kèm yêu cầu xuất Excel (mã hoá URL vì header chỉ nhận ASCII); người lập báo cáo backend lấy từ token. */
  headers(): Record<string, string> {
    const out: Record<string, string> = {};
    if (this.parent().trim()) out['X-Report-Parent'] = encodeURIComponent(this.parent().trim().slice(0, 250));
    if (this.place().trim()) out['X-Report-Place'] = encodeURIComponent(this.place().trim().slice(0, 100));
    return out;
  }
}

/** Mọi API xuất file (đường dẫn kết thúc bằng "Export") nhận thông tin đầu/cuối báo cáo — các endpoint khác bỏ qua. */
export const reportMetaInterceptor: HttpInterceptorFn = (req, next) =>
  req.url.startsWith('/api/') && req.method === 'POST' && /Export$/.test(req.url.split('?')[0])
    ? next(req.clone({ setHeaders: inject(ReportMeta).headers() }))
    : next(req);

/** "ngày 20 tháng 10 năm 2026" — dòng ngày của chân báo cáo. */
export function vnDateLine(d: Date = new Date()): string {
  return `ngày ${String(d.getDate()).padStart(2, '0')} tháng ${String(d.getMonth() + 1).padStart(2, '0')} năm ${d.getFullYear()}`;
}
