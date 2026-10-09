import { Component, inject, OnInit, OnDestroy, signal, ViewChild, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ReactiveFormsModule, FormGroup, FormControl, Validators, FormsModule } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { firstValueFrom, Subject, takeUntil, forkJoin } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { SelectionModel } from '@angular/cdk/collections';
import { ToastrService } from '../../../services/shared/toastr.service';
import { NganhHocService, NganhHoc } from '../../../services/evaluate/nganh-hoc.service';
import { EvaluateProgramService, EvaluateProgram } from '../../../services/evaluate/evaluate-program.service';
import { DataTableParams } from '../../../models/shared/datatable';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { DepartmentService } from '../../../services/system/department.service';
import { ListPageStateService } from '../../../services/shared/list-page-state.service';

@Component({
  selector: 'app-nganh-hoc',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, CanDirective, NgSelectModule],
  templateUrl: './nganh-hoc.html'
})
export class NganhHocPage implements OnInit, OnDestroy, AfterViewInit {
  private service = inject(NganhHocService);
  private listState = inject(ListPageStateService);
  private programService = inject(EvaluateProgramService);
  private router = inject(Router);
  public translate = inject(TranslateService);
  private toastr = inject(ToastrService);
  private auth = inject(Auth);
  private departmentService = inject(DepartmentService);
  private destroy$ = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantOptions: { id: string; name: string }[] = [];
  tenantId: string | null = null;

  parentOptions: NganhHoc[] = [];
  programOptions: EvaluateProgram[] = [];

  showModal = signal<boolean>(false);
  showConfirmDelete = signal<boolean>(false);
  editMode = signal<boolean>(false);
  itemToDelete = signal<string | null>(null);
  currentId = signal<string | null>(null);
  currentItem = signal<NganhHoc | null>(null);
  isLoading = signal<boolean>(false);

  dataForm = new FormGroup({
    majorsCode: new FormControl('', [Validators.required]),
    majorsName: new FormControl('', [Validators.required]),
    parentId: new FormControl<number | null>(null),
    programId: new FormControl<number | null>(null),
    amountStudent: new FormControl<number | null>(null),
    sortOrder: new FormControl<number | null>(null),
    status: new FormControl<number>(2)
  });

  displayedColumns: string[] = this.auth.isPrivileged()
    ? ['select', 'id', 'majorsCode', 'majorsName', 'parent', 'status', 'tenant', 'actions']
    : ['select', 'id', 'majorsCode', 'majorsName', 'parent', 'status', 'actions'];
  dataSource = new MatTableDataSource<NganhHoc>([]);
  selection = new SelectionModel<NganhHoc>(true, []);
  totalRecords = 0;
  pageSize = 10;
  pageIndex = 0;
  drawCount = 0;
  searchTerm = '';

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  ngOnInit() {
    const saved = this.listState.recall('evaluate.nganh-hoc'); if (saved) { this.pageIndex = saved.pageIndex; this.pageSize = saved.pageSize; }
    this.translate.onLangChange.pipe(takeUntil(this.destroy$)).subscribe(() => this.loadData());
    if (this.isPrivileged) {
      this.departmentService.getAll({ draw: 1, start: 0, length: 500, search: { value: '', regex: false } })
        .pipe(takeUntil(this.destroy$))
        .subscribe(res => this.tenantOptions = res.data.map((t: any) => ({ id: t.publicId ?? t.id, name: t.name })));
    }
    this.service.searchAll().pipe(takeUntil(this.destroy$)).subscribe(list => this.parentOptions = list);
    this.programService.searchAll().pipe(takeUntil(this.destroy$)).subscribe(list => this.programOptions = list);
  }

  onTenantChange() {
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadData();
  }

  ngAfterViewInit() { this.loadData(); }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadData() {
    this.listState.remember('evaluate.nganh-hoc', this.pageIndex, this.pageSize);
    this.drawCount++;
    const params: DataTableParams = {
      draw: this.drawCount,
      start: this.pageIndex * this.pageSize,
      length: this.pageSize,
      search: { value: this.searchTerm },
      order: [],
      tenantId: this.tenantId,
    };

    this.isLoading.set(true);
    this.service.getAll(params).pipe(takeUntil(this.destroy$)).subscribe({
      next: (response) => {
        this.dataSource.data = response.data;
        this.totalRecords = response.recordsTotal;
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Error fetching data', err);
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
        this.isLoading.set(false);
      }
    });
  }

  onPageChange(event: PageEvent) {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadData();
  }

