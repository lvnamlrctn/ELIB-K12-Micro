import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ToastrService } from '../../../../services/shared/toastr.service';
import { MapFloorService } from '../../../../services/map/map-floor.service';
import { AccessControlAdminService, RoomBookingAdminService } from '../../../../services/map/room-booking.service';
import { StaffBoard, StaffBoardBusy, StaffBoardRoom } from '../../../../models/map/room-booking';
import { MapFloor } from '../../../../models/map/map-floor';
import { CanDirective } from '../../../../directives/can.directive';
import { DateInputComponent as DateInput } from '../../../../components/date-input/date-input';

/** Màu khối lượt đặt theo trạng thái (1 chờ duyệt, 2 đã duyệt, 3 đang dùng). */
const BLOCK: Record<number, string> = {
  1: 'bg-amber-100 border-amber-400 text-amber-900',
  2: 'bg-rose-100 border-rose-400 text-rose-900',
  3: 'bg-blue-800 border-blue-900 text-white',
};

/** Tab "Sơ đồ": tình trạng sử dụng các phòng của 1 tầng trong ngày, tô màu theo trạng thái, bấm lượt đặt để duyệt / trả phòng /
 *  mở cửa ngay trên lịch (yêu cầu kỹ thuật 1.2: xem tình trạng khu vực bằng màu khác nhau). */
@Component({
  selector: 'app-room-booking-map-tab',
  standalone: true,
  imports: [DateInput, FormsModule, DatePipe, MatIconModule, TranslateModule, CanDirective],
  templateUrl: './room-booking-map-tab.html'
})
export class RoomBookingMapTab implements OnInit, OnDestroy {
  private svc = inject(RoomBookingAdminService);
  private access = inject(AccessControlAdminService);
  private floorSvc = inject(MapFloorService);
  private toastr = inject(ToastrService);
  private translate = inject(TranslateService);

  floors = signal<MapFloor[]>([]);
  floorId: number | null = null;
  date = this.today();
  board = signal<StaffBoard | null>(null);
  loading = signal(false);
  selected = signal<{ room: StaffBoardRoom; busy: StaffBoardBusy } | null>(null);
  private now = signal(Date.now());
  private timer?: ReturnType<typeof setInterval>;

  hours = computed(() => {
    const b = this.board();
    if (!b) return [];
    const open = this.minutes(b.openTime), close = this.minutes(b.closeTime);
    const list: number[] = [];
    for (let m = Math.ceil(open / 60) * 60; m < close; m += 60) list.push(m);
    return list;
  });

  ngOnInit(): void {
    this.floorSvc.searchAll().subscribe(list => {
      this.floors.set(list);
      if (list.length && this.floorId == null) this.pickFloorWithRooms(list, 0);
    });
    this.timer = setInterval(() => this.now.set(Date.now()), 60_000);
  }

  ngOnDestroy(): void { clearInterval(this.timer); }

  private today(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  /** Mặc định chọn tầng đầu tiên có phòng khả đặt (thử lần lượt từng tầng). */
  private pickFloorWithRooms(list: MapFloor[], i: number): void {
    if (i >= list.length) { this.floorId = list[0].id; this.load(); return; }
    this.svc.floorBoard(list[i].id, this.date).subscribe({
      next: b => {
        if (b.rooms.length) { this.floorId = list[i].id; this.board.set(b); }
        else this.pickFloorWithRooms(list, i + 1);
      },
      error: () => this.pickFloorWithRooms(list, i + 1)
    });
  }

  load(): void {
    if (this.floorId == null) return;
    this.loading.set(true);
    this.selected.set(null);
    this.svc.floorBoard(this.floorId, this.date).subscribe({
      next: b => { this.board.set(b); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  floorLabel(f: MapFloor): string { return f.name || `${this.translate.instant('STUDY_ROOM_BOOKING.FLOOR')} ${f.floorNumber ?? ''}`; }

  private minutes(hhmm: string): number { const [h, m] = (hhmm || '0:0').split(':').map(Number); return (h || 0) * 60 + (m || 0); }
  private dayMinutes(iso: string): number { const d = new Date(iso); return d.getHours() * 60 + d.getMinutes(); }

  private span(): [number, number] {
    const b = this.board()!;
    return [this.minutes(b.openTime), this.minutes(b.closeTime)];
  }

  /** Vị trí % trên trục giờ của tầng. */
  left(minutes: number): number { const [o, c] = this.span(); return Math.max(0, Math.min(100, (minutes - o) * 100 / (c - o))); }
  blockLeft(b: StaffBoardBusy): number { return this.left(this.dayMinutes(b.startAt)); }
  blockWidth(b: StaffBoardBusy): number {
    const end = new Date(b.endAt).getDate() !== new Date(b.startAt).getDate() ? 24 * 60 : this.dayMinutes(b.endAt);
    return Math.max(1, this.left(end) - this.blockLeft(b));
  }
  closedLeft(r: StaffBoardRoom): number { return this.left(this.minutes(r.openTime)); }
  closedRight(r: StaffBoardRoom): number { return 100 - this.left(this.minutes(r.closeTime)); }
  nowLeft(): number | null {
    if (this.date !== this.today() || !this.board()) return null;
    const d = new Date(this.now()); const m = d.getHours() * 60 + d.getMinutes();
    const [o, c] = this.span();
    return m < o || m > c ? null : this.left(m);
  }
  blockClass(b: StaffBoardBusy): string { return BLOCK[b.status] ?? BLOCK[2]; }
  hourLabel(m: number): string { return `${String(Math.floor(m / 60)).padStart(2, '0')}:00`; }

  /** Phòng đang có người dùng ngay lúc này (để tô màu tên phòng). */
  roomState(r: StaffBoardRoom): 'closed' | 'inuse' | 'booked' | 'free' {
    if (r.closed) return 'closed';
    const now = this.now();
    const cur = r.busy.find(b => new Date(b.startAt).getTime() <= now && new Date(b.endAt).getTime() > now);
    return cur ? (cur.status === 3 ? 'inuse' : 'booked') : 'free';
  }

  open(room: StaffBoardRoom, busy: StaffBoardBusy): void { this.selected.set({ room, busy }); }

  approve(b: StaffBoardBusy): void {
    if (!b.publicId) return;
    this.svc.approve(b.publicId).subscribe({ next: () => { this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS')); this.load(); }, error: () => {} });
  }

  checkOut(b: StaffBoardBusy): void {
    if (!b.publicId) return;
    this.svc.checkOut(b.publicId).subscribe({ next: () => { this.toastr.success(this.translate.instant('STUDY_ROOM_BOOKING.CHECKED_OUT')); this.load(); }, error: () => {} });
  }

  unlock(b: StaffBoardBusy): void {
    if (!b.publicId) return;
    const note = prompt(this.translate.instant('STUDY_ROOM_BOOKING.UNLOCK_NOTE_PROMPT')) ?? '';
    this.access.unlock({ bookingPublicId: b.publicId, note }).subscribe({
      next: r => this.toastr.success(this.translate.instant('STUDY_ROOM_BOOKING.UNLOCK_SENT', { count: r.devices })),
      error: () => {}
    });
  }
}
