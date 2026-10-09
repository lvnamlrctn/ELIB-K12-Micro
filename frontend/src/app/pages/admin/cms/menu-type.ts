import { Component, inject, OnInit, OnDestroy, signal, ViewChild, AfterViewInit } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators, AbstractControl, ValidationErrors, AsyncValidatorFn } from '@angular/forms';
import { Router } from '@angular/router';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { firstValueFrom, Subject, takeUntil, forkJoin, timer, Observable } from 'rxjs';
import { switchMap, map } from 'rxjs/operators';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { SelectionModel } from '@angular/cdk/collections';
import { ToastrService } from '../../../services/shared/toastr.service';
import { MenuTypeService } from '../../../services/cms/menu-type.service';
import { MenuType } from '../../../models/cms/menu-type';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { DepartmentService } from '../../../services/system/department.service';
import { ListPageStateService } from '../../../services/shared/list-page-state.service';

@Component({
  selector: 'app-menu-type',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule],
  templateUrl: './menu-type.html'
})
export class MenuTypePage implements OnInit, OnDestroy, AfterViewInit {
  private service = inject(MenuTypeService);
  private listState = inject(ListPageStateService);
  private toastr = inject(ToastrService);
  public translate = inject(TranslateService);
  private router = inject(Router);
  private auth = inject(Auth);
  private departmentService = inject(DepartmentService);
  private destroy$ = new Subject<void>();

  // Đơn vị (chỉ super-admin)
  isPrivileged = this.auth.isPrivileged();
  tenantOptions: { id: string; name: string }[] = [];
  tenantId: string | null = null;

  showModal   = signal<boolean>(false);
  showConfirm = signal<boolean>(false);
  pendingId   = signal<string | null>(null);
  editMode    = signal<boolean>(false);
  currentId   = signal<string | null>(null);
  isLoading   = signal<boolean>(false);

  private originalCode = '';

  dataForm = new FormGroup({
    code:        new FormControl('', { nonNullable: true }),
    name:        new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    description: new FormControl('', { nonNullable: true }),
    status:      new FormControl<boolean>(true, { nonNullable: true })
  });

  private get codeAsyncValidator(): AsyncValidatorFn {
    return (control: AbstractControl): Observable<ValidationErrors | null> => {
      const val = (control.value as string || '').trim();
      if (!val) return timer(0).pipe(map(() => null));
      if (val.toLowerCase() === this.originalCode.toLowerCase()) return timer(0).pipe(map(() => null));
      return timer(400).pipe(
        switchMap(() => this.service.checkCodeExists(val)),
        map(exists => exists ? { codeExists: true } : null)
      );
    };
  }

  displayedColumns: string[] = ['select', 'id', 'code', 'name', 'description', 'status', 'actions'];

  private applyTenantColumn(): void {
    if (this.isPrivileged && !this.displayedColumns.includes('tenant')) {
      this.displayedColumns.splice(this.displayedColumns.length - 1, 0, 'tenant');
    }
  }

  private loadTenants(): void {
    if (!this.isPrivileged) return;
    this.departmentService.getAll({ draw: 1, start: 0, length: 500, search: { value: '', regex: false } })
      .pipe(takeUntil(this.destroy$))
      .subscribe(res => this.tenantOptions = res.data.map((t: any) => ({ id: t.publicId ?? t.id, name: t.name })));
  }

  onTenantChange(): void {
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadData();
  }
  dataSource = new MatTableDataSource<MenuType>([]);
  selection  = new SelectionModel<MenuType>(true, []);
  totalRecords = 0;
  pageSize     = 10;
  pageIndex    = 0;
  drawCount    = 0;
  searchTerm   = '';

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  ngOnInit() {
    const saved = this.listState.recall('cms.menu-type'); if (saved) { this.pageIndex = saved.pageIndex; this.pageSize = saved.pageSize; }
    this.applyTenantColumn();
    this.loadTenants();
    this.translate.onLangChange.pipe(takeUntil(this.destroy$)).subscribe(() => this.loadData());
  }

