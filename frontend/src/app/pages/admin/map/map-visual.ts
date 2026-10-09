import { Component, inject, OnInit, OnDestroy, signal, computed, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { MapBuildingService } from '../../../services/map/map-building.service';
import { MapFloorService } from '../../../services/map/map-floor.service';
import { MapFloorUtilityService } from '../../../services/map/map-floor-utility.service';
import { MapObjectService } from '../../../services/map/map-object.service';
import { MapShelfDetailService } from '../../../services/map/map-shelf-detail.service';
import { MapEquipmentService } from '../../../services/map/map-equipment.service';
import { StoreService } from '../../../services/printbook/store.service';
import { MapBuilding } from '../../../models/map/map-building';
import { MapFloor } from '../../../models/map/map-floor';
import { MapFloorUtility } from '../../../models/map/map-floor-utility';
import { MapObject, MAP_OBJECT_TYPES } from '../../../models/map/map-object';
import { MapShelfDetail } from '../../../models/map/map-shelf-detail';
import { MapEquipment } from '../../../models/map/map-equipment';
import { Store } from '../../../models/printbook/store';
import { MapObjectFormModalComponent, MapObjectFormInitial } from '../../../shared/map/map-object-form-modal';
import { statusColor, statusLabel } from '../../../shared/map/map-status.util';

const MIN_SIZE = 3;  // % tối thiểu chiều rộng/cao 1 đối tượng
const DRAG_CLICK_THRESHOLD = 4; // px — dưới ngưỡng này coi là click, không phải kéo
const PLACE_WIDTH = 15; const PLACE_HEIGHT = 10; // kích thước mặc định 1 đối tượng mới đặt trên canvas

@Component({
  selector: 'app-map-visual',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, MatIconModule, MapObjectFormModalComponent, NgSelectModule],
  templateUrl: './map-visual.html'
})
export class MapVisualPage implements OnInit, OnDestroy {
  private buildingService = inject(MapBuildingService);
  private floorService    = inject(MapFloorService);
  private utilityService  = inject(MapFloorUtilityService);
  private objectService   = inject(MapObjectService);
  private shelfDetailSvc  = inject(MapShelfDetailService);
  private equipmentSvc    = inject(MapEquipmentService);
  private storeService    = inject(StoreService);
  private toastr          = inject(ToastrService);
  private router          = inject(Router);
  public  translate       = inject(TranslateService);
  private destroy$        = new Subject<void>();

  readonly objectTypes = MAP_OBJECT_TYPES;

  @ViewChild('canvas') canvasRef!: ElementRef<HTMLDivElement>;

  buildings         = signal<MapBuilding[]>([]);
  floors            = signal<MapFloor[]>([]);
  selectedBuildingId= signal<number | null>(null);
  selectedFloorId   = signal<number | null>(null);
  objects           = signal<MapObject[]>([]);
  isLoading         = signal(false);
  stores            = signal<Store[]>([]);
  floorUtilities    = signal<MapFloorUtility[]>([]);

  readonly statusColor = statusColor;
  readonly statusLabel = statusLabel;

  searchQuery   = signal('');
  typeFilter    = signal<string>('all');
  selectedObject= signal<MapObject | null>(null);
  selectedShelfDetail = signal<MapShelfDetail | null>(null);
  selectedEquipment   = signal<MapEquipment[]>([]);

  // Trạng thái kéo-thả / resize — chỉ 1 object thao tác tại 1 thời điểm
  private dragMode: 'move' | 'resize' | null = null;
  private dragObjectId: number | null = null;
  private dragStartClientX = 0;
  private dragStartClientY = 0;
  private dragStartGeom = { x: 0, y: 0, w: 0, h: 0 };
  private dragMoved = false;
  liveGeom = signal<{ id: number; x: number; y: number; w: number; h: number } | null>(null);
  isSavingPosition = signal(false);

