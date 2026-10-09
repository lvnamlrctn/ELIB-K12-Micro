import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../core/auth.service';

/** /admin/callback — nhận code từ identity, đổi lấy token (PKCE) rồi quay lại trang ban đầu. */
@Component({
  selector: 'app-callback',
  template: `
    <div class="min-h-screen bg-[#f0f2f5] flex items-center justify-center p-4">
      <div class="bg-white rounded-2xl shadow-sm border border-gray-100 p-8 max-w-md w-full text-center">
        <div class="flex items-center justify-center text-blue-600 font-bold text-xl mb-6">
          <span class="material-icons mr-2">layers</span> ELIB-K12
        </div>
        @if (error()) {
          <div class="alert-error">{{ error() }}</div>
          <button (click)="retry()" class="btn-primary w-full">Đăng nhập lại</button>
        } @else {
          <span class="material-icons animate-spin text-blue-600 text-4xl">autorenew</span>
          <p class="text-gray-600 mt-3">Đang đăng nhập…</p>
        }
      </div>
    </div>
  `,
})
export class Callback implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly error = signal('');

  async ngOnInit(): Promise<void> {
    try {
      await this.router.navigateByUrl(await this.auth.completeLogin(), { replaceUrl: true });
    } catch {
      this.error.set('Không hoàn tất được đăng nhập (phiên đã hết hạn hoặc bị huỷ).');
    }
  }

  protected retry(): void {
    void this.auth.login('/');
  }
}
