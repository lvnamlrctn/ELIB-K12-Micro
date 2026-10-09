import { Injectable, signal, PLATFORM_ID, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { isPlatformBrowser } from '@angular/common';
import { Router } from '@angular/router';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { ToastrService } from './shared/toastr.service';
import { ListPageStateService } from './shared/list-page-state.service';
import { APP_CONFIG } from '../opac/config';

const CHECK_INTERVAL_MS  = 60_000;      // kiểm tra mỗi 1 phút
const WARN_BEFORE_MS     = 5 * 60_000;  // cảnh báo trước 5 phút
const ADMIN_USER_KEY     = 'admin_user'; // key riêng cho session admin (tách biệt OPAC)

// Lớp bảo mật đăng nhập admin thứ 1 (tùy chọn, bật/tắt theo tenant qua SystemParameter
// ADMIN_LOGIN_CAPTCHA_ENABLED). Mirror CaptchaChallenge của OPAC (auth-api.service.ts).
export interface CaptchaChallenge {
  enabled: boolean;
  captchaId?: string;
  svg?: string;
}

// Kết quả login(): 3 khả năng — đăng nhập xong (success=true), cần xác thực OTP tiếp
// (otpRequired=true, dùng otpSessionToken để gọi verifyOtp), hoặc thất bại (success=false, có message).
export interface AdminLoginResult {
  success: boolean;
  message?: string;
  otpRequired?: boolean;
  otpSessionToken?: string;
}

@Injectable({
  providedIn: 'root'
})
export class Auth {
  private loggedIn     = signal<boolean>(false);
  private platformId   = inject(PLATFORM_ID);
  private http         = inject(HttpClient);
  private router       = inject(Router);
  private toastr       = inject(ToastrService);
  private listState    = inject(ListPageStateService);

  private expiryTimer: ReturnType<typeof setInterval> | null = null;
  private warnedExpiry = false;

  constructor() {
    if (isPlatformBrowser(this.platformId)) {
      const userStr = localStorage.getItem(ADMIN_USER_KEY);
      if (userStr) {
        if (this.isTokenExpired()) {
          localStorage.removeItem(ADMIN_USER_KEY);
        } else {
          this.loggedIn.set(true);
          this.startExpiryWatch();
        }
      }
    }
  }

  isLoggedIn() {
    return this.loggedIn;
  }

  // Giải mã trường `exp` từ JWT payload (không cần thư viện)
  private decodeExp(token: string): number | null {
    try {
      const payload = token.split('.')[1];
      const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
      const decoded = JSON.parse(json);
      return typeof decoded.exp === 'number' ? decoded.exp : null;
    } catch {
      return null;
    }
  }

  isTokenExpired(): boolean {
    const token = this.getToken();
    if (!token) return true;
    const exp = this.decodeExp(token);
    if (exp === null) return false; // không đọc được exp → coi như còn hạn
    return Date.now() >= exp * 1000;
  }

  // Số giây còn lại trước khi hết hạn (< 0 = đã hết hạn)
  getSecondsUntilExpiry(): number {
    const token = this.getToken();
    if (!token) return -1;
    const exp = this.decodeExp(token);
    if (exp === null) return Infinity;
    return exp - Math.floor(Date.now() / 1000);
  }

  private startExpiryWatch(): void {
    this.stopExpiryWatch();
    this.warnedExpiry = false;
    this.expiryTimer = setInterval(() => this.checkExpiry(), CHECK_INTERVAL_MS);
  }

  private stopExpiryWatch(): void {
    if (this.expiryTimer !== null) {
      clearInterval(this.expiryTimer);
      this.expiryTimer = null;
    }
  }

  private checkExpiry(): void {
    if (this.isTokenExpired()) {
      this.toastr.warning('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
      this.logout();
      this.router.navigate(['/admin/login']);
      return;
    }

    // Cảnh báo 5 phút trước khi hết hạn (chỉ một lần)
    if (!this.warnedExpiry && this.getSecondsUntilExpiry() <= WARN_BEFORE_MS / 1000) {
      this.warnedExpiry = true;
      this.toastr.warning('Phiên đăng nhập sẽ hết hạn trong vòng 5 phút.');
    }
  }

  // Lớp bảo mật đăng nhập admin thứ 1 (tùy chọn, bật/tắt theo tenant qua SystemParameter
  // ADMIN_LOGIN_CAPTCHA_ENABLED). enabled=false khi tenant chưa bật — component không hiển thị
  // ô CAPTCHA và không cần gửi captchaId/captchaAnswer khi login(). Mirror AuthApiService (OPAC).
  loadCaptcha(): Observable<CaptchaChallenge> {
    const params = APP_CONFIG.TenantId ? `?tenantId=${encodeURIComponent(APP_CONFIG.TenantId)}` : '';
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return this.http.get<any>(`${environment.baseApiUrl}/api/Auth/Captcha${params}`).pipe(
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

  login(loginName: string, pass: string, captcha?: { captchaId: string; captchaAnswer: string }): Observable<AdminLoginResult> {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const payload: any = { loginName, password: pass, tenantId: APP_CONFIG.TenantId };
    if (captcha) {
      payload.captchaId = captcha.captchaId;
      payload.captchaAnswer = captcha.captchaAnswer;
    }
    // Đa đơn vị: gửi kèm TenantId (resolve theo subdomain) để backend kiểm tra tài khoản đúng đơn vị.
    return this.http.post<any>(`${environment.baseApiUrl}/api/Auth/Login`, payload).pipe(
      map(res => this.mapLoginResult(res)),
      catchError(err => of({ success: false, message: err?.error?.message }))
    );
  }

  // Lớp bảo mật đăng nhập admin thứ 2: xác thực mã OTP 6 số gửi qua email, dùng otpSessionToken
  // nhận được từ login() khi otpRequired=true. Thành công thì đăng nhập y hệt login() thường.
  verifyOtp(otpSessionToken: string, code: string): Observable<AdminLoginResult> {
    const payload = { otpSessionToken, code };
    return this.http.post<any>(`${environment.baseApiUrl}/api/Auth/VerifyOtp`, payload).pipe(
      map(res => this.mapLoginResult(res)),
      catchError(err => of({ success: false, message: err?.error?.message }))
    );
  }

  // Dùng chung bởi login() và verifyOtp() — cả 2 đều nhận về LoginResponse khi thành công.
  private mapLoginResult(res: any): AdminLoginResult { // eslint-disable-line @typescript-eslint/no-explicit-any
    if (!(res && res.success && res.data)) {
      // Đăng nhập thất bại (200 nhưng success=false) → giữ message backend để hiển thị.
      return { success: false, message: res?.message };
    }

    const d = res.data;
    // Bước 1 (Login) khi tenant bật OTP: chưa có token thật, phải xác thực OTP tiếp.
    if (d.otpRequired) {
      return { success: false, otpRequired: true, otpSessionToken: d.otpSessionToken ?? '' };
    }
    if (!d.token) {
      return { success: false, message: res?.message };
    }

    if (isPlatformBrowser(this.platformId)) {
      localStorage.setItem(ADMIN_USER_KEY, JSON.stringify({
        id: d.id ?? null,
        publicId: d.publicId ?? null,
        roleId: d.roleId ?? null,
        name: d.fullName,
        loginName: d.loginName,
        token: d.token,
        isPrivileged: d.isPrivileged ?? false, // super-admin: xem/lọc theo nhiều đơn vị
        tenantId: d.tenantId ?? null
      }));
    }
    this.loggedIn.set(true);
    this.startExpiryWatch();
    return { success: true };
  }

  logout() {
    this.stopExpiryWatch();
    this.listState.clear(); // tài khoản kế tiếp (có thể khác đơn vị) không kế thừa trang danh sách cũ
    if (isPlatformBrowser(this.platformId)) {
      localStorage.removeItem(ADMIN_USER_KEY);
    }
    this.loggedIn.set(false);
  }

  getUser() {
    if (isPlatformBrowser(this.platformId)) {
      const u = localStorage.getItem(ADMIN_USER_KEY);
      return u ? JSON.parse(u) : null;
    }
    return null;
  }

  getToken() {
    const user = this.getUser();
    return user ? user.token : null;
  }

  // ID dùng cho GetPermission/{userId} — backend nhận publicId hoặc id
  getUserId(): string | number | null {
    const user = this.getUser();
    if (!user) return null;
    return user.publicId ?? user.id ?? null;
  }

  // Super-admin (được phép lọc theo nhiều đơn vị)
  isPrivileged(): boolean {
    return this.getUser()?.isPrivileged === true;
  }

  // TenantId của đơn vị người dùng (từ response login)
  getTenantId(): string | null {
    return this.getUser()?.tenantId ?? null;
  }
}
