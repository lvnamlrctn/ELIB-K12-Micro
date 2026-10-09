import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/** Gắn access token cho API cùng origin (qua gateway). 401 → đăng nhập lại, quay về trang đang xem. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const token = auth.accessToken();
  const sameOriginApi = req.url.startsWith('/api/');

  const request = token && sameOriginApi ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  return next(request).pipe(
    catchError((error: unknown) => {
      if (sameOriginApi && error instanceof HttpErrorResponse && error.status === 401) {
        void auth.login(router.url);
      }
      return throwError(() => error);
    }),
  );
};
