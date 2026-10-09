import { Component, inject, OnInit, OnDestroy, signal, ViewChild, AfterViewInit } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { firstValueFrom, Subject, takeUntil, forkJoin } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { SelectionModel } from '@angular/cdk/collections';
import { ToastrService } from '../../../services/shared/toastr.service';
import { PhotoAlbumService } from '../../../services/cms/photo-album.service';
import { PhotoAlbum } from '../../../models/cms/photo-album';
import { environment } from '../../../../environments/environment';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { DepartmentService } from '../../../services/system/department.service';

@Component({
  selector: 'app-photo-album',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule],
  templateUrl: './photo-album.html'
})
export class PhotoAlbumPage implements OnInit, OnDestroy, AfterViewInit {
  private photoAlbumService = inject(PhotoAlbumService);
  private toastr = inject(ToastrService);
  public translate = inject(TranslateService);
  private auth = inject(Auth);
  private departmentService = inject(DepartmentService);
  private destroy$ = new Subject<void>();

  // Đơn vị (chỉ super-admin)
  isPrivileged = this.auth.isPrivileged();
  tenantOptions: { id: string; name: string }[] = [];
  tenantId: string | null = null;

  // Modal State
  showModal = signal<boolean>(false);
  showConfirm = signal<boolean>(false);
  pendingId = signal<string | number | null>(null);
  editMode = signal<boolean>(false);
  currentId = signal<string | number | null>(null);
  isLoading = signal<boolean>(false);

  // Form
  dataForm = new FormGroup({
    Name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    Code: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    Description: new FormControl('', { nonNullable: true }),
    SortOrder: new FormControl<number>(0, { nonNullable: true }),
    Status: new FormControl<boolean>(true, { nonNullable: true, validators: [Validators.required] }),
    IsSpecial: new FormControl<boolean>(false, { nonNullable: true, validators: [Validators.required] })
  });

