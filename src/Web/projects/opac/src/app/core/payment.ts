import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** Khoản bạn đọc còn nợ (phiếu phạt, sao chụp, phí cấp lại thẻ). providers: cổng đơn vị đang bật. */
export interface MyCharge { publicId: string; kind: string; sourceService: string; sourceId: string; label: string; amount: number; status: string; cardNo: string; providers: string[]; }
/** status: Pending | Paid | Expired | Cancelled | PaidLate. */
export interface MyPayment {
  publicId: string; transactionCode: string; provider: string; amount: number; status: string; expiresAt: string; qrContent: string; label: string; kind: string;
}

/**
 * API thanh toán của bạn đọc — /api/opac/payment/** (gateway → payment's /api/reader/**, chỉ token bạn đọc, cần license PAYMENT). Bạn đọc chỉ thấy và
 * thanh toán khoản của chính mình; số tiền do hệ thống quyết định, không gửi từ trình duyệt.
 */
@Injectable({ providedIn: 'root' })
export class PaymentApi {
  private readonly http = inject(HttpClient);

  charges() { return firstValueFrom(this.http.get<MyCharge[]>('/api/opac/payment/charges')); }
  create(chargePublicId: string, provider: string) { return firstValueFrom(this.http.post<MyPayment>('/api/opac/payment/payments/Create', { chargePublicId, provider })); }
  status(publicId: string) { return firstValueFrom(this.http.get<MyPayment>(`/api/opac/payment/payments/${publicId}`)); }
}
