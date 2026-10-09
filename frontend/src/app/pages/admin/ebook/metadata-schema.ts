import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { SelectionModel } from '@angular/cdk/collections';
import { ToastrService } from '../../../services/shared/toastr.service';
import { MetadataSchemaService } from '../../../services/ebook/metadata-schema.service';
import { MetadataSchemaRegistry } from '../../../models/ebook/metadata-schema';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { DepartmentService } from '../../../services/system/department.service';
import { ListPageStateService } from '../../../services/shared/list-page-state.service';

@Component({
  selector: 'app-metadata-schema',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule],
  templateUrl: './metadata-schema.html'
})
export class MetadataSchemaPage implements OnInit, OnDestroy {
  private service = inject(MetadataSchemaService);
  private listState = inject(ListPageStateService);
  private toastr  = inject(ToastrService);
  private router  = inject(Router);
  private auth = inject(Auth);
  private departmentService = inject(DepartmentService);
  public translate = inject(TranslateService);
  private destroy$ = new Subject<void>();

  // Đơn vị (chỉ super-admin)
  isPrivileged = this.auth.isPrivileged();
  tenantOptions: { id: string; name: string }[] = [];
  tenantId: string | null = null;

  displayedColumns: string[] = this.auth.isPrivileged()
    ? ['select', 'id', 'nameSpace', 'shortId', 'descriptionVn', 'tenant', 'actions']
    : ['select', 'id', 'nameSpace', 'shortId', 'descriptionVn', 'actions'];
  dataSource: MetadataSchemaRegistry[] = [];
  selection = new SelectionModel<MetadataSchemaRegistry>(true, []);

  totalRecords = 0;
  pageSize     = 10;
  pageIndex    = 0;
  pageSizeOptions = [5, 10, 25, 50, 100];
  keyword = '';

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  showModal         = signal(false);
  editMode          = signal(false);
  currentId         = signal<string | null>(null);
  isLoading         = signal(false);
  isSaving          = signal(false);
  showConfirmDelete = signal(false);
  confirmDeleteId   = signal<string | null>(null);

  dataForm = new FormGroup({
    NameSpace:     new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    ShortId:       new FormControl<string>('', { nonNullable: true }),
    DescriptionVn: new FormControl<string>('', { nonNullable: true })
  });

  ngOnInit(): void {
    const saved = this.listState.recall('ebook.metadata-schema'); if (saved) { this.pageIndex = saved.pageIndex; this.pageSize = saved.pageSize; }
    if (this.isPrivileged) {
      this.departmentService.getAll({ draw: 1, start: 0, length: 500, search: { value: '', regex: false } })
        .pipe(takeUntil(this.destroy$))
        .subscribe(res => this.tenantOptions = res.data.map((t: any) => ({ id: t.publicId ?? t.id, name: t.name })));
    }
    this.loadData();
  }

  onTenantChange(): void {
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadData();
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

  loadData(): void {
    this.listState.remember('ebook.metadata-schema', this.pageIndex, this.pageSize);
    this.isLoading.set(true);
    this.service.search(this.keyword, this.pageIndex + 1, this.pageSize, this.tenantId)
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
    const el = document.getElementById('schemaSearch') as HTMLInputElement;
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

  openAddModal(): void {
    this.editMode.set(false);
    this.currentId.set(null);
    this.dataForm.reset();
    this.showModal.set(true);
  }

  openEditModal(item: MetadataSchemaRegistry): void {
    this.editMode.set(true);
    this.currentId.set(item.publicId ?? null);
    this.dataForm.patchValue({
      NameSpace:     item.nameSpace     || '',
      ShortId:       item.shortId       || '',
      DescriptionVn: item.descriptionVn || ''
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
    const payload: Partial<MetadataSchemaRegistry> = {
      nameSpace:     v.NameSpace,
      shortId:       v.ShortId,
      descriptionVn: v.DescriptionVn || undefined
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

  goToFields(publicId: string | undefined): void {
    if (!publicId) return;
    this.router.navigate(['/admin/ebook-metadata-fields', publicId]);
  }

  handleDelete(publicId: string): void {
    this.confirmDeleteId.set(publicId);
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
      forkJoin(this.selection.selected.map(item => this.service.delete(item.publicId ?? String(item.metadataSchemaId)))).subscribe({
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
}
