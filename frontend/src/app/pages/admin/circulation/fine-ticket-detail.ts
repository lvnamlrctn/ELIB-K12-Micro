import { Component, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { PaymentQrDialogComponent } from '../../../components/payment-qr-dialog/payment-qr-dialog';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { FineTicketService } from '../../../services/circulation/fine-ticket.service';
import { CFineTypeService } from '../../../services/printbook/cfine-type.service';
import { CFineMethodService } from '../../../services/printbook/cfine-method.service';
import { BarcodeStatusService } from '../../../services/printbook/barcode-status.service';
import { BorrowService } from '../../../services/circulation/borrow.service';
import { FineTicket } from '../../../models/circulation/fine-ticket';
import { BarcodeStatus, CFineType } from '../../../models/printbook/cfine-type';
import { CFineMethod } from '../../../models/printbook/cfine-method';
import { BorrowReaderSnapshot } from '../../../models/circulation/borrow';

/** Dòng phạt đang sửa; isNew = thêm tay chưa lưu (id tạm âm). */
interface LineEdit { id: number; fineTypeId: string; value: number; barcode: string; isNew: boolean; }

// Lý do phạt cho staff chọn: mọi loại phạt TRỪ 2 mã nội bộ tính phí tự động khi tích "Nợ tài liệu" ở luồng
// tài liệu quá hạn (XLKY/VANCHUYEN). Deny-list thay cho allow-list cứng (port ELIB-LRC 09-08) — mỗi đơn vị
// tự cấu hình danh mục loại phạt riêng, mã mới thêm phải hiện ra chứ không bị ẩn ngầm.
const INTERNAL_ONLY_CODES = ['XLKY', 'VANCHUYEN'];
const DONE_STATUS = 2;

@Component({
  selector: 'app-fine-ticket-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, NgSelectModule, TranslateModule, MatIconModule, DateInputComponent, PaymentQrDialogComponent],
  templateUrl: './fine-ticket-detail.html'
})
export class FineTicketDetailPage implements OnInit, OnDestroy {
  private service    = inject(FineTicketService);
  private typeSvc     = inject(CFineTypeService);
  private methodSvc    = inject(CFineMethodService);
  private barcodeStatusSvc = inject(BarcodeStatusService);
  private borrowSvc     = inject(BorrowService);
  private toastr      = inject(ToastrService);
  private route        = inject(ActivatedRoute);
  private router        = inject(Router);
  public  translate    = inject(TranslateService);
  private destroy$     = new Subject<void>();

  ticket = signal<FineTicket | null>(null);
  fineTypes = signal<CFineType[]>([]);
  fineMethods = signal<CFineMethod[]>([]);
  barcodeStatuses = signal<BarcodeStatus[]>([]);
  lineEdits = signal<LineEdit[]>([]);
  /** Dòng đã lưu bị xoá trên màn hình — gửi lên khi bấm Lưu. */
  deletedLineIds = signal<number[]>([]);
  private tempId = 0;
  isLoading = signal(false);
  isSaving = signal(false);
  showPaymentQr = signal(false);

  // Chế độ "Thêm mới": chưa có phiếu, cần tra cứu bạn đọc theo Số thẻ trước.
  isNew = signal(false);
  cardNoInput = signal('');
  isLookingUp = signal(false);
  lookedUpReader = signal<BorrowReaderSnapshot | null>(null);

  fineDate = signal('');
  status = signal<number>(1);
  discountAmount = signal<number>(0);
  paidAmount = signal<number>(0);
  owesDocument = signal(false);
  fineTypeId = signal<number | null>(null);
  fineMethodId = signal<number | null>(null);
  fineAmount = signal<number>(0);
  readerIssueDate = signal('');
  note = signal('');

  reasonTypes = computed(() => this.fineTypes().filter(t => !INTERNAL_ONLY_CODES.includes(t.code ?? '')));
  // Lý do phạt theo từng dòng (port ELIB-LRC 10-03): cùng danh mục với đầu phiếu. Dòng cũ có thể đang mang mã nội bộ
  // (XLKY/VANCHUYEN) — vẫn đưa mã đó vào danh sách để hiển thị được.
  lineReasonTypes = computed(() => {
    const used = new Set(this.lineEdits().map(l => l.fineTypeId));
    return this.fineTypes().filter(t => !INTERNAL_ONLY_CODES.includes(t.code ?? '') || used.has(t.code ?? ''));
  });
  /** Phiếu đã hoàn thành (theo trạng thái ĐÃ LƯU) thì khoá lý do phạt / thêm-xoá dòng; chưa hoàn thành thì luôn sửa được. */
  isDone = computed(() => this.ticket()?.status === DONE_STATUS);
  hasLines = computed(() => this.lineEdits().length > 0);
  /** Thông tin hiển thị (nhan đề, số ngày, đơn giá) của dòng đã lưu theo id. */
  lineInfo = computed(() => new Map((this.ticket()?.lines ?? []).map(l => [l.id, l])));
  totalAmount = computed(() => {
    const base = this.hasLines()
      ? this.lineEdits().reduce((sum, l) => sum + (l.value || 0), 0)
      : (this.fineAmount() || 0);
    return base + (this.owesDocument() ? (this.ticket()?.feeLegend?.techFee ?? 0) + (this.ticket()?.feeLegend?.shippingFee ?? 0) : 0);
  });
  remaining = computed(() => this.totalAmount() - (this.discountAmount() || 0) - (this.paidAmount() || 0));

  statusOptions = [
    { value: 1, label: 'FINE_TICKET.ST_PROCESSING' },
    { value: 2, label: 'FINE_TICKET.ST_DONE' },
  ];

  ngOnInit(): void {
    this.typeSvc.search({ pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.fineTypes.set(r.data), error: () => {} });
    this.methodSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.fineMethods.set(r), error: () => {} });
    this.barcodeStatusSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.barcodeStatuses.set(r), error: () => {} });
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe(p => {
      const publicId = p.get('publicId');
      if (!publicId) return;
      if (publicId === 'new') { this.startNew(); return; }
      this.load(publicId);
    });
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  private startNew(): void {
    this.isNew.set(true);
    this.ticket.set(null);
    this.lookedUpReader.set(null);
    this.cardNoInput.set('');
    this.fineDate.set(new Date().toISOString().substring(0, 10));
    this.status.set(1);
    this.discountAmount.set(0);
    this.paidAmount.set(0);
    this.owesDocument.set(false);
    this.fineTypeId.set(null);
    this.fineMethodId.set(null);
    this.fineAmount.set(0);
    this.readerIssueDate.set('');
    this.note.set('');
    this.lineEdits.set([]);
  }

  lookupCardNo(): void {
    const cardNo = this.cardNoInput().trim();
    if (!cardNo) return;
    this.isLookingUp.set(true);
    this.borrowSvc.getReaderSnapshot(cardNo).pipe(takeUntil(this.destroy$)).subscribe({
      next: r => {
        this.isLookingUp.set(false);
        if (!r?.readerId) { this.toastr.error(this.translate.instant('FINE_TICKET.READER_NOT_FOUND')); return; }
        this.lookedUpReader.set(r);
        this.readerIssueDate.set(r.issueDate ? r.issueDate.substring(0, 10) : '');
      },
      error: () => { this.isLookingUp.set(false); this.toastr.error(this.translate.instant('FINE_TICKET.READER_NOT_FOUND')); }
    });
  }

  // Thanh toán QR (port ELIB-LRC 09-25): webhook gạch nợ phiếu → nạp lại để thấy tiền nộp/trạng thái mới.
  openPaymentQr(): void { this.showPaymentQr.set(true); }
  onPaymentQrPaid(): void {
    this.showPaymentQr.set(false);
    const t = this.ticket();
    if (t?.publicId) this.load(t.publicId);
  }

  load(publicId: string): void {
    this.isNew.set(false);
    this.isLoading.set(true);
    this.service.getByPublicId(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: t => {
        this.isLoading.set(false);
        if (!t) { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); return; }
        this.applyTicket(t);
      },
      error: () => { this.isLoading.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); }
    });
  }

  private applyTicket(t: FineTicket): void {
    this.ticket.set(t);
    this.fineDate.set(t.fineDate ? t.fineDate.substring(0, 10) : '');
    this.status.set(t.status ?? 1);
    this.discountAmount.set(t.discountAmount ?? 0);
    this.paidAmount.set(t.paidAmount ?? 0);
    this.owesDocument.set(t.owesDocument === 2);
    this.fineTypeId.set(t.fineTypeId ?? null);
    this.fineMethodId.set(t.fineMethodId ?? null);
    this.fineAmount.set(t.totalAmount ?? 0);
    this.readerIssueDate.set(t.readerIssueDate ? t.readerIssueDate.substring(0, 10) : '');
    this.note.set(t.note ?? '');
    this.lineEdits.set(t.lines.map(l => ({ id: l.id, fineTypeId: l.fine_type_id ?? '', value: l.value ?? 0, barcode: l.barcode ?? '', isNew: false })));
    this.deletedLineIds.set([]);
  }

  /** Tên trạng thái ĐKCB mà lý do phạt sẽ gán khi lưu (cột "Trạng thái" của danh mục lý do phạt); không có thì rỗng. */
  reasonStatusName(t: CFineType): string {
    const ref = t.statusRegId?.trim();
    if (!ref) return '';
    const s = this.barcodeStatuses().find(x => x.id === ref || x.publicId === ref);
    return s?.commentStatus || s?.name || ref;
  }

  getFineTypeName(code: string | undefined): string { if (!code) return '—'; return this.fineTypes().find(x => x.code === code)?.name || '—'; }

  updateLineType(id: number, code: string): void {
    this.lineEdits.update(list => list.map(l => l.id === id ? { ...l, fineTypeId: code } : l));
  }
  updateLineValue(id: number, value: number): void {
    this.lineEdits.update(list => list.map(l => l.id === id ? { ...l, value } : l));
  }
  updateLineBarcode(id: number, barcode: string): void {
    this.lineEdits.update(list => list.map(l => l.id === id ? { ...l, barcode } : l));
  }

  /** Thêm 1 dòng phạt trống (theo ĐKCB hoặc tự do) — lưu cùng nút Lưu. */
  addLine(): void {
    this.lineEdits.update(list => [...list, { id: --this.tempId, fineTypeId: '', value: 0, barcode: '', isNew: true }]);
  }

  /** Dòng mới: bỏ luôn; dòng đã lưu: đánh dấu xoá, gửi lên khi Lưu (không hoàn tác trạng thái ĐKCB / phiếu mượn). */
  removeLine(line: LineEdit): void {
    if (!line.isNew) this.deletedLineIds.update(ids => [...ids, line.id]);
    this.lineEdits.update(list => list.filter(l => l.id !== line.id));
  }

  save(): void {
    if (this.isNew()) { this.createTicket(); return; }
    const t = this.ticket(); if (!t?.publicId) return;
    if (this.lineEdits().some(l => l.isNew && !l.fineTypeId)) {
      this.toastr.warning(this.translate.instant('FINE_TICKET.LINE_REASON_REQUIRED'));
      return;
    }
    this.isSaving.set(true);
    this.service.save(t.publicId, {
      fineDate: this.fineDate() || null,
      status: this.status(),
      discountAmount: this.discountAmount(),
      paidAmount: this.paidAmount(),
      totalAmount: this.hasLines() ? null : this.fineAmount(),
      owesDocument: this.owesDocument() ? 2 : 1,
      fineTypeId: this.fineTypeId(),
      fineMethodId: this.fineMethodId(),
      note: this.note() || null,
      lines: this.lineEdits().map(l => l.isNew
        ? { id: 0, fineTypeId: l.fineTypeId, value: l.value || 0, barcode: l.barcode.trim() || null }
        : { id: l.id, fineTypeId: l.fineTypeId, value: l.value }),
      deletedLineIds: this.deletedLineIds(),
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        this.isSaving.set(false);
        if (!res) { return; }
        this.applyTicket(res);
        this.toastr.success(this.translate.instant('COMMON.SAVE_SUCCESS'));
      },
      error: () => { this.isSaving.set(false); }
    });
  }

  private createTicket(): void {
    const reader = this.lookedUpReader();
    if (!reader?.readerId) { this.toastr.error(this.translate.instant('FINE_TICKET.READER_NOT_FOUND')); return; }
    this.isSaving.set(true);
    this.service.create({
      readerId: reader.readerId,
      fineDate: this.fineDate() || null,
      status: this.status(),
      fineTypeId: this.fineTypeId(),
      fineMethodId: this.fineMethodId(),
      totalAmount: this.fineAmount(),
      discountAmount: this.discountAmount(),
      paidAmount: this.paidAmount(),
      owesDocument: this.owesDocument() ? 2 : 1,
      note: this.note() || null,
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        this.isSaving.set(false);
        if (!res?.publicId) { return; }
        this.toastr.success(this.translate.instant('COMMON.SAVE_SUCCESS'));
        this.router.navigate(['/admin/fine-ticket', res.publicId], { replaceUrl: true });
      },
      error: () => { this.isSaving.set(false); }
    });
  }

  goToNew(): void { this.router.navigate(['/admin/fine-ticket', 'new']); }
  print(): void { if (typeof window !== 'undefined') window.print(); }
  // Phiếu có dòng tài liệu đến từ luồng quét quá hạn ở Mượn/Trả; phiếu thủ công đến từ trang tra cứu phiếu phạt.
  back(): void { this.router.navigate([this.hasLines() ? '/admin/borrow' : '/admin/fine-tickets']); }
}
