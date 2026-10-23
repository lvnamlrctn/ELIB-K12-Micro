import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Language, LDatePipe } from '../core/i18n';
import { ReaderAuth } from '../core/reader-auth';
import { DateInput } from '../core/date-input';
import {
  SpaceApi, SpaceBoardRoom, SpaceBookingStatus, SpaceBookingSummary, SpaceBuildingSummary, SpaceFloorBoard, SpaceFloorSummary,
  SpaceMemberSummary, SpaceRoomSummary,
} from '../core/space';

type CellStatus = 'free' | 'selected' | 'pending' | 'approved' | 'inuse' | 'closed';

/** Một ô lưới: phòng × khung giờ (30'). */
interface Cell { startMin: number; status: CellStatus; title: string | null; }

/**
 * Sơ đồ tầng trực quan + lưới đặt phòng (monolith: /dat-phong-hoc-nhom, FloorBoard) — chọn toà/tầng, sơ đồ tầng để định vị
 * và chọn phòng, lưới giờ bên dưới (mỗi phòng một dòng, cột là khung 30') để chọn giờ bắt đầu/kết thúc: bấm ô trống = bắt
 * đầu, bấm ô sau (hoặc bấm lại ô vừa chọn) = kết thúc — đúng tương tác bản gốc, không dùng ô nhập giờ thủ công.
 */
