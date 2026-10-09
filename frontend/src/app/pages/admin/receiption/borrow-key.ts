import { Component, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { BorrowKeyService } from '../../../services/receiption/borrow-key.service';
import { CircPlaceService } from '../../../services/printbook/circ-place.service';
import { ReaderService } from '../../../services/reader/reader.service';
import { KeyHolderSnapshot } from '../../../models/receiption/borrow-key';
import { CircPlace } from '../../../models/printbook/circ-place';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';

@Component({
  selector: 'app-borrow-key',
  standalone: true,
  imports: [CommonModule, FormsModule, NgSelectModule, TranslateModule, MatIconModule, AppDatePipe],
  templateUrl: './borrow-key.html'
})
export class BorrowKeyPage implements OnInit, OnDestroy {
  private service     = inject(BorrowKeyService);
  private circPlaceSvc = inject(CircPlaceService);
  private readerSvc   = inject(ReaderService);
  private toastr      = inject(ToastrService);
  public  translate   = inject(TranslateService);
  private destroy$    = new Subject<void>();

  circPlaces = signal<CircPlace[]>([]);
  circPlaceId = signal<number | null>(null);
  cardNo = signal('');
  compartmentBarcode = signal('');
  reasonText = signal('');
  snapshot = signal<KeyHolderSnapshot | null>(null);
  notFound = signal(false);
  isBusy = signal(false);
  selectedIds = signal<Set<number>>(new Set());

  isLocked = computed(() => { const s = this.snapshot(); return !!s && s.status !== 2; });
  statusBadgeClass = computed(() => this.isLocked() ? 'bg-red-50 text-red-700' : 'bg-green-50 text-green-700');
  countBorrowing = computed(() => this.snapshot()?.currentKeys.length ?? 0);
  countLocked = computed(() => this.isLocked() ? 1 : 0);

  loanCountBorrowing = computed(() => this.snapshot()?.currentLoans.length ?? 0);
  loanCountOverdue   = computed(() => (this.snapshot()?.currentLoans ?? []).filter(l => this.isOverdue(l.dueDate)).length);
  loanCountRenew     = computed(() => (this.snapshot()?.currentLoans ?? []).reduce((sum, l) => sum + (l.renewCount || 0), 0));

  ngOnInit(): void {
    this.circPlaceSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({
      next: l => { this.circPlaces.set(l); if (l.length && this.circPlaceId() == null) this.circPlaceId.set(Math.min(...l.map(x => x.id))); }, error: () => {}
    });
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  lookup(): void {
    const card = this.cardNo().trim(); if (!card) return;
    this.isBusy.set(true); this.notFound.set(false); this.snapshot.set(null);
    this.service.getSnapshot(card, this.circPlaceId()).pipe(takeUntil(this.destroy$)).subscribe({
      next: s => { this.isBusy.set(false); this.selectedIds.set(new Set()); if (s) this.snapshot.set(s); else this.notFound.set(true); },
      error: () => { this.isBusy.set(false); this.notFound.set(true); }
    });
  }

  // Ô "Mượn trả chìa khóa" gộp 1 luồng duy nhất: mã đã có trong danh sách đang mượn → trả, chưa có → mượn.
  onScanKey(): void {
    const s = this.snapshot(); const bc = this.compartmentBarcode().trim();
    if (!s?.readerId || !bc) return;
    const existing = s.currentKeys.find(k => k.cabinetBarcode === bc);
    if (existing) this.returnByBarcode(bc); else this.borrow(bc);
  }

  private borrow(bc: string): void {
    const s = this.snapshot(); if (!s) return;
    this.isBusy.set(true);
    this.service.borrowKey({ readerId: s.readerId, compartmentCode: bc, circPlaceId: this.circPlaceId() }).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isBusy.set(false); this.compartmentBarcode.set(''); this.toastr.success(this.translate.instant('BORROW_KEY.BORROW_OK')); this.lookup(); },
      error: err => { this.isBusy.set(false); this.toastr.error(err?.error?.message || this.translate.instant('BORROW_KEY.BORROW_ERROR')); }
    });
  }

  private returnByBarcode(bc: string): void {
    this.isBusy.set(true);
    this.service.returnKey({ compartmentCode: bc, cardNo: this.snapshot()?.cardNo, circPlaceId: this.circPlaceId() }).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isBusy.set(false); this.compartmentBarcode.set(''); this.toastr.success(this.translate.instant('BORROW_KEY.RETURN_OK')); this.lookup(); },
      error: err => { this.isBusy.set(false); this.toastr.error(err?.error?.message || this.translate.instant('COMMON.LOAD_ERROR')); }
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

  // --- Thanh công cụ icon ---

  toolbarAdd(): void { this.onScanKey(); }

  toolbarEdit(): void {
    const ids = Array.from(this.selectedIds());
    const note = this.reasonText().trim();
    if (ids.length === 0 || !note) return;
    this.toastr.success(this.translate.instant('BORROW_KEY.NOTE_OK'));
  }

  toolbarSearch(): void { this.lookup(); }

  toolbarUndo(): void {
    this.cardNo.set(''); this.compartmentBarcode.set(''); this.reasonText.set('');
    this.snapshot.set(null); this.selectedIds.set(new Set()); this.notFound.set(false);
  }

  toolbarRefresh(): void { this.lookup(); }

  toolbarLockCard(): void {
    const s = this.snapshot();
    if (!s?.readerId) return;
    const reason = this.reasonText().trim();
    if (!reason) { this.toastr.warning(this.translate.instant('BORROW.LOCK_REASON_REQUIRED')); return; }
    this.readerSvc.lockCard(s.readerId, reason).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('BORROW.LOCK_OK')); this.lookup(); },
      error: () => {}
    });
  }
}