  ngAfterViewInit() {
    this.loadData();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadData() {
    this.listState.remember('cms.menu-type', this.pageIndex, this.pageSize);
    this.drawCount++;
    const params: any = {
      draw:   this.drawCount,
      start:  this.pageIndex * this.pageSize,
      length: this.pageSize,
      search: { value: this.searchTerm },
      tenantId: this.tenantId,
      order:  []
    };
    this.isLoading.set(true);
    this.service.getAll(params).pipe(takeUntil(this.destroy$)).subscribe({
      next: (res) => {
        this.dataSource.data = res.data;
        this.totalRecords    = res.recordsTotal;
        this.isLoading.set(false);
      },
      error: () => {
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
        this.isLoading.set(false);
      }
    });
  }

  onPageChange(event: PageEvent) {
    this.pageIndex = event.pageIndex;
    this.pageSize  = event.pageSize;
    this.loadData();
  }

  triggerSearch() {
    this.searchTerm = (document.getElementById('searchName') as HTMLInputElement).value;
    this.pageIndex  = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.selection.clear();
    this.loadData();
  }

  isAllSelected() {
    return this.selection.selected.length > 0 &&
           this.selection.selected.length === this.dataSource.data.length;
  }

  toggleAllRows() {
    if (this.isAllSelected()) { this.selection.clear(); return; }
    this.selection.select(...this.dataSource.data);
  }

  deleteSelected() {
    if (this.selection.selected.length === 0) return;
    this.pendingId.set('bulk');
    this.showConfirm.set(true);
  }

  openAddModal() {
    this.originalCode = '';
    this.editMode.set(false);
    this.currentId.set(null);
    this.dataForm.reset({ code: '', name: '', description: '', status: true });
    this.dataForm.controls.code.setAsyncValidators(this.codeAsyncValidator);
    this.dataForm.controls.code.updateValueAndValidity();
    this.showModal.set(true);
  }

  async openEditModal(id: string) {
    try {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const record = await firstValueFrom(this.service.getById(id)) as any;
      this.originalCode = record?.code || record?.Code || '';
      this.currentId.set(record?.publicId || id);
      this.dataForm.controls.code.setAsyncValidators(this.codeAsyncValidator);
      this.dataForm.patchValue({
        code:        this.originalCode,
        name:        record?.name        || record?.Name        || '',
        description: record?.description || record?.Description || '',
        status:      (record?.status ?? record?.Status ?? 2) === 2
      });
      this.editMode.set(true);
      this.showModal.set(true);
    } catch {
      this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
    }
  }

  closeModal() {
    this.dataForm.controls.code.clearAsyncValidators();
    this.showModal.set(false);
    this.dataForm.reset();
  }

  onSubmit() {
    if (this.dataForm.invalid) return;
    const v = this.dataForm.getRawValue();
    const payload: Partial<MenuType> = {
      code:        v.code,
      name:        v.name,
      description: v.description,
      status:      v.status ? 2 : 1
    };

    if (this.editMode() && this.currentId()) {
      const updatePayload: MenuType = { id: this.currentId()!, publicId: this.currentId()!, name: payload.name || '', ...payload };
      this.service.update(updatePayload).subscribe({
        next: () => { this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS')); this.loadData(); this.closeModal(); },
        error: () => {}
      });
    } else {
      this.service.create(payload).subscribe({
        next: () => { this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS')); this.loadData(); this.closeModal(); },
        error: () => {}
      });
    }
  }

  handleDelete(id: string) {
    this.pendingId.set(id);
    this.showConfirm.set(true);
  }

  closeConfirm() {
    this.showConfirm.set(false);
    this.pendingId.set(null);
  }

  confirmActionExecute() {
    const id = this.pendingId();
    if (id === 'bulk') {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const ids = this.selection.selected.map((item: any) => item.publicId || item.id);
      forkJoin(ids.map(i => this.service.delete(i))).subscribe({
        next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.loadData(); this.selection.clear(); this.closeConfirm(); },
        error: ()  => { this.closeConfirm(); }
      });
      return;
    }
    if (id) {
      this.service.delete(id).subscribe({
        next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.loadData(); this.closeConfirm(); },
        error: ()  => { this.closeConfirm(); }
      });
    }
  }

  goToMenus(publicId: string): void {
    this.router.navigate(['/admin/cms-menus', publicId]);
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  isActive(element: any): boolean {
    return (element?.status ?? element?.Status) === 2;
  }

  toggleStatus(element: MenuType) {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const publicId = (element as any).publicId || element.id;
    if (publicId) {
      this.service.changeStatus(publicId, !this.isActive(element)).subscribe({
        next: () => { this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS')); this.loadData(); },
        error: () => {}
      });
    }
  }
}
