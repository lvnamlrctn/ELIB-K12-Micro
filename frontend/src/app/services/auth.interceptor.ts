import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Auth } from './auth';
import { TranslateService } from '@ngx-translate/core';
import { ToastrService } from './shared/toastr.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService    = inject(Auth);
  const translateService = inject(TranslateService);
  const router         = inject(Router);
  const toastr         = inject(ToastrService);
  const token          = authService.getToken();
  const currentLang    = translateService.currentLang || translateService.defaultLang || 'vi';

  let headers = req.headers.set('Accept-Language', currentLang);
  // Không gắn token nhân viên cho các API công khai OPAC (/api/public/*) — tránh lộ token admin sang
  // endpoint bạn đọc/công khai khi cùng trình duyệt có phiên admin đang mở. opacAuthInterceptor tự gắn
  // token bạn đọc riêng cho các path OPAC cần đăng nhập (MyLibrary/DocumentSubmission).
  if (token && !req.url.includes('/api/public')) {
    headers = headers.set('Authorization', `Bearer ${token}`);
  }

  const cloned = req.clone({ headers });

  return next(cloned).pipe(
    catchError((err: HttpErrorResponse) => {
      // Bỏ qua 401 từ request đăng nhập và từ API công khai OPAC (/api/public) để tránh vòng lặp / đăng xuất nhầm
      if (err.status === 401 && !req.url.includes('/Auth/Login') && !req.url.includes('/api/public')) {
        authService.logout();
        toastr.warning('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
        router.navigate(['/admin/login']);
      }
      return throwError(() => err);
    })
  );
};
