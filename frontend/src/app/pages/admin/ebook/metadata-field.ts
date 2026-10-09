import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Router, ActivatedRoute } from '@angular/router';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { SelectionModel } from '@angular/cdk/collections';
import { ToastrService } from '../../../services/shared/toastr.service';
import { MetadataFieldService } from '../../../services/ebook/metadata-field.service';
import { MetadataSchemaService } from '../../../services/ebook/metadata-schema.service';
import { MetaDataFieldRegistery } from '../../../models/ebook/metadata-field';
import { CanDirective } from '../../../directives/can.directive';

@Component({
  selector: 'app-metadata-field',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule],
  templateUrl: './metadata-field.html'
})
export class MetadataFieldPage implements OnInit, OnDestroy {
  private service       = inject(MetadataFieldService);
  private schemaService = inject(MetadataSchemaService);
  private toastr        = inject(ToastrService);
  private router        = inject(Router);
  private route         = inject(ActivatedRoute);
  public translate      = inject(TranslateService);
  private destroy$      = new Subject<void>();

  currentSchemaPublicId = '';
  currentSchemaId       = 0;
  currentSchemaName     = '';

  displayedColumns: string[] = ['select', 'id', 'field', 'subfield', 'description', 'sortOrder', 'status', 'exportField', 'actions'];
  dataSource: MetaDataFieldRegistery[] = [];
  selection = new SelectionModel<MetaDataFieldRegistery>(true, []);

  totalRecords    = 0;
  pageSize        = 10;
  pageIndex       = 0;
  pageSizeOptions = [5, 10, 25, 50, 100];
  keyword         = '';

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  showModal         = signal(false);
  editMode          = signal(false);
  currentId         = signal<number | null>(null);
  isLoading         = signal(false);
  isSaving          = signal(false);
  showConfirmDelete = signal(false);
  confirmDeleteId   = signal<number | null>(null);

  dataForm = new FormGroup({
    Field:         new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    Subfield:      new FormControl<string>('', { nonNullable: true }),
    DescriptionVn: new FormControl<string>('', { nonNullable: true }),
    DescriptionEn: new FormControl<string>('', { nonNullable: true }),
    SortOrder:     new FormControl<number | null>(null),
    Status:        new FormControl<number>(2, { nonNullable: true }),
    Input:         new FormControl<string>('', { nonNullable: true }),
    ExportField:   new FormControl<number>(0, { nonNullable: true })
  });

  ngOnInit(): void {
    this.currentSchemaPublicId = this.route.snapshot.paramMap.get('schemaId') || '';
    this.loadSchemaName();
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize  = event.pageSize;
    this.loadData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadSchemaName(): void {
    if (!this.currentSchemaPublicId) return;
    this.schemaService.getById(this.currentSchemaPublicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: schema => {
        this.currentSchemaId   = schema?.metadataSchemaId ?? 0;
        this.currentSchemaName = schema?.nameSpace || schema?.shortId || this.currentSchemaPublicId;
        this.loadData();
      },
      error: () => {
        this.currentSchemaName = this.currentSchemaPublicId;
      }
    });
  }

  loadData(): void {
    this.isLoading.set(true);
    this.service.search(this.currentSchemaId, this.keyword, this.pageIndex + 1, this.pageSize)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: res => {
          this.dataSource   = res.data;
          this.totalRecords = res.recordsTotal;
          this.isLoading.set(false);
        },
        error: () => {
          this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
          this.isLoading.set(false);
        }
      });
  }

  triggerSearch(): void {
    const el = document.getElementById('fieldSearch') as HTMLInputElement;
    this.keyword   = el?.value || '';
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadData();
  }

  isAllSelected(): boolean {
    return this.selection.selected.length === this.dataSource.length && this.dataSource.length > 0;
  }

  masterToggle(): void {
    this.isAllSelected() ? this.selection.clear() : this.dataSource.forEach(r => this.selection.select(r));
  }

  goBack(): void {
    this.router.navigate(['/admin/ebook-metadata-schemas']);
  }

  openAddModal(): void {
    this.editMode.set(false);
    this.currentId.set(null);
    this.dataForm.reset({ Status: 2, ExportField: 0, SortOrder: null });
    this.showModal.set(true);
  }

  openEditModal(item: MetaDataFieldRegistery): void {
    this.editMode.set(true);
    this.currentId.set(item.metaDataFieldId);
    this.dataForm.patchValue({
      Field:         item.field         || '',
      Subfield:      item.subfield      || '',
      DescriptionVn: item.descriptionVn || '',
      DescriptionEn: item.descriptionEn || '',
      SortOrder:     item.sortOrder     ?? null,
      Status:        item.status        ?? 2,
      Input:         item.input         || '',
      ExportField:   item.exportField   ?? 0
    });
    this.showModal.set(true);
  }

  closeModal(): void {
    this.showModal.set(false);
    this.dataForm.reset();
  }

  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    this.isSaving.set(true);
    const v = this.dataForm.getRawValue();
    const payload: Partial<MetaDataFieldRegistery> = {
      metaDataSchemaId: this.currentSchemaId,
      field:            v.Field,
      subfield:         v.Subfield,
      descriptionVn:    v.DescriptionVn,
      descriptionEn:    v.DescriptionEn,
      sortOrder:        v.SortOrder,
      status:           v.Status,
      input:            v.Input,
      exportField:      v.ExportField
    };

    const req$ = this.editMode() && this.currentId()
      ? this.service.update(this.currentId()!, payload)
      : this.service.create(payload);

    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.closeModal();
        if (!this.editMode()) {
          this.pageIndex = 0;
          if (this.paginator) this.paginator.pageIndex = 0;
        }
        this.loadData();
        this.toastr.success(this.translate.instant(this.editMode() ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS'));
      },
      error: () => {
        this.isSaving.set(false);
      }
    });
  }

  handleDelete(id: number): void {
    this.confirmDeleteId.set(id);
    this.showConfirmDelete.set(true);
  }

  deleteSelected(): void {
    if (!this.selection.selected.length) return;
    this.confirmDeleteId.set(null);
    this.showConfirmDelete.set(true);
  }

  closeConfirm(): void {
    this.showConfirmDelete.set(false);
    this.confirmDeleteId.set(null);
  }

  confirmActionExecute(): void {
    const id = this.confirmDeleteId();
    if (id === null) {
      forkJoin(this.selection.selected.map(item => this.service.delete(item.metaDataFieldId))).subscribe({
        next: () => {
          this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
          this.selection.clear();
          this.closeConfirm();
          this.loadData();
        },
        error: () => {
          this.closeConfirm();
          this.loadData();
        }
      });
      return;
    }
    this.service.delete(id).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
        this.closeConfirm();
        this.loadData();
      },
      error: () => {
        this.closeConfirm();
      }
    });
  }

  isActive(item: MetaDataFieldRegistery): boolean {
    return (item.status ?? 2) === 2;
  }
}
