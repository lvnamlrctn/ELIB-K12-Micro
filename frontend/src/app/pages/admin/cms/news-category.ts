import { Component, inject, OnInit, OnDestroy, signal, ViewChild, AfterViewInit } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { SelectionModel } from '@angular/cdk/collections';
import { Subject, takeUntil, firstValueFrom, forkJoin } from 'rxjs';
import { CategoryService } from '../../../services/cms/category.service';
import { Category } from '../../../models/cms/category';
import { DataTableParams } from '../../../models/shared/datatable';
import { ToastrService } from '../../../services/shared/toastr.service';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { DepartmentService } from '../../../services/system/department.service';

@Component({
  selector: 'app-news-category',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule],
  templateUrl: './news-category.html'
})
export class NewsCategoryPage implements OnInit, OnDestroy, AfterViewInit {
  private categoryService = inject(CategoryService);
  private translate = inject(TranslateService);
  private toastr = inject(ToastrService);
  private auth = inject(Auth);
  private departmentService = inject(DepartmentService);
  private destroy$ = new Subject<void>();

  // Đơn vị (chỉ super-admin)
  isPrivileged = this.auth.isPrivileged();
  tenantOptions: { id: string; name: string }[] = [];
  tenantId: string | null = null;

  showModal         = signal<boolean>(false);
  showConfirm       = signal<boolean>(false);
  confirmMessage    = signal<string>('');
  confirmAction     = signal<'delete' | 'bulkDelete' | null>(null);
  pendingId         = signal<string | null>(null);
  editMode          = signal<boolean>(false);
  currentId         = signal<string | null>(null);
  currentItem       = signal<Category | null>(null);
  isLoading         = signal<boolean>(false);

  dataForm = new FormGroup({
    Name:            new FormControl('', [Validators.required]),
    ParentId:        new FormControl<number | null>(null),
    Status:          new FormControl(true),
    PageTitle:       new FormControl(''),
    Keyword:         new FormControl(''),
    MetaDescription: new FormControl(''),
    Order:           new FormControl<number | null>(null),
    Link:            new FormControl('')
  });

  displayedColumns: string[] = ['select', 'name', 'status', 'order', 'actions'];

  private applyTenantColumn(): void {
    if (this.isPrivileged && !this.displayedColumns.includes('tenant')) {
      this.displayedColumns.splice(this.displayedColumns.length - 1, 0, 'tenant');
    }
  }

  private loadTenants(): void {
    if (!this.isPrivileged) return;
    this.departmentService.getAll({ draw: 1, start: 0, length: 500, search: { value: '', regex: false } })
      .pipe(takeUntil(this.destroy$))
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      .subscribe(res => this.tenantOptions = res.data.map((t: any) => ({ id: t.publicId ?? t.id, name: t.name })));
  }

  onTenantChange(): void {
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.treeOptions = [];
    this.loadData();
  }
  dataSource = new MatTableDataSource<any>([]);
  selection  = new SelectionModel<any>(true, []);

  totalRecords = 0;
  pageSize     = 10;
  pageIndex    = 0;
  drawCount    = 0;
  searchTerm   = '';

  allCategories: Category[] = [];
  treeOptions:   any[]      = [];
  isTreeMode     = true;

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  ngOnInit() {
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
    this.drawCount++;
    const params: DataTableParams = {
      draw: this.drawCount,
      start: this.pageIndex * this.pageSize,
      length: this.pageSize,
      search: { value: this.searchTerm },
      tenantId: this.tenantId,
      order: []
    };

    this.isLoading.set(true);
    if (this.isTreeMode && !this.searchTerm) {
      this.categoryService.getAllCategories(this.tenantId).pipe(takeUntil(this.destroy$)).subscribe({
        next: data => {
          this.allCategories = data;
          const treeData = this.buildTree(data, null);
          this.treeOptions = treeData;
          this.totalRecords = treeData.length;
          const start = this.pageIndex * this.pageSize;
          this.dataSource.data = treeData.slice(start, start + this.pageSize);
          this.selection.clear();
          this.isLoading.set(false);
        },
        error: () => {
          this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
          this.isLoading.set(false);
        }
      });
    } else {
      this.categoryService.getAll(params).pipe(takeUntil(this.destroy$)).subscribe({
        next: response => {
          this.allCategories = response.data;
          this.dataSource.data = response.data;
          this.totalRecords = response.recordsTotal;
          this.selection.clear();
          if (this.treeOptions.length === 0) {
            this.categoryService.getAllCategories(this.tenantId).subscribe(data => {
              this.treeOptions = this.buildTree(data, null);
            });
          }
          this.isLoading.set(false);
        },
        error: () => {
          this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
          this.isLoading.set(false);
        }
      });
    }
  }

