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
import { LinkService } from '../../../services/cms/link';
import { LinkGroupService } from '../../../services/cms/link-group';
import { Link } from '../../../models/cms/link';
import { LinkGroup } from '../../../models/cms/link-group';
import { environment } from '../../../../environments/environment';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { DepartmentService } from '../../../services/system/department.service';

@Component({
  selector: 'app-link',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule],
  templateUrl: './link.html'
})
export class LinkPage implements OnInit, OnDestroy, AfterViewInit {
  private linkService = inject(LinkService);
  private linkGroupService = inject(LinkGroupService);
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
  linkGroups = signal<LinkGroup[]>([]);

  // Form
  dataForm = new FormGroup({
    Name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    linkUrl: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^(https?:\/\/)?([\da-z.-]+)\.([a-z.]{2,6})([/\w .-]*)*\/?$/)] }),
    Description: new FormControl('', { nonNullable: true }),
    Images: new FormControl('', { nonNullable: true }),
    LinkGroupId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
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
  dataSource = new MatTableDataSource<Link>([]);
  selection = new SelectionModel<Link>(true, []);
  totalRecords = 0;
  pageSize = 10;
  pageIndex = 0;
  drawCount = 0;
  searchTerm = '';
  searchLinkGroupId = '0';
  selectedImageBase64 = signal<string>('');
  selectedImageFile = signal<File | undefined>(undefined);

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  ngOnInit() {
    this.applyTenantColumn();
    this.loadTenants();
    this.translate.onLangChange.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.loadData();
    });
    this.loadLinkGroups();
  }

  ngAfterViewInit() {
    this.loadData();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadLinkGroups() {
    this.linkGroupService.getAll({ draw: 1, start: 0, length: 1000, search: { value: '' }, order: [] }).subscribe({
      next: (res) => {
        this.linkGroups.set(res.data || []);
      }
    });
  }

  loadData() {
    this.drawCount++;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const params: any = {
      draw: this.drawCount,
      start: this.pageIndex * this.pageSize,
      length: this.pageSize,
      search: { value: this.searchTerm },
      linkGroupId: this.searchLinkGroupId,
      tenantId: this.tenantId,
      order: []
    };

    this.isLoading.set(true);
    this.linkService.getAll(params).pipe(takeUntil(this.destroy$)).subscribe({
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
      linkUrl: '',
      Description: '',
      Images: '',
      LinkGroupId: '',
      Status: true
    });
    this.showModal.set(true);
  }

  async openEditModal(id: string | number) {
    try {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const record = await firstValueFrom(this.linkService.getById(String(id))) as any;
      if (record) {
        this.editMode.set(true);
        this.currentId.set(id);
        
        const nameVal = record.name || record.Name || '';
        const linkVal = record.linkUrl || record.LinkUrl || record.link || record.Link || '';
        const descVal = record.description || record.Description || '';
        const imageVal = record.images || record.Images || record.image || record.Image || '';
        const linkGroupIdVal = record.linkGroupId || record.LinkGroupId || '';
        const statusVal = record.status ?? record.Status ?? 1;
        
        this.selectedImageBase64.set(imageVal);
        this.selectedImageFile.set(undefined);

        this.dataForm.patchValue({
          Name: nameVal,
          linkUrl: linkVal,
          Description: descVal,
          Images: imageVal,
          LinkGroupId: String(linkGroupIdVal),
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
    // Dùng thẳng URL ảnh do API trả về (giống ảnh bìa OPAC)
    return element.images || element.Images || element.image || element.Image || '';
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
      const payload: Partial<Link> = {
        name: rawValues.Name,
        linkUrl: rawValues.linkUrl,
        description: rawValues.Description,
        images: rawValues.Images,
        linkGroupId: rawValues.LinkGroupId,
        status: rawValues.Status ? 2 : 1
      };
      
      if (this.editMode() && this.currentId()) {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const updatePayload: any = {
          id: String(this.currentId()),
          ...payload,
          publicId: String(this.currentId()),
        };
        this.linkService.update(updatePayload, this.selectedImageFile()).subscribe(() => {
          this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS'));
          this.refreshTable();
          this.closeModal();
        });
      } else {
        this.linkService.create(payload as Link, this.selectedImageFile()).subscribe(() => {
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
      this.linkService.changeStatus(publicId, !currentStatus).subscribe({
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
      const requests = selectedIds.map(selectedId => this.linkService.delete(String(selectedId)));
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
      this.linkService.delete(String(id)).subscribe({
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
