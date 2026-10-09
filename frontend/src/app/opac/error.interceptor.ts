import { HttpContextToken, HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError } from 'rxjs/operators';
import { throwError } from 'rxjs';
import { ToastrService } from '../services/shared/toastr.service';

// Chỉ hiện toast tự động cho request thay đổi dữ liệu (Add/Update/Delete/Save/...) — GET (tải dữ liệu)
// vẫn để nguyên hành vi cũ, nhiều nơi đang cố tình nuốt lỗi im lặng khi tải dữ liệu phụ/nền.
const MUTATING_METHODS = ['POST', 'PUT', 'PATCH', 'DELETE'];

/** Đặt true khi nơi gọi tự hiển thị lỗi (vd màn quét QR check-in) để không bật thêm toast trùng. */
export const SKIP_ERROR_TOAST = new HttpContextToken<boolean>(() => false);

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toastr = inject(ToastrService);
  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      console.error(`HTTP Error: ${error.status} ${error.statusText} for URL: ${req.url}`);
      if (error.error) {
        console.error('Error Details:', error.error);
      }
      // 401 đã được auth.interceptor.ts xử lý riêng (đăng xuất + thông báo hết phiên) — không hiện thêm ở đây.
      if (error.status !== 401 && MUTATING_METHODS.includes(req.method) && !req.context.get(SKIP_ERROR_TOAST)) {
        const message = error.error && typeof error.error === 'object' ? (error.error as { message?: string }).message : undefined;
        if (message) toastr.error(message);
      }
      return throwError(() => error);
    })
  );
};
