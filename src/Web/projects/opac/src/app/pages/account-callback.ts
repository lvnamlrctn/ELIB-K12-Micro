import { Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ReaderAuth } from '../core/reader-auth';

/** /tai-khoan/callback — identity trả mã đăng nhập về đây; đổi lấy token rồi quay lại trang bạn đọc đang xem. */
@Component({
  selector: 'opac-account-callback',
  imports: [RouterLink],
  template: `
    <div class="max-w-md mx-auto text-center py-24 px-4">
      @if (error()) {
        <span class="material-icons text-slate-300 text-6xl">error_outline</span>
        <p class="mt-4 text-slate-700">{{ error() }}</p>
        <a routerLink="/tai-khoan" class="inline-block mt-6 text-blue-600 hover:underline">Thử đăng nhập lại</a>
      } @else {
        <span class="material-icons animate-spin text-blue-600 text-4xl">autorenew</span>
        <p class="mt-4 text-slate-600">Đang đăng nhập…</p>
      }
    </div>
  `,
})
export class AccountCallback implements OnInit {
  private readonly auth = inject(ReaderAuth);
  private readonly router = inject(Router);
  protected readonly error = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      await this.router.navigateByUrl(await this.auth.completeLogin(), { replaceUrl: true });
    } catch {
      this.error.set('Không hoàn tất được đăng nhập (liên kết đã dùng hoặc hết hạn).');
    }
  }
}
