import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { ToastrService } from '../../services/shared/toastr.service';
import { MapObjectService } from '../../services/map/map-object.service';
import { MapShelfDetailService } from '../../services/map/map-shelf-detail.service';
import { MapEquipmentService } from '../../services/map/map-equipment.service';
import { MapFloor } from '../../models/map/map-floor';
import { Store } from '../../models/printbook/store';
import { MapEquipment } from '../../models/map/map-equipment';
import { MAP_OBJECT_TYPES, MAP_OBJECT_CATEGORIES } from '../../models/map/map-object';
import { statusColor, statusLabel } from './map-status.util';

export interface MapObjectFormInitial {
  objectId?: number | null;
  publicId?: string | null; // null/undefined = đang thêm mới
  name: string;
  code?: string;
  floorId: number | null;
  objectType: string;
  category: number;
  colorHex: string;
  iconName?: string;
  storeId?: number | null;
  positionX: number;
  positionY: number;
  width: number;
  height: number;
  shelfDetail?: {
    id: number | null;
    publicId: string | null;
    categoryRange: string;
    subjectName: string;
    capacity: number;
    description: string;
  } | null;
}

@Component({
  selector: 'app-map-object-form-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatIconModule],
  templateUrl: './map-object-form-modal.html'
})
export class MapObjectFormModalComponent implements OnChanges {
  private service        = inject(MapObjectService);
  private shelfDetailSvc = inject(MapShelfDetailService);
  private equipmentSvc   = inject(MapEquipmentService);
  private toastr         = inject(ToastrService);
  public  translate      = inject(TranslateService);

  readonly statusColor = statusColor;
  readonly statusLabel = statusLabel;

  readonly objectTypes = MAP_OBJECT_TYPES;
  readonly categories  = MAP_OBJECT_CATEGORIES;

  @Input() open: boolean = false;
  @Input() floors: MapFloor[] = [];
  @Input() stores: Store[] = [];
  @Input() initial: MapObjectFormInitial | null = null;
  @Output() saved  = new EventEmitter<{ id: number; publicId: string }>();
  @Output() closed = new EventEmitter<void>();

  editMode = computed(() => !!this.initial?.publicId);
  isSaving = signal(false);

  private objectId: number | null = null;
  private publicId: string | null = null;
  private geometry = { positionX: 10, positionY: 10, width: 15, height: 10 };

  dataForm = new FormGroup({
    Name:       new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    Code:       new FormControl<string>('', { nonNullable: true }),
    FloorId:    new FormControl<number | null>(null, [Validators.required]),
    ObjectType: new FormControl<string>('ROOM', { nonNullable: true }),
    Category:   new FormControl<number>(4, { nonNullable: true }),
    ColorHex:   new FormControl<string>('#3b82f6', { nonNullable: true }),
    IconName:   new FormControl<string>('', { nonNullable: true }),
    StoreId:    new FormControl<number | null>(null),
  });

  shelfForm = new FormGroup({
    CategoryRange: new FormControl<string>('', { nonNullable: true }),
    SubjectName:   new FormControl<string>('', { nonNullable: true }),
    Capacity:      new FormControl<number>(100, { nonNullable: true }),
    Description:   new FormControl<string>('', { nonNullable: true }),
  });

