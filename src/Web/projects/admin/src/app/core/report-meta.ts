import { HttpClient, HttpInterceptorFn } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Preferences } from './preferences';
import { Session } from './session';

/** Khoá tuỳ chọn theo tài khoản (cũ): chỉ còn dùng làm giá trị dự phòng khi đơn vị chưa khai báo tham số PARENTLIBRARY / GEOGRAPHIC. */
export const REPORT_HEADER_PREF = 'labels.header';
export const REPORT_PLACE_PREF = 'report.place';

/**
 * Thông tin ghi ở đầu/cuối mọi báo cáo in và xuất Excel (mẫu ELIB-LRC: cơ quan chủ quản, tên thư viện, "Địa danh, ngày … tháng … năm …",
 * khối chữ ký người lập báo cáo / thủ trưởng đơn vị). Cơ quan chủ quản, tên thư viện, địa danh lấy từ tham số của đơn vị (PARENTLIBRARY, LIBRARYNAME,
 * GEOGRAPHIC — màn Tham số); sửa ở màn in chỉ áp dụng cho phiên làm việc hiện tại, không ghi đè tham số. Người lập báo cáo là người đang đăng nhập.
 */
@Injectable({ providedIn: 'root' })
export class ReportMeta {
  private readonly prefs = inject(Preferences);
  private readonly session = inject(Session);
  private readonly http = inject(HttpClient);

  private readonly param = signal({ parent: '', library: '', place: '' });
  private readonly pref = signal({ parent: '', place: '' });
  private readonly edit = signal<{ parent?: string; library?: string; place?: string }>({});

  /** Cơ quan chủ quản: sửa tại chỗ > tham số PARENTLIBRARY > tuỳ chọn cũ của tài khoản. */
  readonly parent = computed(() => this.edit().parent ?? (this.param().parent || this.pref().parent));
  /** Tên thư viện: sửa tại chỗ > tham số LIBRARYNAME > tên đơn vị. */
  readonly library = computed(() => this.edit().library ?? (this.param().library || this.session.features()?.name || ''));
  /** Địa danh: sửa tại chỗ > tham số GEOGRAPHIC > tuỳ chọn cũ của tài khoản. */
  readonly place = computed(() => this.edit().place ?? (this.param().place || this.pref().place));

  constructor() {
    this.prefs.watch<{ parent?: string }>(REPORT_HEADER_PREF, (h) => this.pref.update((p) => ({ ...p, parent: h?.parent ?? '' })));
    this.prefs.watch<string>(REPORT_PLACE_PREF, (v) => this.pref.update((p) => ({ ...p, place: typeof v === 'string' ? v : '' })));
    // Tham số công khai (nhân viên chỉ có quyền báo cáo vẫn đọc được); lỗi mạng → dùng giá trị dự phòng.
    this.http.get<{ code: string; value: string | null }[]>('/api/opac/tenant/parameters', { params: { codes: 'PARENTLIBRARY,LIBRARYNAME,GEOGRAPHIC' } }).subscribe({
      next: (rows) => {
        const get = (code: string) => plain(rows.find((r) => r.code === code)?.value);
        this.param.set({ parent: get('PARENTLIBRARY'), library: get('LIBRARYNAME'), place: get('GEOGRAPHIC') });
      },
      error: () => undefined,
    });
  }

  /** Bỏ các chỗ đã sửa tay ở màn in trước — mỗi lần mở màn in lại bắt đầu từ tham số của đơn vị. */
  resetEdits(): void {
    this.edit.set({});
  }

  setParent(parent: string): void {
    this.edit.update((e) => ({ ...e, parent }));
  }

  setLibrary(library: string): void {
    this.edit.update((e) => ({ ...e, library }));
  }

  /** Họ tên người đang đăng nhập — ghi dưới "Người lập báo cáo". */
  maker(): string {
    return this.session.me()?.fullName || this.session.me()?.userName || '';
  }

  setPlace(place: string): void {
    this.edit.update((e) => ({ ...e, place }));
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
  return `ngày ${String(d.getDate()).padStart(2, '0')} tháng ${String(d.getMonth() + 1).padStart(2, '0')} năm ${d.getFullYear()}`; // i18n-data: dòng ngày in trên báo cáo (mẫu văn bản hành chính)
}

/** Giá trị tham số có thể mang thẻ HTML (dữ liệu chuyển từ ELIB-LRC: "<p>Thái  Nguyên</p>") — bỏ thẻ, gộp khoảng trắng. */
function plain(value: string | null | undefined): string {
  return (value ?? '').replace(/<[^>]*>/g, ' ').replace(/&nbsp;/g, ' ').replace(/\s+/g, ' ').trim();
}
