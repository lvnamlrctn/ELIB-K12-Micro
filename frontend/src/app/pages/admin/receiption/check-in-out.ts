import { Component, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { CheckInService } from '../../../services/receiption/checkin.service';
import { CircPlaceService } from '../../../services/printbook/circ-place.service';
import { CheckStatus } from '../../../models/receiption/checkin';
import { CircPlace } from '../../../models/printbook/circ-place';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { FaceCaptureComponent } from '../../../components/face-capture/face-capture';
import { FaceMatchResult } from '../../../models/circulation/face-match';

@Component({
  selector: 'app-check-in-out',
  standalone: true,
  imports: [CommonModule, FormsModule, NgSelectModule, TranslateModule, MatIconModule, AppDatePipe, FaceCaptureComponent],
  templateUrl: './check-in-out.html'
})
export class CheckInOutPage implements OnInit, OnDestroy {
  private service     = inject(CheckInService);
  private circPlaceSvc = inject(CircPlaceService);
  private toastr      = inject(ToastrService);
  public  translate   = inject(TranslateService);
  private destroy$    = new Subject<void>();

  circPlaces = signal<CircPlace[]>([]);
  circPlaceId = signal<number | null>(null);
  cardNo = '';
  status = signal<CheckStatus | null>(null);
  notFound = signal(false);
  isBusy = signal(false);
  recent = signal<{ cardNo: string; fullName: string; action: string; time: string }[]>([]);

  countLocked        = computed(() => { const s = this.status(); return s && s.status !== 2 ? 1 : 0; });
  loanCountBorrowing = computed(() => this.status()?.currentLoans.length ?? 0);
  loanCountOverdue   = computed(() => (this.status()?.currentLoans ?? []).filter(l => this.isOverdue(l.dueDate)).length);
  loanCountRenew     = computed(() => (this.status()?.currentLoans ?? []).reduce((sum, l) => sum + (l.renewCount || 0), 0));

  ngOnInit(): void {
    this.circPlaceSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({
      next: l => { this.circPlaces.set(l); if (l.length && this.circPlaceId() == null) this.circPlaceId.set(Math.min(...l.map(x => x.id))); }, error: () => {}
    });
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  lookup(): void {
    const card = (this.cardNo || '').trim(); if (!card) return;
    this.isBusy.set(true); this.notFound.set(false); this.status.set(null);
    this.service.getStatus(card, this.circPlaceId()).pipe(takeUntil(this.destroy$)).subscribe({
      next: s => {
        this.isBusy.set(false);
        if (s && s.found) { this.status.set(s); s.checkedIn ? this.doCheckOut(s) : this.doCheckIn(s); }
        else this.notFound.set(true);
      },
      error: () => { this.isBusy.set(false); this.notFound.set(true); }
    });
  }

  private doCheckIn(s: CheckStatus): void {
    this.isBusy.set(true);
    this.service.checkIn({ cardNo: s.cardNo || this.cardNo.trim(), circPlaceId: this.circPlaceId() }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        this.isBusy.set(false); this.pushRecent(s, 'IN'); this.toastr.success(this.translate.instant('CHECK.IN_OK'));
        this.status.update(cur => cur ? { ...cur, checkedIn: true, checkInId: res?.id ?? cur.checkInId, checkInTime: res?.checkInTime ?? new Date().toISOString() } : cur);
        this.cardNo = '';
      },
      error: () => { this.isBusy.set(false); }
    });
  }
  private doCheckOut(s: CheckStatus): void {
    this.isBusy.set(true);
    this.service.checkOut({ cardNo: s.cardNo || this.cardNo.trim(), checkInId: s.checkInId, circPlaceId: this.circPlaceId() }).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.isBusy.set(false); this.pushRecent(s, 'OUT'); this.toastr.success(this.translate.instant('CHECK.OUT_OK'));
        this.status.update(cur => cur ? { ...cur, checkedIn: false, checkInId: undefined } : cur);
        this.cardNo = '';
      },
      error: () => { this.isBusy.set(false); }
    });
  }

  private pushRecent(s: CheckStatus, action: 'IN' | 'OUT'): void {
    const now = new Date(); const time = now.toLocaleTimeString('vi-VN');
    this.recent.set([{ cardNo: s.cardNo || this.cardNo.trim(), fullName: s.fullName || '', action, time }, ...this.recent()].slice(0, 12));
  }
  clear(): void { this.cardNo = ''; this.status.set(null); this.notFound.set(false); }

  onFaceMatched(result: FaceMatchResult): void {
    if (!result.cardNo) return;
    this.cardNo = result.cardNo;
    this.lookup();
  }

  isOverdue(dueDate?: string): boolean {
    if (!dueDate) return false;
    return new Date(dueDate).getTime() < new Date(new Date().toDateString()).getTime();
  }
}
