import { Component, OnDestroy, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { LibraryMapApiService, MapBuilding, MapFloor, MapFloorSummary, MapObjectInfo } from '../../services/library-map-api.service';
import { FloorBoardBooking, FloorBoardRoom, RoomBookingOpacService, RoomFloorBoard } from '../../services/room-booking-opac.service';

type SlotState = 'free' | 'booked' | 'inuse' | 'closed';

interface TimeSlot { index: number; label: string; start: Date; end: Date; }
interface BookingBlock { booking: FloorBoardBooking; from: number; to: number; label: string; }

const STATE_STYLE: Record<SlotState, { box: string; dot: string; label: string }> = {
  free:   { box: 'bg-emerald-50 border-emerald-400 text-emerald-800 hover:bg-emerald-100', dot: 'bg-emerald-500', label: 'Còn trống' },
  booked: { box: 'bg-rose-50 border-rose-300 text-rose-800',                               dot: 'bg-rose-500',    label: 'Đã có người đặt' },
  inuse:  { box: 'bg-amber-50 border-amber-400 text-amber-800',                            dot: 'bg-amber-500',   label: 'Đang sử dụng' },
  closed: { box: 'bg-gray-50 border-gray-300 text-gray-500',                               dot: 'bg-gray-300',    label: 'Không thể đặt' },
};

/** Khối lượt đặt trên bảng lịch theo trạng thái: 1 chờ duyệt, 2 đã xác nhận, 3 đang dùng. */
const BOOKING_STYLE: Record<number, { box: string; label: string }> = {
  1: { box: 'bg-amber-100 border-amber-300 text-amber-900', label: 'Chờ duyệt' },
  2: { box: 'bg-rose-100 border-rose-300 text-rose-900',    label: 'Đã xác nhận' },
  3: { box: 'bg-blue-900 border-blue-950 text-white',       label: 'Đang dùng' },
};

const SLOT_COL_PX = 64;

/**
 * Đặt phòng học nhóm theo sơ đồ tầng + bảng lịch (phòng × khung 30 phút) — port trang opac-lrc/room-booking của ELIB-LRC (09-28,
 * 09-29, 10-04). Xem công khai (backend lấy đơn vị theo tên miền/APP_CONFIG), chỉ gửi yêu cầu đặt khi đã đăng nhập bạn đọc.
 * Chọn giờ: bấm ô bắt đầu rồi bấm ô giờ kết thúc (ô đó không tính vào khoảng; bấm lại ô bắt đầu = 1 khung). `?room=` mở sẵn phòng
 * (từ Sơ đồ thư viện).
 */
@Component({
  selector: 'app-opac-room-booking',
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './room-booking.html'
})
export class RoomBookingPageComponent implements OnInit, OnDestroy {
  private mapApi = inject(LibraryMapApiService);
  private api = inject(RoomBookingOpacService);
  private auth = inject(AuthService);
  private router = inject(Router);
  private platformId = inject(PLATFORM_ID);
  /** Phòng cần mở sẵn (?room=MapObjectId). */
  private pendingRoom: number | null = Number(inject(ActivatedRoute).snapshot.queryParamMap.get('room')) || null;

  buildings = signal<MapBuilding[]>([]);
  floors = signal<MapFloorSummary[]>([]);
  buildingId = signal<number | null>(null);
  floorId = signal<number | null>(null);
  floor = signal<MapFloor | null>(null);
  board = signal<RoomFloorBoard | null>(null);
  loading = signal(false);

  readonly today = this.toDateInput(new Date());
  date = signal(this.today);
  selectedRoomId = signal<number | null>(null);
  /** Chỉ số ô bắt đầu đã chọn. */
  rangeStart = signal<number | null>(null);
  /** Chỉ số MỐC kết thúc (loại trừ); null = mới chọn ô bắt đầu (tạm tính 1 ô). */
  rangeEnd = signal<number | null>(null);
  partySize = signal(1);
  members = signal<{ card: string; name: string }[]>([]);
  memberInput = '';
  memberChecking = signal(false);
  note = '';
  submitting = signal(false);
  message = signal<{ ok: boolean; text: string } | null>(null);

  private now = signal(Date.now());
  private timer: ReturnType<typeof setInterval> | null = null;

  readonly isLoggedIn = computed(() => this.auth.isAuthenticated());
  readonly boardRooms = computed(() => this.board()?.rooms ?? []);
  private readonly roomById = computed(() => new Map(this.boardRooms().map(r => [r.mapObjectId, r])));
  readonly selectedRoom = computed(() => this.roomById().get(this.selectedRoomId() ?? -1) ?? null);
  readonly aspectRatio = computed(() => {
    const f = this.floor();
    return f?.width && f?.height && f.width > 0 && f.height > 0 ? `${f.width} / ${f.height}` : '3 / 2';
  });

  readonly maxDate = computed(() => {
    const days = Math.max(7, ...this.boardRooms().map(r => r.maxAdvanceDays || 0));
    const d = new Date(); d.setDate(d.getDate() + days);
    return this.toDateInput(d);
  });

  readonly slots = computed<TimeSlot[]>(() => {
    const b = this.board();
    if (!b) return [];
    const [y, m, d] = this.date().split('-').map(Number);
    const open = this.minutesOf(b.openTime), close = this.minutesOf(b.closeTime), step = b.stepMinutes || 30;
    const result: TimeSlot[] = [];
    for (let t = open, i = 0; t + step <= close; t += step, i++) {
      const start = new Date(y, m - 1, d, 0, t);
      result.push({ index: i, label: this.hhmm(start), start, end: new Date(start.getTime() + step * 60000) });
    }
    return result;
  });
  readonly closeLabel = computed(() => { const s = this.slots(); return s.length ? this.hhmm(s[s.length - 1].end) : ''; });
  readonly gridColumns = computed(() => `220px repeat(${this.slots().length}, ${SLOT_COL_PX}px) 56px`);

  readonly selectedRange = computed(() => {
    const s = this.rangeStart(), slots = this.slots();
    if (s == null || !slots[s]) return null;
    const e = this.rangeEnd() ?? s + 1;
    const to = e >= slots.length ? slots[slots.length - 1].end : slots[e].start;
    return { from: slots[s].start, to, minutes: (e - s) * (this.board()?.stepMinutes || 30) };
  });

  /** Khối lượt đặt của từng phòng, quy về chỉ số cột (cắt theo giờ mở/đóng cửa). */
  readonly blocksByRoom = computed(() => {
    const slots = this.slots(), result = new Map<number, BookingBlock[]>();
    if (!slots.length) return result;
    const open = slots[0].start.getTime(), step = slots[0].end.getTime() - open, n = slots.length;
    for (const room of this.boardRooms()) {
      const blocks: BookingBlock[] = [];
      for (const b of room.busy) {
        const s = new Date(b.startAt), e = new Date(b.endAt);
        const from = Math.max(0, Math.floor((s.getTime() - open) / step));
        const to = Math.min(n, Math.ceil((e.getTime() - open) / step));
        if (to > from) blocks.push({ booking: b, from, to, label: `${this.hhmm(s)}–${this.hhmm(e)}` });
      }
      result.set(room.mapObjectId, blocks);
    }
    return result;
  });

  readonly partyOptions = computed(() => Array.from({ length: Math.max(1, this.selectedRoom()?.capacity ?? 1) }, (_, i) => i + 1));
  readonly minGroup = computed(() => Math.max(1, this.selectedRoom()?.minGroupSize ?? 1));
  readonly membersMissing = computed(() => Math.max(0, this.minGroup() - 1 - this.members().length));

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    this.timer = setInterval(() => this.now.set(Date.now()), 60_000);
    this.mapApi.getBuildings().subscribe(list => {
      this.buildings.set(list);
      if (list.length) this.onBuildingChange(list[0].id, true);
    });
  }

  ngOnDestroy(): void { if (this.timer) clearInterval(this.timer); }

  onBuildingChange(id: number, initial = false): void {
    this.buildingId.set(Number(id));
    this.mapApi.getFloors(Number(id)).subscribe(list => {
      this.floors.set(list);
      if (list.length) this.pickInitialFloor(list, initial);
      else { this.floorId.set(null); this.floor.set(null); this.board.set(null); }
    });
  }

  /** Lần đầu: ưu tiên tầng chứa phòng được yêu cầu (?room=), rồi tầng có phòng học nhóm. */
  private pickInitialFloor(list: MapFloorSummary[], initial: boolean): void {
    if (!initial || list.length === 1) { this.onFloorChange(list[0].id); return; }
    let i = 0;
    const tryNext = () => {
      if (i >= list.length) { this.onFloorChange(list[0].id); return; }
      const f = list[i++];
      this.mapApi.getFloor(f.id).subscribe(floor => {
        const hasRequested = this.pendingRoom != null && floor?.objects.some(o => o.id === this.pendingRoom);
        const hasRoom = floor?.objects.some(o => ['ROOM', 'STUDY_SPACE'].includes((o.objectType ?? '').toUpperCase()));
        if (hasRequested || (this.pendingRoom == null && hasRoom)) this.onFloorChange(f.id);
        else tryNext();
      });
    };
    tryNext();
  }

  onFloorChange(id: number): void {
    this.floorId.set(Number(id));
    this.mapApi.getFloor(Number(id)).subscribe(f => this.floor.set(f));
    this.reloadBoard(true);
  }

  onDateChange(value: string): void {
    if (!value) return;
    this.date.set(value < this.today ? this.today : value > this.maxDate() ? this.maxDate() : value);
    this.reloadBoard(true);
  }

  reloadBoard(resetSelection: boolean): void {
    const floorId = this.floorId();
    if (floorId == null) return;
    if (resetSelection) { this.clearSelection(); this.selectedRoomId.set(null); this.members.set([]); }
    this.loading.set(true);
    this.api.getFloorBoard(floorId, this.date()).subscribe(b => {
      this.board.set(b);
      this.loading.set(false);
      if (this.pendingRoom != null) {
        const room = b?.rooms.find(r => r.mapObjectId === this.pendingRoom);
        this.pendingRoom = null;
        if (room) this.selectRoom(room, true);
      }
    });
  }

  // ── Sơ đồ ──────────────────────────────────────────────────────────────
  boardRoomFor(o: MapObjectInfo): FloorBoardRoom | null { return this.roomById().get(o.id) ?? null; }
  geom(o: MapObjectInfo) { return { x: o.positionX ?? 0, y: o.positionY ?? 0, w: o.width ?? 10, h: o.height ?? 10 }; }

  selectRoom(room: FloorBoardRoom | null, scroll = false): void {
    if (!room) return;
    if (room.mapObjectId !== this.selectedRoomId()) {
      this.selectedRoomId.set(room.mapObjectId);
      this.partySize.set(Math.min(Math.max(1, this.partySize()), Math.max(1, room.capacity)));
      this.members.set([]);
      this.clearSelection();
    }
    if (scroll) setTimeout(() => document.getElementById(`rb-row-${room.mapObjectId}`)?.scrollIntoView({ behavior: 'smooth', block: 'center' }));
  }

  // ── Trạng thái ─────────────────────────────────────────────────────────
  slotState(room: FloorBoardRoom, slot: TimeSlot): SlotState {
    const busy = this.overlapStatus(room, slot.start, slot.end);
    if (busy === 3) return 'inuse';
    if (busy) return 'booked';
    if (room.maintenance || !this.withinRoomHours(room, slot)) return 'closed';
    const earliest = this.now() + (room.minAdvanceMinutes || 0) * 60000;
    const latest = new Date(this.now());
    if (room.maxAdvanceHours && room.maxAdvanceHours > 0) latest.setTime(latest.getTime() + room.maxAdvanceHours * 3600000);
    else latest.setDate(latest.getDate() + (room.maxAdvanceDays || 7));
    return slot.start.getTime() < earliest || slot.start > latest ? 'closed' : 'free';
  }

  /** Giờ mở cửa riêng của phòng (theo thứ / loại cơ sở / ngày đặc biệt) — trục lịch là khung rộng nhất của cả tầng. */
  private withinRoomHours(room: FloorBoardRoom, slot: TimeSlot): boolean {
    if (room.closed) return false;
    if (!room.openTime || !room.closeTime) return true;
    const from = slot.start.getHours() * 60 + slot.start.getMinutes();
    const to = slot.end.getHours() * 60 + slot.end.getMinutes() || 24 * 60;
    return from >= this.minutesOf(room.openTime) && to <= this.minutesOf(room.closeTime);
  }

  /** Trạng thái hiển thị trên sơ đồ: theo khoảng đang chọn (nếu là phòng đó), không thì theo giờ hiện tại (hôm nay); ngày khác
   *  thì "trống" khi còn ít nhất 1 khung đặt được. */
  roomState(room: FloorBoardRoom): SlotState {
    const range = room.mapObjectId === this.selectedRoomId() ? this.selectedRange() : null;
    if (range) {
      const busy = this.overlapStatus(room, range.from, range.to);
      return busy === 3 ? 'inuse' : busy ? 'booked' : 'free';
    }
    if (this.date() === this.today) {
      const n = new Date(this.now());
      const busy = this.overlapStatus(room, n, new Date(n.getTime() + 60000));
      if (busy === 3) return 'inuse';
      if (busy) return 'booked';
    }
    return this.freeCount(room) > 0 ? 'free' : 'closed';
  }

  style(state: SlotState) { return STATE_STYLE[state]; }
  bookingStyle(status: number) { return BOOKING_STYLE[status] ?? BOOKING_STYLE[2]; }
  readonly legend = (['free', 'booked', 'inuse', 'closed'] as SlotState[]).map(s => ({ state: s, ...STATE_STYLE[s] }));

  freeCount(room: FloorBoardRoom): number { return this.slots().filter(s => this.slotState(room, s) === 'free').length; }
  earliestFree(room: FloorBoardRoom): string | null { return room.earliestFreeAt ? this.hhmm(new Date(room.earliestFreeAt)) : null; }
  notAllowed(room: FloorBoardRoom): boolean { return room.readerAllowed === false; }
  isSelectedRow(room: FloorBoardRoom) { return room.mapObjectId === this.selectedRoomId(); }

  isInRange(room: FloorBoardRoom, index: number): boolean {
    const s = this.rangeStart();
    if (s == null || !this.isSelectedRow(room)) return false;
    return index >= s && index < (this.rangeEnd() ?? s + 1);
  }

  awaitingEnd(room: FloorBoardRoom): boolean {
    return this.isSelectedRow(room) && this.rangeStart() != null && this.rangeEnd() == null;
  }

  // ── Chọn khung giờ: ô bấm thứ hai là GIỜ KẾT THÚC ────────────────────
  /** index = chỉ số ô (0..N-1) hoặc N = cột giờ đóng cửa. */
  pickCell(room: FloorBoardRoom, index: number): void {
    const slots = this.slots();
    const s = this.rangeStart();
    this.message.set(null);
    if (this.awaitingEnd(room) && s != null && index >= s) {
      const end = index === s ? s + 1 : index;
      if (slots.slice(s, end).some(x => this.slotState(room, x) !== 'free')) {
        this.message.set({ ok: false, text: 'Khoảng giờ đã chọn có khung không đặt được — vui lòng chọn lại.' });
        this.startAt(room, index);
        return;
      }
      this.rangeEnd.set(end);
      return;
    }
    this.startAt(room, index);
  }

  /** Bấm vào khối lượt đặt khi đang chờ giờ kết thúc = kết thúc đúng lúc lượt đó bắt đầu. */
  pickBlock(room: FloorBoardRoom, block: BookingBlock): void {
    const s = this.rangeStart();
    if (this.awaitingEnd(room) && s != null && block.from > s) this.pickCell(room, block.from);
  }

  private startAt(room: FloorBoardRoom, index: number): void {
    const slot = this.slots()[index];
    this.selectRoom(room);
    if (!slot || this.slotState(room, slot) !== 'free') { this.clearSelection(); return; }
    this.rangeStart.set(index);
    this.rangeEnd.set(null);
  }

  clearSelection(): void { this.rangeStart.set(null); this.rangeEnd.set(null); }

  addMember(): void {
    const card = this.memberInput.trim();
    const room = this.selectedRoom();
    if (!card || !room) return;
    if (!this.isLoggedIn()) { this.goLogin(); return; }
    if (this.members().some(m => m.card.toLowerCase() === card.toLowerCase())) { this.memberInput = ''; return; }
    if (this.members().length + 1 >= room.capacity) { this.message.set({ ok: false, text: `Phòng chỉ chứa tối đa ${room.capacity} người.` }); return; }
    this.memberChecking.set(true);
    this.api.lookupMember(card).subscribe(name => {
      this.memberChecking.set(false);
      if (!name) { this.message.set({ ok: false, text: `Không tìm thấy bạn đọc có thẻ "${card}".` }); return; }
      this.members.set([...this.members(), { card, name }]);
      this.memberInput = '';
    });
  }

  removeMember(card: string): void { this.members.set(this.members().filter(m => m.card !== card)); }

  submit(): void {
    const room = this.selectedRoom(), range = this.selectedRange();
    if (!room || !range) return;
    if (!this.isLoggedIn()) { this.goLogin(); return; }
    if (this.notAllowed(room)) { this.message.set({ ok: false, text: `Phòng này chỉ dành cho: ${(room.allowedReaderTypes ?? []).join(', ')}.` }); return; }
    if (this.membersMissing() > 0) {
      this.message.set({ ok: false, text: `Phòng cần tối thiểu ${this.minGroup()} người: nhập thêm ${this.membersMissing()} thẻ thành viên.` });
      return;
    }
    const memberCards = this.members().map(m => m.card);
    const partySize = memberCards.length > 0 ? memberCards.length + 1 : this.partySize();
    this.submitting.set(true);
    this.api.book({ mapObjectId: room.mapObjectId, startAt: range.from.toISOString(), endAt: range.to.toISOString(), partySize, note: this.note.trim(), memberCards })
      .subscribe(res => {
        this.submitting.set(false);
        if (!res.ok) { this.message.set({ ok: false, text: res.message || 'Không thể đặt phòng.' }); return; }
        this.message.set({ ok: true, text: res.status === 2
          ? 'Đặt phòng thành công — lượt đặt đã được duyệt. Xem mã QR check-in ở Tài khoản → Đặt phòng học nhóm.'
          : 'Đã gửi yêu cầu đặt phòng, vui lòng chờ thư viện duyệt.' });
        this.clearSelection();
        this.members.set([]);
        this.note = '';
        this.reloadBoard(false);
      });
  }

  goLogin(): void { this.router.navigate(['/login'], { queryParams: { returnUrl: this.router.url } }); }

  hhmm(d: Date): string { return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`; }

  /** 3 nếu có lượt đang dùng giao khoảng, 1/2 nếu có lượt chờ duyệt/đã duyệt, 0 nếu trống. */
  private overlapStatus(room: FloorBoardRoom, from: Date, to: Date): number {
    let result = 0;
    for (const b of room.busy) {
      if (new Date(b.startAt) < to && new Date(b.endAt) > from) {
        if (b.status === 3) return 3;
        result = b.status;
      }
    }
    return result;
  }

  private minutesOf(hhmm: string): number {
    const [h, m] = (hhmm || '0:0').split(':').map(Number);
    return (h || 0) * 60 + (m || 0);
  }

  private toDateInput(d: Date): string {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }
}