  // ── Thêm mới / sửa đối tượng qua form dùng chung (app-map-object-form-modal) ────────────────────
  placingType  = signal<string | null>(null); // loại đang chọn từ palette, chờ click lên canvas để đặt
  formOpen     = signal(false);
  formInitial  = signal<MapObjectFormInitial | null>(null);
  private formWasEdit = false;

  currentFloor = computed(() => this.floors().find(f => f.id === this.selectedFloorId()) || null);

  filteredObjects = computed(() => {
    const q = this.searchQuery().toLowerCase().trim();
    const t = this.typeFilter();
    return this.objects().filter(o => {
      const matchType  = t === 'all' || o.objectType === t;
      const matchQuery = !q || (o.name || '').toLowerCase().includes(q) || (o.code || '').toLowerCase().includes(q);
      return matchType && matchQuery;
    });
  });

  ngOnInit(): void {
    this.loadBuildings();
    this.storeService.searchAll().pipe(takeUntil(this.destroy$)).subscribe(list => this.stores.set(list));
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  typeMeta(type?: string) { return this.objectTypes.find(t => t.value === type) || this.objectTypes[this.objectTypes.length - 1]; }

  loadBuildings(): void {
    this.buildingService.searchAll().pipe(takeUntil(this.destroy$)).subscribe(list => {
      this.buildings.set(list);
      if (list.length) { this.selectedBuildingId.set(list[0].id); this.loadFloors(list[0].id); }
    });
  }

  onBuildingChange(buildingId: number): void {
    this.selectedBuildingId.set(Number(buildingId));
    this.loadFloors(Number(buildingId));
  }

  loadFloors(buildingId: number): void {
    this.floorService.searchAll(buildingId).pipe(takeUntil(this.destroy$)).subscribe(list => {
      this.floors.set(list);
      if (list.length) {
        this.selectedFloorId.set(list[0].id);
        this.loadObjects(list[0].id);
        this.loadFloorUtilities(list[0].id);
      } else {
        this.selectedFloorId.set(null);
        this.objects.set([]);
        this.floorUtilities.set([]);
      }
    });
  }

  onFloorChange(floorId: number): void {
    this.selectedFloorId.set(floorId);
    this.loadObjects(floorId);
    this.loadFloorUtilities(floorId);
  }

  loadFloorUtilities(floorId: number): void {
    this.utilityService.searchAllByFloor(floorId).pipe(takeUntil(this.destroy$)).subscribe(list => this.floorUtilities.set(list));
  }

  deleteFloorUtility(item: MapFloorUtility): void {
    if (!item.publicId) return;
    this.utilityService.delete(item.publicId).pipe(takeUntil(this.destroy$)).subscribe(() => {
      const floorId = this.selectedFloorId();
      if (floorId) this.loadFloorUtilities(floorId);
    });
  }

  loadObjects(floorId: number): void {
    this.isLoading.set(true);
    this.objectService.searchAllByFloor(floorId).pipe(takeUntil(this.destroy$)).subscribe({
      next: list => { this.objects.set(list); this.isLoading.set(false); },
      error: () => { this.isLoading.set(false); }
    });
  }

  geomFor(obj: MapObject): { x: number; y: number; w: number; h: number } {
    const live = this.liveGeom();
    if (live && live.id === obj.id) return { x: live.x, y: live.y, w: live.w, h: live.h };
    return { x: obj.positionX ?? 0, y: obj.positionY ?? 0, w: obj.width ?? 10, h: obj.height ?? 10 };
  }

  // ── Kéo-thả di chuyển ─────────────────────────────────────────────────────
  onObjectPointerDown(ev: PointerEvent, obj: MapObject): void {
    ev.preventDefault();
    ev.stopPropagation();
    this.dragMode = 'move';
    this.dragObjectId = obj.id;
    this.dragMoved = false;
    this.dragStartClientX = ev.clientX;
    this.dragStartClientY = ev.clientY;
    const g = this.geomFor(obj);
    this.dragStartGeom = { ...g };
    this.liveGeom.set({ id: obj.id, ...g });
    document.addEventListener('pointermove', this.onPointerMove);
    document.addEventListener('pointerup', this.onPointerUp);
  }

  // ── Kéo resize (handle góc dưới-phải) ────────────────────────────────────
  onResizeHandlePointerDown(ev: PointerEvent, obj: MapObject): void {
    ev.preventDefault();
    ev.stopPropagation();
    this.dragMode = 'resize';
    this.dragObjectId = obj.id;
    this.dragMoved = false;
    this.dragStartClientX = ev.clientX;
    this.dragStartClientY = ev.clientY;
    const g = this.geomFor(obj);
    this.dragStartGeom = { ...g };
    this.liveGeom.set({ id: obj.id, ...g });
    document.addEventListener('pointermove', this.onPointerMove);
    document.addEventListener('pointerup', this.onPointerUp);
  }

  private onPointerMove = (ev: PointerEvent): void => {
    if (!this.dragMode || this.dragObjectId === null || !this.canvasRef) return;
    const rect = this.canvasRef.nativeElement.getBoundingClientRect();
    const dxPx = ev.clientX - this.dragStartClientX;
    const dyPx = ev.clientY - this.dragStartClientY;
    if (Math.abs(dxPx) > DRAG_CLICK_THRESHOLD || Math.abs(dyPx) > DRAG_CLICK_THRESHOLD) this.dragMoved = true;

    const dxPct = (dxPx / rect.width) * 100;
    const dyPct = (dyPx / rect.height) * 100;

    if (this.dragMode === 'move') {
      const w = this.dragStartGeom.w, h = this.dragStartGeom.h;
      const x = clamp(this.dragStartGeom.x + dxPct, 0, 100 - w);
      const y = clamp(this.dragStartGeom.y + dyPct, 0, 100 - h);
      this.liveGeom.set({ id: this.dragObjectId, x, y, w, h });
    } else {
      const x = this.dragStartGeom.x, y = this.dragStartGeom.y;
      const w = clamp(this.dragStartGeom.w + dxPct, MIN_SIZE, 100 - x);
      const h = clamp(this.dragStartGeom.h + dyPct, MIN_SIZE, 100 - y);
      this.liveGeom.set({ id: this.dragObjectId, x, y, w, h });
    }
  };

  private onPointerUp = (): void => {
    document.removeEventListener('pointermove', this.onPointerMove);
    document.removeEventListener('pointerup', this.onPointerUp);
    const objectId = this.dragObjectId;
    const moved    = this.dragMoved;
    const geom     = this.liveGeom();
    this.dragMode = null;
    this.dragObjectId = null;

    if (objectId === null) return;
    const obj = this.objects().find(o => o.id === objectId);
    if (!obj) { this.liveGeom.set(null); return; }

    if (!moved) {
      // Không kéo đáng kể → coi như click, mở panel xem chi tiết.
      this.liveGeom.set(null);
      this.openInspector(obj);
      return;
    }

    if (!geom || !obj.publicId) { this.liveGeom.set(null); return; }
    this.isSavingPosition.set(true);
    const payload: Partial<MapObject> = {
      name: obj.name, code: obj.code, floorId: obj.floorId, objectType: obj.objectType, category: obj.category,
      colorHex: obj.colorHex, iconName: obj.iconName,
      positionX: round1(geom.x), positionY: round1(geom.y), width: round1(geom.w), height: round1(geom.h),
    };
    this.objectService.update(obj.publicId, payload).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.objects.update(list => list.map(o => o.id === objectId
          ? { ...o, positionX: payload.positionX, positionY: payload.positionY, width: payload.width, height: payload.height }
          : o));
        this.liveGeom.set(null);
        this.isSavingPosition.set(false);
      },
      error: () => {
        this.liveGeom.set(null);
        this.isSavingPosition.set(false);
      }
    });
  };

  openInspector(obj: MapObject): void {
    this.selectedObject.set(obj);
    this.selectedShelfDetail.set(null);
    this.selectedEquipment.set([]);
    if (obj.objectType === 'SHELF') {
      this.shelfDetailSvc.getByObjectId(obj.id).pipe(takeUntil(this.destroy$)).subscribe(d => this.selectedShelfDetail.set(d));
    }
    this.equipmentSvc.searchAllByObject(obj.id).pipe(takeUntil(this.destroy$)).subscribe(list => this.selectedEquipment.set(list));
  }

  closeInspector(): void { this.selectedObject.set(null); }

  // ── Thêm mới: chọn loại từ palette rồi click lên canvas để đặt ──────────────────────────────────
  startPlacing(type: string): void { this.placingType.set(this.placingType() === type ? null : type); }
  cancelPlacing(): void { this.placingType.set(null); }

  onCanvasBackgroundClick(ev: MouseEvent): void {
    const type = this.placingType();
    if (!type || !this.canvasRef || ev.target !== this.canvasRef.nativeElement) return; // chỉ nhận click vào nền trống
    const floorId = this.selectedFloorId();
    if (!floorId) return;
    const rect = this.canvasRef.nativeElement.getBoundingClientRect();
    const x = clamp(((ev.clientX - rect.left) / rect.width) * 100 - PLACE_WIDTH / 2, 0, 100 - PLACE_WIDTH);
    const y = clamp(((ev.clientY - rect.top) / rect.height) * 100 - PLACE_HEIGHT / 2, 0, 100 - PLACE_HEIGHT);
    this.formInitial.set({
      objectId: null, publicId: null, name: '', code: '', floorId, objectType: type,
      category: 4, colorHex: '#3b82f6', iconName: '', storeId: null,
      positionX: round1(x), positionY: round1(y), width: PLACE_WIDTH, height: PLACE_HEIGHT, shelfDetail: null,
    });
    this.formWasEdit = false;
    this.placingType.set(null);
    this.formOpen.set(true);
  }

  // ── Sửa đối tượng đang chọn (mở từ Inspector) ────────────────────────────────────────────────────
  editSelectedObject(): void {
    const obj = this.selectedObject();
    if (!obj?.publicId) return;
    this.objectService.getById(obj.publicId).pipe(takeUntil(this.destroy$)).subscribe(data => {
      const setInitial = (shelfDetail: MapObjectFormInitial['shelfDetail']) => {
        this.formInitial.set({
          objectId: obj.id, publicId: obj.publicId!,
          name: data.name ?? '', code: data.code ?? '', floorId: data.floorId,
          objectType: data.objectType ?? 'ROOM', category: data.category ?? 4,
          colorHex: data.colorHex ?? '#3b82f6', iconName: data.iconName ?? '', storeId: data.storeId ?? null,
          positionX: data.positionX ?? 0, positionY: data.positionY ?? 0, width: data.width ?? 15, height: data.height ?? 10,
          shelfDetail,
        });
        this.formWasEdit = true;
        this.closeInspector();
        this.formOpen.set(true);
      };
      if (data.objectType === 'SHELF') {
        this.shelfDetailSvc.getByObjectId(obj.id).pipe(takeUntil(this.destroy$)).subscribe(detail => {
          setInitial(detail ? {
            id: detail.id, publicId: detail.publicId ?? null,
            categoryRange: detail.categoryRange ?? '', subjectName: detail.subjectName ?? '',
            capacity: detail.capacity ?? 100, description: detail.description ?? '',
          } : null);
        });
      } else {
        setInitial(null);
      }
    });
  }

  onFormSaved(): void {
    this.formOpen.set(false);
    const floorId = this.selectedFloorId();
    if (floorId) this.loadObjects(floorId);
    this.toastr.success(this.translate.instant(this.formWasEdit ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS'));
  }

  viewFloorOverview(): void {
    const floorId = this.selectedFloorId();
    if (!floorId) return;
    this.router.navigate(['/admin/map-floor-overview'], { queryParams: { floorId } });
  }
}

function clamp(v: number, min: number, max: number): number {
  if (max < min) return min;
  return Math.min(Math.max(v, min), max);
}

function round1(v: number): number { return Math.round(v * 10) / 10; }
