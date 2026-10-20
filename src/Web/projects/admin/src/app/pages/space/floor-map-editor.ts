import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, SpaceBuilding, SpaceFloor, SpaceRoom, errorMessage } from '../../core/api';
import { Loading } from '../../shared/ui';
import { ToastrService } from '../../shared/toastr';

type DragMode = 'move' | 'resize';
/** Kích thước mặc định (%) khi đặt một không gian chưa có vị trí lên sơ đồ. */
const DEFAULT_SIZE = 12;

/**
 * Sơ đồ tầng trực quan (monolith: /admin/so-do-tang — kéo-thả vị trí phòng trên ảnh nền), quyền SPACE_ROOMS.
 * Vị trí/kích thước lưu theo % (0-100) trên khung ảnh — độc lập độ phân giải ảnh thật, khớp SpaceRoom.PositionX/Y/Width/Height.
 */
@Component({
  selector: 'app-space-floor-map-editor',
  imports: [FormsModule, TranslatePipe, Loading],
  template: `
    <div class="mb-5 flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3">
      <h4 class="page-title">{{ 'SPACE_FLOOR_MAP_PAGE.TITLE' | translate }}</h4>
    </div>

    <div class="panel mb-4">
      <div class="grid grid-cols-1 md:grid-cols-3 gap-4 items-end">
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
      </div>
    </div>

    @if (floor(); as f) {
      <div class="flex flex-col lg:flex-row gap-4">
        <div class="flex-1 panel p-3 relative">
          @if (busy()) { <app-loading /> }
          <div class="relative w-full bg-gray-100 rounded-lg border border-gray-200 overflow-hidden select-none"
               [style.aspect-ratio]="f.canvasWidth + ' / ' + f.canvasHeight"
               (pointerdown)="deselect()">
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
              <button type="button" class="btn-secondary !py-1 text-xs w-full" (click)="deselect()">{{ 'COMMON.CLOSE' | translate }}</button>
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
  `,
})
export class SpaceFloorMapEditor implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);

  protected readonly buildings = signal<SpaceBuilding[]>([]);
  protected readonly floors = signal<SpaceFloor[]>([]);
  protected readonly rooms = signal<SpaceRoom[]>([]);
  protected readonly buildingId = signal<number | null>(null);
  protected readonly floorId = signal<number | null>(null);
  protected readonly floor = signal<SpaceFloor | null>(null);
  protected readonly selected = signal<SpaceRoom | null>(null);
  protected readonly busy = signal(false);

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
    const f = this.floors().find((x) => x.id === id) ?? null;
    this.floor.set(f);
    if (!f) { this.rooms.set([]); return; }
    this.rooms.set(await this.api.crud<SpaceRoom>('spaces', 'space').searchAll({ floorId: f.id }));
  }

  protected deselect(): void { this.selected.set(null); }

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