  // Table State
  displayedColumns: string[] = ['select', 'id', 'image', 'name', 'code', 'sortOrder', 'isSpecial', 'status', 'actions'];

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
  dataSource = new MatTableDataSource<PhotoAlbum>([]);
  selection = new SelectionModel<PhotoAlbum>(true, []);
  totalRecords = 0;
  pageSize = 10;
  pageIndex = 0;
  drawCount = 0;
  searchTerm = '';
  selectedImageBase64 = signal<string>('');
  selectedImageFile = signal<File | undefined>(undefined);

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  ngOnInit() {
    this.applyTenantColumn();
    this.loadTenants();
    this.translate.onLangChange.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.loadData();
    });
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
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const params: any = {
      draw: this.drawCount,
      start: this.pageIndex * this.pageSize,
      length: this.pageSize,
      search: { value: this.searchTerm },
      tenantId: this.tenantId,
      order: []
    };

    this.isLoading.set(true);
    this.photoAlbumService.getAll(params).pipe(takeUntil(this.destroy$)).subscribe({
      next: (res) => {
        this.dataSource.data = res.data;
        this.totalRecords = res.recordsTotal;
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Error loading data', err);
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
    this.searchTerm = (document.getElementById('searchName') as HTMLInputElement).value;
    this.pageIndex = 0;
    if (this.paginator) {
      this.paginator.pageIndex = 0;
    }
    this.selection.clear();
    this.loadData();
  }

  isAllSelected() {
    const numSelected = this.selection.selected.length;
    const numRows = this.dataSource.data.length;
    return numSelected > 0 && numSelected === numRows;
  }

  toggleAllRows() {
    if (this.isAllSelected()) {
      this.selection.clear();
      return;
    }
    this.selection.select(...this.dataSource.data);
  }

  deleteSelected() {
    if (this.selection.selected.length === 0) return;
    this.pendingId.set('bulk');
    this.showConfirm.set(true);
  }

  openAddModal() {
    this.editMode.set(false);
    this.currentId.set(null);
    this.selectedImageBase64.set('');
    this.selectedImageFile.set(undefined);
    this.dataForm.reset({
      Name: '',
      Code: '',
      Description: '',
      SortOrder: 0,
      Status: true,
      IsSpecial: false
    });
    this.showModal.set(true);
  }

  async openEditModal(id: string | number) {
    try {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const record = await firstValueFrom(this.photoAlbumService.getById(String(id))) as any;
      if (record) {
        this.editMode.set(true);
        this.currentId.set(id);
        
        const nameVal = record.name || record.Name || '';
        const codeVal = record.code || record.Code || '';
        const descVal = record.description || record.Description || '';
        const sortOrderVal = record.sortOrder ?? record.SortOrder ?? 0;
        const imageVal = record.image || record.Image || record.images || record.Images || '';
        const statusVal = record.status ?? record.Status ?? 1;
        const isSpecialVal = record.isSpecial ?? record.IsSpecial ?? false;
        
        this.selectedImageBase64.set(imageVal);
        this.selectedImageFile.set(undefined);

        this.dataForm.patchValue({
          Name: nameVal,
          Code: codeVal,
          Description: descVal,
          SortOrder: Number(sortOrderVal),
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          Status: statusVal === 2 || (statusVal as any) === true,
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          IsSpecial: isSpecialVal === 1 || isSpecialVal === 2 || (isSpecialVal as any) === true || String(isSpecialVal) === 'true'
        });
        
        this.showModal.set(true);
      }
    } catch(err) {
      console.error(err);
    }
  }

  closeModal() {
    this.showModal.set(false);
  }

  get displayImage(): string {
    const val = this.selectedImageBase64();
    if (!val) return '';
    if (val.startsWith('data:image') || val.startsWith('http')) {
      return val;
    }
    const path = val.startsWith('/') ? val : '/' + val;
    return `${environment.imageUrl}${path}`;
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getImageUrl(element: any): string {
    // Dùng thẳng URL ảnh do API trả về (giống ảnh bìa OPAC)
    return element.image || element.Image || element.images || element.Images || '';
  }

  onFileSelected(event: Event) {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (file) {
      this.selectedImageFile.set(file);
      const reader = new FileReader();
      reader.onload = () => {
        this.selectedImageBase64.set(reader.result as string);
      };
      reader.readAsDataURL(file);
    }
  }

  onSubmit() {
    if (this.dataForm.valid) {
      const rawValues = this.dataForm.getRawValue();
      const payload: Partial<PhotoAlbum> = {
        name: rawValues.Name,
        code: rawValues.Code,
        description: rawValues.Description,
        sortOrder: rawValues.SortOrder,
        status: rawValues.Status ? 2 : 1,
        isSpecial: rawValues.IsSpecial ? true : false
      };
      
      if (this.editMode() && this.currentId()) {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const updatePayload: any = {
          id: String(this.currentId()),
          ...payload,
          publicId: String(this.currentId()),
        };
        this.photoAlbumService.update(updatePayload, this.selectedImageFile()).subscribe(() => {
          this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS'));
          this.refreshTable();
          this.closeModal();
        });
      } else {
        this.photoAlbumService.create(payload as PhotoAlbum, this.selectedImageFile()).subscribe(() => {
          this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS'));
          this.refreshTable();
          this.closeModal();
        });
      }
    }
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  toggleStatus(element: any) {
    const publicId = element.publicId || element.id || element.Id;
    const currentStatus = element.status === 2 || element.Status === 2 || element.status === true || element.Status === true;
    if (publicId) {
      this.photoAlbumService.changeStatus(publicId, !currentStatus).subscribe({
        next: () => {
          this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS'));
          this.loadData();
        },
        error: (err) => {
          console.error('Error toggling status', err);
          this.loadData(); // Revert back display
        }
      });
    }
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  toggleIsSpecial(element: any) {
    // If the backend doesn't support a dedicated toggle API for isSpecial, we can update the entire item.
    const publicId = element.publicId || element.id || element.Id;
    const currentIsSpecial = element.isSpecial === 1 || element.IsSpecial === 1 || element.isSpecial === true || element.IsSpecial === true || String(element.isSpecial) === 'true';
    if (publicId) {
       const updatedElement = {
           id: publicId,
           publicId: publicId,
           name: element.name || element.Name,
           code: element.code || element.Code,
           description: element.description || element.Description,
           sortOrder: element.sortOrder ?? element.SortOrder,
           status: element.status ?? element.Status,
           isSpecial: !currentIsSpecial,
           image: element.image || element.Image
       };

       this.photoAlbumService.update(updatedElement as any).subscribe({
           next: () => {
             this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS'));
             this.loadData();
           },
           error: (err) => {
               console.error('Error toggling isSpecial', err);
               this.loadData();
           }
       })
    }
  }

  handleDelete(id: string | number) {
    this.pendingId.set(id);
    this.showConfirm.set(true);
  }

  confirmActionExecute() {
    const id = this.pendingId();
    if (id === 'bulk') {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const selectedIds = this.selection.selected.map((item: any) => item.publicId || item.id || item.Id);
      const requests = selectedIds.map(selectedId => this.photoAlbumService.delete(String(selectedId)));
      forkJoin(requests).subscribe({
        next: () => {
          this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
          this.refreshTable();
          this.selection.clear();
          this.closeConfirm();
        },
        error: (err) => {
          console.error(err);
          this.refreshTable();
          this.closeConfirm();
        }
      });
      return;
    }

    if (id) {
      this.photoAlbumService.delete(String(id)).subscribe({
        next: () => {
          this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS'));
          this.refreshTable();
          this.closeConfirm();
        },
        error: (err) => {
          console.error(err);
          this.closeConfirm();
        }
      });
    }
  }

  closeConfirm() {
    this.showConfirm.set(false);
    this.pendingId.set(null);
  }

  public refreshTable() {
    this.loadData();
  }
}
