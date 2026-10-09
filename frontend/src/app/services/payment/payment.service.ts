import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { PaymentCreateResult, PaymentProvider, PaymentTargetType, PaymentTransaction } from '../../models/payment/payment';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type Any = any;

/** Tạo/kiểm tra giao dịch thanh toán QR tại quầy (thủ thư, api/Payment) — xem PaymentReaderService cho luồng OPAC. */
@Injectable({ providedIn: 'root' })
export class PaymentService {
  private http = inject(HttpClient);
  private get baseUrl(): string { return `${environment.baseApiUrl}/api/Payment`; }

  providers(): Observable<PaymentProvider[]> {
    return this.http.get<Any>(`${this.baseUrl}/Providers`).pipe(
      map(r => (r?.data ?? []) as PaymentProvider[]),
      catchError(() => of([] as PaymentProvider[]))
    );
  }

  create(readerId: number, targetType: PaymentTargetType, targetId: number | null, provider: PaymentProvider): Observable<PaymentCreateResult> {
    return this.http.post<Any>(`${this.baseUrl}/Create`, { readerId, targetType, targetId, provider }).pipe(
      map(r => ({ txn: (r?.data ?? null) as PaymentTransaction | null })),
      catchError(e => of({ txn: null, error: e?.error?.message ?? null }))
    );
  }

  status(publicId: string): Observable<PaymentTransaction | null> {
    return this.http.get<Any>(`${this.baseUrl}/Status/${publicId}`).pipe(
      map(r => (r?.data ?? null) as PaymentTransaction | null),
      catchError(() => of(null))
    );
  }
}