  triggerSearch() {
    this.searchTerm = (document.getElementById('searchNganh') as HTMLInputElement)?.value || '';
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.selection.clear();
    this.loadData();
  }

  getParentName(id: number | null | undefined): string {
    if (!id) return '—';
    return this.parentOptions.find(p => Number(p.id) === Number(id))?.majorsName || '—';
  }

  /** Danh sách "Ngành cha" khả dụng cho form — loại trừ chính bản ghi đang sửa để tránh tự tham chiếu. */
  parentOptionsFor(currentId: string | null): NganhHoc[] {
    if (!currentId) return this.parentOptions;
    return this.parentOptions.filter(p => String(p.publicId || p.id) !== String(currentId));
  }

  isAllSelected() {
    const numSelected = this.selection.selected.length;
    const numRows = this.dataSource.data.length;
    return numSelected > 0 && numSelected === numRows;
  }

  toggleAllRows() {
    if (this.isAllSelected()) { this.selection.clear(); return; }
    this.selection.select(...this.dataSource.data);
  }

  deleteSelected() {
    if (this.selection.selected.length === 0) return;
    this.itemToDelete.set('bulk');
    this.showConfirmDelete.set(true);
  }

  openAddModal() {
    this.editMode.set(false);
    this.currentId.set(null);
    this.currentItem.set(null);
    this.dataForm.reset({ status: 2 });
    this.showModal.set(true);
  }

  async openEditModal(id: string) {
    try {
      const item = await firstValueFrom(this.service.getById(id));
      this.currentItem.set(item);
      this.currentId.set(item.publicId || item.id || id);
      this.dataForm.patchValue({
        majorsCode: item.majorsCode,
        majorsName: item.majorsName,
        parentId: item.parentId ?? null,
        programId: item.programId ?? null,
        amountStudent: item.amountStudent ?? null,
        sortOrder: item.sortOrder ?? null,
        status: item.status ?? 2
      });
      this.editMode.set(true);
      this.showModal.set(true);
    } catch (error) {
      console.error('Error fetching data details', error);
      this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
    }
  }

  closeModal() {
    this.showModal.set(false);
    this.dataForm.reset({ status: 2 });
  }

  onSubmit() {
    if (this.dataForm.invalid) return;

    const v = this.dataForm.value;
    const payload: Partial<NganhHoc> = {
      majorsCode: v.majorsCode || '',
      majorsName: v.majorsName || '',
      parentId: v.parentId ?? null,
      programId: v.programId ?? null,
      amountStudent: v.amountStudent ?? null,
      sortOrder: v.sortOrder ?? null,
      status: v.status ?? 2
    };

    if (this.editMode() && this.currentId()) {
      this.service.update(this.currentId()!, payload).subscribe({
        next: () => {
          this.closeModal();
          this.loadData();
          this.service.searchAll().subscribe(list => this.parentOptions = list);
          this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS'));
        },
        error: (err) => console.error('Update failed', err)
      });
    } else {
      this.service.create(payload).subscribe({
        next: () => {
          this.closeModal();
          this.loadData();
          this.service.searchAll().subscribe(list => this.parentOptions = list);
          this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS'));
        },
        error: (err) => console.error('Create failed', err)
      });
    }
  }

  confirmDelete(id: string) {
    this.itemToDelete.set(id);
    this.showConfirmDelete.set(true);
  }

  closeConfirm() {
    this.showConfirmDelete.set(false);
    this.itemToDelete.set(null);
  }

  confirmActionExecute() {
    const id = this.itemToDelete();
    if (id === 'bulk') {
      const selectedIds = this.selection.selected.map((item: any) => item.publicId || item.id);
      const requests = selectedIds.map(selectedId => this.service.delete(selectedId));
      forkJoin(requests).subscribe({
        next: () => {
          this.closeConfirm();
          this.loadData();
          this.selection.clear();
          this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
        },
        error: (err) => { console.error('Delete failed', err); this.closeConfirm(); }
      });
      return;
    }

    if (id) {
      this.service.delete(id).subscribe({
        next: () => {
          this.closeConfirm();
          this.loadData();
          this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
        },
        error: (err) => { console.error('Delete failed', err); this.closeConfirm(); }
      });
    }
  }

  goToSubjects(id: string) {
    this.router.navigate(['/admin/subject-majors', id, 'subjects']);
  }

  goToReport(id: string) {
    this.router.navigate(['/admin/subject-majors', id, 'report']);
  }
}
