import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { RoomBookingConfigService } from '../../../services/map/room-booking-config.service';
import { RoomBookingAdminService } from '../../../services/map/room-booking.service';
import { ReaderTypeService } from '../../../services/circulation/reader-type.service';
import { AffectedBooking } from '../../../models/map/room-booking';
import { FormsModule } from '@angular/forms';
import { MapObjectService } from '../../../services/map/map-object.service';
import { RoomBookingConfig } from '../../../models/map/room-booking-config';
import { MapObject } from '../../../models/map/map-object';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-room-booking-config',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './room-booking-config.html'
})
export class RoomBookingConfigPage implements OnInit, OnDestroy {
  private service       = inject(RoomBookingConfigService);
  private adminSvc      = inject(RoomBookingAdminService);
  private readerTypeSvc = inject(ReaderTypeService);
  private mapObjectSvc  = inject(MapObjectService);
  private toastr        = inject(ToastrService);
  public  translate     = inject(TranslateService);
  private auth          = inject(Auth);
  private destroy$      = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['stt', 'roomName', 'capacity', 'slotMinutes', 'maxAdvanceDays', 'checkInGraceMinutes', 'roomType', 'audience', 'actions'];
  dataSource: RoomBookingConfig[] = [];
  mapObjects = signal<MapObject[]>([]);
  readerTypes = signal<{ id: number; name: string }[]>([]);

  // Tạm ngưng phòng (port ELIB-LRC 10-04)
  maintenanceTarget = signal<RoomBookingConfig | null>(null);
  maintenanceNote = '';
  maintenanceCancel = true;
  maintenanceAffected = signal<AffectedBooking[] | null>(null);

  totalRecords    = 0;
  pageSize        = 10;
  pageIndex       = 0;
  pageSizeOptions = [5, 10, 25, 50];

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  isLoading        = signal(false);
  isSaving         = signal(false);
  showFormModal    = signal(false);
  editMode         = signal(false);
  currentPublicId  = signal<string | null>(null);

  showConfirmDelete     = signal(false);
  confirmDeleteTarget   = signal<RoomBookingConfig | null>(null);

  searchForm = new FormGroup({
    mapObjectId: new FormControl<number | null>(null),
  });

  configForm = new FormGroup({
    MapObjectId: new FormControl<number | null>(null, Validators.required),
    Capacity: new FormControl<number>(4, [Validators.required, Validators.min(1)]),
    SlotMinutes: new FormControl<number>(60, [Validators.required, Validators.min(15)]),
    MinAdvanceMinutes: new FormControl<number>(0, [Validators.required, Validators.min(0)]),
    MaxAdvanceDays: new FormControl<number>(7, [Validators.required, Validators.min(1)]),
    MaxBookingMinutesPerReader: new FormControl<number>(0, [Validators.required, Validators.min(0)]),
    CheckInGraceMinutes: new FormControl<number>(15, [Validators.required, Validators.min(0)]),
    AutoApprove: new FormControl<boolean>(false, { nonNullable: true }),
    MaxAdvanceHours: new FormControl<number | null>(null, [Validators.min(0)]),
    Rules: new FormControl<string>('', { nonNullable: true }),
    AllowedReaderTypeIds: new FormControl<number[]>([], { nonNullable: true }),
    MinGroupSize: new FormControl<number | null>(null, [Validators.min(1)]),
  });

  ngOnInit(): void {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    this.loadMapObjects();
    this.loadReaderTypes();
    this.loadData();
  }

  loadReaderTypes(): void {
    this.readerTypeSvc.getAll({ draw: 0, start: 0, length: 1000, search: { value: '' } } as any).pipe(takeUntil(this.destroy$)).subscribe({
      next: (res: any) => this.readerTypes.set((res.data ?? []).map((t: any) => ({ id: Number(t.id), name: t.name }))),
      error: () => {}
    });
  }

  /** "1,3" → tên loại bạn đọc; trống = mọi đối tượng. */
  audienceLabel(raw: string | null | undefined): string {
    const ids = (raw ?? '').split(',').map(x => Number(x.trim())).filter(x => x > 0);
    if (ids.length === 0) return this.translate.instant('STUDY_ROOM_BOOKING.ALL_READERS');
    return ids.map(id => this.readerTypes().find(t => t.id === id)?.name ?? `#${id}`).join(', ');
  }

  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadMapObjects(): void {
    this.mapObjectSvc.search({ pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => this.mapObjects.set(res.data),
      error: () => {}
    });
  }

  roomName(mapObjectId: number): string {
    return this.mapObjects().find(o => o.id === mapObjectId)?.name || `#${mapObjectId}`;
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize  = event.pageSize;
    this.loadData();
  }

  onTenantChange(): void { this.triggerSearch(); }

  loadData(): void {
    this.isLoading.set(true);
    const s = this.searchForm.getRawValue();
    this.service.search({ mapObjectId: s.mapObjectId, tenantId: this.tenantId, pageIndex: this.pageIndex + 1, pageSize: this.pageSize })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }

