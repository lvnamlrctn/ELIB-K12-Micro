import { DOCUMENT, formatDate, formatNumber, registerLocaleData } from '@angular/common';
import localeVi from '@angular/common/locales/vi';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Injectable, Pipe, PipeTransform, inject, signal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

export type Lang = 'vi' | 'en';
export const LANGS: readonly Lang[] = ['vi', 'en'];
/** Khoá lưu ngôn ngữ (như ELIB-LRC opac-lrc/services/lang.ts) — dùng chung giữa OPAC và Admin cùng host. */
const STORAGE_KEY = 'elib.lang';
/** Cookie cho trang đăng nhập của identity (render ở server, cùng host qua gateway). */
const COOKIE = 'elib_lang';
/** Locale Angular cho ngày/số. */
const LOCALES: Record<Lang, string> = { vi: 'vi', en: 'en-US' };

registerLocaleData(localeVi, 'vi');

function saved(): Lang {
  try {
    const v = localStorage.getItem(STORAGE_KEY);
    if (v === 'vi' || v === 'en') return v;
  } catch {
    /* chế độ riêng tư — dùng mặc định */
  }
  return 'vi';
}

/** Bản đang chạy — cho hàm thuần (errorMessage) dùng khi không có DI. */
let current: Language | null = null;

/** Thông báo lỗi theo ngôn ngữ cho code không inject được Language (hàm errorMessage dùng ở mọi màn). */
export function translateError(error: unknown): string | null {
  return current ? current.apiError(error) : null;
}

/** Ngôn ngữ giao diện (vi mặc định, en). Đổi ngay không tải lại trang; nhớ trên trình duyệt. */
@Injectable({ providedIn: 'root' })
export class Language {
  private readonly translate = inject(TranslateService);
  private readonly document = inject(DOCUMENT);
  readonly lang = signal<Lang>(saved());

  /** Chạy khi khởi động app — chờ nạp từ điển để lần vẽ đầu đã đúng ngôn ngữ. */
  init(): Promise<unknown> {
    current = this;
    return this.apply(this.lang());
  }

  use(lang: Lang): Promise<unknown> {
    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch {
      /* bỏ qua */
    }
    return this.apply(lang);
  }

  /** Dịch trong code (toast, tiêu đề trang…) — khoá chưa nạp thì trả lại khoá. */
  t(key: string, params?: Record<string, unknown>): string {
    return this.translate.instant(key, params) as string;
  }

  /**
   * Thông báo lỗi API theo ngôn ngữ: dịch theo mã lỗi ổn định của backend (ERRORS.<code>), chưa có bản dịch thì dùng câu backend gửi.
   */
  apiError(error: unknown): string {
    // Lỗi do chính app ném (new Error('KHOÁ.DỊCH' hoặc câu thường)).
    if (!(error instanceof HttpErrorResponse)) return error instanceof Error && error.message ? this.t(error.message) : this.t('ERRORS.UNKNOWN');
    if (error.status === 0) return this.t('ERRORS.NETWORK');
    if (error.status === 401) return this.t('ERRORS.SESSION_EXPIRED');
    if (error.status === 429) return this.t('ERRORS.TOO_MANY_REQUESTS');
    const body = error.error as { code?: string; detail?: string; title?: string } | null;
    if (body?.code) {
      const key = `ERRORS.${body.code}`;
      const text = this.t(key);
      if (text !== key) return text;
    }
    if (body?.detail) return body.detail;
    if (error.status === 403) return this.t('ERRORS.PERMISSION_DENIED');
    return body?.title || this.t('ERRORS.HTTP', { status: error.status });
  }

  locale(): string {
    return LOCALES[this.lang()];
  }

  private apply(lang: Lang): Promise<unknown> {
    this.lang.set(lang);
    this.document.documentElement.lang = lang;
    this.document.cookie = `${COOKIE}=${lang}; path=/; max-age=31536000; SameSite=Lax`;
    return firstValueFrom(this.translate.use(lang));
  }
}

/** Ngày theo ngôn ngữ đang chọn (thay DatePipe có LOCALE_ID cố định). */
@Pipe({ name: 'ldate', pure: false })
export class LDatePipe implements PipeTransform {
  private readonly language = inject(Language);
  private last: { value: unknown; format: string; locale: string; out: string | null } | null = null;

  transform(value: string | number | Date | null | undefined, format = 'dd/MM/yyyy'): string | null {
    const locale = this.language.locale();
    if (this.last && this.last.value === value && this.last.format === format && this.last.locale === locale) return this.last.out;
    const out = value === null || value === undefined || value === '' ? null : formatDate(value, format, locale);
    this.last = { value, format, locale, out };
    return out;
  }
}

/** Số theo ngôn ngữ đang chọn. */
@Pipe({ name: 'lnumber', pure: false })
export class LNumberPipe implements PipeTransform {
  private readonly language = inject(Language);

  transform(value: number | null | undefined, digits = '1.0-0'): string | null {
    return value === null || value === undefined ? null : formatNumber(value, this.language.locale(), digits);
  }
}

/** API của hệ thống nhận Accept-Language — để backend trả thông báo theo ngôn ngữ khi có (hiện frontend dịch theo mã lỗi). */
export const languageInterceptor: HttpInterceptorFn = (req, next) =>
  req.url.startsWith('/api/') ? next(req.clone({ setHeaders: { 'Accept-Language': inject(Language).lang() } })) : next(req);
