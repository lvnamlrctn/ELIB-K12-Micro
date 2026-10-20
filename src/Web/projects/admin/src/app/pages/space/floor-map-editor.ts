import { Component, HostListener, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import {
  Api, STATUS_ACTIVE, STATUS_INACTIVE, SPACE_CATEGORIES, SPACE_CONDITION_STATUSES, SPACE_FIXTURE_TYPES, SPACE_KIND_ROOM, SPACE_KIND_STUDY, SpaceBuilding, SpaceFixture,
  SpaceFixtureEquipment, SpaceFloor, SpaceFloorUtility, SpaceLockerCabinet, SpaceLockerCompartment, SpaceLockerLoan, SpaceRoom, SpaceStoreOption,
  errorMessage,
} from '../../core/api';
import { Language } from '../../core/i18n';
import { Loading, Modal } from '../../shared/ui';
import { ToastrService } from '../../shared/toastr';

type DragMode = 'move' | 'resize';
type Corner = 'nw' | 'ne' | 'sw' | 'se';
interface Rect { x: number; y: number; w: number; h: number; }
/** Cạnh nhỏ nhất (%) của một vật trên sơ đồ, và độ kéo (%) dưới ngưỡng này coi là bấm chứ không phải vẽ. */
const MIN_SIZE = 3;
const CLICK_TOLERANCE = 1.5;
/** Kích thước mặc định (%) khi đặt một không gian/vật dụng mới hoặc chưa có vị trí lên sơ đồ. */
const DEFAULT_SIZE = 12;

const KIND_OPTIONS = [
  { value: SPACE_KIND_ROOM, label: 'SPACE_CAT.KIND_ROOM' },
  { value: SPACE_KIND_STUDY, label: 'SPACE_CAT.KIND_STUDY' },
];

/** 'ROOM' = không gian đặt được (SpaceRoom); 'LOCKER' = tủ đựng đồ (SpaceLockerCabinet); còn lại là ObjectType của vật dụng (SpaceFixture). */
type PlacingKind = 'ROOM' | 'LOCKER' | string;

interface RoomForm { code: string; name: string; kind: number; category: number; capacity: number; autoApprove: boolean; colorHex: string | null; }
interface FixtureForm {
  code: string; name: string; objectType: string; colorHex: string | null; iconName: string | null;
  shelfCategoryRange: string | null; shelfSubjectName: string | null; shelfCapacity: number | null; shelfDescription: string | null;
  storePublicId: string | null;
}
interface LockerForm { code: string; name: string; rows: number; cols: number; colorHex: string | null; iconName: string | null; note: string | null; }

type DragTarget = { kind: 'room'; item: SpaceRoom } | { kind: 'fixture'; item: SpaceFixture } | { kind: 'locker'; item: SpaceLockerCabinet };

/**
 * Sơ đồ tầng trực quan (monolith: /admin/so-do-tang) — đúng luồng bản gốc: bấm 1 loại trong bảng "Thêm" rồi bấm vào sơ đồ để
 * đặt vị trí (tạo mới ngay tại đó, mở form nhanh đặt tên), hoặc kéo-thả vật đã có để di chuyển/đổi kích thước. Gồm cả không
 * gian đặt được (phòng — quyền SPACE_ROOMS) và vật dụng không đặt được (giá sách/máy tính/quầy/...— quyền SPACE_FIXTURES,
 * monolith map.MapObject các ObjectType ngoài ROOM/STUDY_SPACE). Vị trí/kích thước lưu theo % (0-100) trên khung ảnh — độc
 * lập độ phân giải ảnh thật. Cấu hình đầy đủ của phòng (nội quy, loại bạn đọc cho phép, giờ...) vẫn ở trang Danh mục không
 * gian — form nhanh ở đây chỉ đủ để tạo/sửa nhanh ngay trên sơ đồ.
 */
@Component({
  selector: 'app-space-floor-map-editor',
  imports: [FormsModule, TranslatePipe, Loading, Modal, RouterLink],
  template: `
    <div class="mb-5 flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3">
      <h4 class="page-title">{{ 'SPACE_FLOOR_MAP_PAGE.TITLE' | translate }}</h4>
      <button type="button" class="btn-secondary" [disabled]="!floor()" (click)="openUtilities()">
        <span class="material-icons text-[18px]">inventory_2</span> {{ 'SPACE_FLOOR_MAP_PAGE.FLOOR_UTILITIES' | translate }}
      </button>
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

      <div class="mt-4">
        <label class="field-label mb-1.5 block">{{ 'SPACE_FLOOR_MAP_PAGE.ADD_PALETTE' | translate }}</label>
        <div class="flex flex-wrap gap-1.5">
          <button type="button" class="btn-secondary !py-1.5 !px-2.5 text-xs" [class.btn-primary]="placingKind() === 'ROOM'"
                  [disabled]="!floor() || busy()" (click)="togglePlacing('ROOM')">
            <span class="material-icons text-[16px] align-middle mr-1">meeting_room</span>{{ 'SPACE_FLOOR_MAP_PAGE.TYPE_ROOM' | translate }}
          </button>
          <button type="button" class="btn-secondary !py-1.5 !px-2.5 text-xs" [class.btn-primary]="placingKind() === 'LOCKER'"
                  [disabled]="!floor() || busy()" (click)="togglePlacing('LOCKER')">
            <span class="material-icons text-[16px] align-middle mr-1">lock</span>{{ 'SPACE_FLOOR_MAP_PAGE.TYPE_LOCKER' | translate }}
          </button>
          @for (t of fixtureTypes; track t.value) {
            <button type="button" class="btn-secondary !py-1.5 !px-2.5 text-xs" [class.btn-primary]="placingKind() === t.value"
                    [disabled]="!floor() || busy()" (click)="togglePlacing(t.value)">
              <span class="material-icons text-[16px] align-middle mr-1">{{ t.icon }}</span>{{ t.label | translate }}
            </button>
          }
        </div>
        @if (placingKind()) {
          <p class="text-sm text-blue-700 mt-2 flex items-center gap-1.5">
            <span class="material-icons text-[16px]">touch_app</span>{{ 'SPACE_FLOOR_MAP_PAGE.ADD_SPACE_HINT' | translate }}
          </p>
        }
      </div>
    </div>

    @if (floor(); as f) {
      <div class="flex flex-col lg:flex-row gap-4">
        <div class="flex-1 panel p-3 relative">
          @if (busy()) { <app-loading /> }
          <div data-canvas class="relative w-full bg-gray-100 rounded-lg border border-gray-200 overflow-hidden select-none touch-none"
               [class.cursor-crosshair]="placingKind()"
               [style.aspect-ratio]="f.canvasWidth + ' / ' + f.canvasHeight"
               (pointerdown)="onBackgroundPointerDown($event)">
            @if (f.layoutImageUrl) {
              <img [src]="f.layoutImageUrl" [alt]="f.name" class="absolute inset-0 w-full h-full object-contain pointer-events-none" />
            } @else {
              <!-- Chưa upload ảnh — lưới ô caro mặc định đúng bản gốc ELIB-LRC (map-visual.html), không phải file ảnh thật. -->
              <div class="absolute inset-0 pointer-events-none"
                   style="background-image: linear-gradient(#cbd5e1 1px, transparent 1px), linear-gradient(90deg, #cbd5e1 1px, transparent 1px); background-size: 30px 30px;"></div>
            }
            @for (s of placedRooms(); track s.id) {
              <div class="absolute rounded-md border-2 flex items-center justify-center text-center text-xs font-medium cursor-move overflow-hidden touch-none"
                   [class.border-blue-600]="isSelected('room', s.id)" [class.bg-blue-100/80]="isSelected('room', s.id)"
                   [class.border-amber-500]="s.maintenance && !isSelected('room', s.id)" [class.bg-amber-50/80]="s.maintenance && !isSelected('room', s.id)"
                   [class.border-gray-400]="!s.maintenance && !isSelected('room', s.id)" [class.bg-white/80]="!s.maintenance && !isSelected('room', s.id)"
                   [style.left.%]="s.positionX" [style.top.%]="s.positionY" [style.width.%]="s.width" [style.height.%]="s.height"
                   [style.background-color]="s.colorHex ? s.colorHex + '33' : null" [style.border-color]="s.colorHex"
                   (pointerdown)="startDrag($event, 'room', s, 'move')">
                @if (s.iconName) { <span class="material-icons text-[16px] mr-0.5">{{ s.iconName }}</span> }
                <span class="truncate px-1">{{ s.name }}</span>
                @if (isSelected('room', s.id)) {
                  @for (k of corners; track k) {
                    <div class="absolute w-4 h-4 rounded-sm bg-blue-600 border-2 border-white shadow" [class]="cornerClass(k)" (pointerdown)="startDrag($event, 'room', s, 'resize', k)"></div>
                  }
                }
              </div>
            }
            @for (x of placedFixtures(); track x.id) {
              <div class="absolute rounded-md border-2 border-dashed flex items-center justify-center text-center text-xs font-medium cursor-move overflow-hidden touch-none"
                   [class.border-blue-600]="isSelected('fixture', x.id)" [class.bg-blue-100/80]="isSelected('fixture', x.id)"
                   [class.border-gray-400]="!isSelected('fixture', x.id)" [class.bg-white/80]="!isSelected('fixture', x.id)"
                   [style.left.%]="x.positionX" [style.top.%]="x.positionY" [style.width.%]="x.width" [style.height.%]="x.height"
                   [style.background-color]="x.colorHex ? x.colorHex + '33' : null" [style.border-color]="x.colorHex"
                   (pointerdown)="startDrag($event, 'fixture', x, 'move')">
                <span class="material-icons text-[16px] mr-0.5">{{ x.iconName || fixtureIcon(x.objectType) }}</span>
                <span class="truncate px-1">{{ x.name }}</span>
                @if (isSelected('fixture', x.id)) {
                  @for (k of corners; track k) {
                    <div class="absolute w-4 h-4 rounded-sm bg-blue-600 border-2 border-white shadow" [class]="cornerClass(k)" (pointerdown)="startDrag($event, 'fixture', x, 'resize', k)"></div>
                  }
                }
              </div>
            }
            @for (c of placedLockers(); track c.id) {
              <div class="absolute rounded-md border-2 border-dotted flex items-center justify-center text-center text-xs font-medium cursor-move overflow-hidden touch-none"
                   [class.border-blue-600]="isSelected('locker', c.id)" [class.bg-blue-100/80]="isSelected('locker', c.id)"
                   [class.border-indigo-400]="!isSelected('locker', c.id)" [class.bg-white/80]="!isSelected('locker', c.id)"
                   [style.left.%]="c.positionX" [style.top.%]="c.positionY" [style.width.%]="c.width" [style.height.%]="c.height"
                   [style.background-color]="c.colorHex ? c.colorHex + '33' : null" [style.border-color]="c.colorHex"
                   (pointerdown)="startDrag($event, 'locker', c, 'move')">
                <span class="material-icons text-[16px] mr-0.5">{{ c.iconName || 'lock' }}</span>
                <span class="truncate px-1">{{ c.name }}</span>
                @if (isSelected('locker', c.id)) {
                  @for (k of corners; track k) {
                    <div class="absolute w-4 h-4 rounded-sm bg-blue-600 border-2 border-white shadow" [class]="cornerClass(k)" (pointerdown)="startDrag($event, 'locker', c, 'resize', k)"></div>
                  }
                }
              </div>
            }
            @if (draft(); as d) {
              <div class="absolute rounded-md border-2 border-dashed border-blue-600 bg-blue-200/40 pointer-events-none"
                   [style.left.%]="d.x" [style.top.%]="d.y" [style.width.%]="d.w" [style.height.%]="d.h"></div>
            }
          </div>
        </div>

        <div class="w-full lg:w-72 space-y-3">
          @if (selected(); as sel) {
            <div class="panel p-3">
              @if (sel.kind === 'room') {
                <div class="font-medium text-gray-800 mb-1">{{ sel.item.name }} <span class="text-gray-400">({{ sel.item.code }})</span></div>
                <div class="text-xs text-gray-500 mb-2">{{ 'SPACE_CAT.CAPACITY' | translate }}: {{ sel.item.capacity }}</div>
                @if (sel.item.maintenance) {
                  <div class="text-xs text-amber-700 mb-2">{{ 'SPACE_CAT.MAINTENANCE_ON' | translate }}{{ sel.item.maintenanceNote ? ' — ' + sel.item.maintenanceNote : '' }}</div>
                }
              } @else if (sel.kind === 'fixture') {
                <div class="font-medium text-gray-800 mb-1">{{ sel.item.name }} <span class="text-gray-400">({{ sel.item.code }})</span></div>
                <div class="text-xs text-gray-500 mb-2">{{ fixtureLabel(sel.item.objectType) | translate }}</div>
                @if (sel.item.objectType === 'SHELF') {
                  @if (sel.item.shelfCategoryRange) { <div class="text-xs text-gray-500">{{ 'SPACE_FLOOR_MAP_PAGE.SHELF_RANGE' | translate }}: {{ sel.item.shelfCategoryRange }}</div> }
                  @if (sel.item.shelfSubjectName) { <div class="text-xs text-gray-500">{{ 'SPACE_FLOOR_MAP_PAGE.SHELF_SUBJECT' | translate }}: {{ sel.item.shelfSubjectName }}</div> }
                  @if (sel.item.shelfCapacity) { <div class="text-xs text-gray-500">{{ 'SPACE_FLOOR_MAP_PAGE.SHELF_CAPACITY' | translate }}: {{ sel.item.shelfCapacity }}</div> }
                  @if (sel.item.storeName) { <div class="text-xs text-gray-500 mb-2">{{ 'SPACE_FLOOR_MAP_PAGE.SHELF_STORE' | translate }}: {{ sel.item.storeName }}</div> }
                }
              } @else {
                <div class="font-medium text-gray-800 mb-1">{{ sel.item.name }} <span class="text-gray-400">({{ sel.item.code }})</span></div>
                <div class="text-xs text-gray-500 mb-2">{{ 'SPACE_FLOOR_MAP_PAGE.LOCKER_GRID' | translate: { rows: sel.item.rows, cols: sel.item.cols } }}</div>
              }
              <div class="flex gap-2 mt-2">
                <button type="button" class="btn-secondary !py-1 text-xs flex-1" (click)="openEdit(sel)">{{ 'COMMON.EDIT' | translate }}</button>
                <button type="button" class="btn-secondary !py-1 text-xs flex-1" (click)="deselect()">{{ 'COMMON.CLOSE' | translate }}</button>
              </div>
              @if (sel.kind === 'room') {
                <a [routerLink]="['/space-rooms']" class="text-xs text-blue-600 hover:underline mt-2 inline-block">{{ 'SPACE_FLOOR_MAP_PAGE.FULL_CONFIG' | translate }} →</a>
              }
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

    @if (roomForm(); as f) {
      <app-modal [title]="(f.publicId ? 'SPACE_FLOOR_MAP_PAGE.EDIT_SPACE' : 'SPACE_FLOOR_MAP_PAGE.NEW_SPACE') | translate" widthClass="max-w-lg" (closed)="roomForm.set(null)">
        <form id="quick-space-form" (ngSubmit)="submitRoomForm()" class="grid grid-cols-1 sm:grid-cols-2 gap-4">
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
          <button type="button" (click)="roomForm.set(null)" class="btn-secondary">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="submit" form="quick-space-form" [disabled]="busy()" class="btn-primary"><span class="material-icons text-[16px]">save</span> {{ 'COMMON.SAVE' | translate }}</button>
        </ng-container>
      </app-modal>
    }

    @if (fixtureForm(); as f) {
      <app-modal [title]="(f.publicId ? 'SPACE_FLOOR_MAP_PAGE.EDIT_FIXTURE' : 'SPACE_FLOOR_MAP_PAGE.NEW_FIXTURE') | translate" widthClass="max-w-lg" (closed)="fixtureForm.set(null)">
        <form id="quick-fixture-form" (ngSubmit)="submitFixtureForm()" class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div><label class="form-label">{{ 'COMMON.CODE' | translate }} <span class="text-red-500">*</span></label><input class="input" [(ngModel)]="f.code" name="code" required /></div>
          <div><label class="form-label">{{ 'COMMON.NAME' | translate }} <span class="text-red-500">*</span></label><input class="input" [(ngModel)]="f.name" name="name" required /></div>
          <div>
            <label class="form-label">{{ 'SPACE_FLOOR_MAP_PAGE.OBJECT_TYPE' | translate }}</label>
            <select class="input" [(ngModel)]="f.objectType" name="objectType">
              @for (t of fixtureTypes; track t.value) { <option [ngValue]="t.value">{{ t.label | translate }}</option> }
            </select>
          </div>
          <div><label class="form-label">{{ 'SPACE_CAT.COLOR' | translate }}</label><input class="input" [(ngModel)]="f.colorHex" name="colorHex" placeholder="#2563eb" /></div>
          @if (f.objectType === 'SHELF') {
            <div class="sm:col-span-2 rounded-lg border border-amber-200 bg-amber-50 p-3 grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div class="sm:col-span-2 text-xs font-medium text-amber-800 flex items-center gap-1">
                <span class="material-icons text-[16px]">menu_book</span>{{ 'SPACE_FLOOR_MAP_PAGE.SHELF_DETAIL' | translate }}
              </div>
              <div><label class="form-label">{{ 'SPACE_FLOOR_MAP_PAGE.SHELF_RANGE' | translate }}</label><input class="input" [(ngModel)]="f.shelfCategoryRange" name="shelfCategoryRange" placeholder="000 - 099" /></div>
              <div><label class="form-label">{{ 'SPACE_FLOOR_MAP_PAGE.SHELF_SUBJECT' | translate }}</label><input class="input" [(ngModel)]="f.shelfSubjectName" name="shelfSubjectName" /></div>
              <div><label class="form-label">{{ 'SPACE_FLOOR_MAP_PAGE.SHELF_CAPACITY' | translate }}</label><input type="number" class="input" [(ngModel)]="f.shelfCapacity" name="shelfCapacity" min="0" /></div>
              <div>
                <label class="form-label">{{ 'SPACE_FLOOR_MAP_PAGE.SHELF_STORE' | translate }}</label>
                <select class="input" [(ngModel)]="f.storePublicId" name="storePublicId">
                  <option [ngValue]="null">{{ 'COMMON.SELECT' | translate }}</option>
                  @for (s of storeOptions(); track s.storePublicId) { <option [ngValue]="s.storePublicId">{{ s.code }} — {{ s.name }}</option> }
                </select>
              </div>
              <div class="sm:col-span-2"><label class="form-label">{{ 'SPACE_FLOOR_MAP_PAGE.SHELF_DESCRIPTION' | translate }}</label><textarea class="input" [(ngModel)]="f.shelfDescription" name="shelfDescription" rows="2"></textarea></div>
            </div>
          }
        </form>

        @if (f.publicId) {
          <div class="mt-4 rounded-lg border border-violet-200 bg-violet-50 p-3">
            <div class="text-xs font-medium text-violet-800 flex items-center gap-1 mb-2">
              <span class="material-icons text-[16px]">inventory_2</span>{{ 'SPACE_FLOOR_MAP_PAGE.EQUIPMENT_TITLE' | translate: { count: equipment().length } }}
            </div>
            @if (equipment().length) {
              <ul class="space-y-1 mb-2">
                @for (eq of equipment(); track eq.id) {
                  <li class="flex items-center justify-between gap-2 bg-white rounded px-2 py-1 text-xs">
                    <span>{{ eq.name }} @if (eq.quantity) { <span class="text-gray-400">×{{ eq.quantity }}</span> } — {{ conditionLabel(eq.conditionStatus) | translate }}</span>
                    <button type="button" class="material-icons text-[16px] text-gray-400 hover:text-red-600" (click)="deleteEquipment(eq)">delete</button>
                  </li>
                }
              </ul>
            }
            <div class="grid grid-cols-4 gap-1.5">
              <input class="input !py-1 text-xs col-span-2" [(ngModel)]="equipmentName" name="eqName" [placeholder]="'COMMON.NAME' | translate" />
              <input type="number" class="input !py-1 text-xs" [(ngModel)]="equipmentQty" name="eqQty" min="0" [placeholder]="'SPACE_FLOOR_MAP_PAGE.QUANTITY' | translate" />
              <select class="input !py-1 text-xs" [(ngModel)]="equipmentStatus" name="eqStatus">
                @for (c of conditionOptions; track c.value) { <option [ngValue]="c.value">{{ c.label | translate }}</option> }
              </select>
            </div>
            <button type="button" class="btn-secondary !py-1 text-xs w-full mt-1.5" (click)="addEquipment(f.publicId)">
              <span class="material-icons text-[14px] align-middle mr-1">add</span>{{ 'COMMON.ADD' | translate }}
            </button>
          </div>
        }

        <ng-container footer>
          <button type="button" (click)="fixtureForm.set(null)" class="btn-secondary">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="submit" form="quick-fixture-form" [disabled]="busy()" class="btn-primary"><span class="material-icons text-[16px]">save</span> {{ 'COMMON.SAVE' | translate }}</button>
        </ng-container>
      </app-modal>
    }

    @if (lockerForm(); as f) {
      <app-modal [title]="(f.publicId ? 'SPACE_FLOOR_MAP_PAGE.EDIT_LOCKER' : 'SPACE_FLOOR_MAP_PAGE.NEW_LOCKER') | translate" widthClass="max-w-lg" (closed)="lockerForm.set(null)">
        <form id="quick-locker-form" (ngSubmit)="submitLockerForm()" class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div><label class="form-label">{{ 'COMMON.CODE' | translate }} <span class="text-red-500">*</span></label><input class="input" [(ngModel)]="f.code" name="code" required /></div>
          <div><label class="form-label">{{ 'COMMON.NAME' | translate }} <span class="text-red-500">*</span></label><input class="input" [(ngModel)]="f.name" name="name" required /></div>
          <div><label class="form-label">{{ 'SPACE_FLOOR_MAP_PAGE.LOCKER_ROWS' | translate }} <span class="text-red-500">*</span></label><input type="number" class="input" [(ngModel)]="f.rows" name="rows" min="1" max="20" required /></div>
          <div><label class="form-label">{{ 'SPACE_FLOOR_MAP_PAGE.LOCKER_COLS' | translate }} <span class="text-red-500">*</span></label><input type="number" class="input" [(ngModel)]="f.cols" name="cols" min="1" max="20" required /></div>
          <div><label class="form-label">{{ 'SPACE_CAT.COLOR' | translate }}</label><input class="input" [(ngModel)]="f.colorHex" name="colorHex" placeholder="#6366f1" /></div>
          <div><label class="form-label">{{ 'SPACE_CAT.ICON' | translate }}</label><input class="input" [(ngModel)]="f.iconName" name="iconName" placeholder="lock" /></div>
          <div class="sm:col-span-2"><label class="form-label">{{ 'COMMON.NOTE' | translate }}</label><textarea class="input" [(ngModel)]="f.note" name="note" rows="2"></textarea></div>
        </form>

        @if (f.publicId) {
          <div class="mt-4 rounded-lg border border-indigo-200 bg-indigo-50 p-3">
            <div class="text-xs font-medium text-indigo-800 flex items-center gap-1 mb-1">
              <span class="material-icons text-[16px]">grid_view</span>{{ 'SPACE_FLOOR_MAP_PAGE.LOCKER_COMPARTMENTS' | translate }}
              <span class="text-indigo-500 font-normal">· {{ lockerGrid().rows }} × {{ lockerGrid().cols }}</span>
            </div>
            <p class="text-[11px] text-indigo-700/80 mb-2">{{ 'SPACE_FLOOR_MAP_PAGE.COMPARTMENT_HINT' | translate }}</p>
            <input class="input !py-1 text-xs mb-2" [(ngModel)]="lockerScanCardNo" name="scanCardNo" [placeholder]="'SPACE_FLOOR_MAP_PAGE.LOCKER_SCAN_CARD' | translate" />
            <div class="grid gap-1.5" [style.grid-template-columns]="'repeat(' + lockerGrid().cols + ', minmax(0,1fr))'">
              @for (cell of lockerCells(); track cell.row + '-' + cell.col) {
                @if (cell.item; as c) {
                  <div class="relative group">
                    @if (c.status === inactiveStatus) {
                      <div class="rounded px-1.5 py-1 text-[11px] text-left bg-gray-100 border border-gray-300 text-gray-400 min-h-[2.75rem]">
                        <div class="font-medium truncate pr-9">{{ c.code }}</div>
                        <div class="truncate">{{ 'SPACE_FLOOR_MAP_PAGE.COMPARTMENT_INACTIVE' | translate }}</div>
                      </div>
                    } @else {
                      @if (lockerOpenLoanOf(c.id); as loan) {
                      <button type="button" class="block w-full rounded px-1.5 py-1 text-[11px] text-left bg-red-50 border border-red-300 text-red-700 hover:bg-red-100 min-h-[2.75rem]"
                              (click)="returnFromCompartment(c)">
                        <div class="font-medium truncate pr-9">{{ c.code }}</div>
                        <div class="truncate">{{ 'SPACE_FLOOR_MAP_PAGE.LOCKER_OCCUPIED' | translate }} — {{ loan.cardNo }}</div>
                      </button>
                    } @else {
                      <button type="button" class="block w-full rounded px-1.5 py-1 text-[11px] text-left bg-white border border-gray-300 text-gray-600 hover:bg-gray-50 min-h-[2.75rem]"
                              [disabled]="!lockerScanCardNo.trim()" (click)="borrowToCompartment(c)">
                        <div class="font-medium truncate pr-9">{{ c.code }}</div>
                        <div class="truncate text-gray-400">{{ c.name || ('SPACE_FLOOR_MAP_PAGE.LOCKER_FREE' | translate) }}</div>
                      </button>
                    }
                    }
                    <div class="absolute top-0.5 right-0.5 flex">
                      <button type="button" class="material-icons text-[14px] text-gray-400 hover:text-blue-600" [title]="'COMMON.EDIT' | translate" (click)="editCompartment(c)">edit</button>
                      <button type="button" class="material-icons text-[14px] text-gray-400 hover:text-red-600" [title]="'COMMON.DELETE' | translate" (click)="deleteCompartment(c)">delete</button>
                    </div>
                  </div>
                } @else {
                  <button type="button" class="rounded px-1.5 py-1 text-[11px] border border-dashed border-indigo-300 text-indigo-500 hover:bg-white min-h-[2.75rem] flex flex-col items-center justify-center"
                          [title]="'SPACE_FLOOR_MAP_PAGE.COMPARTMENT_ADD' | translate" (click)="addCompartment(cell.row, cell.col)">
                    <span class="material-icons text-[16px]">add</span>
                    <span class="text-[10px]">{{ cell.row }}.{{ cell.col }}</span>
                  </button>
                }
              }
            </div>

            @if (compartmentForm(); as cf) {
              <div class="mt-3 rounded-lg border border-indigo-300 bg-white p-3 grid grid-cols-2 gap-2">
                <div class="col-span-2 text-xs font-medium text-indigo-800">
                  {{ (cf.publicId ? 'SPACE_FLOOR_MAP_PAGE.COMPARTMENT_EDIT' : 'SPACE_FLOOR_MAP_PAGE.COMPARTMENT_NEW') | translate: { row: cf.row, col: cf.col } }}
                </div>
                <div><label class="form-label">{{ 'COMMON.CODE' | translate }} <span class="text-red-500">*</span></label>
                  <input class="input !py-1 text-xs font-mono uppercase" maxlength="20" [(ngModel)]="cf.code" name="cpCode" /></div>
                <div><label class="form-label">{{ 'COMMON.NAME' | translate }}</label>
                  <input class="input !py-1 text-xs" maxlength="250" [(ngModel)]="cf.name" name="cpName" /></div>
                <div class="col-span-2"><label class="form-label">{{ 'COMMON.NOTE' | translate }}</label>
                  <input class="input !py-1 text-xs" maxlength="500" [(ngModel)]="cf.note" name="cpNote" /></div>
                <label class="col-span-2 flex items-center gap-2 text-xs text-gray-700">
                  <input type="checkbox" class="rounded border-gray-300" [(ngModel)]="cf.active" name="cpActive" /> {{ 'SPACE_FLOOR_MAP_PAGE.COMPARTMENT_ACTIVE' | translate }}
                </label>
                <div class="col-span-2 flex justify-end gap-2">
                  <button type="button" class="btn-secondary !py-1 !px-3 text-xs" (click)="compartmentForm.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
                  <button type="button" class="btn-primary !py-1 !px-3 text-xs" [disabled]="busy()" (click)="saveCompartment()">{{ 'COMMON.SAVE' | translate }}</button>
                </div>
              </div>
            }
          </div>
        }

        <ng-container footer>
          <button type="button" (click)="lockerForm.set(null)" class="btn-secondary">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="submit" form="quick-locker-form" [disabled]="busy()" class="btn-primary"><span class="material-icons text-[16px]">save</span> {{ 'COMMON.SAVE' | translate }}</button>
        </ng-container>
      </app-modal>
    }

    @if (utilityModalOpen()) {
      <app-modal [title]="'SPACE_FLOOR_MAP_PAGE.FLOOR_UTILITIES' | translate" widthClass="max-w-lg" (closed)="utilityModalOpen.set(false)">
        @if (utilities().length) {
          <ul class="space-y-1 mb-3">
            @for (u of utilities(); track u.id) {
              <li class="flex items-center justify-between gap-2 bg-gray-50 rounded px-2 py-1.5 text-sm">
                <span class="flex items-center gap-1.5">
                  <span class="material-icons text-[16px] text-gray-500">{{ u.iconName || 'inventory_2' }}</span>
                  {{ u.name }} @if (u.quantity) { <span class="text-gray-400 text-xs">×{{ u.quantity }}</span> }
                  <span class="text-xs text-gray-400">— {{ conditionLabel(u.conditionStatus) | translate }}</span>
                </span>
                <button type="button" class="material-icons text-[16px] text-gray-400 hover:text-red-600" (click)="deleteUtility(u)">delete</button>
              </li>
            }
          </ul>
        } @else {
          <p class="text-sm text-gray-500 mb-3">{{ 'COMMON.NO_DATA' | translate }}</p>
        }
        <div class="grid grid-cols-2 gap-2">
          <input class="input" [(ngModel)]="utilityName" name="utName" [placeholder]="'COMMON.NAME' | translate" />
          <input class="input" [(ngModel)]="utilityIcon" name="utIcon" [placeholder]="'SPACE_CAT.ICON' | translate" />
          <input type="number" class="input" [(ngModel)]="utilityQty" name="utQty" min="0" [placeholder]="'SPACE_FLOOR_MAP_PAGE.QUANTITY' | translate" />
          <select class="input" [(ngModel)]="utilityStatus" name="utStatus">
            @for (c of conditionOptions; track c.value) { <option [ngValue]="c.value">{{ c.label | translate }}</option> }
          </select>
          <textarea class="input col-span-2" [(ngModel)]="utilityDesc" name="utDesc" rows="2" [placeholder]="'SPACE_FLOOR_MAP_PAGE.SHELF_DESCRIPTION' | translate"></textarea>
        </div>
        <button type="button" class="btn-secondary w-full mt-2" (click)="addUtility()">
          <span class="material-icons text-[16px] align-middle mr-1">add</span>{{ 'COMMON.ADD' | translate }}
        </button>
        <ng-container footer>
          <button type="button" (click)="utilityModalOpen.set(false)" class="btn-secondary">{{ 'COMMON.CLOSE' | translate }}</button>
        </ng-container>
      </app-modal>
    }
  `,
})
export class SpaceFloorMapEditor implements OnInit, OnDestroy {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  private readonly language = inject(Language);

  protected readonly kindOptions = KIND_OPTIONS;
  protected readonly categoryOptions = SPACE_CATEGORIES;
  protected readonly fixtureTypes = SPACE_FIXTURE_TYPES;

  protected readonly buildings = signal<SpaceBuilding[]>([]);
  protected readonly floors = signal<SpaceFloor[]>([]);
  protected readonly rooms = signal<SpaceRoom[]>([]);
  protected readonly fixtures = signal<SpaceFixture[]>([]);
  protected readonly lockers = signal<SpaceLockerCabinet[]>([]);
  protected readonly buildingId = signal<number | null>(null);
  protected readonly floorId = signal<number | null>(null);
  protected readonly floor = signal<SpaceFloor | null>(null);
  protected readonly selected = signal<DragTarget | null>(null);
  protected readonly busy = signal(false);
  protected readonly placingKind = signal<PlacingKind | null>(null);
  protected readonly roomForm = signal<(RoomForm & { publicId?: string; x?: number; y?: number; w?: number; h?: number }) | null>(null);
  protected readonly fixtureForm = signal<(FixtureForm & { publicId?: string; x?: number; y?: number; w?: number; h?: number }) | null>(null);
  protected readonly lockerForm = signal<(LockerForm & { publicId?: string; x?: number; y?: number; w?: number; h?: number }) | null>(null);
  protected readonly conditionOptions = SPACE_CONDITION_STATUSES;
  protected readonly corners: Corner[] = ['nw', 'ne', 'sw', 'se'];
  protected readonly inactiveStatus = STATUS_INACTIVE;
  /** Khung đang vẽ (%) khi đặt vật mới — hiện nét đứt cho tới khi thả chuột. */
  protected readonly draft = signal<Rect | null>(null);

  protected readonly lockerCompartments = signal<SpaceLockerCompartment[]>([]);
  /** Lưới đã lưu của tủ đang mở (số dòng × cột) — ô nào trong lưới mà chưa có ngăn thì hiện nút "+". */
  protected readonly lockerGrid = signal<{ rows: number; cols: number; code: string }>({ rows: 0, cols: 0, code: '' });
  protected readonly compartmentForm = signal<{ publicId?: string; row: number; col: number; code: string; name: string; note: string; active: boolean; wasActive: boolean } | null>(null);
  protected readonly lockerCells = (): { row: number; col: number; item: SpaceLockerCompartment | null }[] => {
    const { rows, cols } = this.lockerGrid();
    const items = this.lockerCompartments();
    const cells: { row: number; col: number; item: SpaceLockerCompartment | null }[] = [];
    for (let row = 1; row <= rows; row++) {
      for (let col = 1; col <= cols; col++) cells.push({ row, col, item: items.find((c) => c.rowIndex === row && c.colIndex === col) ?? null });
    }
    return cells;
  };
  protected readonly lockerOpenLoans = signal<SpaceLockerLoan[]>([]);
  protected lockerScanCardNo = '';

  protected readonly equipment = signal<SpaceFixtureEquipment[]>([]);
  protected equipmentName = '';
  protected equipmentQty: number | null = null;
  protected equipmentStatus = 1;

  protected readonly utilities = signal<SpaceFloorUtility[]>([]);
  protected readonly utilityModalOpen = signal(false);
  protected utilityName = '';
  protected utilityIcon = '';
  protected utilityQty: number | null = null;
  protected utilityStatus = 1;
  protected utilityDesc = '';
  protected readonly storeOptions = signal<SpaceStoreOption[]>([]);

  protected readonly floorsOfBuilding = () => this.floors().filter((f) => f.buildingId === this.buildingId());
  protected readonly placedRooms = () => this.rooms().filter((r) => r.positionX !== null && r.positionY !== null);
  protected readonly unplacedRooms = () => this.rooms().filter((r) => r.positionX === null || r.positionY === null);
  protected readonly placedFixtures = () => this.fixtures().filter((x) => x.positionX !== null && x.positionY !== null);
  protected readonly placedLockers = () => this.lockers().filter((c) => c.positionX !== null && c.positionY !== null);

  private drag: { target: DragTarget; mode: DragMode; corner: Corner; container: HTMLElement; startX: number; startY: number; orig: Rect; moved: boolean } | null = null;
  private draw: { kind: PlacingKind; container: HTMLElement; sx: number; sy: number } | null = null;

  async ngOnInit(): Promise<void> {
    this.buildings.set(await this.api.crud<SpaceBuilding>('buildings', 'space').searchAll());
    this.floors.set(await this.api.crud<SpaceFloor>('floors', 'space').searchAll());
    this.storeOptions.set(await this.api.spaceStores());
  }

  protected fixtureIcon(objectType: string): string {
    return this.fixtureTypes.find((t) => t.value === objectType)?.icon ?? 'category';
  }

  protected fixtureLabel(objectType: string): string {
    return this.fixtureTypes.find((t) => t.value === objectType)?.label ?? 'SPACE_FLOOR_MAP_PAGE.TYPE_OTHER';
  }

  protected lockerOpenLoanOf(compartmentId: number): SpaceLockerLoan | null {
    return this.lockerOpenLoans().find((l) => l.compartmentId === compartmentId) ?? null;
  }

  protected isSelected(kind: 'room' | 'fixture' | 'locker', id: number): boolean {
    const sel = this.selected();
    return !!sel && sel.kind === kind && sel.item.id === id;
  }

  protected selectBuilding(id: number | null): void {
    this.buildingId.set(id);
    this.floorId.set(null);
    this.floor.set(null);
    this.rooms.set([]);
    this.fixtures.set([]);
    this.lockers.set([]);
  }

  protected async selectFloor(id: number | null): Promise<void> {
    this.floorId.set(id);
    this.selected.set(null);
    this.placingKind.set(null);
    const f = this.floors().find((x) => x.id === id) ?? null;
    this.floor.set(f);
    if (!f) { this.rooms.set([]); this.fixtures.set([]); this.lockers.set([]); return; }
    const [rooms, fixtures, lockers] = await Promise.all([
      this.api.crud<SpaceRoom>('spaces', 'space').searchAll({ floorId: f.id }),
      this.api.crud<SpaceFixture>('fixtures', 'space').searchAll({ floorId: f.id }),
      this.api.crud<SpaceLockerCabinet>('locker-cabinets', 'space').searchAll({ floorId: f.id }),
    ]);
    this.rooms.set(rooms);
    this.fixtures.set(fixtures);
    this.lockers.set(lockers);
  }

  protected deselect(): void { this.selected.set(null); }

  protected togglePlacing(kind: PlacingKind): void {
    this.placingKind.set(this.placingKind() === kind ? null : kind);
    this.selected.set(null);
  }

  protected onBackgroundPointerDown(event: PointerEvent): void {
    const kind = this.placingKind();
    if (!kind) { this.deselect(); return; }
    // Đặt vật mới: kéo để vẽ khung (như vẽ hình chữ nhật), thả chuột mới mở form — bấm một điểm thì dùng cỡ mặc định.
    event.preventDefault();
    const container = event.currentTarget as HTMLElement;
    const p = this.pointOf(event, container);
    this.draw = { kind, container, sx: p.x, sy: p.y };
    this.draft.set({ x: p.x, y: p.y, w: 0, h: 0 });
    window.addEventListener('pointermove', this.onDrawMove);
    window.addEventListener('pointerup', this.onDrawEnd);
  }

  @HostListener('window:keydown.escape')
  protected cancelPlacing(): void {
    this.stopDrawing();
    this.placingKind.set(null);
  }

  ngOnDestroy(): void {
    this.stopDrawing();
    window.removeEventListener('pointermove', this.onPointerMove);
    window.removeEventListener('pointerup', this.onPointerUp);
  }

  /** Toạ độ con trỏ theo % của khung sơ đồ (0–100). */
  private pointOf(event: PointerEvent, container: HTMLElement): { x: number; y: number } {
    const rect = container.getBoundingClientRect();
    return { x: clamp(((event.clientX - rect.left) / rect.width) * 100, 0, 100), y: clamp(((event.clientY - rect.top) / rect.height) * 100, 0, 100) };
  }

  private readonly onDrawMove = (event: PointerEvent): void => {
    const d = this.draw;
    if (!d) return;
    const p = this.pointOf(event, d.container);
    this.draft.set({ x: Math.min(d.sx, p.x), y: Math.min(d.sy, p.y), w: Math.abs(p.x - d.sx), h: Math.abs(p.y - d.sy) });
  };

  private readonly onDrawEnd = (): void => {
    const d = this.draw;
    const rect = this.draft();
    this.stopDrawing();
    if (!d || !rect) return;
    const drawn = rect.w >= CLICK_TOLERANCE || rect.h >= CLICK_TOLERANCE;
    const box: Rect = drawn
      ? { x: rect.x, y: rect.y, w: Math.max(rect.w, MIN_SIZE), h: Math.max(rect.h, MIN_SIZE) }
      : { x: clamp(d.sx - DEFAULT_SIZE / 2, 0, 100 - DEFAULT_SIZE), y: clamp(d.sy - DEFAULT_SIZE / 2, 0, 100 - DEFAULT_SIZE), w: DEFAULT_SIZE, h: DEFAULT_SIZE };
    box.w = Math.min(box.w, 100 - box.x);
    box.h = Math.min(box.h, 100 - box.y);
    this.placingKind.set(null);
    this.beginCreate(d.kind, box);
  };

  private stopDrawing(): void {
    this.draw = null;
    this.draft.set(null);
    window.removeEventListener('pointermove', this.onDrawMove);
    window.removeEventListener('pointerup', this.onDrawEnd);
  }

  private beginCreate(kind: PlacingKind, box: Rect): void {
    const place = { x: box.x, y: box.y, w: box.w, h: box.h };
    if (kind === 'ROOM') {
      this.roomForm.set({ code: '', name: '', kind: SPACE_KIND_ROOM, category: 1, capacity: 4, autoApprove: false, colorHex: null, ...place });
    } else if (kind === 'LOCKER') {
      this.lockerForm.set({ code: '', name: '', rows: 2, cols: 3, colorHex: null, iconName: 'lock', note: null, ...place });
    } else {
      this.fixtureForm.set({
        code: '', name: '', objectType: kind, colorHex: null, iconName: this.fixtureIcon(kind),
        shelfCategoryRange: null, shelfSubjectName: null, shelfCapacity: kind === 'SHELF' ? 100 : null, shelfDescription: null, storePublicId: null, ...place,
      });
    }
  }

  protected cornerClass(c: Corner): string {
    return {
      nw: 'top-0 left-0 cursor-nwse-resize', ne: 'top-0 right-0 cursor-nesw-resize',
      sw: 'bottom-0 left-0 cursor-nesw-resize', se: 'bottom-0 right-0 cursor-nwse-resize',
    }[c];
  }

  protected openEdit(sel: DragTarget): void {
    if (sel.kind === 'room') {
      const room = sel.item;
      this.roomForm.set({
        publicId: room.publicId, code: room.code, name: room.name, kind: room.kind, category: room.category,
        capacity: room.capacity, autoApprove: room.autoApprove, colorHex: room.colorHex,
      });
    } else if (sel.kind === 'locker') {
      const c = sel.item;
      this.lockerForm.set({
        publicId: c.publicId, code: c.code, name: c.name, rows: c.rows, cols: c.cols, colorHex: c.colorHex, iconName: c.iconName, note: c.note,
      });
      this.lockerScanCardNo = '';
      void this.loadLockerCompartments(c.id);
    } else {
      const x = sel.item;
      this.fixtureForm.set({
        publicId: x.publicId, code: x.code, name: x.name, objectType: x.objectType, colorHex: x.colorHex, iconName: x.iconName,
        shelfCategoryRange: x.shelfCategoryRange, shelfSubjectName: x.shelfSubjectName, shelfCapacity: x.shelfCapacity, shelfDescription: x.shelfDescription,
        storePublicId: x.storePublicId,
      });
      this.equipmentName = '';
      this.equipmentQty = null;
      this.equipmentStatus = 1;
      void this.loadEquipment(x.id);
    }
  }

  private async loadLockerCompartments(cabinetId: number): Promise<void> {
    const [compartments, openLoans] = await Promise.all([
      this.api.crud<SpaceLockerCompartment>('locker-compartments', 'space').searchAll({ cabinetId }),
      this.api.searchLockerLoans({ loanStatus: 1, pageIndex: 1, pageSize: 500 }),
    ]);
    this.lockerCompartments.set(compartments);
    this.compartmentForm.set(null);
    const cabinet = this.lockers().find((x) => x.id === cabinetId);
    if (cabinet) this.lockerGrid.set({ rows: cabinet.rows, cols: cabinet.cols, code: cabinet.code });
    const ids = new Set(compartments.map((c) => c.id));
    this.lockerOpenLoans.set(openLoans.items.filter((l) => ids.has(l.compartmentId)));
  }

  protected async borrowToCompartment(c: SpaceLockerCompartment): Promise<void> {
    if (!this.lockerScanCardNo.trim()) return;
    try {
      await this.api.borrowLocker(c.code, this.lockerScanCardNo.trim());
      const cabinet = this.lockers().find((x) => x.publicId === this.lockerForm()?.publicId);
      if (cabinet) await this.loadLockerCompartments(cabinet.id);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected async returnFromCompartment(c: SpaceLockerCompartment): Promise<void> {
    try {
      await this.api.returnLocker(c.code);
      const cabinet = this.lockers().find((x) => x.publicId === this.lockerForm()?.publicId);
      if (cabinet) await this.loadLockerCompartments(cabinet.id);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  /** Mã gợi ý cho ngăn thêm tay — cùng quy tắc với mã tự sinh của backend. */
  private suggestCompartmentCode(row: number, col: number): string {
    const code = this.lockerGrid().code;
    return row <= 9 && col <= 9 ? `${code}-${row}${col}` : `${code}-${row}-${col}`;
  }

  protected addCompartment(row: number, col: number): void {
    this.compartmentForm.set({ row, col, code: this.suggestCompartmentCode(row, col), name: '', note: '', active: true, wasActive: true });
  }

  protected editCompartment(c: SpaceLockerCompartment): void {
    this.compartmentForm.set({
      publicId: c.publicId, row: c.rowIndex, col: c.colIndex, code: c.code, name: c.name ?? '', note: c.note ?? '',
      active: c.status !== STATUS_INACTIVE, wasActive: c.status !== STATUS_INACTIVE,
    });
  }

  protected async saveCompartment(): Promise<void> {
    const f = this.compartmentForm();
    const cabinet = this.lockers().find((x) => x.publicId === this.lockerForm()?.publicId);
    if (!f || !cabinet) return;
    if (!f.code.trim()) { this.toastr.warning('SPACE_FLOOR_MAP_PAGE.COMPARTMENT_CODE_REQUIRED'); return; }
    this.busy.set(true);
    try {
      const body = { cabinetId: cabinet.id, rowIndex: f.row, colIndex: f.col, code: f.code.trim(), name: f.name.trim() || null, note: f.note.trim() || null };
      const client = this.api.crud<SpaceLockerCompartment>('locker-compartments', 'space');
      const saved = f.publicId ? await client.update(f.publicId, body) : await client.add(body);
      if (f.active !== f.wasActive || (!f.publicId && !f.active)) await client.changeStatus(saved.publicId, f.active ? STATUS_ACTIVE : STATUS_INACTIVE);
      await this.loadLockerCompartments(cabinet.id);
      this.toastr.success('COMMON.SAVE_SUCCESS');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async deleteCompartment(c: SpaceLockerCompartment): Promise<void> {
    const cabinet = this.lockers().find((x) => x.publicId === this.lockerForm()?.publicId);
    if (!cabinet || !confirm(this.language.t('SPACE_FLOOR_MAP_PAGE.COMPARTMENT_DELETE_CONFIRM', { code: c.code }))) return;
    try {
      await this.api.crud('locker-compartments', 'space').delete(c.publicId);
      await this.loadLockerCompartments(cabinet.id);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected conditionLabel(status: number): string {
    return this.conditionOptions.find((c) => c.value === status)?.label ?? 'SPACE_FLOOR_MAP_PAGE.CONDITION_1';
  }

  private async loadEquipment(fixtureId: number): Promise<void> {
    this.equipment.set(await this.api.crud<SpaceFixtureEquipment>('fixture-equipment', 'space').searchAll({ fixtureId }));
  }

  protected async addEquipment(fixturePublicId: string): Promise<void> {
    const fixture = this.fixtures().find((x) => x.publicId === fixturePublicId);
    if (!fixture || !this.equipmentName.trim()) return;
    try {
      await this.api.crud<SpaceFixtureEquipment>('fixture-equipment', 'space').add({
        fixtureId: fixture.id, name: this.equipmentName.trim(), description: null, quantity: this.equipmentQty, conditionStatus: this.equipmentStatus,
      });
      this.equipmentName = '';
      this.equipmentQty = null;
      await this.loadEquipment(fixture.id);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected async deleteEquipment(eq: SpaceFixtureEquipment): Promise<void> {
    try {
      await this.api.crud('fixture-equipment', 'space').delete(eq.publicId);
      this.equipment.set(this.equipment().filter((x) => x.id !== eq.id));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected async openUtilities(): Promise<void> {
    const floor = this.floor();
    if (!floor) return;
    this.utilityName = '';
    this.utilityIcon = '';
    this.utilityQty = null;
    this.utilityStatus = 1;
    this.utilityDesc = '';
    this.utilities.set(await this.api.crud<SpaceFloorUtility>('floor-utilities', 'space').searchAll({ floorId: floor.id }));
    this.utilityModalOpen.set(true);
  }

  protected async addUtility(): Promise<void> {
    const floor = this.floor();
    if (!floor || !this.utilityName.trim()) return;
    try {
      await this.api.crud<SpaceFloorUtility>('floor-utilities', 'space').add({
        floorId: floor.id, name: this.utilityName.trim(), description: this.utilityDesc.trim() || null, quantity: this.utilityQty,
        conditionStatus: this.utilityStatus, iconName: this.utilityIcon.trim() || null,
      });
      this.utilityName = '';
      this.utilityIcon = '';
      this.utilityQty = null;
      this.utilityDesc = '';
      this.utilities.set(await this.api.crud<SpaceFloorUtility>('floor-utilities', 'space').searchAll({ floorId: floor.id }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected async deleteUtility(u: SpaceFloorUtility): Promise<void> {
    try {
      await this.api.crud('floor-utilities', 'space').delete(u.publicId);
      this.utilities.set(this.utilities().filter((x) => x.id !== u.id));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected async submitRoomForm(): Promise<void> {
    const f = this.roomForm();
    const floor = this.floor();
    if (!f || !floor) return;
    this.busy.set(true);
    try {
      if (f.publicId) {
        const existing = this.rooms().find((r) => r.publicId === f.publicId)!;
        const updated = await this.api.crud<SpaceRoom>('spaces', 'space').update(f.publicId, { ...existing, ...f });
        this.rooms.set(this.rooms().map((r) => (r.publicId === updated.publicId ? updated : r)));
        this.selected.set({ kind: 'room', item: updated });
      } else {
        const created = await this.api.crud<SpaceRoom>('spaces', 'space').add({
          floorId: floor.id, code: f.code, name: f.name, kind: f.kind, category: f.category, capacity: f.capacity,
          minAdvanceMinutes: 0, maxAdvanceDays: 7, maxAdvanceHours: null, maxBookingMinutesPerReader: 0, checkInGraceMinutes: 15,
          autoApprove: f.autoApprove, rules: null, allowedReaderTypeIds: [], minGroupSize: null, colorHex: f.colorHex, iconName: null,
        });
        const placed = await this.api.placeSpaceRoom(created.publicId, { positionX: f.x!, positionY: f.y!, width: f.w ?? DEFAULT_SIZE, height: f.h ?? DEFAULT_SIZE });
        this.rooms.set([...this.rooms(), placed]);
        this.selected.set({ kind: 'room', item: placed });
      }
      this.roomForm.set(null);
      this.toastr.success('COMMON.SAVE_SUCCESS');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async submitFixtureForm(): Promise<void> {
    const f = this.fixtureForm();
    const floor = this.floor();
    if (!f || !floor) return;
    this.busy.set(true);
    try {
      if (f.publicId) {
        const existing = this.fixtures().find((x) => x.publicId === f.publicId)!;
        const updated = await this.api.crud<SpaceFixture>('fixtures', 'space').update(f.publicId, { ...existing, ...f, floorId: existing.floorId });
        this.fixtures.set(this.fixtures().map((x) => (x.publicId === updated.publicId ? updated : x)));
        this.selected.set({ kind: 'fixture', item: updated });
      } else {
        const created = await this.api.crud<SpaceFixture>('fixtures', 'space').add({
          floorId: floor.id, code: f.code, name: f.name, objectType: f.objectType, colorHex: f.colorHex, iconName: f.iconName,
          shelfCategoryRange: f.shelfCategoryRange, shelfSubjectName: f.shelfSubjectName, shelfCapacity: f.shelfCapacity,
          shelfDescription: f.shelfDescription, storePublicId: f.storePublicId,
        });
        const placed = await this.api.placeSpaceFixture(created.publicId, { positionX: f.x!, positionY: f.y!, width: f.w ?? DEFAULT_SIZE, height: f.h ?? DEFAULT_SIZE });
        this.fixtures.set([...this.fixtures(), placed]);
        this.selected.set({ kind: 'fixture', item: placed });
      }
      this.fixtureForm.set(null);
      this.toastr.success('COMMON.SAVE_SUCCESS');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async submitLockerForm(): Promise<void> {
    const f = this.lockerForm();
    const floor = this.floor();
    if (!f || !floor) return;
    this.busy.set(true);
    try {
      if (f.publicId) {
        const existing = this.lockers().find((c) => c.publicId === f.publicId)!;
        const updated = await this.api.crud<SpaceLockerCabinet>('locker-cabinets', 'space').update(f.publicId, { ...existing, ...f, floorId: existing.floorId });
        this.lockers.set(this.lockers().map((c) => (c.publicId === updated.publicId ? updated : c)));
        this.selected.set({ kind: 'locker', item: updated });
        await this.loadLockerCompartments(updated.id);
      } else {
        const created = await this.api.crud<SpaceLockerCabinet>('locker-cabinets', 'space').add({
          floorId: floor.id, code: f.code, name: f.name, rows: f.rows, cols: f.cols, colorHex: f.colorHex, iconName: f.iconName, note: f.note,
        });
        const placed = await this.api.placeLockerCabinet(created.publicId, { positionX: f.x!, positionY: f.y!, width: f.w ?? DEFAULT_SIZE, height: f.h ?? DEFAULT_SIZE });
        this.lockers.set([...this.lockers(), placed]);
        this.selected.set({ kind: 'locker', item: placed });
        // Tủ mới: giữ hộp thoại và chuyển sang sửa tủ vừa tạo để thấy lưới ngăn đã sinh và quản lý từng ngăn.
        this.openEdit({ kind: 'locker', item: placed });
        this.toastr.success('SPACE_FLOOR_MAP_PAGE.LOCKER_CREATED');
        return;
      }
      this.lockerForm.set(null);
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
    await this.savePosition({ kind: 'room', item: room }, 50 - half, 50 - half, DEFAULT_SIZE, DEFAULT_SIZE);
  }

  protected startDrag(event: PointerEvent, kind: 'room' | 'fixture' | 'locker', item: SpaceRoom | SpaceFixture | SpaceLockerCabinet, mode: DragMode, corner: Corner = 'se'): void {
    event.stopPropagation();
    event.preventDefault();
    // Khung sơ đồ (không phải phần tử cha trực tiếp — với tay nắm đổi kích thước đó là chính vật đang kéo).
    const container = (event.currentTarget as HTMLElement).closest('[data-canvas]') as HTMLElement | null;
    if (!container) return;
    const target = (
      kind === 'room' ? { kind: 'room', item: item as SpaceRoom }
      : kind === 'fixture' ? { kind: 'fixture', item: item as SpaceFixture }
      : { kind: 'locker', item: item as SpaceLockerCabinet }
    ) as DragTarget;
    this.selected.set(target);
    const orig: Rect = { x: item.positionX ?? 0, y: item.positionY ?? 0, w: item.width ?? DEFAULT_SIZE, h: item.height ?? DEFAULT_SIZE };
    this.drag = { target, mode, corner, container, startX: event.clientX, startY: event.clientY, orig, moved: false };
    window.addEventListener('pointermove', this.onPointerMove);
    window.addEventListener('pointerup', this.onPointerUp);
  }

  private readonly onPointerMove = (event: PointerEvent): void => {
    const drag = this.drag;
    if (!drag) return;
    const rect = drag.container.getBoundingClientRect();
    // Luôn tính từ vị trí lúc bắt đầu kéo (không cộng dồn lên vị trí đã dời).
    const dx = ((event.clientX - drag.startX) / rect.width) * 100;
    const dy = ((event.clientY - drag.startY) / rect.height) * 100;
    if (Math.abs(dx) + Math.abs(dy) > 0.2) drag.moved = true;
    const o = drag.orig;
    let next: Rect = o;
    if (drag.mode === 'move') {
      next = { ...o, x: clamp(o.x + dx, 0, 100 - o.w), y: clamp(o.y + dy, 0, 100 - o.h) };
    } else {
      let { x, y, w, h } = o;
      if (drag.corner.includes('e')) w = clamp(o.w + dx, MIN_SIZE, 100 - o.x);
      else { x = clamp(o.x + dx, 0, o.x + o.w - MIN_SIZE); w = o.x + o.w - x; }
      if (drag.corner.includes('s')) h = clamp(o.h + dy, MIN_SIZE, 100 - o.y);
      else { y = clamp(o.y + dy, 0, o.y + o.h - MIN_SIZE); h = o.y + o.h - y; }
      next = { x, y, w, h };
    }
    const pos = { positionX: next.x, positionY: next.y, width: next.w, height: next.h };
    const target = drag.target;
    if (target.kind === 'room') {
      const item = { ...target.item, ...pos };
      this.rooms.set(this.rooms().map((r) => (r.id === item.id ? item : r)));
      drag.target = { kind: 'room', item };
    } else if (target.kind === 'fixture') {
      const item = { ...target.item, ...pos };
      this.fixtures.set(this.fixtures().map((r) => (r.id === item.id ? item : r)));
      drag.target = { kind: 'fixture', item };
    } else {
      const item = { ...target.item, ...pos };
      this.lockers.set(this.lockers().map((c) => (c.id === item.id ? item : c)));
      drag.target = { kind: 'locker', item };
    }
    this.selected.set(drag.target);
  };

  private readonly onPointerUp = (): void => {
    const drag = this.drag;
    this.drag = null;
    window.removeEventListener('pointermove', this.onPointerMove);
    window.removeEventListener('pointerup', this.onPointerUp);
    if (!drag || !drag.moved) return; // chỉ bấm chọn — không ghi lại vị trí
    const it = drag.target.item;
    void this.savePosition(drag.target, it.positionX, it.positionY, it.width, it.height);
  };

  private async savePosition(target: DragTarget, x: number | null, y: number | null, w: number | null, h: number | null): Promise<void> {
    try {
      if (target.kind === 'room') {
        const updated = await this.api.placeSpaceRoom(target.item.publicId, { positionX: x, positionY: y, width: w, height: h });
        this.rooms.set(this.rooms().map((r) => (r.id === updated.id ? updated : r)));
        if (this.isSelected('room', updated.id)) this.selected.set({ kind: 'room', item: updated });
      } else if (target.kind === 'fixture') {
        const updated = await this.api.placeSpaceFixture(target.item.publicId, { positionX: x, positionY: y, width: w, height: h });
        this.fixtures.set(this.fixtures().map((r) => (r.id === updated.id ? updated : r)));
        if (this.isSelected('fixture', updated.id)) this.selected.set({ kind: 'fixture', item: updated });
      } else {
        const updated = await this.api.placeLockerCabinet(target.item.publicId, { positionX: x, positionY: y, width: w, height: h });
        this.lockers.set(this.lockers().map((c) => (c.id === updated.id ? updated : c)));
        if (this.isSelected('locker', updated.id)) this.selected.set({ kind: 'locker', item: updated });
      }
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