  triggerSearch(): void {
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadData();
  }

  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  openAddModal(): void {
    this.editMode.set(false);
    this.currentPublicId.set(null);
    this.configForm.reset({
      MapObjectId: null, Capacity: 4, SlotMinutes: 60, MinAdvanceMinutes: 0,
      MaxAdvanceDays: 7, MaxBookingMinutesPerReader: 0, CheckInGraceMinutes: 15, AutoApprove: false,
      MaxAdvanceHours: null, Rules: '', AllowedReaderTypeIds: [], MinGroupSize: null
    });
    this.showFormModal.set(true);
  }

  openEditModal(c: RoomBookingConfig): void {
    this.editMode.set(true);
    this.currentPublicId.set(c.publicId ?? null);
    this.configForm.patchValue({
      MapObjectId: c.mapObjectId, Capacity: c.capacity, SlotMinutes: c.slotMinutes,
      MinAdvanceMinutes: c.minAdvanceMinutes, MaxAdvanceDays: c.maxAdvanceDays,
      MaxBookingMinutesPerReader: c.maxBookingMinutesPerReader, CheckInGraceMinutes: c.checkInGraceMinutes,
      AutoApprove: !!c.autoApprove,
      MaxAdvanceHours: c.maxAdvanceHours ?? null,
      Rules: c.rules ?? '',
      AllowedReaderTypeIds: (c.allowedReaderTypeIds ?? '').split(',').map(x => Number(x.trim())).filter(x => x > 0),
      MinGroupSize: c.minGroupSize ?? null
    });
    this.showFormModal.set(true);
  }

  closeFormModal(): void { this.showFormModal.set(false); }

  submitForm(): void {
    if (this.configForm.invalid) { this.configForm.markAllAsTouched(); return; }
    this.isSaving.set(true);
    const v = this.configForm.getRawValue();
    const payload: Partial<RoomBookingConfig> = {
      mapObjectId: v.MapObjectId!, capacity: v.Capacity!, slotMinutes: v.SlotMinutes!,
      minAdvanceMinutes: v.MinAdvanceMinutes!, maxAdvanceDays: v.MaxAdvanceDays!,
      maxBookingMinutesPerReader: v.MaxBookingMinutesPerReader!, checkInGraceMinutes: v.CheckInGraceMinutes!,
      autoApprove: v.AutoApprove,
      maxAdvanceHours: v.MaxAdvanceHours ? Number(v.MaxAdvanceHours) : null,
      rules: v.Rules?.trim() || null,
      allowedReaderTypeIds: v.AllowedReaderTypeIds.length ? v.AllowedReaderTypeIds.join(',') : null,
      minGroupSize: v.MinGroupSize ? Number(v.MinGroupSize) : null
    };
    const req$ = this.editMode() && this.currentPublicId()
      ? this.service.update(this.currentPublicId()!, payload)
      : this.service.create(payload);
    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.closeFormModal();
        if (!this.editMode()) { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; }
        this.loadData();
        this.toastr.success(this.translate.instant(this.editMode() ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS'));
      },
      error: (err) => { this.isSaving.set(false); this.toastr.error(err?.error?.message || this.translate.instant('COMMON.SAVE_ERROR')); }
    });
  }

  handleDelete(c: RoomBookingConfig): void {
    this.confirmDeleteTarget.set(c);
    this.showConfirmDelete.set(true);
  }

  closeConfirm(): void { this.showConfirmDelete.set(false); this.confirmDeleteTarget.set(null); }

  confirmDeleteExecute(): void {
    const c = this.confirmDeleteTarget();
    if (!c?.publicId) return;
    this.service.delete(c.publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.closeConfirm(); this.loadData(); },
      error: () => { this.closeConfirm(); }
    });
  }

  // ── Tạm ngưng / mở lại phòng (bảo trì) ─────────────────────────────────
  toggleMaintenance(c: RoomBookingConfig): void {
    if (!c.publicId) return;
    if (c.maintenance) {
      this.adminSvc.maintenance(c.publicId, { on: false }, false).pipe(takeUntil(this.destroy$)).subscribe({
        next: () => { this.toastr.success(this.translate.instant('STUDY_ROOM_BOOKING.REOPENED')); this.loadData(); }, error: () => {}
      });
      return;
    }
    this.maintenanceTarget.set(c);
    this.maintenanceNote = '';
    this.maintenanceCancel = true;
    this.maintenanceAffected.set(null);
    this.adminSvc.maintenance(c.publicId, { on: true }, true).pipe(takeUntil(this.destroy$)).subscribe({
      next: r => this.maintenanceAffected.set(r.affected), error: () => this.maintenanceAffected.set([])
    });
  }

  confirmMaintenance(): void {
    const c = this.maintenanceTarget();
    if (!c?.publicId) return;
    this.adminSvc.maintenance(c.publicId, { on: true, note: this.maintenanceNote.trim() || null, cancelAffected: this.maintenanceCancel }, false)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: r => {
          this.maintenanceTarget.set(null);
          this.toastr.success(r.cancelled > 0
            ? this.translate.instant('STUDY_ROOM_BOOKING.DAY_SAVED_CANCELLED', { count: r.cancelled })
            : this.translate.instant('COMMON.UPDATE_SUCCESS'));
          this.loadData();
        },
        error: () => {}
      });
  }
}
