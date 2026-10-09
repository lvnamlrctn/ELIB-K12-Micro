import { Component, EventEmitter, Input, OnDestroy, OnInit, Output, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Observable, Subject, of, takeUntil, timer } from 'rxjs';
import { exhaustMap, filter, take } from 'rxjs/operators';
import { PaymentService } from '../../services/payment/payment.service';
import { PaymentReaderService } from '../../services/payment/payment-reader.service';
import { PaymentCreateResult, PaymentProvider, PaymentTargetType, PaymentTransaction } from '../../models/payment/payment';

/** Dialog QR thanh toán dùng chung (port ELIB-LRC 09-25) — quầy lưu thông (mode="staff", cần readerId) và OPAC tự phục vụ
 * (mode="reader", ReaderId backend tự lấy từ JWT). Chọn cổng (chỉ những cổng đơn vị đã cấu hình), sinh QR, đếm ngược tới hạn,
 * poll trạng thái mỗi 2s và báo `paid` khi webhook xác nhận. */
@Component({
  selector: 'app-payment-qr-dialog',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  templateUrl: './payment-qr-dialog.html'
})
export class PaymentQrDialogComponent implements OnInit, OnDestroy {
  @Input() mode: 'staff' | 'reader' = 'staff';
  @Input() readerId: number | null = null; // bắt buộc khi mode="staff"
  @Input() targetType: PaymentTargetType = 'FINE_TICKET';
  @Input() targetId: number | null = null;
  /** Cổng dùng được (OPAC đã có từ MyDebts); null = dialog tự hỏi backend (quầy). */
  @Input() providers: PaymentProvider[] | null = null;

  @Output() paid = new EventEmitter<PaymentTransaction>();
  @Output() closed = new EventEmitter<void>();

  private staffSvc = inject(PaymentService);
  private readerSvc = inject(PaymentReaderService);
  private translate = inject(TranslateService);
  private destroy$ = new Subject<void>();

  isLoading = signal(true);
  errorMessage = signal<string | null>(null);
  available = signal<PaymentProvider[]>([]);
  txn = signal<PaymentTransaction | null>(null);
  qrImage = signal<string | null>(null);
  secondsLeft = signal(0);

  progressLabel = computed(() => {
    const t = this.txn();
    if (!t) return '';
    if (t.status === 'Paid') return this.translate.instant('PAYMENT.PAID');
    if (t.status === 'Expired' || t.status === 'Cancelled') return this.translate.instant('PAYMENT.EXPIRED');
    return this.translate.instant('PAYMENT.WAITING');
  });

  private countdownHandle: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    if (this.mode === 'staff' && this.readerId == null) { this.fail(this.translate.instant('PAYMENT.MISSING_READER')); return; }
    const providers$: Observable<PaymentProvider[]> = this.providers ? of(this.providers) : this.staffSvc.providers();
    providers$.pipe(takeUntil(this.destroy$)).subscribe(list => {
      this.available.set(list);
      if (list.length === 0) { this.fail(this.translate.instant('PAYMENT.NOT_CONFIGURED')); return; }
      if (list.length === 1) { this.choose(list[0]); return; }
      this.isLoading.set(false); // > 1 cổng: chờ người dùng chọn
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.stopCountdown();
  }

  choose(provider: PaymentProvider): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);
    const create$: Observable<PaymentCreateResult> = this.mode === 'reader'
      ? this.readerSvc.create(this.targetType, this.targetId, provider)
      : this.staffSvc.create(this.readerId!, this.targetType, this.targetId, provider);

    create$.pipe(takeUntil(this.destroy$)).subscribe(async res => {
      if (!res.txn) { this.fail(res.error || this.translate.instant('PAYMENT.CREATE_ERROR')); return; }
      await this.show(res.txn);
      this.isLoading.set(false);
      this.startCountdown(res.txn.expiresAt);
      this.poll(res.txn.publicId);
    });
  }

  private async show(t: PaymentTransaction): Promise<void> {
    this.txn.set(t);
    if (t.status !== 'Pending' || !t.qrContent) { this.qrImage.set(null); return; }
    const QRCode = (await import('qrcode')).default;
    this.qrImage.set(await QRCode.toDataURL(t.qrContent, { width: 240, margin: 1 }));
  }

  private fail(message: string): void {
    this.isLoading.set(false);
    this.errorMessage.set(message);
  }

  private poll(publicId: string): void {
    // Poll đúng endpoint theo mode — reader dùng JWT bạn đọc, staff dùng JWT quản trị; gọi nhầm sẽ bị 401/403.
    const status$ = () => this.mode === 'reader' ? this.readerSvc.status(publicId) : this.staffSvc.status(publicId);
    timer(2000, 2000).pipe(
      exhaustMap(status$),
      filter(t => !!t && t.status !== 'Pending'),
      take(1),
      takeUntil(this.destroy$)
    ).subscribe(t => {
      if (!t) return;
      this.txn.set(t);
      this.stopCountdown();
      if (t.status === 'Paid') this.paid.emit(t);
    });
  }

  private startCountdown(expiresAt: string): void {
    const tick = () => {
      const left = Math.max(0, Math.round((new Date(expiresAt).getTime() - Date.now()) / 1000));
      this.secondsLeft.set(left);
      if (left <= 0) this.stopCountdown();
    };
    tick();
    this.countdownHandle = setInterval(tick, 1000);
  }
  private stopCountdown(): void {
    if (this.countdownHandle) { clearInterval(this.countdownHandle); this.countdownHandle = null; }
  }

  mmss(): string {
    const s = this.secondsLeft();
    return `${Math.floor(s / 60).toString().padStart(2, '0')}:${(s % 60).toString().padStart(2, '0')}`;
  }

  close(): void { this.closed.emit(); }
}