  // ── Thiết bị của đối tượng (chỉ hiện khi sửa — đã có objectId) ──────────────
  equipmentList = signal<MapEquipment[]>([]);
  equipmentForm = { publicId: null as string | null, name: '', description: '', quantity: 1, conditionStatus: 'Sẵn sàng' };

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open'] && this.open) this.resetFromInitial();
  }

  private resetFromInitial(): void {
    const i = this.initial;
    this.objectId = i?.objectId ?? null;
    this.publicId = i?.publicId ?? null;
    this.geometry = {
      positionX: i?.positionX ?? 10, positionY: i?.positionY ?? 10,
      width: i?.width ?? 15, height: i?.height ?? 10,
    };
    this.dataForm.reset({
      Name: i?.name ?? '', Code: i?.code ?? '', FloorId: i?.floorId ?? null,
      ObjectType: i?.objectType ?? 'ROOM', Category: i?.category ?? 4,
      ColorHex: i?.colorHex ?? '#3b82f6', IconName: i?.iconName ?? '', StoreId: i?.storeId ?? null,
    });
    this.shelfForm.reset({
      CategoryRange: i?.shelfDetail?.categoryRange ?? '', SubjectName: i?.shelfDetail?.subjectName ?? '',
      Capacity: i?.shelfDetail?.capacity ?? 100, Description: i?.shelfDetail?.description ?? '',
    });
    this.equipmentForm = { publicId: null, name: '', description: '', quantity: 1, conditionStatus: 'Sẵn sàng' };
    if (this.objectId) this.loadEquipment(); else this.equipmentList.set([]);
  }

  loadEquipment(): void {
    if (!this.objectId) return;
    this.equipmentSvc.searchAllByObject(this.objectId).subscribe(list => this.equipmentList.set(list));
  }

  async submitEquipment(): Promise<void> {
    if (!this.objectId || !this.equipmentForm.name.trim()) return;
    const { publicId, ...rest } = this.equipmentForm;
    try {
      if (publicId) await firstValueFrom(this.equipmentSvc.update(publicId, { objectId: this.objectId, ...rest }));
      else await firstValueFrom(this.equipmentSvc.create({ objectId: this.objectId, ...rest }));
      this.equipmentForm = { publicId: null, name: '', description: '', quantity: 1, conditionStatus: 'Sẵn sàng' };
      this.loadEquipment();
    } catch { this.toastr.error(this.translate.instant(publicId ? 'COMMON.UPDATE_ERROR' : 'COMMON.ADD_ERROR')); }
  }

  startEditEquipment(eq: MapEquipment): void {
    this.equipmentForm = {
      publicId: eq.publicId ?? null, name: eq.name ?? '', description: eq.description ?? '',
      quantity: eq.quantity ?? 1, conditionStatus: eq.conditionStatus ?? 'Sẵn sàng',
    };
  }

  cancelEditEquipment(): void {
    this.equipmentForm = { publicId: null, name: '', description: '', quantity: 1, conditionStatus: 'Sẵn sàng' };
  }

  async deleteEquipmentItem(eq: MapEquipment): Promise<void> {
    if (!eq.publicId) return;
    try { await firstValueFrom(this.equipmentSvc.delete(eq.publicId)); this.loadEquipment(); }
    catch { this.toastr.error(this.translate.instant('COMMON.DELETE_ERROR')); }
  }

  cancel(): void { this.closed.emit(); }

  async onSubmit(): Promise<void> {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload = {
      name: v.Name, code: v.Code || undefined, floorId: v.FloorId!,
      objectType: v.ObjectType, category: v.Category, colorHex: v.ColorHex || undefined, iconName: v.IconName || undefined,
      storeId: v.ObjectType === 'SHELF' ? (v.StoreId ?? undefined) : undefined,
      positionX: this.geometry.positionX, positionY: this.geometry.positionY,
      width: this.geometry.width, height: this.geometry.height,
    };

    try {
      let objectId = this.objectId;
      let publicId = this.publicId;
      if (publicId !== null) {
        await firstValueFrom(this.service.update(publicId, payload));
      } else {
        const created = await firstValueFrom(this.service.create(payload));
        objectId = created?.id ?? created?.data?.id ?? null;
        publicId = created?.publicId ?? created?.data?.publicId ?? null;
      }

      if (v.ObjectType === 'SHELF' && objectId) {
        const shelfPayload = { objectId, ...this.shelfForm.getRawValue() };
        const shelfPublicId = this.initial?.shelfDetail?.publicId ?? null;
        if (shelfPublicId) await firstValueFrom(this.shelfDetailSvc.update(shelfPublicId, shelfPayload));
        else await firstValueFrom(this.shelfDetailSvc.create(shelfPayload));
      }

      this.isSaving.set(false);
      this.saved.emit({ id: objectId ?? 0, publicId: publicId ?? '' });
    } catch {
      this.isSaving.set(false);
      this.toastr.error(this.translate.instant(this.editMode() ? 'COMMON.UPDATE_ERROR' : 'COMMON.ADD_ERROR'));
    }
  }
}
