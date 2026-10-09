import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ToastrService } from '../../../../services/shared/toastr.service';
import { RoomBookingAdminService } from '../../../../services/map/room-booking.service';
import { NotificationTemplate } from '../../../../models/map/room-booking';
import { CanDirective } from '../../../../directives/can.directive';

/** Dữ liệu mẫu cho phần xem trước (khớp mẫu backend gửi thử). */
const SAMPLE: Record<string, string> = {
  ReaderName: 'Nguyễn Văn A', RoomName: 'Phòng học nhóm 1', StartAt: '05/10/2026 09:00', EndAt: '05/10/2026 10:00',
  Reason: '(lý do mẫu)', BannedUntil: '12/10/2026 09:00',
};

/** Tab "Mẫu thông báo": sửa tiêu đề/nội dung email từng sự kiện đặt phòng, xem trước, gửi thử, khôi phục mặc định. */
@Component({
  selector: 'app-room-booking-templates-tab',
  standalone: true,
  imports: [FormsModule, MatIconModule, TranslateModule, CanDirective],
  templateUrl: './room-booking-templates-tab.html'
})
export class RoomBookingTemplatesTab implements OnInit {
  private svc = inject(RoomBookingAdminService);
  private toastr = inject(ToastrService);
  private translate = inject(TranslateService);

  templates = signal<NotificationTemplate[]>([]);
  tokens = signal<string[]>([]);
  selected = signal<string | null>(null);
  subject = signal('');
  body = signal('');
  testTo = '';
  saving = signal(false);

  current = computed(() => this.templates().find(t => t.event === this.selected()) ?? null);
  preview = computed(() => this.render(this.body()));
  previewSubject = computed(() => this.render(this.subject()));
  dirty = computed(() => { const c = this.current(); return !!c && (c.subject !== this.subject() || c.body !== this.body()); });

  ngOnInit(): void { this.load(); }

  load(keep?: string): void {
    this.svc.templates().subscribe({
      next: r => { this.templates.set(r.templates); this.tokens.set(r.tokens); this.select(keep ?? this.selected() ?? r.templates[0]?.event ?? null); },
      error: () => {}
    });
  }

  select(event: string | null): void {
    this.selected.set(event);
    const t = this.templates().find(x => x.event === event);
    this.subject.set(t?.subject ?? '');
    this.body.set(t?.body ?? '');
  }

  eventLabel(e: string): string { return this.translate.instant(`STUDY_ROOM_BOOKING.EVENT_${e.toUpperCase()}`); }

  tokenLabel(token: string): string { return `{${token}}`; }

  insertToken(token: string): void { this.body.set(this.body() + `{${token}}`); }

  private render(text: string): string {
    return Object.entries(SAMPLE).reduce((s, [k, v]) => s.split(`{${k}}`).join(v), text ?? '');
  }

  save(): void {
    const e = this.selected();
    if (!e) return;
    this.saving.set(true);
    this.svc.saveTemplate(e, this.subject(), this.body()).subscribe({
      next: list => { this.templates.set(list); this.select(e); this.saving.set(false); this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS')); },
      error: () => this.saving.set(false)
    });
  }

  reset(): void {
    const e = this.selected();
    if (!e || !confirm(this.translate.instant('STUDY_ROOM_BOOKING.TEMPLATE_RESET_CONFIRM'))) return;
    this.svc.resetTemplate(e).subscribe({
      next: list => { this.templates.set(list); this.select(e); this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS')); },
      error: () => {}
    });
  }

  sendTest(): void {
    const e = this.selected();
    if (!e) return;
    if (this.dirty()) { this.toastr.warning(this.translate.instant('STUDY_ROOM_BOOKING.TEMPLATE_SAVE_FIRST')); return; }
    this.svc.testTemplate(e, this.testTo.trim() || null).subscribe({
      next: () => this.toastr.success(this.translate.instant('STUDY_ROOM_BOOKING.TEMPLATE_TEST_SENT')),
      error: () => {}
    });
  }
}
