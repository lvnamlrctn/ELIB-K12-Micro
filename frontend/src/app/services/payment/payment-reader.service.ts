import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { isPlatformServer } from '@angular/common';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { APP_CONFIG } from '../../opac/config';
import { SKIP_ERROR_TOAST } from '../../opac/error.interceptor';
import { PaymentCreateResult, PaymentProvider, PaymentTargetType, PaymentTransaction, ReaderDebtsResult } from '../../models/payment/payment';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type Any = any;

/** Thanh toán tự phục vụ (OPAC, api/public/PaymentReader) — ReaderId lấy từ JWT bạn đọc phía backend, không truyền từ client.
 * Token bạn đọc do opac/auth.interceptor.ts gắn (đường dẫn '/api/public/PaymentReader/' trong READER_AUTH_PATHS). */
@Injectable({ providedIn: 'root' })
export class PaymentReaderService {
  private http = inject(HttpClient);
  private platformId = inject(PLATFORM_ID);
  private get baseUrl(): string {
    return `${isPlatformServer(this.platformId) ? APP_CONFIG.BackendBase : ''}/api/public/PaymentReader`;
  }
  /** Dialog tự hiện lỗi — không để interceptor toast thêm lần nữa. */
  private quiet = { context: new HttpContext().set(SKIP_ERROR_TOAST, true) };

  myDebts(): Observable<ReaderDebtsResult> {
    const empty: ReaderDebtsResult = { debts: { tickets: [], photos: [] }, providers: [] };
    return this.http.get<Any>(`${this.baseUrl}/MyDebts`, this.quiet).pipe(
      map(r => (r?.data ?? empty) as ReaderDebtsResult),
      catchError(() => of(empty))
    );
  }

  create(targetType: PaymentTargetType, targetId: number | null, provider: PaymentProvider): Observable<PaymentCreateResult> {
    return this.http.post<Any>(`${this.baseUrl}/Create`, { targetType, targetId, provider }, this.quiet).pipe(
      map(r => ({ txn: (r?.data ?? null) as PaymentTransaction | null })),
      catchError(e => of({ txn: null, error: e?.error?.message ?? null }))
    );
  }

  status(publicId: string): Observable<PaymentTransaction | null> {
    return this.http.get<Any>(`${this.baseUrl}/Status/${publicId}`, this.quiet).pipe(
      map(r => (r?.data ?? null) as PaymentTransaction | null),
      catchError(() => of(null))
    );
  }
}
