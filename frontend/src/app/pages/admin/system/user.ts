import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil, forkJoin } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { SelectionModel } from '@angular/cdk/collections';
import { ToastrService } from '../../../services/shared/toastr.service';
import { UserService, PermRow } from '../../../services/system/user.service';
import { RoleService } from '../../../services/system/role.service';
import { ChucVuService, ChucVu } from '../../../services/system/chuc-vu.service';
import { User } from '../../../models/system/user';
import { environment } from '../../../../environments/environment';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { DepartmentService } from '../../../services/system/department.service';

@Component({
  selector: 'app-user',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule, DateInputComponent],
  templateUrl: './user.html'
})
export class UserPage implements OnInit, OnDestroy {
  private service    = inject(UserService);
  private roleSvc    = inject(RoleService);
  private chucVuSvc  = inject(ChucVuService);
  private toastr     = inject(ToastrService);
  private auth       = inject(Auth);
  private departmentService = inject(DepartmentService);
  public  translate  = inject(TranslateService);
  private destroy$   = new Subject<void>();

  readonly imageUrl = environment.imageUrl;

  // Đơn vị (chỉ super-admin)
  isPrivileged = this.auth.isPrivileged();
  tenantOptions: { id: string; name: string }[] = [];

  displayedColumns = this.auth.isPrivileged()
    ? ['select', 'stt', 'photo', 'info', 'contact', 'role', 'tenant', 'status', 'actions']
    : ['select', 'stt', 'photo', 'info', 'contact', 'role', 'status', 'actions'];
  dataSource: User[] = [];
  selection = new SelectionModel<User>(true, []);

  totalRecords    = 0;
  pageSize        = 10;
  pageIndex       = 0;
  pageSizeOptions = [5, 10, 25, 50, 100];

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  showModal         = signal(false);
  editMode          = signal(false);
  currentId         = signal<string | number | null>(null);
  isLoading         = signal(false);
  isSaving          = signal(false);
  isExporting       = signal(false);
  showConfirmDelete = signal(false);
  confirmDeleteId   = signal<string | number | null>(null);
  showResetConfirm      = signal(false);
  resetTargetId         = signal<string | number | null>(null);
  showBulkResetConfirm  = signal(false);
  loginNameExists       = signal(false);
  isCheckingLogin       = signal(false);

  photoFile        = signal<File | null>(null);
  photoPreviewUrl  = signal<string | null>(null);
  photoPath        = signal<string | null>(null);
  uploadingPhoto   = signal(false);

  showImportModal  = signal(false);
  importFile       = signal<File | null>(null);
  isImporting      = signal(false);

  showPermModal  = signal(false);
  permUserId     = signal<string | number | null>(null);
  permUserName   = signal('');
  permRows:      PermRow[] = [];
  isLoadingPerm  = signal(false);
  isSavingPerm   = signal(false);

  roles:   { id: number; name: string }[] = [];
  chucVus: ChucVu[] = [];

  sexOptions = [
    { value: 1, label: 'Nam' },
    { value: 2, label: 'Nữ' },
    { value: 0, label: 'Khác' },
  ];

  statusOptions = [
    { value: 2, label: 'Hoạt động' },
    { value: 1, label: 'Ẩn' },
  ];

  langOptions = [
    { value: 'vi', label: 'Tiếng Việt' },
    { value: 'en', label: 'English' },
  ];

  searchForm = new FormGroup({
    keyword: new FormControl(''),
    roleId:  new FormControl<number | null>(null),
    tenantId: new FormControl<string | null>(null),
  });

  userForm = new FormGroup({
    fullName:   new FormControl('', { validators: [Validators.required] }),
    loginName:  new FormControl('', { validators: [Validators.required] }),
    email:      new FormControl(''),
    phone:      new FormControl(''),
    password:   new FormControl(''),
    sex:        new FormControl<number | null>(null),
    birthDate:  new FormControl(''),
    address:    new FormControl(''),
    roleId:     new FormControl<number | null>(null),
    positionId: new FormControl<number | null>(null),
    language:   new FormControl('vi'),
    status:     new FormControl<number>(2),
  });

