import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ToastrService } from '../../../../services/shared/toastr.service';
import { RoomBookingAdminService } from '../../../../services/map/room-booking.service';
import { ReaderService } from '../../../../services/reader/reader.service';
import { RoomBookingBan } from '../../../../models/map/room-booking';
import { Reader } from '../../../../models/reader/reader';
import { CanDirective } from '../../../../directives/can.directive';

/** Tab "Danh sách chặn": bạn đọc bị tạm khoá đặt phòng (tự động do vắng mặt hoặc thủ thư khoá), gỡ khoá, khoá tay. */
@Component({
  selector: 'app-room-booking-bans-tab',
  standalone: true,
  imports: [FormsModule, DatePipe, MatIconModule, TranslateModule, CanDirective],
  templateUrl: './room-booking-bans-tab.html'
})
export class RoomBookingBansTab implements OnInit {
  private svc = inject(RoomBookingAdminService);
  private readerSvc = inject(ReaderService);
  private toastr = inject(ToastrService);
  private translate = inject(TranslateService);

  bans = signal<RoomBookingBan[]>([]);
  total = signal(0);
  loading = signal(false);
  activeOnly = true;
  keyword = '';
  pageIndex = 1;
  readonly pageSize = 20;

  showAdd = signal(false);
  readerKeyword = '';
  readerResults = signal<Reader[]>([]);
  selectedReader = signal<Reader | null>(null);
  days = 7;
  reason = '';
  saving = signal(false);

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.svc.bans(this.activeOnly, this.keyword, this.pageIndex, this.pageSize).subscribe({
      next: r => { this.bans.set(r.data); this.total.set(r.recordsTotal); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  search(): void { this.pageIndex = 1; this.load(); }

  page(delta: number): void {
    const last = Math.max(1, Math.ceil(this.total() / this.pageSize));
    this.pageIndex = Math.min(last, Math.max(1, this.pageIndex + delta));
    this.load();
  }

  lastPage(): number { return Math.max(1, Math.ceil(this.total() / this.pageSize)); }

  isActive(b: RoomBookingBan): boolean { return !b.liftedAt && new Date(b.bannedUntil).getTime() > Date.now(); }

  lift(b: RoomBookingBan): void {
    if (!confirm(this.translate.instant('STUDY_ROOM_BOOKING.LIFT_CONFIRM', { name: b.readerName ?? '' }))) return;
    this.svc.liftBan(b.publicId).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS')); this.load(); },
      error: () => {}
    });
  }

  openAdd(): void {
    this.readerKeyword = ''; this.readerResults.set([]); this.selectedReader.set(null);
    this.days = 7; this.reason = '';
    this.showAdd.set(true);
  }

  findReaders(): void {
    const k = this.readerKeyword.trim();
    if (!k) { this.readerResults.set([]); return; }
    this.readerSvc.search({ keyword: k, pageIndex: 1, pageSize: 10 }).subscribe({
      next: r => this.readerResults.set(r.data), error: () => this.readerResults.set([])
    });
  }

  readerLabel(r: Reader): string {
    return `${[r.firstName, r.lastName].filter(Boolean).join(' ')} (${r.cardno ?? ''})`;
  }

  saveBan(): void {
    const reader = this.selectedReader();
    if (!reader || !this.reason.trim()) return;
    this.saving.set(true);
    this.svc.addBan(Number(reader.id), this.days, this.reason.trim()).subscribe({
      next: () => {
        this.saving.set(false); this.showAdd.set(false);
        this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS'));
        this.search();
      },
      error: () => this.saving.set(false)
    });
  }
}
