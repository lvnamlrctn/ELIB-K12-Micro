import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { SelectionModel } from '@angular/cdk/collections';
import { ToastrService } from '../../../services/shared/toastr.service';
import { ConfigReceiptionService } from '../../../services/printbook/config-receiption.service';
import { CircPlaceService } from '../../../services/printbook/circ-place.service';
import { UserService } from '../../../services/system/user.service';
import { ConfigReceiption } from '../../../models/printbook/config-receiption';
import { CircPlace } from '../../../models/printbook/circ-place';
import { User } from '../../../models/system/user';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-config-receiption',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, NgSelectModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, TenantFilterSelectComponent],
  templateUrl: './config-receiption.html'
})
export class ConfigReceiptionPage implements OnInit, OnDestroy {
  private service       = inject(ConfigReceiptionService);
  private circPlaceSvc  = inject(CircPlaceService);
  private userSvc       = inject(UserService);
  private toastr        = inject(ToastrService);
  public  translate     = inject(TranslateService);
  private auth          = inject(Auth);
  private destroy$      = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['select', 'stt', 'userName', 'circPlaceName', 'checkType', 'autoCheck', 'requirePassword', 'actions'];
  dataSource:  ConfigReceiption[] = [];
  selection    = new SelectionModel<ConfigReceiption>(true, []);

  circPlaces   = signal<CircPlace[]>([]);
  users        = signal<User[]>([]);

  totalRecords    = 0;
  pageSize        = 10;
  pageIndex       = 0;
  pageSizeOptions = [5, 10, 25, 50];

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  showModal             = signal(false);
  editMode              = signal(false);
  currentPublicId       = signal<string | null>(null);
  isLoading             = signal(false);
  isSaving              = signal(false);
  showConfirmDelete     = signal(false);
  confirmDeletePublicId = signal<string | null>(null);

  searchForm = new FormGroup({
    keyword: new FormControl<string>('', { nonNullable: true }),
  });

  dataForm = new FormGroup({
    CircPlaceId:     new FormControl<number | null>(null),
    CheckType:       new FormControl<number | null>(null),
    AutoCheck:       new FormControl<number | null>(null),
    RequirePassword: new FormControl<number | null>(null),
    UserId:          new FormControl<number | null>(null),
  });

  ngOnInit(): void {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    this.loadCircPlaces();
    this.loadUsers();
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

  loadCircPlaces(): void {
    this.circPlaceSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe(data => this.circPlaces.set(data));
  }

  loadUsers(): void {
    this.userSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe(data => this.users.set(data));
  }

  getUserName(userId?: number): string {
    if (userId == null) return '—';
    const found = this.users().find(u => Number(u.id) === userId);
    return found?.fullName || found?.loginName || String(userId);
  }

  loadData(): void {
    this.isLoading.set(true);
    const s = this.searchForm.getRawValue();
    this.service.search({
      keyword:   s.keyword || null,
      pageIndex: this.pageIndex + 1,
      pageSize:  this.pageSize,
      tenantId:  this.tenantId,
    }).pipe(takeUntil(this.destroy$)).subscribe({
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
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadData();
  }

  getRowIndex(i: number): number {
    return this.pageIndex * this.pageSize + i + 1;
  }

  isAllSelected(): boolean {
    return this.selection.selected.length > 0 && this.selection.selected.length === this.dataSource.length;
  }

  masterToggle(): void {
    this.isAllSelected() ? this.selection.clear() : this.dataSource.forEach(r => this.selection.select(r));
  }

  getCircPlaceName(circPlaceId?: number): string {
    if (circPlaceId == null) return '—';
    const found = this.circPlaces().find(c => c.id === circPlaceId);
    return found?.name ?? String(circPlaceId);
  }

  openAddModal(): void {
    this.editMode.set(false);
    this.currentPublicId.set(null);
    this.dataForm.reset();
    this.showModal.set(true);
  }

  openEditModal(item: ConfigReceiption): void {
    if (!item.publicId) return;
    this.editMode.set(true);
    this.currentPublicId.set(item.publicId);
    this.service.getById(item.publicId).pipe(takeUntil(this.destroy$)).subscribe(data => {
      this.dataForm.patchValue({
        CircPlaceId:     data.circPlaceId     ?? null,
        CheckType:       data.checkType       ?? null,
        AutoCheck:       data.autoCheck       ?? null,
        RequirePassword: data.requirePassword ?? null,
        UserId:          data.userId          ?? null,
      });
      this.showModal.set(true);
    });
  }

  closeModal(): void {
    this.showModal.set(false);
    this.dataForm.reset();
  }

  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload: Partial<ConfigReceiption> = {
      circPlaceId:     v.CircPlaceId     ?? undefined,
      checkType:       v.CheckType       ?? undefined,
      autoCheck:       v.AutoCheck       ?? undefined,
      requirePassword: v.RequirePassword ?? undefined,
      userId:          v.UserId          ?? undefined,
    };

    const publicId = this.currentPublicId();
    const mode     = this.editMode();
    const req$ = mode && publicId !== null
      ? this.service.update(publicId, payload)
      : this.service.create(payload);

    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.closeModal();
        if (!mode) {
          this.pageIndex = 0;
          if (this.paginator) this.paginator.pageIndex = 0;
        }
        this.loadData();
        this.toastr.success(this.translate.instant(mode ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS'));
      },
      error: () => {
        this.isSaving.set(false);
      }
    });
  }

  handleDelete(item: ConfigReceiption): void {
    if (!item.publicId) return;
    this.confirmDeletePublicId.set(item.publicId);
    this.showConfirmDelete.set(true);
  }

  deleteSelected(): void {
    if (!this.selection.selected.length) return;
    this.confirmDeletePublicId.set(null);
    this.showConfirmDelete.set(true);
  }

  closeConfirm(): void {
    this.showConfirmDelete.set(false);
    this.confirmDeletePublicId.set(null);
  }

  confirmActionExecute(): void {
    const publicId = this.confirmDeletePublicId();
    if (publicId === null) {
      const reqs = this.selection.selected
        .filter(item => !!item.publicId)
        .map(item => this.service.delete(item.publicId!));
      forkJoin(reqs).pipe(takeUntil(this.destroy$)).subscribe({
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
    this.service.delete(publicId).pipe(takeUntil(this.destroy$)).subscribe({
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