  newPassword = new FormControl('', [Validators.required, Validators.minLength(6)]);

  ngOnInit(): void {
    this.loadRoles();
    this.loadChucVus();
    if (this.isPrivileged) {
      this.departmentService.getAll({ draw: 1, start: 0, length: 500, search: { value: '', regex: false } })
        .pipe(takeUntil(this.destroy$))
        .subscribe(res => this.tenantOptions = res.data.map((t: any) => ({ id: t.publicId ?? t.id, name: t.name })));
    }
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

  private loadRoles(): void {
    this.roleSvc.getAll({ draw: 1, start: 0, length: 999, search: { value: '', regex: false } })
      .pipe(takeUntil(this.destroy$))
      .subscribe(res => {
        this.roles = res.data.map((r: any) => ({ id: r.id, name: r.name || r.Name || '' }));
      });
  }

  private loadChucVus(): void {
    this.chucVuSvc.getAllForCombobox().pipe(takeUntil(this.destroy$)).subscribe(data => { this.chucVus = data; });
  }

  loadData(): void {
    this.isLoading.set(true);
    const s = this.searchForm.getRawValue();
    this.service.search({
      keyword:   s.keyword  || '',
      roleId:    s.roleId   !== null ? +s.roleId! : null,
      tenantId:  s.tenantId ?? null,
      pageIndex: this.pageIndex + 1,
      pageSize:  this.pageSize,
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        this.dataSource   = res.data;
        this.totalRecords = res.recordsTotal;
        this.selection.clear();
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

  isAllSelected(): boolean {
    return this.selection.selected.length > 0 && this.selection.selected.length === this.dataSource.length;
  }

  masterToggle(): void {
    this.isAllSelected() ? this.selection.clear() : this.dataSource.forEach(r => this.selection.select(r));
  }

  getRowIndex(i: number): number {
    return this.pageIndex * this.pageSize + i + 1;
  }

  openAddModal(): void {
    this.editMode.set(false);
    this.currentId.set(null);
    this.userForm.reset({ status: 2, language: 'vi' });
    this.loginNameExists.set(false);
    this.isCheckingLogin.set(false);
    this.userForm.get('password')?.setValidators([Validators.required, Validators.minLength(6)]);
    this.userForm.get('password')?.updateValueAndValidity();
    this.photoFile.set(null);
    this.photoPreviewUrl.set(null);
    this.photoPath.set(null);
    this.showModal.set(true);
  }

  openEditModal(item: User): void {
    this.editMode.set(true);
    this.currentId.set(item.publicId ?? item.id);
    this.isLoading.set(true);
    this.service.getById(item.publicId ?? item.id).pipe(takeUntil(this.destroy$)).subscribe({
      next: u => {
        this.isLoading.set(false);
        this.userForm.get('password')?.clearValidators();
        this.userForm.get('password')?.updateValueAndValidity();
        this.userForm.patchValue({
          fullName:   u.fullName   ?? '',
          loginName:  u.loginName  ?? '',
          email:      u.email      ?? '',
          phone:      u.phone      ?? '',
          password:   '',
          sex:        u.sex        ?? null,
          birthDate:  u.birthDate  ? u.birthDate.split('T')[0] : '',
          address:    u.address    ?? '',
          roleId:     u.roleId     ?? null,
          positionId: (u as any).positionId ?? u.postionId ?? null,
          language:   u.language   ?? 'vi',
          status:     u.status     ?? 2,
        });
        this.photoFile.set(null);
        this.photoPath.set(u.photo ?? null);
        this.photoPreviewUrl.set(u.photo ? `${this.imageUrl}/${u.photo}` : null);
        this.showModal.set(true);
      },
      error: () => {
        this.isLoading.set(false);
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
      }
    });
  }

  closeModal(): void {
    this.showModal.set(false);
    this.photoFile.set(null);
    this.photoPreviewUrl.set(null);
    this.photoPath.set(null);
  }

  onPhotoSelect(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    if (!file) return;
    this.photoFile.set(file);
    const reader = new FileReader();
    reader.onload = e => this.photoPreviewUrl.set(e.target?.result as string);
    reader.readAsDataURL(file);
    (event.target as HTMLInputElement).value = '';

    this.uploadingPhoto.set(true);
    this.service.uploadPhoto(file).pipe(takeUntil(this.destroy$)).subscribe({
      next: path => {
        this.photoPath.set(path || null);
        this.uploadingPhoto.set(false);
      },
      error: () => {
        this.uploadingPhoto.set(false);
      }
    });
  }

  onSubmit(): void {
    if (this.userForm.invalid) {
      this.userForm.markAllAsTouched();
      return;
    }
    if (this.loginNameExists()) {
      this.toastr.error(this.translate.instant('USER_MGT.LOGIN_NAME_EXISTS'));
      return;
    }
    this.isSaving.set(true);
    const v  = this.userForm.getRawValue();
    const id = this.currentId();

    const payload: any = {
      FullName:      v.fullName   || '',
      LoginName:     v.loginName  || '',
      Email:         v.email      || '',
      Phone:         v.phone      || '',
      Address:       v.address    || '',
      Sex:           v.sex        ?? null,
      BirthDate:     v.birthDate  ? `${v.birthDate}T00:00:00` : null,
      Photo:         this.photoPath() ?? '',
      RoleId:        v.roleId     ?? null,
      PositionId:    v.positionId ?? null,
      RoleWinformId: null,
      PortalId:      '',
      Language:      v.language   || 'vi',
      Status:        v.status     ?? 2,
    };

    if (!this.editMode()) {
      payload.Password = v.password || '';
    } else if (v.password) {
      payload.Password = v.password;
    }

    const obs$ = this.editMode() && id !== null
      ? this.service.update(id, payload)
      : this.service.create(payload);

    obs$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.isSaving.set(false);
        this.closeModal();
        if (!this.editMode()) {
          this.pageIndex = 0;
          if (this.paginator) this.paginator.pageIndex = 0;
        }
        this.loadData();
        this.toastr.success(this.translate.instant(
          this.editMode() ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS'
        ));
      },
      error: () => {
        this.isSaving.set(false);
      }
    });
  }

  handleDelete(item: User): void {
    this.confirmDeleteId.set(item.publicId ?? item.id);
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
      const reqs = this.selection.selected.map(item => this.service.delete(item.publicId ?? item.id));
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

  openResetConfirm(item: User): void {
    this.resetTargetId.set(item.publicId ?? item.id);
    this.newPassword.reset('');
    this.showResetConfirm.set(true);
  }

  closeResetConfirm(): void {
    this.showResetConfirm.set(false);
    this.resetTargetId.set(null);
    this.newPassword.reset('');
  }

  confirmReset(): void {
    if (this.newPassword.invalid) { this.newPassword.markAsTouched(); return; }
    const id = this.resetTargetId();
    if (id === null) return;
    this.service.resetPassword(id, this.newPassword.value!).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.toastr.success(this.translate.instant('USER_MGT.RESET_PASSWORD_SUCCESS'));
        this.closeResetConfirm();
      },
      error: () => {
        this.closeResetConfirm();
      }
    });
  }

  toggleStatus(item: User): void {
    const newStatus = item.status === 2 ? 1 : 2;
    const pid = item.publicId ?? item.id;
    this.service.changeStatus(pid, newStatus).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { item.status = newStatus; },
      error: () => {}
    });
  }

  getSexLabel(sex: number | null | undefined): string {
    return this.sexOptions.find(o => o.value === sex)?.label ?? '—';
  }

  getRoleName(roleId: number | null | undefined): string {
    return this.roles.find(r => r.id === roleId)?.name ?? (roleId ? String(roleId) : '—');
  }

  checkLoginName(value: string): void {
    if (!value.trim()) { this.loginNameExists.set(false); return; }
    this.isCheckingLogin.set(true);
    this.loginNameExists.set(false);
    const excludeId = this.editMode() ? this.currentId() : null;
    this.service.checkLoginNameExists(value.trim(), excludeId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: exists => {
          this.loginNameExists.set(exists);
          this.isCheckingLogin.set(false);
        },
        error: () => this.isCheckingLogin.set(false)
      });
  }

  openBulkResetConfirm(): void {
    if (!this.selection.selected.length) return;
    this.newPassword.reset('');
    this.showBulkResetConfirm.set(true);
  }

  closeBulkResetConfirm(): void {
    this.showBulkResetConfirm.set(false);
    this.newPassword.reset('');
  }

  confirmBulkReset(): void {
    if (this.newPassword.invalid) { this.newPassword.markAsTouched(); return; }
    const pw   = this.newPassword.value!;
    const reqs = this.selection.selected.map(item => this.service.resetPassword(item.publicId ?? item.id, pw));
    forkJoin(reqs).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.toastr.success(this.translate.instant('USER_MGT.RESET_PASSWORD_SUCCESS'));
        this.closeBulkResetConfirm();
      },
      error: () => {
        this.closeBulkResetConfirm();
      }
    });
  }

  private downloadBlob(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const a   = document.createElement('a');
    a.href     = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }

  exportExcel(): void {
    if (this.isExporting()) return;
    this.isExporting.set(true);
    const s = this.searchForm.getRawValue();
    this.service.exportExcel(s.keyword || '', s.roleId ?? null)
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: blob => {
          this.downloadBlob(blob, `users_${Date.now()}.xlsx`);
          this.isExporting.set(false);
        },
        error: () => {
          this.toastr.error(this.translate.instant('COMMON.EXPORT_ERROR'));
          this.isExporting.set(false);
        }
      });
  }

  openPermModal(item: User): void {
    const id = item.publicId ?? item.id;
    this.permUserId.set(id);
    this.permUserName.set(item.fullName || item.loginName || '');
    this.permRows = [];
    this.isLoadingPerm.set(true);
    this.showPermModal.set(true);
    this.service.getUserPermissions(id).pipe(takeUntil(this.destroy$)).subscribe({
      next: rows => { this.permRows = rows; this.isLoadingPerm.set(false); },
      error: () => { this.isLoadingPerm.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); }
    });
  }

  closePermModal(): void {
    this.showPermModal.set(false);
    this.permRows = [];
  }

  onPermToggle(row: PermRow, field: 'canView' | 'canAdd' | 'canEdit' | 'canDelete'): void {
    row[field] = !row[field];
    this.cascadePermChildren(row.moduleId, field, row[field]);
    this.permRows = [...this.permRows];
  }

  private cascadePermChildren(parentId: number, field: 'canView' | 'canAdd' | 'canEdit' | 'canDelete', value: boolean): void {
    this.permRows.filter(r => r.parentId === parentId).forEach(child => {
      child[field] = value;
      this.cascadePermChildren(child.moduleId, field, value);
    });
  }

  savePermissions(): void {
    const id = this.permUserId();
    if (!id) return;
    this.isSavingPerm.set(true);
    this.service.saveUserPermissions(id, this.permRows).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.isSavingPerm.set(false);
        this.closePermModal();
        this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS'));
      },
      error: () => {
        this.isSavingPerm.set(false);
      }
    });
  }

  openImportModal(): void  { this.importFile.set(null); this.showImportModal.set(true); }
  closeImportModal(): void { this.showImportModal.set(false); this.importFile.set(null); }

  onImportFileSelect(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.importFile.set(file);
    (event.target as HTMLInputElement).value = '';
  }

  confirmImport(): void {
    const file = this.importFile();
    if (!file) return;
    this.isImporting.set(true);
    this.service.importData(file).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.isImporting.set(false);
        this.closeImportModal();
        this.toastr.success(this.translate.instant('COMMON.IMPORT_SUCCESS'));
        this.pageIndex = 0;
        if (this.paginator) this.paginator.pageIndex = 0;
        this.loadData();
      },
      error: () => {
        this.isImporting.set(false);
      }
    });
  }
}
