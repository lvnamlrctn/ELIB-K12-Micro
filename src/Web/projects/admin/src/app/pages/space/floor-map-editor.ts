import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, SPACE_CATEGORIES, SPACE_KIND_ROOM, SPACE_KIND_STUDY, SpaceBuilding, SpaceFloor, SpaceRoom, errorMessage } from '../../core/api';
import { Loading, Modal } from '../../shared/ui';
import { ToastrService } from '../../shared/toastr';

type DragMode = 'move' | 'resize';
/** Kích thước mặc định (%) khi đặt một không gian mới hoặc chưa có vị trí lên sơ đồ. */
const DEFAULT_SIZE = 12;

const KIND_OPTIONS = [
  { value: SPACE_KIND_ROOM, label: 'SPACE_CAT.KIND_ROOM' },
  { value: SPACE_KIND_STUDY, label: 'SPACE_CAT.KIND_STUDY' },
];

interface QuickForm { code: string; name: string; kind: number; category: number; capacity: number; autoApprove: boolean; colorHex: string | null; }

/**
 * Sơ đồ tầng trực quan (monolith: /admin/so-do-tang) — đúng luồng bản gốc: bấm "Thêm không gian" rồi bấm vào sơ đồ để đặt vị
 * trí (tạo mới ngay tại đó, mở form nhanh đặt tên/loại), hoặc kéo-thả không gian đã có để di chuyển/đổi kích thước. Quyền
 * SPACE_ROOMS. Vị trí/kích thước lưu theo % (0-100) trên khung ảnh — độc lập độ phân giải ảnh thật, khớp
 * SpaceRoom.PositionX/Y/Width/Height. Cấu hình đầy đủ (nội quy, loại bạn đọc cho phép, giờ...) vẫn ở trang Danh mục không gian
 * — form nhanh ở đây chỉ đủ để tạo/sửa nhanh ngay trên sơ đồ.
 */
