import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { RoomBookingAdminService, RoomBookingTenantScope } from '../../../../services/map/room-booking.service';
import { RoomBookingConfigService } from '../../../../services/map/room-booking-config.service';
import { RoomBookingReport, RoomBookingReportRequest } from '../../../../models/map/room-booking';
import { MAP_OBJECT_CATEGORIES } from '../../../../models/map/map-object';
import { DateInputComponent as DateInput } from '../../../../components/date-input/date-input';

/** Tab "Báo cáo": tổng hợp theo khoảng ngày (trạng thái, vắng mặt/huỷ, tỷ lệ sử dụng theo phòng, giờ cao điểm, bạn đọc) + Excel. */
@Component({
  selector: 'app-room-booking-report-tab',
  standalone: true,
  imports: [DateInput, FormsModule, MatIconModule, TranslateModule],
  templateUrl: './room-booking-report-tab.html'
})
export class RoomBookingReportTab implements OnInit {
  private svc = inject(RoomBookingAdminService);
  private configSvc = inject(RoomBookingConfigService);
  private scope = inject(RoomBookingTenantScope);
  private translate = inject(TranslateService);

  readonly categories = MAP_OBJECT_CATEGORIES;
  readonly statuses = [1, 2, 3, 4, 5, 6, 7];
  readonly weekdays = [1, 2, 3, 4, 5, 6, 0];
  rooms = signal<{ id: number; name: string }[]>([]);
  from = this.dateInput(-29);
  to = this.dateInput(0);
  mapObjectId: number | null = null;
  category: number | null = null;

  report = signal<RoomBookingReport | null>(null);
  loading = signal(false);
  exporting = signal(false);
  error = signal<string | null>(null);

  maxHour = computed(() => Math.max(1, ...(this.report()?.byHour ?? [0])));
  maxWeekday = computed(() => Math.max(1, ...(this.report()?.byWeekday ?? [0])));
  /** Chỉ hiện các giờ có lượt hoặc trong khung 6–22h cho gọn. */
  hours = computed(() => {
    const h = this.report()?.byHour ?? [];
    return Array.from({ length: 24 }, (_, i) => i).filter(i => (h[i] ?? 0) > 0 || (i >= 6 && i <= 22));
  });

  ngOnInit(): void {
    this.configSvc.search({ tenantId: this.scope.tenantId(), pageIndex: 1, pageSize: 200 }).subscribe(res =>
      this.rooms.set(res.data.map(c => ({ id: c.mapObjectId, name: c.roomName || `#${c.mapObjectId}` }))));
    this.load();
  }

  private dateInput(offset: number): string {
    const d = new Date(); d.setDate(d.getDate() + offset);
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  private request(): RoomBookingReportRequest {
    return { from: this.from, to: this.to, mapObjectId: this.mapObjectId, category: this.category };
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.svc.report(this.request()).subscribe({
      next: r => { this.report.set(r); this.loading.set(false); },
      error: err => { this.error.set(err?.error?.message ?? this.translate.instant('COMMON.UPDATE_ERROR')); this.loading.set(false); }
    });
  }

  exportExcel(): void {
    this.exporting.set(true);
    this.svc.reportExport(this.request()).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = `bao-cao-dat-phong-${this.from}-${this.to}.xlsx`; a.click();
        URL.revokeObjectURL(url);
        this.exporting.set(false);
      },
      error: () => this.exporting.set(false)
    });
  }

  statusCount(s: number): number { return this.report()?.byStatus?.[s] ?? 0; }
  weekdayLabel(d: number): string { return this.translate.instant(`STUDY_ROOM_BOOKING.WEEKDAY_${d}`); }
  pad(h: number): string { return String(h).padStart(2, '0'); }
}