@Component({
  selector: 'opac-space-booking',
  imports: [DateInput, FormsModule, LDatePipe, RouterLink, TranslatePipe],
  template: `
    <div class="max-w-6xl mx-auto px-4 py-8">
      <h1 class="text-2xl font-semibold text-slate-900">{{ 'OPAC.SPACE.TITLE' | translate }}</h1>
      <p class="text-slate-600 mt-1">{{ 'OPAC.SPACE.INTRO' | translate }}</p>

      <div class="mt-6 flex flex-wrap gap-3 items-end">
        <select class="border border-slate-300 rounded-lg px-3 py-2 text-sm" [ngModel]="buildingId()" (ngModelChange)="selectBuilding($event)">
          <option [ngValue]="null">{{ 'OPAC.SPACE.SELECT_BUILDING' | translate }}</option>
          @for (b of buildings(); track b.id) { <option [ngValue]="b.id">{{ b.name }}</option> }
        </select>
        <select class="border border-slate-300 rounded-lg px-3 py-2 text-sm" [ngModel]="floorId()" (ngModelChange)="selectFloor($event)" [disabled]="!buildingId()">
          <option [ngValue]="null">{{ 'OPAC.SPACE.SELECT_FLOOR' | translate }}</option>
          @for (f of floorsOfBuilding(); track f.id) { <option [ngValue]="f.id">{{ f.name }}</option> }
        </select>
        <div>
          <label class="text-xs text-slate-500 block">{{ 'OPAC.SPACE.DATE' | translate }}</label>
          <app-date-input [(ngModel)]="date" (ngModelChange)="loadBoard()" [disabled]="!floorId()"></app-date-input>
        </div>
      </div>

      @if (floor(); as f) {
        <div class="mt-4 relative w-full bg-slate-100 rounded-xl border border-slate-200 overflow-hidden"
             [style.aspect-ratio]="f.canvasWidth + ' / ' + f.canvasHeight">
          @if (f.layoutImageUrl) {
            <img [src]="f.layoutImageUrl" [alt]="f.name" class="absolute inset-0 w-full h-full object-contain pointer-events-none" />
          }
          @for (r of rooms(); track r.id) {
            @if (r.positionX !== null) {
              <button type="button" class="absolute rounded-md border-2 flex items-center justify-center text-center text-xs font-medium px-1 overflow-hidden"
                      [class.border-blue-600]="selectedRoomId() === r.id" [class.bg-blue-100/90]="selectedRoomId() === r.id"
                      [class.border-red-400]="r.maintenance && selectedRoomId() !== r.id" [class.bg-red-50/80]="r.maintenance && selectedRoomId() !== r.id"
                      [class.border-slate-400]="!r.maintenance && selectedRoomId() !== r.id" [class.bg-white/85]="!r.maintenance && selectedRoomId() !== r.id"
                      [style.left.%]="r.positionX" [style.top.%]="r.positionY" [style.width.%]="r.width" [style.height.%]="r.height"
                      [disabled]="r.maintenance" (click)="focusRoom(r.id)">
                <span class="truncate">{{ r.name }}</span>
              </button>
            }
          }
        </div>
      }

      @if (board(); as b) {
        <div class="mt-6 bg-white border border-slate-200 rounded-2xl p-4 overflow-x-auto">
          <div class="flex items-center gap-4 text-xs text-slate-600 mb-3 flex-wrap">
            <span class="inline-flex items-center gap-1"><span class="w-3 h-3 rounded border border-dashed border-emerald-400"></span>{{ 'OPAC.SPACE.LEGEND_FREE' | translate }}</span>
            <span class="inline-flex items-center gap-1"><span class="w-3 h-3 rounded bg-blue-600"></span>{{ 'OPAC.SPACE.LEGEND_SELECTED' | translate }}</span>
            <span class="inline-flex items-center gap-1"><span class="w-3 h-3 rounded bg-amber-400"></span>{{ 'OPAC.SPACE.LEGEND_PENDING' | translate }}</span>
            <span class="inline-flex items-center gap-1"><span class="w-3 h-3 rounded bg-rose-400"></span>{{ 'OPAC.SPACE.LEGEND_APPROVED' | translate }}</span>
            <span class="inline-flex items-center gap-1"><span class="w-3 h-3 rounded bg-slate-700"></span>{{ 'OPAC.SPACE.LEGEND_INUSE' | translate }}</span>
            <span class="inline-flex items-center gap-1"><span class="w-3 h-3 rounded bg-gray-200"></span>{{ 'OPAC.SPACE.LEGEND_CLOSED' | translate }}</span>
          </div>

          <table class="border-collapse text-xs">
            <thead>
              <tr>
                <th class="sticky left-0 bg-white text-left pr-3 pb-2 font-medium text-slate-700 min-w-[140px]">{{ 'OPAC.SPACE.ROOM' | translate }}</th>
                @for (c of columns(b); track c) { <th class="px-0.5 pb-2 font-normal text-slate-400 w-9">{{ minToHHmm(c) }}</th> }
                <th class="px-0.5 pb-2 font-normal text-slate-400">{{ b.closeTime.slice(0, 5) }}</th>
              </tr>
            </thead>
            <tbody>
              @for (room of b.rooms; track room.id) {
                <tr [id]="'board-room-' + room.id" [class.bg-blue-50/40]="selectedRoomId() === room.id">
                  <td class="sticky left-0 bg-white pr-3 py-1 font-medium text-slate-800 whitespace-nowrap">
                    {{ room.name }} <span class="text-slate-400">({{ room.capacity }})</span>
                  </td>
                  @for (c of columns(b); track c) {
                    @let cell = cellFor(room, c);
                    <td class="p-0.5">
                      <button type="button" class="w-8 h-7 rounded flex items-center justify-center"
                              [class.border]="cell.status === 'free'" [class.border-dashed]="cell.status === 'free'" [class.border-emerald-400]="cell.status === 'free'"
                              [class.text-emerald-400]="cell.status === 'free'" [class.hover:bg-emerald-50]="cell.status === 'free'"
                              [class.bg-blue-600]="cell.status === 'selected'" [class.text-white]="cell.status === 'selected' || cell.status === 'pending' || cell.status === 'approved' || cell.status === 'inuse'"
                              [class.bg-amber-400]="cell.status === 'pending'" [class.bg-rose-400]="cell.status === 'approved'" [class.bg-slate-700]="cell.status === 'inuse'"
                              [class.bg-gray-100]="cell.status === 'closed'" [class.cursor-not-allowed]="cell.status === 'closed'"
                              [disabled]="cell.status === 'closed' || (cell.status !== 'free' && cell.status !== 'selected')"
                              [title]="cell.title" (click)="clickCell(room, c)">
                        @if (cell.status === 'free') { <span class="material-icons text-[14px]">add</span> }
                      </button>
                    </td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
      }

      @if (selectedRoom(); as r) {
        <div class="mt-6 bg-white border border-slate-200 rounded-2xl p-6">
          <h2 class="font-semibold text-slate-900 text-lg">{{ r.name }}</h2>
          @if (r.rules) { <p class="text-sm text-slate-500 mt-1">{{ r.rules }}</p> }

          @if (!auth.loggedIn()) {
            <div class="mt-4 text-center py-6 border-t border-slate-100">
              <p class="text-slate-600">{{ 'OPAC.SPACE.LOGIN_REQUIRED' | translate }}</p>
              <button type="button" class="mt-3 inline-flex items-center gap-1.5 px-4 py-2 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700"
                      (click)="auth.login(router.url)"><span class="material-icons text-[18px]">login</span>{{ 'OPAC.NAVBAR.LOGIN' | translate }}</button>
            </div>
          } @else if (created(); as done) {
            <div class="mt-4 border-t border-slate-100 pt-4">
              <p class="text-green-700 font-medium">{{ (done.status === SpaceBookingStatus.Approved ? 'OPAC.SPACE.BOOKED_APPROVED' : 'OPAC.SPACE.BOOKED_PENDING') | translate }}</p>
              <p class="text-sm text-slate-600 mt-1">{{ done.startAt | ldate: 'dd/MM/yyyy HH:mm' }} → {{ done.endAt | ldate: 'HH:mm' }}</p>
              <a routerLink="/tai-khoan" class="text-blue-700 text-sm font-medium mt-2 inline-block">{{ 'OPAC.SPACE.VIEW_MY_BOOKINGS' | translate }} →</a>
            </div>
          } @else {
            <div class="mt-4 border-t border-slate-100 pt-4">
              @if (rangeStartMin() === null) {
                <p class="text-sm text-slate-500">{{ 'OPAC.SPACE.PICK_START' | translate }}</p>
              } @else {
                <p class="text-sm text-slate-700">
                  {{ minToHHmm(rangeStartMin()!) }} → {{ rangeEndMin() !== null ? minToHHmm(rangeEndMin()!) : '…' }}
                  @if (rangeEndMin() === null) { <span class="text-slate-400 ml-1">{{ 'OPAC.SPACE.PICK_END' | translate }}</span> }
                </p>
              }

              <div class="mt-3">
                <label class="text-xs text-slate-500">{{ 'OPAC.SPACE.MEMBERS' | translate }}</label>
                <div class="flex gap-2 mt-1">
                  <input class="flex-1 border border-slate-300 rounded-lg px-3 py-2 text-sm" [(ngModel)]="memberCardInput"
                         [placeholder]="'OPAC.SPACE.MEMBER_CARD_PH' | translate" (keydown.enter)="addMember()" />
                  <button type="button" class="px-3 py-2 rounded-lg border border-slate-300 text-sm hover:bg-slate-50" (click)="addMember()">{{ 'COMMON.ADD' | translate }}</button>
                </div>
                @if (memberError()) { <p class="text-red-600 text-sm mt-1">{{ memberError() | translate }}</p> }
                @if (members().length) {
                  <ul class="flex flex-wrap gap-2 mt-2">
                    @for (m of members(); track m.cardNo) {
                      <li class="inline-flex items-center gap-1.5 bg-slate-100 rounded-full px-3 py-1 text-sm">
                        {{ m.name ?? m.cardNo }}
                        <button type="button" class="material-icons text-[16px] text-slate-500 hover:text-red-600" (click)="removeMember(m)">close</button>
                      </li>
                    }
                  </ul>
                }
              </div>

              @if (bookError()) { <p class="text-red-600 text-sm mt-3" role="alert">{{ bookError() }}</p> }
              <button type="button" class="mt-4 inline-flex items-center gap-2 px-5 py-2.5 rounded-lg bg-blue-600 text-white font-medium hover:bg-blue-700 disabled:opacity-50"
                      [disabled]="busy() || rangeStartMin() === null || rangeEndMin() === null" (click)="book(r)">
                <span class="material-icons text-[18px]">event_available</span>{{ r.autoApprove ? ('OPAC.SPACE.BOOK' | translate) : ('OPAC.SPACE.BOOK_FOR_APPROVAL' | translate) }}
              </button>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class SpaceBooking implements OnInit {
  private readonly api = inject(SpaceApi);
  private readonly language = inject(Language);
  protected readonly auth = inject(ReaderAuth);
  protected readonly router = inject(Router);
  protected readonly SpaceBookingStatus = SpaceBookingStatus;

  protected readonly buildings = signal<SpaceBuildingSummary[]>([]);
  protected readonly floors = signal<SpaceFloorSummary[]>([]);
  protected readonly rooms = signal<SpaceRoomSummary[]>([]);
  protected readonly buildingId = signal<number | null>(null);
  protected readonly floorId = signal<number | null>(null);
  protected readonly floor = signal<SpaceFloorSummary | null>(null);
  protected readonly board = signal<SpaceFloorBoard | null>(null);
  protected readonly selectedRoomId = signal<number | null>(null);
  protected readonly rangeStartMin = signal<number | null>(null);
  protected readonly rangeEndMin = signal<number | null>(null);
  protected readonly created = signal<SpaceBookingSummary | null>(null);
  protected readonly busy = signal(false);
  protected readonly bookError = signal<string | null>(null);
  protected readonly memberError = signal<string | null>(null);
  protected readonly members = signal<SpaceMemberSummary[]>([]);

  protected readonly floorsOfBuilding = () => this.floors().filter((f) => f.buildingId === this.buildingId());
  protected readonly selectedRoom = () => this.board()?.rooms.find((r) => r.id === this.selectedRoomId()) ?? null;

  protected date = '';
  protected memberCardInput = '';

  async ngOnInit(): Promise<void> {
    this.date = new Date().toISOString().slice(0, 10);
    this.buildings.set(await this.api.buildings());
    this.floors.set(await this.api.floors());
  }

  protected selectBuilding(id: number | null): void {
    this.buildingId.set(id);
    this.floorId.set(null);
    this.floor.set(null);
    this.rooms.set([]);
    this.board.set(null);
    this.resetSelection();
  }

  protected async selectFloor(id: number | null): Promise<void> {
    this.floorId.set(id);
    this.resetSelection();
    const f = this.floors().find((x) => x.id === id) ?? null;
    this.floor.set(f);
    this.rooms.set(f ? await this.api.rooms(f.id) : []);
    await this.loadBoard();
  }

  protected async loadBoard(): Promise<void> {
    const id = this.floorId();
    if (!id || !this.date) { this.board.set(null); return; }
    this.board.set(await this.api.board(id, this.date, this.auth.loggedIn()).catch(() => null));
  }

  protected focusRoom(roomId: number): void {
    this.selectRoom(roomId);
    document.getElementById('board-room-' + roomId)?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }

  private selectRoom(roomId: number): void {
    if (this.selectedRoomId() !== roomId) this.resetSelection();
    this.selectedRoomId.set(roomId);
  }

  private resetSelection(): void {
    this.selectedRoomId.set(null);
    this.rangeStartMin.set(null);
    this.rangeEndMin.set(null);
    this.created.set(null);
    this.bookError.set(null);
    this.members.set([]);
  }

  protected columns(board: SpaceFloorBoard): number[] {
    const start = toMinutes(board.openTime);
    const end = toMinutes(board.closeTime);
    const out: number[] = [];
    for (let t = start; t < end; t += board.stepMinutes) out.push(t);
    return out;
  }

  protected minToHHmm(min: number): string {
    return `${String(Math.floor(min / 60)).padStart(2, '0')}:${String(min % 60).padStart(2, '0')}`;
  }

  protected cellFor(room: SpaceBoardRoom, colMin: number): Cell {
    const step = this.board()!.stepMinutes;
    if (room.closed || room.maintenance || room.openTime === null || room.closeTime === null
        || colMin < toMinutes(room.openTime) || colMin >= toMinutes(room.closeTime)) {
      return { startMin: colMin, status: 'closed', title: room.maintenance ? (room.maintenanceNote ?? null) : room.closedReason };
    }
    const busySlot = room.busy.find((b) => {
      const bs = localMinutes(b.startAt);
      const be = localMinutes(b.endAt);
      return colMin < be && colMin + step > bs;
    });
    if (busySlot) {
      const status: CellStatus = busySlot.status === SpaceBookingStatus.CheckedIn ? 'inuse' : busySlot.status === SpaceBookingStatus.Approved ? 'approved' : 'pending';
      return { startMin: colMin, status, title: busySlot.mine ? null : busySlot.readerLabel };
    }
    if (this.selectedRoomId() === room.id) {
      const s = this.rangeStartMin();
      const e = this.rangeEndMin();
      if (s !== null && colMin >= s && colMin < (e ?? s + step)) return { startMin: colMin, status: 'selected', title: null };
    }
    return { startMin: colMin, status: 'free', title: null };
  }

  protected clickCell(room: SpaceBoardRoom, colMin: number): void {
    const step = this.board()!.stepMinutes;
    if (this.selectedRoomId() !== room.id) {
      this.selectRoom(room.id);
      this.rangeStartMin.set(colMin);
      return;
    }
    const start = this.rangeStartMin();
    if (start === null || this.rangeEndMin() !== null) {
      this.rangeStartMin.set(colMin);
      this.rangeEndMin.set(null);
      return;
    }
    this.rangeEndMin.set(colMin <= start ? start + step : colMin + step);
  }

  protected async addMember(): Promise<void> {
    const card = this.memberCardInput.trim();
    if (!card) return;
    this.memberError.set(null);
    try {
      const found = await this.api.lookupMember(card);
      if (!found) {
        this.memberError.set('OPAC.SPACE.MEMBER_NOT_FOUND');
        return;
      }
      if (!this.members().some((m) => m.readerPublicId === found.readerPublicId)) this.members.set([...this.members(), found]);
      this.memberCardInput = '';
    } catch {
      this.memberError.set('OPAC.SPACE.MEMBER_NOT_FOUND');
    }
  }

  protected removeMember(m: SpaceMemberSummary): void {
    this.members.set(this.members().filter((x) => x.readerPublicId !== m.readerPublicId));
  }

  protected async book(r: SpaceBoardRoom): Promise<void> {
    const startMin = this.rangeStartMin();
    const endMin = this.rangeEndMin();
    if (startMin === null || endMin === null) return;
    this.busy.set(true);
    this.bookError.set(null);
    try {
      // Giờ nhập là giờ Việt Nam (UI không hỏi múi giờ) — gửi kèm offset +07:00, backend tự quy đổi UTC khi lưu.
      const startAt = `${this.date}T${this.minToHHmm(startMin)}:00+07:00`;
      const endAt = `${this.date}T${this.minToHHmm(endMin)}:00+07:00`;
      const booking = await this.api.book({ spaceId: r.id, startAt, endAt, memberCards: this.members().map((m) => m.cardNo) });
      this.created.set(booking);
      await this.loadBoard();
    } catch (e) {
      this.bookError.set(this.language.apiError(e));
    } finally {
      this.busy.set(false);
    }
  }
}

function toMinutes(t: string): number {
  const [h, m] = t.split(':').map(Number);
  return h * 60 + m;
}

/** Giờ địa phương (VN, UTC+7) của một mốc ISO UTC — đủ dùng vì lượt đặt nằm trong đúng ngày đang xem. */
function localMinutes(iso: string): number {
  const d = new Date(iso);
  return ((d.getUTCHours() + 7) % 24) * 60 + d.getUTCMinutes();
}