@Component({
  selector: 'app-space-floor-map-editor',
  imports: [FormsModule, TranslatePipe, Loading, Modal, RouterLink],
  template: `
    <div class="mb-5 flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3">
      <h4 class="page-title">{{ 'SPACE_FLOOR_MAP_PAGE.TITLE' | translate }}</h4>
    </div>

    <div class="panel mb-4">
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4 items-end">
        <div>
          <label class="field-label">{{ 'SPACE_CAT.BUILDING' | translate }}</label>
          <select class="input" [ngModel]="buildingId()" (ngModelChange)="selectBuilding($event)">
            <option [ngValue]="null">{{ 'COMMON.SELECT' | translate }}</option>
            @for (b of buildings(); track b.id) { <option [ngValue]="b.id">{{ b.name }}</option> }
          </select>
        </div>
        <div>
          <label class="field-label">{{ 'SPACE_CAT.FLOOR' | translate }}</label>
          <select class="input" [ngModel]="floorId()" (ngModelChange)="selectFloor($event)" [disabled]="!buildingId()">
            <option [ngValue]="null">{{ 'COMMON.SELECT' | translate }}</option>
            @for (f of floorsOfBuilding(); track f.id) { <option [ngValue]="f.id">{{ f.name }}</option> }
          </select>
        </div>
        <div class="flex gap-2">
          <label class="btn-secondary !py-2 cursor-pointer" [class.opacity-50]="!floor() || busy()">
            <span class="material-icons text-[18px]">upload</span> {{ 'SPACE_FLOOR_MAP_PAGE.UPLOAD_IMAGE' | translate }}
            <input type="file" class="hidden" accept="image/png,image/jpeg,image/webp" [disabled]="!floor() || busy()" (change)="uploadLayout($event)" />
          </label>
        </div>
        <div>
          <button type="button" class="w-full !py-2" [class.btn-primary]="placing()" [class.btn-secondary]="!placing()"
                  [disabled]="!floor() || busy()" (click)="togglePlacing()">
            <span class="material-icons text-[18px]">add_location_alt</span> {{ 'SPACE_FLOOR_MAP_PAGE.ADD_SPACE' | translate }}
          </button>
        </div>
      </div>
      @if (placing()) {
        <p class="text-sm text-blue-700 mt-3 flex items-center gap-1.5">
          <span class="material-icons text-[16px]">touch_app</span>{{ 'SPACE_FLOOR_MAP_PAGE.ADD_SPACE_HINT' | translate }}
        </p>
      }
    </div>

    @if (floor(); as f) {
      <div class="flex flex-col lg:flex-row gap-4">
        <div class="flex-1 panel p-3 relative">
          @if (busy()) { <app-loading /> }
          <div class="relative w-full bg-gray-100 rounded-lg border border-gray-200 overflow-hidden select-none"
               [class.cursor-crosshair]="placing()"
               [style.aspect-ratio]="f.canvasWidth + ' / ' + f.canvasHeight"
               (pointerdown)="onBackgroundPointerDown($event)">
            @if (f.layoutImageUrl) {
              <img [src]="f.layoutImageUrl" [alt]="f.name" class="absolute inset-0 w-full h-full object-contain pointer-events-none" />
            } @else {
              <div class="absolute inset-0 flex items-center justify-center text-gray-400 text-sm gap-2">
                <span class="material-icons">image</span>{{ 'SPACE_FLOOR_MAP_PAGE.NO_IMAGE' | translate }}
              </div>
            }
            @for (s of placedRooms(); track s.id) {
              <div class="absolute rounded-md border-2 flex items-center justify-center text-center text-xs font-medium cursor-move overflow-hidden"
                   [class.border-blue-600]="selected()?.id === s.id" [class.bg-blue-100/80]="selected()?.id === s.id"
                   [class.border-amber-500]="s.maintenance && selected()?.id !== s.id" [class.bg-amber-50/80]="s.maintenance && selected()?.id !== s.id"
                   [class.border-gray-400]="!s.maintenance && selected()?.id !== s.id" [class.bg-white/80]="!s.maintenance && selected()?.id !== s.id"
                   [style.left.%]="s.positionX" [style.top.%]="s.positionY" [style.width.%]="s.width" [style.height.%]="s.height"
                   [style.background-color]="s.colorHex ? s.colorHex + '33' : null" [style.border-color]="s.colorHex"
                   (pointerdown)="startDrag($event, s, 'move')">
                @if (s.iconName) { <span class="material-icons text-[16px] mr-0.5">{{ s.iconName }}</span> }
                <span class="truncate px-1">{{ s.name }}</span>
                <div class="absolute bottom-0 right-0 w-3 h-3 bg-gray-500/70 cursor-nwse-resize" (pointerdown)="startDrag($event, s, 'resize')"></div>
              </div>
            }
          </div>
        </div>

        <div class="w-full lg:w-72 space-y-3">
          @if (selected(); as s) {
            <div class="panel p-3">
              <div class="font-medium text-gray-800 mb-1">{{ s.name }} <span class="text-gray-400">({{ s.code }})</span></div>
              <div class="text-xs text-gray-500 mb-2">{{ 'SPACE_CAT.CAPACITY' | translate }}: {{ s.capacity }}</div>
              @if (s.maintenance) {
                <div class="text-xs text-amber-700 mb-2">{{ 'SPACE_CAT.MAINTENANCE_ON' | translate }}{{ s.maintenanceNote ? ' — ' + s.maintenanceNote : '' }}</div>
              }
              <div class="flex gap-2">
                <button type="button" class="btn-secondary !py-1 text-xs flex-1" (click)="openEdit(s)">{{ 'COMMON.EDIT' | translate }}</button>
                <button type="button" class="btn-secondary !py-1 text-xs flex-1" (click)="deselect()">{{ 'COMMON.CLOSE' | translate }}</button>
              </div>
              <a [routerLink]="['/space-rooms']" class="text-xs text-blue-600 hover:underline mt-2 inline-block">{{ 'SPACE_FLOOR_MAP_PAGE.FULL_CONFIG' | translate }} →</a>
            </div>
          }
          @if (unplacedRooms().length > 0) {
            <div class="panel p-3">
              <div class="font-medium text-gray-800 mb-2 text-sm">{{ 'SPACE_FLOOR_MAP_PAGE.UNPLACED' | translate }}</div>
              <ul class="space-y-1">
                @for (s of unplacedRooms(); track s.id) {
                  <li>
                    <button type="button" class="btn-secondary !py-1 text-xs w-full text-left truncate" (click)="placeAtCenter(s)">
                      <span class="material-icons text-[16px] align-middle mr-1">add_location</span>{{ s.name }}
                    </button>
                  </li>
                }
              </ul>
            </div>
          }
        </div>
      </div>
    }

    @if (form(); as f) {
      <app-modal [title]="(f.publicId ? 'SPACE_FLOOR_MAP_PAGE.EDIT_SPACE' : 'SPACE_FLOOR_MAP_PAGE.NEW_SPACE') | translate" widthClass="max-w-lg" (closed)="form.set(null)">
        <form id="quick-space-form" (ngSubmit)="submitForm()" class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div><label class="form-label">{{ 'COMMON.CODE' | translate }} <span class="text-red-500">*</span></label><input class="input" [(ngModel)]="f.code" name="code" required /></div>
          <div><label class="form-label">{{ 'COMMON.NAME' | translate }} <span class="text-red-500">*</span></label><input class="input" [(ngModel)]="f.name" name="name" required /></div>
          <div>
            <label class="form-label">{{ 'SPACE_CAT.KIND' | translate }}</label>
            <select class="input" [(ngModel)]="f.kind" name="kind">
              @for (k of kindOptions; track k.value) { <option [ngValue]="k.value">{{ k.label | translate }}</option> }
            </select>
          </div>
          <div>
            <label class="form-label">{{ 'SPACE_CAT.CATEGORY' | translate }}</label>
            <select class="input" [(ngModel)]="f.category" name="category">
              @for (c of categoryOptions; track c.value) { <option [ngValue]="c.value">{{ c.label | translate }}</option> }
            </select>
          </div>
          <div><label class="form-label">{{ 'SPACE_CAT.CAPACITY' | translate }} <span class="text-red-500">*</span></label><input type="number" class="input" [(ngModel)]="f.capacity" name="capacity" min="1" required /></div>
          <div><label class="form-label">{{ 'SPACE_CAT.COLOR' | translate }}</label><input class="input" [(ngModel)]="f.colorHex" name="colorHex" placeholder="#2563eb" /></div>
          <div class="sm:col-span-2 flex items-center gap-2">
            <input type="checkbox" id="auto" [(ngModel)]="f.autoApprove" name="autoApprove" class="rounded border-gray-300" />
            <label for="auto" class="text-sm text-gray-700">{{ 'SPACE_CAT.AUTO_APPROVE' | translate }}</label>
          </div>
        </form>
        <ng-container footer>
          <button type="button" (click)="form.set(null)" class="btn-secondary">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="submit" form="quick-space-form" [disabled]="busy()" class="btn-primary"><span class="material-icons text-[16px]">save</span> {{ 'COMMON.SAVE' | translate }}</button>
        </ng-container>
      </app-modal>
    }
  `,
})
export class SpaceFloorMapEditor implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);

  protected readonly kindOptions = KIND_OPTIONS;
  protected readonly categoryOptions = SPACE_CATEGORIES;

  protected readonly buildings = signal<SpaceBuilding[]>([]);
  protected readonly floors = signal<SpaceFloor[]>([]);
  protected readonly rooms = signal<SpaceRoom[]>([]);
  protected readonly buildingId = signal<number | null>(null);
  protected readonly floorId = signal<number | null>(null);
  protected readonly floor = signal<SpaceFloor | null>(null);
  protected readonly selected = signal<SpaceRoom | null>(null);
  protected readonly busy = signal(false);
  protected readonly placing = signal(false);
  protected readonly form = signal<(QuickForm & { publicId?: string; x?: number; y?: number }) | null>(null);

  protected readonly floorsOfBuilding = () => this.floors().filter((f) => f.buildingId === this.buildingId());
  protected readonly placedRooms = () => this.rooms().filter((r) => r.positionX !== null && r.positionY !== null);
  protected readonly unplacedRooms = () => this.rooms().filter((r) => r.positionX === null || r.positionY === null);

  private drag: { room: SpaceRoom; mode: DragMode; container: HTMLElement; startX: number; startY: number; orig: SpaceRoom } | null = null;

  async ngOnInit(): Promise<void> {
    this.buildings.set(await this.api.crud<SpaceBuilding>('buildings', 'space').searchAll());
    this.floors.set(await this.api.crud<SpaceFloor>('floors', 'space').searchAll());
  }

  protected selectBuilding(id: number | null): void {
    this.buildingId.set(id);
    this.floorId.set(null);
    this.floor.set(null);
    this.rooms.set([]);
  }

  protected async selectFloor(id: number | null): Promise<void> {
    this.floorId.set(id);
    this.selected.set(null);
    this.placing.set(false);
    const f = this.floors().find((x) => x.id === id) ?? null;
    this.floor.set(f);
    if (!f) { this.rooms.set([]); return; }
    this.rooms.set(await this.api.crud<SpaceRoom>('spaces', 'space').searchAll({ floorId: f.id }));
  }

  protected deselect(): void { this.selected.set(null); }

  protected togglePlacing(): void {
    this.placing.set(!this.placing());
    this.selected.set(null);
  }

  protected onBackgroundPointerDown(event: PointerEvent): void {
    if (this.placing()) { this.beginCreate(event); return; }
    this.deselect();
  }

  private beginCreate(event: PointerEvent): void {
    const container = event.currentTarget as HTMLElement;
    const rect = container.getBoundingClientRect();
    const half = DEFAULT_SIZE / 2;
    const x = clamp(((event.clientX - rect.left) / rect.width) * 100 - half, 0, 100 - DEFAULT_SIZE);
    const y = clamp(((event.clientY - rect.top) / rect.height) * 100 - half, 0, 100 - DEFAULT_SIZE);
    this.placing.set(false);
    this.form.set({ code: '', name: '', kind: SPACE_KIND_ROOM, category: 1, capacity: 4, autoApprove: false, colorHex: null, x, y });
  }

  protected openEdit(room: SpaceRoom): void {
    this.form.set({
      publicId: room.publicId, code: room.code, name: room.name, kind: room.kind, category: room.category,
      capacity: room.capacity, autoApprove: room.autoApprove, colorHex: room.colorHex,
    });
  }

  protected async submitForm(): Promise<void> {
    const f = this.form();
    const floor = this.floor();
    if (!f || !floor) return;
    this.busy.set(true);
    try {
      if (f.publicId) {
        const existing = this.rooms().find((r) => r.publicId === f.publicId)!;
        const updated = await this.api.crud<SpaceRoom>('spaces', 'space').update(f.publicId, { ...existing, ...f });
        this.rooms.set(this.rooms().map((r) => (r.publicId === updated.publicId ? updated : r)));
        this.selected.set(updated);
      } else {
        const created = await this.api.crud<SpaceRoom>('spaces', 'space').add({
          floorId: floor.id, code: f.code, name: f.name, kind: f.kind, category: f.category, capacity: f.capacity,
          minAdvanceMinutes: 0, maxAdvanceDays: 7, maxAdvanceHours: null, maxBookingMinutesPerReader: 0, checkInGraceMinutes: 15,
          autoApprove: f.autoApprove, rules: null, allowedReaderTypeIds: [], minGroupSize: null, colorHex: f.colorHex, iconName: null,
        });
        const placed = await this.api.placeSpaceRoom(created.publicId, { positionX: f.x!, positionY: f.y!, width: DEFAULT_SIZE, height: DEFAULT_SIZE });
        this.rooms.set([...this.rooms(), placed]);
        this.selected.set(placed);
      }
      this.form.set(null);
      this.toastr.success('COMMON.SAVE_SUCCESS');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async uploadLayout(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    const f = this.floor();
    if (!file || !f) return;
    this.busy.set(true);
    try {
      const uploaded = await this.api.upload('space-floorplan', file);
      if (!uploaded.url) throw new Error('SPACE_FLOOR_MAP_PAGE.NO_URL');
      const size = await imageSize(uploaded.url);
      const updated = await this.api.setSpaceFloorLayout(f.publicId, uploaded.url, size.width, size.height);
      this.floors.set(this.floors().map((x) => (x.id === f.id ? updated : x)));
      this.floor.set(updated);
      this.toastr.success('SPACE_FLOOR_MAP_PAGE.IMAGE_SAVED');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async placeAtCenter(room: SpaceRoom): Promise<void> {
    const half = DEFAULT_SIZE / 2;
    await this.savePosition(room, 50 - half, 50 - half, DEFAULT_SIZE, DEFAULT_SIZE);
  }

  protected startDrag(event: PointerEvent, room: SpaceRoom, mode: DragMode): void {
    event.stopPropagation();
    event.preventDefault();
    const container = (event.currentTarget as HTMLElement).parentElement as HTMLElement;
    this.selected.set(room);
    this.drag = { room, mode, container, startX: event.clientX, startY: event.clientY, orig: room };
    window.addEventListener('pointermove', this.onPointerMove);
    window.addEventListener('pointerup', this.onPointerUp);
  }

  private readonly onPointerMove = (event: PointerEvent): void => {
    if (!this.drag) return;
    const { room, mode, container, startX, startY, orig } = this.drag;
    const rect = container.getBoundingClientRect();
    const dxPct = ((event.clientX - startX) / rect.width) * 100;
    const dyPct = ((event.clientY - startY) / rect.height) * 100;
    const x = orig.positionX ?? 0;
    const y = orig.positionY ?? 0;
    const w = orig.width ?? DEFAULT_SIZE;
    const h = orig.height ?? DEFAULT_SIZE;
    const next = mode === 'move'
      ? { ...room, positionX: clamp(x + dxPct, 0, 100 - w), positionY: clamp(y + dyPct, 0, 100 - h) }
      : { ...room, width: clamp(w + dxPct, 2, 100 - x), height: clamp(h + dyPct, 2, 100 - y) };
    this.rooms.set(this.rooms().map((r) => (r.id === room.id ? next : r)));
    if (this.selected()?.id === room.id) this.selected.set(next);
  };

  private readonly onPointerUp = (): void => {
    const drag = this.drag;
    this.drag = null;
    window.removeEventListener('pointermove', this.onPointerMove);
    window.removeEventListener('pointerup', this.onPointerUp);
    if (!drag) return;
    const current = this.rooms().find((r) => r.id === drag.room.id);
    if (current) void this.savePosition(current, current.positionX, current.positionY, current.width, current.height);
  };

  private async savePosition(room: SpaceRoom, x: number | null, y: number | null, w: number | null, h: number | null): Promise<void> {
    try {
      const updated = await this.api.placeSpaceRoom(room.publicId, { positionX: x, positionY: y, width: w, height: h });
      this.rooms.set(this.rooms().map((r) => (r.id === room.id ? updated : r)));
      if (this.selected()?.id === room.id) this.selected.set(updated);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }
}

function clamp(v: number, min: number, max: number): number {
  return Math.min(Math.max(v, min), Math.max(min, max));
}

function imageSize(url: string): Promise<{ width: number; height: number }> {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.onload = () => resolve({ width: img.naturalWidth || 1000, height: img.naturalHeight || 700 });
    img.onerror = () => reject(new Error('SPACE_FLOOR_MAP_PAGE.NO_URL'));
    img.src = url;
  });
}
