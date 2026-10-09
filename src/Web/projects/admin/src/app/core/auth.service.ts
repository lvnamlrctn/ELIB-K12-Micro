import { Injectable, signal } from '@angular/core';
import { User, UserManager, WebStorageStateStore } from 'oidc-client-ts';

/**
 * Đăng nhập OIDC Authorization Code + PKCE với service identity (docs 05 §1).
 * App chạy dưới /admin/ của CHÍNH host đang mở (host hệ thống hoặc <đơn vị>.<miền>), nên authority = origin hiện tại:
 * gateway chuyển /connect, /.well-known tới identity và gắn đơn vị theo host.
 * Token chỉ giữ trong sessionStorage (đóng tab là hết); làm mới bằng refresh token, không dùng iframe.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly appBase = `${location.origin}/admin/`;

  private readonly manager = new UserManager({
    authority: `${location.origin}/`,
    client_id: 'elib-admin',
    redirect_uri: `${this.appBase}callback`,
    post_logout_redirect_uri: this.appBase,
    response_type: 'code',
    scope: 'openid profile offline_access elib-api',
    automaticSilentRenew: true,
    loadUserInfo: false,
    userStore: new WebStorageStateStore({ store: sessionStorage }),
  });

  readonly user = signal<User | null>(null);

  constructor() {
    this.manager.events.addUserLoaded((u) => this.user.set(u));
    this.manager.events.addUserUnloaded(() => this.user.set(null));
    this.manager.events.addSilentRenewError(() => this.user.set(null));
  }

  /** Người dùng còn phiên hợp lệ (làm mới bằng refresh token nếu access token đã hết hạn), hoặc null. */
  async ensureUser(): Promise<User | null> {
    let user = await this.manager.getUser();
    if (user?.expired && user.refresh_token) {
      try {
        user = await this.manager.signinSilent();
      } catch {
        user = null;
      }
    }
    this.user.set(user && !user.expired ? user : null);
    return this.user();
  }

  login(returnUrl: string): Promise<void> {
    return this.manager.signinRedirect({ state: returnUrl });
  }

  /** Xử lý /admin/callback; trả về đường dẫn trong app cần quay lại. */
  async completeLogin(): Promise<string> {
    const user = await this.manager.signinRedirectCallback();
    this.user.set(user);
    const target = typeof user.state === 'string' ? user.state : '/';
    // Chỉ quay về đường dẫn nội bộ của app.
    return target.startsWith('/') && !target.startsWith('//') ? target : '/';
  }

  async logout(): Promise<void> {
    await this.manager.signoutRedirect();
  }

  accessToken(): string | null {
    return this.user()?.access_token ?? null;
  }
}
