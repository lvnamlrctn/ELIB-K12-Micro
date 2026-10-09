import { APP_CONFIG } from '../config';
import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { isPlatformServer } from '@angular/common';
import { Observable, catchError, map, of } from 'rxjs';
import { User, ReaderCardProfile } from './models';

// readerId hợp lệ = GUID (publicId của bạn đọc). Dùng để loại id rác.
const GUID_RE = /^[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$/;
export function isGuid(v: unknown): v is string {
  return typeof v === 'string' && GUID_RE.test(v);
}

export interface CaptchaChallenge {
  enabled: boolean;
  captchaId?: string;
  svg?: string;
}

// Kết quả login(): 3 khả năng — đăng nhập xong (user != null), cần xác thực OTP tiếp
// (otpRequired = true, dùng otpSessionToken để gọi verifyOtp), hoặc thất bại (user null, có message).
export interface ReaderLoginResult {
  user: User | null;
  message?: string;
  otpRequired?: boolean;
  otpSessionToken?: string;
}

@Injectable({ providedIn: 'root' })
export class AuthApiService {
  private http = inject(HttpClient);
  private platformId = inject(PLATFORM_ID);

  private backendBase = APP_CONFIG.BackendBase; // Assuming Auth uses the same backend

  private get authBaseUrl(): string {
    return isPlatformServer(this.platformId)
      ? `${this.backendBase}/api/public/PublicReader`
      : '/api/public/PublicReader';
  }

  // Lớp bảo mật đăng nhập bạn đọc thứ 1 (tùy chọn, bật/tắt theo tenant qua SystemParameter
  // READER_LOGIN_CAPTCHA_ENABLED). enabled=false khi tenant chưa bật — component không hiển thị
  // ô CAPTCHA và không cần gửi captchaId/captchaAnswer khi login().
  getCaptcha(): Observable<CaptchaChallenge> {
    const params = APP_CONFIG.TenantId ? `?tenantId=${encodeURIComponent(APP_CONFIG.TenantId)}` : '';
    return this.http.get<any>(`${this.authBaseUrl}/Captcha${params}`).pipe(
      map(res => {
        const d = res?.data ?? res;
        return {
          enabled: !!d?.enabled,
          captchaId: d?.captchaId,
          svg: d?.svg
        } as CaptchaChallenge;
      }),
      catchError(() => of({ enabled: false } as CaptchaChallenge))
    );
  }

  // Passwords separate for mock logic if it needs fallback, but we should hit the real API
  login(username: string, password: string, captcha?: { captchaId: string; captchaAnswer: string }): Observable<ReaderLoginResult> {
    const payload: any = {
      loginName: username,
      password: password,
      tenantId: APP_CONFIG.TenantId // scope tài khoản bạn đọc theo đơn vị (đa tenant)
    };
    if (captcha) {
      payload.captchaId = captcha.captchaId;
      payload.captchaAnswer = captcha.captchaAnswer;
    }

    return this.http.post<any>(`${this.authBaseUrl}/Login`, payload).pipe(
      map(res => this.mapLoginResult(res, username)),
      catchError(err => {
        console.warn('login API failed', err);
        return of({ user: null, message: err?.error?.message });
      })
    );
  }

  // Lớp bảo mật đăng nhập bạn đọc thứ 2: xác thực mã OTP 6 số gửi qua email, dùng otpSessionToken
  // nhận được từ login() khi otpRequired=true. Thành công thì trả về user y hệt login() thường.
  verifyOtp(otpSessionToken: string, code: string): Observable<ReaderLoginResult> {
    const payload = { otpSessionToken, code };
    return this.http.post<any>(`${this.authBaseUrl}/VerifyOtp`, payload).pipe(
      map(res => this.mapLoginResult(res)),
      catchError(err => {
        console.warn('verifyOtp API failed', err);
        return of({ user: null, message: err?.error?.message });
      })
    );
  }

  // Đợt 22.6 — hồ sơ thẻ bạn đọc (hạn thẻ...), gọi lại mỗi lần mở trang Thẻ bạn đọc điện tử để làm mới;
  // opac/auth.interceptor.ts gắn Bearer token bạn đọc cho path này ([Authorize] ở backend).
  getProfile(): Observable<ReaderCardProfile | null> {
    return this.http.get<any>(`${this.authBaseUrl}/Profile`).pipe(
      map(res => {
        const d = res?.data ?? res;
        if (!d) return null;
        return {
          cardno: d.cardno ?? d.Cardno ?? '',
          fullName: d.fullName ?? d.FullName ?? '',
          issueDate: d.issueDate ?? d.IssueDate ?? null,
          expireDate: d.expireDate ?? d.ExpireDate ?? null,
        } as ReaderCardProfile;
      }),
      catchError(() => of(null))
    );
  }

  // Dùng chung bởi login() và verifyOtp() — cả 2 đều nhận về ReaderLoginResponse khi thành công.
  private mapLoginResult(res: any, fallbackUsername?: string): ReaderLoginResult {
    // Đăng nhập thất bại (success=false) → trả message backend, KHÔNG tạo User rỗng.
    if (res && res.success === false) {
      return { user: null, message: res?.message };
    }
    // Response: ReaderLoginResponse(Token, FullName, Cardno, UserId, PublicId, ..., OtpRequired, OtpSessionToken).
    // Đọc cả 2 kiểu bọc ({data:{...}} hoặc phẳng) và cả 2 casing (camelCase/PascalCase).
    const d = res?.data ?? res;
    if (!d) return { user: null, message: res?.message };

    // Bước 1 (Login) khi tenant bật OTP: chưa có token thật, phải xác thực OTP tiếp.
    if (d.otpRequired ?? d.OtpRequired) {
      return { user: null, otpRequired: true, otpSessionToken: d.otpSessionToken ?? d.OtpSessionToken ?? '' };
    }

    // readerId cho favorite/review = PublicId (GUID) của bạn đọc. KHÔNG fallback số ngẫu nhiên.
    const readerId = d.publicId ?? d.PublicId ?? '';
    if (!isGuid(readerId)) {
      console.warn('Login bạn đọc không trả PublicId (GUID) — favorite/review sẽ không hoạt động cho tới khi backend trả PublicId', d);
    }
    const userObj: User = {
      id: isGuid(readerId) ? readerId : '',
      username: d.cardno ?? d.Cardno ?? d.loginName ?? fallbackUsername ?? '',
      fullName: d.fullName ?? d.FullName ?? fallbackUsername ?? '',
      email: d.email ?? d.Email ?? '',
      role: 'user',
      tenantId: d.tenantId ?? d.TenantId ?? '', // dùng scope EbookFavorite theo đơn vị của bạn đọc
      token: d.token ?? d.Token ?? '' // JWT bạn đọc — gửi khi kiểm tra quyền đọc/tải tài liệu
    };
    return { user: userObj };
  }
}
