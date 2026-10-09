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
import { MonHocService, MonHoc } from '../../../services/evaluate/mon-hoc.service';
import { EvaluateDegreeService } from '../../../services/evaluate/evaluate-degree.service';
import { KnowledgeService } from '../../../services/evaluate/knowledge.service';
import { CourseOptionService } from '../../../services/evaluate/course-option.service';
import { BaseEntity } from '../../../models/shared/base-entity';
import { DataTableParams } from '../../../models/shared/datatable';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { DepartmentService } from '../../../services/system/department.service';
import { ListPageStateService } from '../../../services/shared/list-page-state.service';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-mon-hoc',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, CanDirective, NgSelectModule],
  templateUrl: './mon-hoc.html'
})
export class MonHocPage implements OnInit, OnDestroy, AfterViewInit {
  private service = inject(MonHocService);
  private listState = inject(ListPageStateService);
  private degreeService = inject(EvaluateDegreeService);
  private knowledgeService = inject(KnowledgeService);
  private optionService = inject(CourseOptionService);
  public translate = inject(TranslateService);
  private toastr = inject(ToastrService);
  private auth = inject(Auth);
  private departmentService = inject(DepartmentService);
  private router = inject(Router);
  private destroy$ = new Subject<void>();

  selectedAttachmentFile = signal<File | null>(null);
  isUploadingAttachment = signal<boolean>(false);

  isPrivileged = this.auth.isPrivileged();
  tenantOptions: { id: string; name: string }[] = [];
  tenantId: string | null = null;

  degreeOptions: BaseEntity[] = [];
  knowledgeOptions: BaseEntity[] = [];
  courseOptionOptions: BaseEntity[] = [];

  showModal = signal<boolean>(false);
  showConfirmDelete = signal<boolean>(false);
  editMode = signal<boolean>(false);
  itemToDelete = signal<string | null>(null);
  currentId = signal<string | null>(null);
  currentItem = signal<MonHoc | null>(null);
  isLoading = signal<boolean>(false);

  dataForm = new FormGroup({
    maMon: new FormControl('', [Validators.required]),
    tenMon: new FormControl('', [Validators.required]),
    soTinChi: new FormControl<number | null>(null),
    degreeId: new FormControl<number | null>(null),
    knowledgeId: new FormControl<number | null>(null),
    optionId: new FormControl<number | null>(null),
    nguoiBienSoan: new FormControl(''),
    attachment: new FormControl(''),
    note: new FormControl(''),
    active: new FormControl<number>(2)
  });

  displayedColumns: string[] = ['select', 'id', 'maMon', 'tenMon', 'soTinChi', 'actions'];
  dataSource = new MatTableDataSource<MonHoc>([]);
  selection = new SelectionModel<MonHoc>(true, []);
  totalRecords = 0;
  pageSize = 10;
  pageIndex = 0;
  drawCount = 0;
  searchTerm = '';

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  ngOnInit() {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    const saved = this.listState.recall('evaluate.mon-hoc'); if (saved) { this.pageIndex = saved.pageIndex; this.pageSize = saved.pageSize; }
    this.translate.onLangChange.pipe(takeUntil(this.destroy$)).subscribe(() => this.loadData());
    if (this.isPrivileged) {
      this.departmentService.getAll({ draw: 1, start: 0, length: 500, search: { value: '', regex: false } })
        .pipe(takeUntil(this.destroy$))
        .subscribe(res => this.tenantOptions = res.data.map((t: any) => ({ id: t.publicId ?? t.id, name: t.name })));
    }
    this.degreeService.searchAll().pipe(takeUntil(this.destroy$)).subscribe(list => this.degreeOptions = list);
    this.knowledgeService.searchAll().pipe(takeUntil(this.destroy$)).subscribe(list => this.knowledgeOptions = list);
    this.optionService.searchAll().pipe(takeUntil(this.destroy$)).subscribe(list => this.courseOptionOptions = list);
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
    this.listState.remember('evaluate.mon-hoc', this.pageIndex, this.pageSize);
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
    this.searchTerm = (document.getElementById('searchMonHoc') as HTMLInputElement)?.value || '';
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.selection.clear();
    this.loadData();
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
    this.selectedAttachmentFile.set(null);
    this.dataForm.reset({ active: 2 });
    this.showModal.set(true);
  }

  async openEditModal(id: string) {
    try {
      const item = await firstValueFrom(this.service.getById(id));
      this.currentItem.set(item);
      this.currentId.set(item.publicId || item.id || id);
      this.selectedAttachmentFile.set(null);
      this.dataForm.patchValue({
        maMon: item.maMon,
        tenMon: item.tenMon,
        soTinChi: item.soTinChi ?? null,
        degreeId: item.degreeId ?? null,
        knowledgeId: item.knowledgeId ?? null,
        optionId: item.optionId ?? null,
        nguoiBienSoan: item.nguoiBienSoan,
        attachment: item.attachment,
        note: item.note,
        active: item.active ?? 2
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
    this.selectedAttachmentFile.set(null);
    this.dataForm.reset({ active: 2 });
  }

  onAttachmentFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    this.selectedAttachmentFile.set(input.files?.[0] ?? null);
  }

  clearAttachment() {
    this.selectedAttachmentFile.set(null);
    this.dataForm.patchValue({ attachment: '' });
  }

  onSubmit() {
    if (this.dataForm.invalid) return;

    const file = this.selectedAttachmentFile();
    if (file) {
      this.isUploadingAttachment.set(true);
      this.service.uploadAttachment(file).subscribe({
        next: (url) => {
          this.isUploadingAttachment.set(false);
          this.saveWithAttachment(url || this.dataForm.value.attachment || '');
        },
        error: () => {
          this.isUploadingAttachment.set(false);
          this.toastr.error(this.translate.instant('MONHOC.UPLOAD_ERROR'));
        }
      });
    } else {
      this.saveWithAttachment(this.dataForm.value.attachment || '');
    }
  }

  private saveWithAttachment(attachmentUrl: string) {
    const v = this.dataForm.value;
    const payload: Partial<MonHoc> = {
      maMon: v.maMon || '',
      tenMon: v.tenMon || '',
      soTinChi: v.soTinChi ?? null,
      degreeId: v.degreeId ?? null,
      knowledgeId: v.knowledgeId ?? null,
      optionId: v.optionId ?? null,
      nguoiBienSoan: v.nguoiBienSoan || '',
      attachment: attachmentUrl,
      note: v.note || '',
      active: v.active ?? 2
    };

    if (this.editMode() && this.currentId()) {
      this.service.update(this.currentId()!, payload).subscribe({
        next: () => {
          this.closeModal();
          this.loadData();
          this.toastr.success(this.translate.instant('COMMON.UPDATE_SUCCESS'));
        },
        error: (err) => console.error('Update failed', err)
      });
    } else {
      this.service.create(payload).subscribe({
        next: () => {
          this.closeModal();
          this.loadData();
          this.toastr.success(this.translate.instant('COMMON.ADD_SUCCESS'));
        },
        error: (err) => console.error('Create failed', err)
      });
    }
  }

  goToDocuments(id: string) {
    this.router.navigate(['/admin/subjects', id, 'documents']);
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
}
