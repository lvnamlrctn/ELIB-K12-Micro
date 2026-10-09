import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ToastrService } from '../../../../services/shared/toastr.service';
import { RoomBookingAdminService } from '../../../../services/map/room-booking.service';
import { AffectedBooking, RoomOpeningHour, RoomSpecialDay, SpecialDayInput } from '../../../../models/map/room-booking';
import { MAP_OBJECT_CATEGORIES } from '../../../../models/map/map-object';
import { CanDirective } from '../../../../directives/can.directive';
import { DateInputComponent as DateInput } from '../../../../components/date-input/date-input';

interface WeekdayRow { weekday: number; isClosed: boolean; openTime: string; closeTime: string; }

/** Tab "Giờ mở cửa": giờ theo thứ cho từng loại cơ sở + ngày đặc biệt (lễ, đóng cửa đột xuất). */
@Component({
  selector: 'app-room-booking-hours-tab',
  standalone: true,
  imports: [DateInput, FormsModule, DatePipe, MatIconModule, TranslateModule, CanDirective],
  templateUrl: './room-booking-hours-tab.html'
})
export class RoomBookingHoursTab implements OnInit {
  private svc = inject(RoomBookingAdminService);
  private toastr = inject(ToastrService);
  private translate = inject(TranslateService);

  readonly categories = MAP_OBJECT_CATEGORIES;
  /** Thứ 2 → Chủ nhật. */
  readonly weekdays = [1, 2, 3, 4, 5, 6, 0];

  category = signal<number | null>(null);
  all = signal<RoomOpeningHour[]>([]);
  rows = signal<WeekdayRow[]>([]);
  savingHours = signal(false);

  specialDays = signal<RoomSpecialDay[]>([]);
  showDayModal = signal(false);
  editingPublicId = signal<string | null>(null);
  day: SpecialDayInput = this.emptyDay();
  affected = signal<AffectedBooking[] | null>(null);
  savingDay = signal(false);

  ngOnInit(): void {
    this.loadHours();
    this.loadSpecialDays();
  }

  weekdayLabel(d: number): string {
    return this.translate.instant(`STUDY_ROOM_BOOKING.WEEKDAY_${d}`);
  }

  categoryLabel(c: number | null): string {
    return c == null ? this.translate.instant('STUDY_ROOM_BOOKING.ALL_CATEGORIES') : (this.categories.find(x => x.value === c)?.label ?? `#${c}`);
  }

  // ── Giờ theo thứ ─────────────────────────────────────────────────────────
  loadHours(): void {
    this.svc.openingHours().subscribe({ next: list => { this.all.set(list); this.buildRows(); }, error: () => {} });
  }

  selectCategory(c: number | null): void {
    this.category.set(c);
    this.buildRows();
  }

  private buildRows(): void {
    const mine = this.all().filter(h => (h.category ?? null) === this.category());
    this.rows.set(this.weekdays.map(d => {
      const h = mine.find(x => x.weekday === d);
      return { weekday: d, isClosed: !!h?.isClosed, openTime: h?.openTime ?? '', closeTime: h?.closeTime ?? '' };
    }));
  }

  saveHours(): void {
    this.savingHours.set(true);
    const payload = this.rows().map(r => ({ weekday: r.weekday, isClosed: r.isClosed, openTime: r.openTime || null, closeTime: r.closeTime || null }));
    this.svc.saveOpeningHours(this.category(), payload).subscribe({
      next: list => {
        this.all.set(list); this.buildRows(); this.savingHours.set(false);
        this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS'));
      },
      error: () => this.savingHours.set(false)
    });
  }

  // ── Ngày đặc biệt ────────────────────────────────────────────────────────
  private today(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  private emptyDay(): SpecialDayInput {
    return { date: '', category: null, isClosed: true, openTime: null, closeTime: null, reason: null, cancelAffected: false };
  }

  loadSpecialDays(): void {
    this.svc.specialDays(this.today()).subscribe({ next: list => this.specialDays.set(list), error: () => {} });
  }

  openAddDay(): void {
    this.editingPublicId.set(null);
    this.day = { ...this.emptyDay(), date: this.today() };
    this.affected.set(null);
    this.showDayModal.set(true);
  }

  openEditDay(d: RoomSpecialDay): void {
    this.editingPublicId.set(d.publicId);
    this.day = { date: d.date.substring(0, 10), category: d.category, isClosed: d.isClosed, openTime: d.openTime, closeTime: d.closeTime,
      reason: d.reason, cancelAffected: false };
    this.affected.set(null);
    this.showDayModal.set(true);
  }

  closeDayModal(): void {
    this.showDayModal.set(false);
  }

  /** Lần bấm đầu: xem trước lượt bị ảnh hưởng; có lượt thì hiện danh sách để thủ thư chọn huỷ hay giữ rồi bấm lưu lần nữa. */
  submitDay(): void {
    const input = { ...this.day, openTime: this.day.isClosed ? null : this.day.openTime, closeTime: this.day.isClosed ? null : this.day.closeTime };
    this.savingDay.set(true);
    if (this.affected() === null) {
      this.svc.previewSpecialDay(input, this.editingPublicId()).subscribe({
        next: r => {
          this.savingDay.set(false);
          if (r.affected.length > 0) { this.affected.set(r.affected); this.day.cancelAffected = true; }
          else { this.affected.set([]); this.submitDay(); }
        },
        error: () => this.savingDay.set(false)
      });
      return;
    }
    this.svc.saveSpecialDay(input, this.editingPublicId()).subscribe({
      next: r => {
        this.savingDay.set(false);
        this.showDayModal.set(false);
        this.loadSpecialDays();
        this.toastr.success(r.cancelled > 0
          ? this.translate.instant('STUDY_ROOM_BOOKING.DAY_SAVED_CANCELLED', { count: r.cancelled })
          : this.translate.instant('COMMON.UPDATE_SUCCESS'));
      },
      error: () => this.savingDay.set(false)
    });
  }

  /** Đổi ngày/giờ sau khi đã xem trước thì phải xem trước lại. */
  dayChanged(): void {
    this.affected.set(null);
  }

  deleteDay(d: RoomSpecialDay): void {
    if (!confirm(this.translate.instant('STUDY_ROOM_BOOKING.DELETE_DAY_CONFIRM'))) return;
    this.svc.deleteSpecialDay(d.publicId).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.loadSpecialDays(); },
      error: () => {}
    });
  }
}
