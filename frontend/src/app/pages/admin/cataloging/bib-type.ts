import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { SelectionModel } from '@angular/cdk/collections';
import { ToastrService } from '../../../services/shared/toastr.service';
import { BibTypeService } from '../../../services/cataloging/bib-type.service';
import { MarcTypeService } from '../../../services/cataloging/marc-type.service';
import { MaterialsTypeService } from '../../../services/cataloging/materials-type.service';
import { MarcBibLevelService } from '../../../services/cataloging/marc-bib-level.service';
import { MarcRecordTypeService } from '../../../services/cataloging/marc-record-type.service';
import { BibType } from '../../../models/cataloging/bib-type';
import { MarcType } from '../../../models/cataloging/marc-type';
import { MaterialsType } from '../../../models/cataloging/materials-type';
import { MarcBibLevel } from '../../../models/cataloging/marc-bib-level';
import { MarcRecordType } from '../../../models/cataloging/marc-record-type';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-bib-type',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './bib-type.html'
})
export class BibTypePage implements OnInit, OnDestroy {
  private service            = inject(BibTypeService);
  private marcTypeSvc        = inject(MarcTypeService);
  private materialsTypeSvc   = inject(MaterialsTypeService);
  private marcBibLevelSvc    = inject(MarcBibLevelService);
  private marcRecordTypeSvc  = inject(MarcRecordTypeService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private auth      = inject(Auth);
  private destroy$  = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  marcTypes        = signal<MarcType[]>([]);
  materialsTypes    = signal<MaterialsType[]>([]);
  marcBibLevels     = signal<MarcBibLevel[]>([]);
  marcRecordTypes   = signal<MarcRecordType[]>([]);

  displayedColumns = ['select', 'stt', 'name', 'code', 'type', 'bib_Level', 'materal_Type', 'record_Type_Code', 'actions'];
  dataSource: BibType[] = [];
  selection = new SelectionModel<BibType>(true, []);

  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  showModal = signal(false);
  editMode = signal(false);
  currentPublicId = signal<string | null>(null);
  isLoading = signal(false);
  isSaving = signal(false);
  showConfirmDelete = signal(false);
  confirmDeletePublicId = signal<string | null>(null);

  searchForm = new FormGroup({ keyword: new FormControl<string>('', { nonNullable: true }) });
  dataForm = new FormGroup({
    Name:             new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    Code:             new FormControl<string>('', { nonNullable: true }),
    TypeCode:         new FormControl<string>('', { nonNullable: true }),
    BibLevelCode:     new FormControl<string>('', { nonNullable: true }),
    MaterialTypeCode: new FormControl<string>('', { nonNullable: true }),
    RecordTypeCode:   new FormControl<string>('', { nonNullable: true }),
  });

  ngOnInit(): void { applyTenantColumn(this.displayedColumns, this.isPrivileged); this.loadData(); this.loadLookups(); }
  onTenantChange(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }

  loadLookups(): void {
    this.marcTypeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.marcTypes.set(list), error: () => {} });
    this.materialsTypeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.materialsTypes.set(list), error: () => {} });
    this.marcBibLevelSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.marcBibLevels.set(list), error: () => {} });
    this.marcRecordTypeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.marcRecordTypes.set(list), error: () => {} });
  }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadData(): void {
    this.isLoading.set(true);
    this.service.search({ keyword: this.searchForm.getRawValue().keyword || null, pageIndex: this.pageIndex + 1, pageSize: this.pageSize, tenantId: this.tenantId })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }
  isAllSelected(): boolean { return this.selection.selected.length > 0 && this.selection.selected.length === this.dataSource.length; }
  masterToggle(): void { this.isAllSelected() ? this.selection.clear() : this.dataSource.forEach(r => this.selection.select(r)); }

  openAddModal(): void { this.editMode.set(false); this.currentPublicId.set(null); this.dataForm.reset(); this.showModal.set(true); }
  openEditModal(item: BibType): void {
    if (!item.publicId) return;
    this.editMode.set(true); this.currentPublicId.set(item.publicId);
    this.service.getById(item.publicId).pipe(takeUntil(this.destroy$)).subscribe(d => {
      this.dataForm.patchValue({
        Name: d.name ?? '', Code: d.code ?? '', TypeCode: d.type ?? '',
        BibLevelCode: d.bib_Level ?? '', MaterialTypeCode: d.materal_Type ?? '', RecordTypeCode: d.record_Type_Code ?? '',
      });
      this.showModal.set(true);
    });
  }
  closeModal(): void { this.showModal.set(false); this.dataForm.reset(); }

  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload: Partial<BibType> = {
      name: v.Name, code: v.Code || undefined, type: v.TypeCode || undefined,
      bib_Level: v.BibLevelCode || undefined, materal_Type: v.MaterialTypeCode || undefined, record_Type_Code: v.RecordTypeCode || undefined,
    };
    const publicId = this.currentPublicId(); const mode = this.editMode();
    const req$ = mode && publicId !== null ? this.service.update(publicId, payload) : this.service.create(payload);
    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isSaving.set(false); this.closeModal(); (mode ? this.loadData() : this.triggerSearch()); this.toastr.success(this.translate.instant(mode ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS')); },
      error: () => { this.isSaving.set(false); }
    });
  }

  handleDelete(item: BibType): void { if (!item.publicId) return; this.confirmDeletePublicId.set(item.publicId); this.showConfirmDelete.set(true); }
  deleteSelected(): void { if (!this.selection.selected.length) return; this.confirmDeletePublicId.set(null); this.showConfirmDelete.set(true); }
  closeConfirm(): void { this.showConfirmDelete.set(false); this.confirmDeletePublicId.set(null); }
  confirmActionExecute(): void {
    const publicId = this.confirmDeletePublicId();
    if (publicId === null) {
      const reqs = this.selection.selected.filter(i => !!i.publicId).map(i => this.service.delete(i.publicId!));
      forkJoin(reqs).pipe(takeUntil(this.destroy$)).subscribe({
        next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.selection.clear(); this.closeConfirm(); this.loadData(); },
        error: () => { this.closeConfirm(); this.loadData(); }
      });
      return;
    }
    this.service.delete(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.closeConfirm(); this.loadData(); },
      error: () => { this.closeConfirm(); }
    });
  }
}
