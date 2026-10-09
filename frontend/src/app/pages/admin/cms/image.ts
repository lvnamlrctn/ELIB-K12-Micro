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
import { PhotoService } from '../../../services/cms/photo';
import { PhotoAlbumService } from '../../../services/cms/photo-album.service';

import { Photo } from '../../../models/cms/photo';

import { environment } from '../../../../environments/environment';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { DepartmentService } from '../../../services/system/department.service';

@Component({
  selector: 'app-image',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule],
  templateUrl: './image.html'
})
export class ImagePage implements OnInit, OnDestroy, AfterViewInit {
  private photoService = inject(PhotoService);
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

  // Combobox Data
  photoAlbums = signal<any[]>([]);
  // Loại ảnh cố định (port ELIB-LRC 09-15) — "quang_cao" là nguồn banner trang chủ app mobile (PublicHomeBanner).
  readonly typeOptions = [
    { value: 'slide', label: 'Ảnh Slide' },
    { value: 'quang_cao', label: 'Ảnh quảng cáo' },
    { value: 'su_kien_mobile', label: 'Ảnh sự kiện mobile' },
  ];

  // Form
  dataForm = new FormGroup({
    Name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    Brief: new FormControl('', { nonNullable: true }),
    Types: new FormControl('', { nonNullable: true }),
    Position: new FormControl('', { nonNullable: true }),
    Link: new FormControl('', { nonNullable: true }),
    Image: new FormControl('', { nonNullable: true }),
    SortOrder: new FormControl<number | null>(null),
    Width: new FormControl<number | null>(null),
    Height: new FormControl<number | null>(null),
    PhotoAlbumId: new FormControl('', { nonNullable: true }),
    Status: new FormControl<boolean>(true, { nonNullable: true, validators: [Validators.required] })
  });

  // Table State
  displayedColumns: string[] = ['select', 'id', 'image', 'name', 'link', 'status', 'actions'];

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
  dataSource = new MatTableDataSource<Photo>([]);
  selection = new SelectionModel<Photo>(true, []);
  totalRecords = 0;
  pageSize = 10;
  pageIndex = 0;
  drawCount = 0;
  searchTerm = '';
  searchPhotoAlbumId = '0';
  selectedImageBase64 = signal<string>('');
  selectedImageFile = signal<File | undefined>(undefined);

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  ngOnInit() {
    this.applyTenantColumn();
    this.loadTenants();
    this.translate.onLangChange.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.loadData();
    });
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    this.photoAlbumService.getAll({ draw: 1, start: 0, length: 1000, search: { value: '' }, order: [] }).subscribe({
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      next: (res: any) => {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        this.photoAlbums.set((res.data || []).map((item: any) => ({ ...item, id: item.id ?? item.Id, name: item.name ?? item.Name })));
      }
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
      photoAlbumId: this.searchPhotoAlbumId,
      tenantId: this.tenantId,
      order: []
    };

    this.isLoading.set(true);
    this.photoService.getAll(params).pipe(takeUntil(this.destroy$)).subscribe({
      next: (res) => {
        this.dataSource.data = res.data;
        this.totalRecords = res.recordsTotal;
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Error loading data', err);
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
      Brief: '',
      Types: '',
      Position: '',
      Link: '',
      Image: '',
      SortOrder: null,
      Width: null,
      Height: null,
      PhotoAlbumId: '',
      Status: true
    });
    this.showModal.set(true);
  }

  async openEditModal(id: string | number) {
    try {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const record = await firstValueFrom(this.photoService.getById(String(id))) as any;
      if (record) {
        this.editMode.set(true);
        this.currentId.set(id);
        
        const nameVal = record.name || record.Name || '';
        const briefVal = record.brief || record.Brief || '';
        const typesVal = record.types || record.Types || '';
        const positionVal = record.position || record.Position || '';
        const linkVal = record.link || record.Link || '';
        const sortOrderVal = record.sortOrder || record.SortOrder;
        const widthVal = record.width || record.Width;
        const heightVal = record.height || record.Height;

        const imageVal = record.image || record.Image || '';
        const photoAlbumIdVal = record.photoAlbumId || record.PhotoAlbumId || '';
        const statusVal = record.status ?? record.Status ?? 1;
        
        this.selectedImageBase64.set(imageVal);
        this.selectedImageFile.set(undefined);

        this.dataForm.patchValue({
          Name: nameVal,
          Brief: briefVal,
          Types: typesVal,
          Position: positionVal,
          Link: linkVal,
          SortOrder: sortOrderVal,
          Width: widthVal,
          Height: heightVal,
          Image: imageVal,
          PhotoAlbumId: String(photoAlbumIdVal),
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          Status: statusVal === 2 || (statusVal as any) === true
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
    let defaultImage = element.image || element.Image || '';
    if (!defaultImage) return '';
    if (defaultImage.startsWith('http') || defaultImage.startsWith('data:image')) {
      return defaultImage;
    }
    if (!defaultImage.startsWith('/')) {
      defaultImage = '/' + defaultImage;
    }
    return `${environment.imageUrl}${defaultImage}`;
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
      const payload: Partial<Photo> = {
        name: rawValues.Name,
        brief: rawValues.Brief,
        types: rawValues.Types,
        position: rawValues.Position,
        link: rawValues.Link,
        sortOrder: rawValues.SortOrder || undefined,
        width: rawValues.Width || undefined,
        height: rawValues.Height || undefined,
        image: rawValues.Image,
        photoAlbumId: rawValues.PhotoAlbumId,
        status: rawValues.Status ? 2 : 1
      };
      
      if (this.editMode() && this.currentId()) {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const updatePayload: any = {
          id: String(this.currentId()),
          ...payload,
          publicId: String(this.currentId()),
        };
        this.photoService.update(updatePayload, this.selectedImageFile()).subscribe(() => {
          this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS'));
          this.refreshTable();
          this.closeModal();
        });
      } else {
        this.photoService.create(payload as Photo, this.selectedImageFile()).subscribe(() => {
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
      this.photoService.changeStatus(publicId, !currentStatus).subscribe({
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

  handleDelete(id: string | number) {
    this.pendingId.set(id);
    this.showConfirm.set(true);
  }

  confirmActionExecute() {
    const id = this.pendingId();
    if (id === 'bulk') {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const selectedIds = this.selection.selected.map((item: any) => item.publicId || item.id || item.Id);
      const requests = selectedIds.map(selectedId => this.photoService.delete(String(selectedId)));
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
      this.photoService.delete(String(id)).subscribe({
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
