import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Language, LDatePipe } from '../core/i18n';
import { ReaderAuth } from '../core/reader-auth';
import {
  SpaceApi, SpaceAvailability, SpaceBookingSummary, SpaceBuildingSummary, SpaceFloorSummary, SpaceMemberSummary, SpaceRoomSummary,
} from '../core/space';

/** Sơ đồ tầng trực quan + đặt phòng/không gian (monolith: /dat-phong-hoc-nhom) — chọn toà/tầng/phòng trên sơ đồ, chọn giờ, mời thành viên. */
@Component({
  selector: 'opac-space-booking',
  imports: [FormsModule, LDatePipe, RouterLink, TranslatePipe],
  template: `
    <div class="max-w-5xl mx-auto px-4 py-8">
      <h1 class="text-2xl font-semibold text-slate-900">{{ 'OPAC.SPACE.TITLE' | translate }}</h1>
      <p class="text-slate-600 mt-1">{{ 'OPAC.SPACE.INTRO' | translate }}</p>

      <div class="mt-6 flex flex-wrap gap-3">
        <select class="border border-slate-300 rounded-lg px-3 py-2 text-sm" [ngModel]="buildingId()" (ngModelChange)="selectBuilding($event)">
          <option [ngValue]="null">{{ 'OPAC.SPACE.SELECT_BUILDING' | translate }}</option>
          @for (b of buildings(); track b.id) { <option [ngValue]="b.id">{{ b.name }}</option> }
        </select>
        <select class="border border-slate-300 rounded-lg px-3 py-2 text-sm" [ngModel]="floorId()" (ngModelChange)="selectFloor($event)" [disabled]="!buildingId()">
          <option [ngValue]="null">{{ 'OPAC.SPACE.SELECT_FLOOR' | translate }}</option>
          @for (f of floorsOfBuilding(); track f.id) { <option [ngValue]="f.id">{{ f.name }}</option> }
        </select>
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
                      [class.border-blue-600]="selected()?.id === r.id" [class.bg-blue-100/90]="selected()?.id === r.id"
                      [class.border-red-400]="r.maintenance && selected()?.id !== r.id" [class.bg-red-50/80]="r.maintenance && selected()?.id !== r.id"
                      [class.border-slate-400]="!r.maintenance && selected()?.id !== r.id" [class.bg-white/85]="!r.maintenance && selected()?.id !== r.id"
                      [style.left.%]="r.positionX" [style.top.%]="r.positionY" [style.width.%]="r.width" [style.height.%]="r.height"
                      [disabled]="r.maintenance" (click)="selectRoom(r)">
                <span class="truncate">{{ r.name }}</span>
              </button>
            }
          }
        </div>
      }

      @if (selected(); as r) {
        <div class="mt-6 bg-white border border-slate-200 rounded-2xl p-6">
          <h2 class="font-semibold text-slate-900 text-lg">{{ r.name }}</h2>
          <p class="text-sm text-slate-500">{{ 'OPAC.SPACE.CAPACITY' | translate }}: {{ r.capacity }}</p>

          @if (!auth.loggedIn()) {
            <div class="mt-4 text-center py-6 border-t border-slate-100">
              <p class="text-slate-600">{{ 'OPAC.SPACE.LOGIN_REQUIRED' | translate }}</p>
              <button type="button" class="mt-3 inline-flex items-center gap-1.5 px-4 py-2 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700"
                      (click)="auth.login(router.url)"><span class="material-icons text-[18px]">login</span>{{ 'OPAC.NAVBAR.LOGIN' | translate }}</button>
            </div>
          } @else if (created(); as done) {
            <div class="mt-4 border-t border-slate-100 pt-4">
              <p class="text-green-700 font-medium">{{ 'OPAC.SPACE.BOOKED' | translate }}</p>
              <p class="text-sm text-slate-600 mt-1">{{ done.startAt | ldate: 'dd/MM/yyyy HH:mm' }} → {{ done.endAt | ldate: 'HH:mm' }}</p>
              <a routerLink="/tai-khoan" class="text-blue-700 text-sm font-medium mt-2 inline-block">{{ 'OPAC.SPACE.VIEW_MY_BOOKINGS' | translate }} →</a>
            </div>
          } @else {
            <div class="mt-4 border-t border-slate-100 pt-4 grid grid-cols-1 md:grid-cols-3 gap-3">
              <div>
                <label class="text-xs text-slate-500">{{ 'OPAC.SPACE.DATE' | translate }}</label>
                <input type="date" class="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm" [(ngModel)]="date" (change)="checkAvailability()" />
              </div>
              <div>
                <label class="text-xs text-slate-500">{{ 'OPAC.SPACE.START' | translate }}</label>
                <input type="time" class="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm" [(ngModel)]="startTime" />
              </div>
              <div>
                <label class="text-xs text-slate-500">{{ 'OPAC.SPACE.END' | translate }}</label>
                <input type="time" class="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm" [(ngModel)]="endTime" />
              </div>
            </div>

            @if (availability(); as a) {
              <div class="mt-2 text-sm">
                @if (a.closed) {
                  <p class="text-red-600">{{ 'OPAC.SPACE.CLOSED' | translate }}{{ a.closedReason ? ' — ' + a.closedReason : '' }}</p>
                } @else {
                  <p class="text-slate-600">{{ 'OPAC.SPACE.OPEN_HOURS' | translate }}: {{ a.openTime }} – {{ a.closeTime }}</p>
                  @if (a.busy.length) {
                    <p class="text-amber-700 mt-1">{{ 'OPAC.SPACE.BUSY_SLOTS' | translate }}:
                      @for (b of a.busy; track b.startAt) { <span class="ml-1">{{ b.startAt | ldate: 'HH:mm' }}–{{ b.endAt | ldate: 'HH:mm' }}</span> }
                    </p>
                  }
                }
              </div>
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
                    [disabled]="busy() || !date || !startTime || !endTime" (click)="book(r)">
              <span class="material-icons text-[18px]">event_available</span>{{ 'OPAC.SPACE.BOOK' | translate }}
            </button>
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

  protected readonly buildings = signal<SpaceBuildingSummary[]>([]);
  protected readonly floors = signal<SpaceFloorSummary[]>([]);
  protected readonly rooms = signal<SpaceRoomSummary[]>([]);
  protected readonly buildingId = signal<number | null>(null);
  protected readonly floorId = signal<number | null>(null);
  protected readonly floor = signal<SpaceFloorSummary | null>(null);
  protected readonly selected = signal<SpaceRoomSummary | null>(null);
  protected readonly availability = signal<SpaceAvailability | null>(null);
  protected readonly created = signal<SpaceBookingSummary | null>(null);
  protected readonly busy = signal(false);
  protected readonly bookError = signal<string | null>(null);
  protected readonly memberError = signal<string | null>(null);
  protected readonly members = signal<SpaceMemberSummary[]>([]);

  protected readonly floorsOfBuilding = () => this.floors().filter((f) => f.buildingId === this.buildingId());

  protected date = '';
  protected startTime = '09:00';
  protected endTime = '10:00';
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
    this.selected.set(null);
  }

  protected async selectFloor(id: number | null): Promise<void> {
    this.floorId.set(id);
    this.selected.set(null);
    const f = this.floors().find((x) => x.id === id) ?? null;
    this.floor.set(f);
    this.rooms.set(f ? await this.api.rooms(f.id) : []);
  }

  protected selectRoom(r: SpaceRoomSummary): void {
    this.selected.set(r);
    this.created.set(null);
    this.bookError.set(null);
    this.members.set([]);
    void this.checkAvailability();
  }

  protected async checkAvailability(): Promise<void> {
    const r = this.selected();
    if (!r || !this.date) return;
    this.availability.set(await this.api.availability(r.id, this.date).catch(() => null));
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

  protected async book(r: SpaceRoomSummary): Promise<void> {
    this.busy.set(true);
    this.bookError.set(null);
    try {
      // Giờ nhập là giờ Việt Nam (UI không hỏi múi giờ) — gửi kèm offset +07:00, backend tự quy đổi UTC khi lưu.
      const startAt = `${this.date}T${this.startTime}:00+07:00`;
      const endAt = `${this.date}T${this.endTime}:00+07:00`;
      const booking = await this.api.book({ spaceId: r.id, startAt, endAt, memberCards: this.members().map((m) => m.cardNo) });
      this.created.set(booking);
    } catch (e) {
      this.bookError.set(this.language.apiError(e));
    } finally {
      this.busy.set(false);
    }
  }
}