  buildTree(categories: Category[], parentId: number | null, level = 0): any[] {
    const children = categories.filter(c => (!parentId ? !c.parentId : c.parentId === parentId));
    children.sort((a, b) => (a.order ?? 0) - (b.order ?? 0));

    let result: any[] = [];
    for (const child of children) {
      const prefix = level > 0 ? '|' + '-'.repeat(level * 4) : '';
      result.push({ ...child, displayName: prefix + child.name, level });
      result = result.concat(this.buildTree(categories, child.id, level + 1));
    }
    return result;
  }

  onPageChange(event: PageEvent) {
    this.pageIndex = event.pageIndex;
    this.pageSize  = event.pageSize;
    this.loadData();
  }

  triggerSearch() {
    this.searchTerm = (document.getElementById('searchName') as HTMLInputElement)?.value || '';
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadData();
  }

  public refreshTable() { this.loadData(); }

  isAllSelected() {
    return this.selection.selected.length === this.dataSource.data.length && this.dataSource.data.length > 0;
  }

  toggleAllRows() {
    if (this.isAllSelected()) { this.selection.clear(); return; }
    this.selection.select(...this.dataSource.data);
  }

  openAddModal() {
    this.editMode.set(false);
    this.currentId.set(null);
    this.currentItem.set(null);
    this.dataForm.reset({ Status: true, ParentId: null });
    this.showModal.set(true);
  }

  async openEditModal(publicId: string) {
    try {
      const item = await firstValueFrom(this.categoryService.getById(publicId));
      this.currentItem.set(item);
      this.currentId.set(item.publicId ?? null);
      this.dataForm.patchValue({
        Name:            item.name,
        ParentId:        item.parentId,
        Status:          item.status === 2 || item.status === true,
        PageTitle:       item.pageTitle,
        Keyword:         item.keyword,
        MetaDescription: item.metaDescription,
        Order:           item.order ?? null,
        Link:            item.link
      });
      this.editMode.set(true);
      this.showModal.set(true);
    } catch {
      this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
    }
  }

  closeModal() {
    this.showModal.set(false);
    this.dataForm.reset();
  }

  onSubmit() {
    if (this.dataForm.invalid) return;
    const v = this.dataForm.value;
    const payload: any = {
      Name:            v.Name,
      ParentId:        v.ParentId || 0,
      Status:          v.Status ? 2 : 1,
      PageTitle:       v.PageTitle,
      Keyword:         v.Keyword,
      MetaDescription: v.MetaDescription,
      Order:           v.Order,
      Link:            v.Link
    };

    if (this.editMode() && this.currentId()) {
      this.categoryService.update(this.currentId()!, { ...this.currentItem(), ...payload }).subscribe({
        next: () => { this.refreshTable(); this.closeModal(); this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS')); },
        error: () => { }
      });
    } else {
      this.categoryService.create(payload).subscribe({
        next: () => { this.refreshTable(); this.closeModal(); this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS')); },
        error: () => { }
      });
    }
  }

  handleDelete(publicId: string) {
    this.pendingId.set(publicId);
    this.confirmAction.set('delete');
    this.confirmMessage.set(this.translate.instant('COMMON.DELETE_CONFIRM_MSG'));
    this.showConfirm.set(true);
  }

  deleteSelected() {
    if (this.selection.selected.length === 0) return;
    this.confirmAction.set('bulkDelete');
    this.confirmMessage.set(this.translate.instant('COMMON.DELETE_CONFIRM_MSG'));
    this.showConfirm.set(true);
  }

  confirmActionExecute() {
    const action = this.confirmAction();
    if (action === 'delete') {
      const id = this.pendingId();
      if (!id) return;
      this.categoryService.delete(id).subscribe({
        next: () => { this.refreshTable(); this.closeConfirm(); this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); },
        error: () => { this.closeConfirm(); }
      });
    } else if (action === 'bulkDelete') {
      const publicIds: string[] = this.selection.selected.map((s: any) => s.publicId);
      forkJoin(publicIds.map(pid => this.categoryService.delete(pid))).subscribe({
        next: () => { this.selection.clear(); this.refreshTable(); this.closeConfirm(); this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); },
        error: () => { this.closeConfirm(); }
      });
    }
  }

  closeConfirm() {
    this.showConfirm.set(false);
    this.confirmAction.set(null);
    this.pendingId.set(null);
  }
}
