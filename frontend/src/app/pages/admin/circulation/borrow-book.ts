import { Component, inject, OnInit, OnDestroy, signal, computed, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { BorrowService } from '../../../services/circulation/borrow.service';
import { FineTicketService } from '../../../services/circulation/fine-ticket.service';
import { CircPlaceService } from '../../../services/printbook/circ-place.service';
import { ReaderService } from '../../../services/reader/reader.service';
import { BorrowReaderSnapshot, Borrow } from '../../../models/circulation/borrow';
import { CircPlace } from '../../../models/printbook/circ-place';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { FaceCaptureComponent } from '../../../components/face-capture/face-capture';
import { FaceMatchResult } from '../../../models/circulation/face-match';

@Component({
  selector: 'app-borrow-book',
  standalone: true,
  imports: [CommonModule, FormsModule, NgSelectModule, TranslateModule, MatIconModule, AppDatePipe, FaceCaptureComponent],
  templateUrl: './borrow-book.html'
})
export class BorrowBookPage implements OnInit, OnDestroy {
  private service     = inject(BorrowService);
  private fineTicketSvc = inject(FineTicketService);
  private circPlaceSvc = inject(CircPlaceService);
  private readerSvc   = inject(ReaderService);
  private toastr      = inject(ToastrService);
  private router      = inject(Router);
  public  translate   = inject(TranslateService);
  private destroy$    = new Subject<void>();

  @ViewChild('cardNoInput') cardNoInput?: ElementRef<HTMLInputElement>;
  @ViewChild('barcodeInput') barcodeInput?: ElementRef<HTMLInputElement>;

  circPlaces = signal<CircPlace[]>([]);
  circPlaceId = signal<number | null>(null);
  cardNo = signal('');
  barcode = signal('');
  reasonText = signal('');
  snapshot = signal<BorrowReaderSnapshot | null>(null);
  isLoadingReader = signal(false);
  isBorrowing = signal(false);
  selectedIds = signal<Set<number>>(new Set());

  isLocked = computed(() => { const s = this.snapshot(); return !!s && s.status !== 2; });
  statusBadgeClass = computed(() => this.isLocked() ? 'bg-red-50 text-red-700' : 'bg-green-50 text-green-700');
  countBorrowing = computed(() => this.snapshot()?.currentLoans.length ?? 0);
  countOverdue = computed(() => (this.snapshot()?.currentLoans ?? []).filter(l => this.isOverdue(l.dueDate)).length);
  countRenew = computed(() => (this.snapshot()?.currentLoans ?? []).reduce((sum, l) => sum + (l.renewCount || 0), 0));
  countLocked = computed(() => this.isLocked() ? 1 : 0);

  showBlockDialog = signal(false);
  showReturnConfirm = signal(false);
  pendingReturnLoan = signal<Borrow | null>(null);
  showNoteDialog = signal(false);
  noteDraft = signal('');
  noteReason = signal('');
  noteTargetId = signal<number | null>(null);
  isSavingNote = signal(false);

  // Đợt 14 — Gia hạn bắt buộc lý do + xác nhận riêng (trước đây gọi thẳng API). Danh sách phiếu bị chốt
  // (renewTargetIds) ngay lúc mở dialog — không đọc lại selectedIds() lúc gửi, tránh trường hợp người
  // dùng đổi lựa chọn trong lúc dialog đang mở.
  showRenewDialog = signal(false);
  renewReason = signal('');
  renewTargetIds = signal<number[]>([]);
  isSavingRenew = signal(false);

  showBulkModal = signal(false);
  bulkPrefix = signal('');
  bulkFromNo = signal<number | null>(null);
  bulkToNo = signal<number | null>(null);
  bulkPad = signal(6);
  bulkManual = signal('');
  bulkResults = signal<{ barcode: string; success: boolean; message: string }[] | null>(null);
  isBulkBorrowing = signal(false);
  // Chỉ disable hoàn toàn khi thẻ bị khoá/hết hạn (không có gì để làm ở màn hình này).
  // Nếu chỉ đang quá hạn tài liệu, vẫn cho quét để xử lý trả/chuyển sang phiếu phạt (rule 3) —
  // việc mượn MỚI vẫn bị chặn ở backend (Checkout) bất kể trạng thái disable ở đây.
  barcodeDisabled = computed(() => { const s = this.snapshot(); return !s?.readerId || s?.isLocked === true || s?.isExpired === true || this.circPlaceId() == null; });

  ngOnInit(): void {
    this.circPlaceSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({
      next: l => { this.circPlaces.set(l); if (l.length && this.circPlaceId() == null) this.circPlaceId.set(Math.min(...l.map(x => x.id))); }, error: () => {}
    });
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadReader(): void {
    const card = this.cardNo().trim();
    if (!card) return;
    this.isLoadingReader.set(true);
    // Đổi bạn đọc — đóng + xoá lý do 2 dialog Gia hạn/Sửa ghi chú nếu đang mở, tránh áp nhầm lên phiếu
    // của bạn đọc cũ.
    this.closeRenewDialog();
    this.closeNoteModal();
    this.service.getReaderSnapshot(card, this.circPlaceId()).pipe(takeUntil(this.destroy$)).subscribe({
      next: s => {
        this.snapshot.set(s); this.isLoadingReader.set(false); this.selectedIds.set(new Set());
        if (!s) {
          this.toastr.warning(this.translate.instant('BORROW.READER_NOT_FOUND'));
          this.selectCardNoInput();
          return;
        }
        this.showBlockDialog.set(s.canBorrow === false);
        this.focusBarcodeInput();
      },
      error: () => {
        this.isLoadingReader.set(false);
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
        this.selectCardNoInput();
      }
    });
  }

  closeBlockDialog(): void { this.showBlockDialog.set(false); }

  // Không tìm thấy bạn đọc (hoặc lỗi gọi API) → tự bôi đen mã thẻ vừa nhập để gõ đè ngay, cho phép
  // quét liên tục bằng bàn phím/máy quét mà không cần chạm chuột.
  private selectCardNoInput(): void {
    setTimeout(() => this.cardNoInput?.nativeElement.select());
  }

  // Tìm thấy bạn đọc → tự chuyển tiêu điểm sang ô "Quét đăng ký cá biệt" (chờ 1 tick để [disabled]
  // của ô barcode cập nhật xong khi bạn đọc không bị khoá/hết hạn, tránh focus vào ô đang disabled).
  private focusBarcodeInput(): void {
    setTimeout(() => this.barcodeInput?.nativeElement.focus());
  }

  onFaceMatched(result: FaceMatchResult): void {
    if (!result.cardNo) return;
    this.cardNo.set(result.cardNo);
    this.loadReader();
  }

  // Ô "Mượn trả tài liệu" gộp 1 luồng duy nhất: mã đã có trong danh sách đang mượn (chưa quá hạn) → trả,
  // đang mượn nhưng quá hạn → chuyển sang màn hình phạt, chưa có → mượn.
  onScanBarcode(): void {
    const s = this.snapshot(); const bc = this.barcode().trim();
    if (!s?.readerId || !bc) return;
    const existing = s.currentLoans.find(l => l.barcode === bc);
    if (existing && this.isOverdue(existing.dueDate)) {
      this.openFineTicket();
      return;
    }
    if (existing) this.confirmReturn(existing); else this.borrowBarcode(bc);
  }

  /** Barcode quét trùng với 1 tài liệu bạn đọc đang mượn — hỏi xác nhận trước khi trả,
   *  tránh trường hợp quét nhầm khiến sách bị trả ngay lập tức không hay biết. */
  confirmReturn(loan: Borrow): void {
    this.pendingReturnLoan.set(loan);
    this.showReturnConfirm.set(true);
  }

  cancelReturnConfirm(): void {
    this.showReturnConfirm.set(false);
    this.pendingReturnLoan.set(null);
    this.barcode.set('');
  }

  confirmReturnExecute(): void {
    const loan = this.pendingReturnLoan();
    this.showReturnConfirm.set(false);
    this.pendingReturnLoan.set(null);
    if (!loan?.barcode) return;
    this.returnBarcode(loan.barcode);
  }

  /** Gom toàn bộ tài liệu quá hạn + các tài liệu đang tích chọn (vd bạn đọc làm mất, chưa quá hạn) của bạn đọc
   *  hiện tại vào 1 phiếu phạt rồi mở trang chi tiết. */
  openFineTicket(): void {
    const s = this.snapshot();
    if (!s?.readerId) return;
    this.fineTicketSvc.buildForReader(s.readerId, this.circPlaceId(), [...this.selectedIds()]).pipe(takeUntil(this.destroy$)).subscribe({
      next: t => {
        if (!t?.publicId) { this.toastr.error(this.translate.instant('BORROW.FINE_TICKET_FAIL')); return; }
        this.router.navigate(['/admin/fine-ticket', t.publicId]);
      },
      error: () => this.toastr.error(this.translate.instant('BORROW.FINE_TICKET_FAIL'))
    });
  }

  private borrowBarcode(bc: string): void {
    const s = this.snapshot();
    if (!s?.readerId) return;
    this.isBorrowing.set(true);
    this.service.borrow(s.readerId, bc, this.circPlaceId()).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        this.isBorrowing.set(false);
        if (res?.error) { this.toastr.error(res.message || this.translate.instant('BORROW.BORROW_FAIL')); return; }
        this.toastr.success(this.translate.instant('BORROW.BORROW_OK'));
        this.barcode.set(''); this.loadReader();
      },
      error: () => { this.isBorrowing.set(false); this.toastr.error(this.translate.instant('BORROW.BORROW_FAIL')); }
    });
  }

  private returnBarcode(bc: string): void {
    this.service.return({ barcode: bc, cardNo: this.snapshot()?.cardNo, circPlaceId: this.circPlaceId() }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        if (res?.error) { this.toastr.error(res.message || this.translate.instant('BORROW.RETURN_FAIL')); return; }
        this.toastr.success(this.translate.instant('BORROW.RETURN_OK'));
        this.barcode.set(''); this.loadReader();
      },
      error: () => this.toastr.error(this.translate.instant('BORROW.RETURN_FAIL'))
    });
  }

  openBulkModal(): void {
    this.bulkPrefix.set(''); this.bulkFromNo.set(null); this.bulkToNo.set(null);
    this.bulkManual.set(''); this.bulkResults.set(null);
    this.showBulkModal.set(true);
  }
  closeBulkModal(): void { this.showBulkModal.set(false); }

  private buildBulkBarcodeList(): string[] {
    const set = new Set<string>();
    const from = this.bulkFromNo(); const to = this.bulkToNo();
    if (from != null && to != null && to >= from) {
      for (let i = from; i <= to && set.size < 1000; i++) {
        set.add(`${this.bulkPrefix()}${String(i).padStart(this.bulkPad(), '0')}`);
      }
    }
    this.bulkManual().split(/[\n,;]+/).map(s => s.trim()).filter(Boolean).forEach(b => set.add(b));
    return Array.from(set);
  }

  submitBulkBorrow(): void {
    const s = this.snapshot();
    if (!s?.readerId) return;
    const barcodes = this.buildBulkBarcodeList();
    if (barcodes.length === 0) { this.toastr.warning(this.translate.instant('BORROW.BULK_EMPTY')); return; }
    this.isBulkBorrowing.set(true);
    this.service.borrowBulk(s.readerId, barcodes, this.circPlaceId()).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        this.isBulkBorrowing.set(false);
        if (res?.error) { this.toastr.error(res.message || this.translate.instant('BORROW.BORROW_FAIL')); return; }
        this.bulkResults.set(res?.items ?? []);
        if ((res?.failCount ?? 0) === 0) this.toastr.success(this.translate.instant('BORROW.BORROW_OK'));
        else this.toastr.warning(`${res.successCount}/${res.totalRequested} ${this.translate.instant('BORROW.BORROW_OK')}`);
        this.loadReader();
      },
      error: () => { this.isBulkBorrowing.set(false); this.toastr.error(this.translate.instant('BORROW.BORROW_FAIL')); }
    });
  }

  toggleSelect(id: number): void {
    this.selectedIds.update(set => {
      const next = new Set(set);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }
  isSelected(id: number): boolean { return this.selectedIds().has(id); }

  isOverdue(dueDate?: string): boolean {
    if (!dueDate) return false;
    return new Date(dueDate).getTime() < new Date(new Date().toDateString()).getTime();
  }

  remainingDays(dueDate?: string): number {
    if (!dueDate) return 0;
    const due = new Date(dueDate).getTime();
    const today = new Date(new Date().toDateString()).getTime();
    return Math.round((due - today) / 86400000);
  }

  // --- Thanh công cụ icon ---

  toolbarAdd(): void { this.onScanBarcode(); }

  toolbarEdit(): void {
    const ids = Array.from(this.selectedIds());
    const note = this.reasonText().trim();
    if (ids.length === 0 || !note) return;
    // Ngoài phạm vi Đợt 14 (không thêm dialog riêng cho nút sửa nhanh này — reasonText() ở đây là NỘI
    // DUNG ghi chú, không phải "lý do" tách biệt). Backend giờ bắt buộc Reason cho mọi lần gọi Note — gửi
    // luôn note làm reason để không vỡ luồng sửa nhanh hàng loạt hiện có.
    ids.forEach(id => this.service.note(id, note, note).pipe(takeUntil(this.destroy$)).subscribe());
    this.toastr.success(this.translate.instant('BORROW.NOTE_OK'));
  }

  toolbarSearch(): void { this.loadReader(); }

  toolbarReturn(): void {
    const ids = Array.from(this.selectedIds());
    if (ids.length === 0) { this.toastr.warning(this.translate.instant('BORROW.RETURN_SELECT_REQUIRED')); return; }
    let done = 0, failed = 0;
    const failMessages: string[] = [];
    ids.forEach(id => {
      this.service.return({ borrowId: id, circPlaceId: this.circPlaceId() }).pipe(takeUntil(this.destroy$)).subscribe({
        next: res => {
          done++;
          if (res?.error) { failed++; if (res?.message) failMessages.push(res.message); }
          if (done === ids.length) {
            if (failed === 0) this.toastr.success(this.translate.instant('BORROW.RETURN_OK'));
            else this.toastr.error(failMessages.length > 0 ? Array.from(new Set(failMessages)).join('; ') : this.translate.instant('BORROW.RETURN_FAIL'));
            this.selectedIds.set(new Set()); this.loadReader();
          }
        },
        error: () => { done++; failed++; if (done === ids.length) { this.toastr.error(failMessages.length > 0 ? Array.from(new Set(failMessages)).join('; ') : this.translate.instant('BORROW.RETURN_FAIL')); this.loadReader(); } }
      });
    });
  }

  // Đợt 14 — chỉ mở dialog xác nhận, không gọi API ngay. Chốt danh sách phiếu vào renewTargetIds tại
  // đúng thời điểm mở, dùng lại đúng danh sách đó lúc xác nhận (xem confirmRenew).
  toolbarRenew(): void {
    const ids = Array.from(this.selectedIds());
    if (ids.length === 0) { this.toastr.warning(this.translate.instant('BORROW.RENEW_SELECT_REQUIRED')); return; }
    this.renewTargetIds.set(ids);
    this.renewReason.set('');
    this.showRenewDialog.set(true);
  }

  closeRenewDialog(): void {
    this.showRenewDialog.set(false);
    this.renewReason.set('');
    this.renewTargetIds.set([]);
  }

  confirmRenew(): void {
    const reason = this.renewReason().trim();
    if (!reason) { this.toastr.warning(this.translate.instant('BORROW.RENEW_REASON_REQUIRED')); return; }
    const ids = this.renewTargetIds();
    if (ids.length === 0 || this.isSavingRenew()) return;
    this.isSavingRenew.set(true);
    let done = 0, failed = 0;
    const failMessages: string[] = [];
    ids.forEach(id => {
      this.service.renew(id, this.circPlaceId(), reason).pipe(takeUntil(this.destroy$)).subscribe({
        next: res => {
          done++;
          if (res?.error) { failed++; if (res?.message) failMessages.push(res.message); }
          if (done === ids.length) {
            this.isSavingRenew.set(false);
            if (failed === 0) this.toastr.success(this.translate.instant('BORROW.RENEW_OK'));
            else this.toastr.error(failMessages.length > 0 ? Array.from(new Set(failMessages)).join('; ') : this.translate.instant('BORROW.RENEW_FAIL'));
            this.closeRenewDialog(); this.selectedIds.set(new Set()); this.loadReader();
          }
        },
        error: () => { done++; failed++; if (done === ids.length) { this.isSavingRenew.set(false); this.toastr.error(failMessages.length > 0 ? Array.from(new Set(failMessages)).join('; ') : this.translate.instant('BORROW.RENEW_FAIL')); this.closeRenewDialog(); this.loadReader(); } }
      });
    });
  }

  toolbarRefresh(): void { this.loadReader(); }

  openNoteModal(): void {
    const ids = Array.from(this.selectedIds());
    if (ids.length !== 1) { this.toastr.warning(this.translate.instant('BORROW.NOTE_SELECT_ONE_REQUIRED')); return; }
    const id = ids[0];
    const loan = (this.snapshot()?.currentLoans ?? []).find(l => l.id === id);
    this.noteTargetId.set(id);
    this.noteDraft.set(loan?.note ?? '');
    this.noteReason.set('');
    this.showNoteDialog.set(true);
  }

  closeNoteModal(): void {
    this.showNoteDialog.set(false);
    this.noteDraft.set('');
    this.noteReason.set('');
    this.noteTargetId.set(null);
  }

  saveNote(): void {
    const id = this.noteTargetId();
    if (id == null || this.isSavingNote()) return;
    const reason = this.noteReason().trim();
    if (!reason) { this.toastr.warning(this.translate.instant('BORROW.NOTE_REASON_REQUIRED')); return; }
    this.isSavingNote.set(true);
    this.service.note(id, this.noteDraft().trim(), reason).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isSavingNote.set(false); this.toastr.success(this.translate.instant('BORROW.NOTE_OK')); this.closeNoteModal(); this.loadReader(); },
      error: () => { this.isSavingNote.set(false); }
    });
  }

  toolbarLockCard(): void {
    const s = this.snapshot();
    if (!s?.readerPublicId) return;
    const reason = this.reasonText().trim();
    if (!reason) { this.toastr.warning(this.translate.instant('BORROW.LOCK_REASON_REQUIRED')); return; }
    this.readerSvc.lockCard(s.readerPublicId, reason).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('BORROW.LOCK_OK')); this.loadReader(); },
      error: () => {}
    });
  }

  print(): void { if (typeof window !== 'undefined') window.print(); }

  now(): Date { return new Date(); }
}
