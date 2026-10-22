import { HttpClient, HttpInterceptorFn } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { User, UserManager, WebStorageStateStore } from 'oidc-client-ts';
import { firstValueFrom } from 'rxjs';

/** API chỉ dành cho bạn đọc đã đăng nhập — chỉ các đường dẫn này mới được gắn token. */
const READER_APIS = ['/api/opac/account/', '/api/opac/circulation/', '/api/opac/digital/', '/api/opac/payment/'];

export interface ReaderMe {
  readerPublicId: string; cardNo: string; fullName: string; email: string | null; expireDate: string | null;
  mustChangePassword: boolean; lastLoginAt: string | null;
}

/**
 * Đăng nhập bạn đọc (Authorization Code + PKCE, client elib-opac) với identity qua gateway cùng origin — trang đăng nhập của identity
 * nằm trên chính host của trường. Token chỉ giữ trong sessionStorage: máy tính dùng chung ở thư viện, đóng tab là hết phiên.
 */
@Injectable({ providedIn: 'root' })
export class ReaderAuth {
  private readonly http = inject(HttpClient);
  private readonly manager = new UserManager({
    authority: `${location.origin}/`,
    client_id: 'elib-opac',
    redirect_uri: `${location.origin}/tai-khoan/callback`,
    post_logout_redirect_uri: `${location.origin}/`,
    response_type: 'code',
    scope: 'openid profile offline_access elib-api',
    automaticSilentRenew: true,
    loadUserInfo: false,
    userStore: new WebStorageStateStore({ store: sessionStorage }),
  });

  readonly user = signal<User | null>(null);
  readonly loggedIn = computed(() => !!this.user());
  readonly displayName = computed(() => (this.user()?.profile.name as string | undefined) ?? '');

  constructor() {
    this.manager.events.addUserLoaded((u) => this.user.set(u));
    this.manager.events.addUserUnloaded(() => this.user.set(null));
    this.manager.events.addSilentRenewError(() => this.user.set(null));
  }

  /** Phiên còn hợp lệ (làm mới bằng refresh token khi access token hết hạn), hoặc null. */
  async restore(): Promise<User | null> {
    let user = await this.manager.getUser();
    if (user?.expired && user.refresh_token) {
      try {
        user = await this.manager.signinSilent();
      } catch {
        await this.manager.removeUser();
        user = null;
      }
    }
    this.user.set(user && !user.expired ? user : null);
    return this.user();
  }

  login(returnUrl = '/tai-khoan'): Promise<void> {
    return this.manager.signinRedirect({ state: returnUrl });
  }

  /** Xử lý /tai-khoan/callback; trả về đường dẫn trong OPAC cần quay lại. */
  async completeLogin(): Promise<string> {
    const user = await this.manager.signinRedirectCallback();
    this.user.set(user);
    const target = typeof user.state === 'string' ? user.state : '/tai-khoan';
    return target.startsWith('/') && !target.startsWith('//') ? target : '/tai-khoan';
  }

  /** Bỏ phiên trên trình duyệt (token bị từ chối) — không gọi identity. */
  async forget(): Promise<void> {
    await this.manager.removeUser();
    this.user.set(null);
  }

  async logout(): Promise<void> {
    await this.manager.signoutRedirect();
  }

  accessToken(): string | null {
    return this.user()?.access_token ?? null;
  }

  me(): Promise<ReaderMe> {
    return firstValueFrom(this.http.get<ReaderMe>('/api/opac/account/me'));
  }

  changePassword(currentPassword: string, newPassword: string): Promise<void> {
    return firstValueFrom(this.http.post<void>('/api/opac/account/me/password', { currentPassword, newPassword }));
  }
}

/** Gắn token bạn đọc chỉ cho API của bạn đọc (tra cứu ẩn danh không mang token). */
export const readerAuthInterceptor: HttpInterceptorFn = (req, next) => {
  const token = READER_APIS.some((p) => req.url.startsWith(p)) ? inject(ReaderAuth).accessToken() : null;
  return next(token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req);
};
