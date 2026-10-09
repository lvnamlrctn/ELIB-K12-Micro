import { Injectable, inject } from '@angular/core';
import { PreloadingStrategy, Route, Router } from '@angular/router';
import { Observable, of } from 'rxjs';

/**
 * Chỉ tải ngầm các chunk lazy khi người dùng đã ở trong khu vực /admin.
 *
 * Lý do không dùng `PreloadAllModules`: OPAC (trang tra cứu công khai) và admin dùng chung
 * router, nên PreloadAllModules sẽ khiến khách vãng lai tải ngầm toàn bộ trang quản trị —
 * đúng thứ mà việc tách lazy đang muốn tránh. Với chiến lược này, khách OPAC không tải thêm gì,
 * còn người dùng admin vẫn được chuyển trang tức thì như trước khi tách lazy.
 */
@Injectable({ providedIn: 'root' })
export class AdminPreloadStrategy implements PreloadingStrategy {
  private router = inject(Router);

  preload(route: Route, load: () => Observable<unknown>): Observable<unknown> {
    return this.router.url.startsWith('/admin') ? load() : of(null);
  }
}
