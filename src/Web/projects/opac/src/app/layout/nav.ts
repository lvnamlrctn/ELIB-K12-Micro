import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { PortalMenuItem } from '../core/portal';

/** Điều hướng chung của đầu/chân trang. */
@Injectable({ providedIn: 'root' })
export class Nav {
  private readonly router = inject(Router);

  /** Đường dẫn nội bộ đi qua router (không tải lại trang); địa chỉ ngoài để trình duyệt tự mở. */
  go(event: Event, item: PortalMenuItem): void {
    if (item.newTab || !item.href.startsWith('/')) return;
    event.preventDefault();
    void this.router.navigateByUrl(item.href);
  }
}
