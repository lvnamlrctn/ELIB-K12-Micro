import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { Z3950ConfigService } from '../../../services/cataloging/z3950-config.service';
import { Z3950GroupService } from '../../../services/acquisition/z3950-group.service';
import { Z3950Config } from '../../../models/cataloging/z3950-config';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-z3950-config',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './z3950-config.html'
})
export class Z3950ConfigPage implements OnInit, OnDestroy {
  private service   = inject(Z3950ConfigService);
  private groupSvc  = inject(Z3950GroupService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private auth      = inject(Auth);
  private destroy$  = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['stt', 'name', 'host', 'database', 'group', 'actions'];
  dataSource: Z3950Config[] = [];
  groups = signal<{ id: number; name: string }[]>([]);
  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  isLoading = signal(false);

  showModal = signal(false);
  editMode = signal(false);
  currentPublicId = signal<string | null>(null);
  isSaving = signal(false);
  showConfirmDelete = signal(false);
  confirmDeletePublicId = signal<string | null>(null);

  searchForm = new FormGroup({ keyword: new FormControl<string>('', { nonNullable: true }) });
  dataForm = new FormGroup({
    Name:         new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    Host:         new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    Port:         new FormControl<string>('210', { nonNullable: true }),
    DatabaseName: new FormControl<string>('', { nonNullable: true }),
    Systax:       new FormControl<string>('USMARC', { nonNullable: true }),
    UserName:     new FormControl<string>('', { nonNullable: true }),
    Password:     new FormControl<string>('', { nonNullable: true }),
    Url:          new FormControl<string>('', { nonNullable: true }),
    Language:     new FormControl<string>('', { nonNullable: true }),
    GroupId:      new FormControl<number | null>(null),
  });

  ngOnInit(): void { applyTenantColumn(this.displayedColumns, this.isPrivileged); this.loadGroups(); this.loadData(); }
  onTenantChange(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadGroups(): void {
    this.groupSvc.getAll({ draw: 0, start: 0, length: 1000, search: { value: '' } }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => this.groups.set((res.data || []).map(g => ({ id: Number(g.id), name: g.name }))), error: () => {}
    });
  }
  getGroupName(id: number | null | undefined): string { if (!id) return '—'; return this.groups().find(g => g.id === id)?.name || '—'; }

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

  openAddModal(): void { this.editMode.set(false); this.currentPublicId.set(null); this.dataForm.reset({ Name: '', Host: '', Port: '210', DatabaseName: '', Systax: 'USMARC', UserName: '', Password: '', Url: '', Language: '', GroupId: null }); this.showModal.set(true); }
  openEditModal(item: Z3950Config): void {
    if (!item.publicId) return;
    this.editMode.set(true); this.currentPublicId.set(item.publicId);
    this.service.getById(item.publicId).pipe(takeUntil(this.destroy$)).subscribe(d => {
      this.dataForm.patchValue({ Name: d.name ?? '', Host: d.host ?? '', Port: d.port ?? '', DatabaseName: d.databaseName ?? '', Systax: d.systax ?? '', UserName: d.userName ?? '', Password: d.password ?? '', Url: d.url ?? '', Language: d.language ?? '', GroupId: d.groupId ?? null });
      this.showModal.set(true);
    });
  }
  closeModal(): void { this.showModal.set(false); }

  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload: Partial<Z3950Config> = { name: v.Name, host: v.Host, port: v.Port, databaseName: v.DatabaseName, systax: v.Systax, userName: v.UserName, password: v.Password, url: v.Url, language: v.Language, groupId: v.GroupId ?? undefined };
    const publicId = this.currentPublicId(); const mode = this.editMode();
    const req$ = mode && publicId !== null ? this.service.update(publicId, payload) : this.service.create(payload);
    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isSaving.set(false); this.closeModal(); (mode ? this.loadData() : this.triggerSearch()); this.toastr.success(this.translate.instant(mode ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS')); },
      error: () => { this.isSaving.set(false); }
    });
  }

  handleDelete(item: Z3950Config): void { if (!item.publicId) return; this.confirmDeletePublicId.set(item.publicId); this.showConfirmDelete.set(true); }
  closeConfirm(): void { this.showConfirmDelete.set(false); this.confirmDeletePublicId.set(null); }
  confirmActionExecute(): void {
    const publicId = this.confirmDeletePublicId(); if (publicId == null) return;
    this.service.delete(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.closeConfirm(); this.loadData(); },
      error: () => { this.closeConfirm(); }
    });
  }
}
