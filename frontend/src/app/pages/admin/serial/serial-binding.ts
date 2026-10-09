import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { SelectionModel } from '@angular/cdk/collections';
import { MatTableModule } from '@angular/material/table';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { SerialBindingService } from '../../../services/serial/serial-binding.service';
import { SerialBinding, SerialBindingItem } from '../../../models/serial/serial-binding';
import { StoreService } from '../../../services/printbook/store.service';
import { SubscriptionService } from '../../../services/serial/subscription.service';
import { Subscription } from '../../../models/serial/subscription';
import { SerialIssueService } from '../../../services/serial/serial-issue.service';
import { SerialIssue } from '../../../models/serial/serial-issue';
import { CanDirective } from '../../../directives/can.directive';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-serial-binding',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, MatTableModule, MatCheckboxModule, MatPaginatorModule, MatIconModule, AppDatePipe, DateInputComponent, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './serial-binding.html'
})
export class SerialBindingPage implements OnInit, OnDestroy {
  private service   = inject(SerialBindingService);
  private storeSvc  = inject(StoreService);
  private subSvc    = inject(SubscriptionService);
  private issueSvc  = inject(SerialIssueService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private auth      = inject(Auth);
  private destroy$  = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['select', 'stt', 'accessionNo', 'volumeTitle', 'store', 'bindingDate', 'itemCount', 'actions'];
  dataSource: SerialBinding[] = [];
  selection = new SelectionModel<SerialBinding>(true, []);
  stores = signal<{ id: number; name: string }[]>([]);
  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  searchForm = new FormGroup({
    accessionNo: new FormControl<string>('', { nonNullable: true }),
    volumeTitle: new FormControl<string>('', { nonNullable: true }),
  });

  showModal = signal(false);
  editMode = signal(false);
  currentPublicId = signal<string | null>(null);
  isLoading = signal(false);
  isSaving = signal(false);
  showConfirmDelete = signal(false);
  confirmDeletePublicId = signal<string | null>(null);

  dataForm = new FormGroup({
    AccessionNo:       new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
    StoreId:           new FormControl<number | null>(null),
    VolumeTitle:       new FormControl<string>('', { nonNullable: true }),
    SubscriptionId:    new FormControl<number | null>(null),
    SubscriptionTitle: new FormControl<string>('', { nonNullable: true }),
    BindingDate:       new FormControl<string>('', { nonNullable: true }),
    Note:              new FormControl<string>('', { nonNullable: true }),
  });
  items = signal<SerialBindingItem[]>([]);

  // sub-modal: tìm tạp chí → chọn → tải các số đã nhận → tích chọn → thêm vào items
  showAddIssuesModal = signal(false);
  issueSearchTitle = signal('');
  issueSearchResults = signal<Subscription[]>([]);
  pickedSubscription = signal<Subscription | null>(null);
  candidateIssues = signal<SerialIssue[]>([]);
  candidateSelection = new SelectionModel<SerialIssue>(true, []);
  isSearchingIssues = signal(false);

  // in nhãn gáy tập
  printTarget = signal<SerialBinding[]>([]);

  ngOnInit(): void {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.stores.set(list.map(s => ({ id: Number(s.id), name: s.name || '' }))), error: () => {} });
    this.loadData();
  }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  getStoreName(id: number | null | undefined): string { if (!id) return '—'; return this.stores().find(s => s.id === id)?.name || '—'; }

  loadData(): void {
    this.isLoading.set(true);
    const f = this.searchForm.getRawValue();
    this.service.search({ accessionNo: f.accessionNo || null, volumeTitle: f.volumeTitle || null, pageIndex: this.pageIndex + 1, pageSize: this.pageSize, tenantId: this.tenantId })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  onTenantChange(): void { this.triggerSearch(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  isAllSelected(): boolean { return this.dataSource.length > 0 && this.selection.selected.length === this.dataSource.length; }
  masterToggle(): void { this.isAllSelected() ? this.selection.clear() : this.dataSource.forEach(row => this.selection.select(row)); }

  openAddModal(): void {
    this.editMode.set(false); this.currentPublicId.set(null);
    const today = new Date().toISOString().substring(0, 10);
    this.dataForm.reset({ AccessionNo: '', StoreId: null, VolumeTitle: '', SubscriptionId: null, SubscriptionTitle: '', BindingDate: today, Note: '' });
    this.items.set([]);
    this.showModal.set(true);
  }
  openEditModal(item: SerialBinding): void {
    if (!item.publicId) return;
    this.editMode.set(true); this.currentPublicId.set(item.publicId);
    this.service.getById(item.publicId).pipe(takeUntil(this.destroy$)).subscribe(d => {
      this.dataForm.patchValue({
        AccessionNo: d.accessionNo ?? '', StoreId: d.storeId ?? null, VolumeTitle: d.volumeTitle ?? '',
        SubscriptionId: d.subscriptionId ?? null, SubscriptionTitle: d.subscriptionTitle ?? '',
        BindingDate: (d.bindingDate || '').substring(0, 10), Note: d.note ?? '',
      });
      this.items.set(d.items ? [...d.items] : []);
      this.showModal.set(true);
    });
  }
  closeModal(): void { this.showModal.set(false); }

  removeItem(issueId: number): void { this.items.set(this.items().filter(i => i.issueId !== issueId)); }

  onSubmit(): void {
    if (this.dataForm.invalid) { this.dataForm.markAllAsTouched(); return; }
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload: Partial<SerialBinding> = {
      accessionNo: v.AccessionNo, storeId: v.StoreId ?? undefined, volumeTitle: v.VolumeTitle,
      subscriptionId: v.SubscriptionId ?? undefined, subscriptionTitle: v.SubscriptionTitle,
      bindingDate: v.BindingDate || undefined, note: v.Note, items: this.items(),
    };
    const publicId = this.currentPublicId(); const mode = this.editMode();
    const req$ = mode && publicId !== null ? this.service.update(publicId, payload) : this.service.create(payload);
    req$.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isSaving.set(false); this.closeModal(); (mode ? this.loadData() : this.triggerSearch()); this.toastr.success(this.translate.instant(mode ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS')); },
      error: err => { this.isSaving.set(false); this.toastr.error(err?.error?.message || this.translate.instant('COMMON.SAVE_ERROR')); }
    });
  }

  handleDelete(item: SerialBinding): void { if (!item.publicId) return; this.confirmDeletePublicId.set(item.publicId); this.showConfirmDelete.set(true); }
  closeConfirm(): void { this.showConfirmDelete.set(false); this.confirmDeletePublicId.set(null); }
  confirmActionExecute(): void {
    const publicId = this.confirmDeletePublicId(); if (publicId == null) return;
    this.service.delete(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.closeConfirm(); this.loadData(); },
      error: err => { this.closeConfirm(); this.toastr.error(err?.error?.message || this.translate.instant('COMMON.DELETE_ERROR')); }
    });
  }

  // sub-modal thêm số phát hành
  openAddIssuesModal(): void {
    this.issueSearchTitle.set(''); this.issueSearchResults.set([]); this.pickedSubscription.set(null);
    this.candidateIssues.set([]); this.candidateSelection.clear();
    this.showAddIssuesModal.set(true);
  }
  closeAddIssuesModal(): void { this.showAddIssuesModal.set(false); }
  searchIssueSubscriptions(): void {
    const title = this.issueSearchTitle().trim(); if (!title) return;
    this.isSearchingIssues.set(true);
    this.subSvc.search({ title, pageSize: 20 }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => { this.issueSearchResults.set(res.data); this.isSearchingIssues.set(false); },
      error: () => { this.isSearchingIssues.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); }
    });
  }
  pickSubscription(s: Subscription): void {
    this.pickedSubscription.set(s);
    this.isSearchingIssues.set(true);
    this.issueSvc.search({ subscriptionId: s.id, pageSize: 1000 }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        const boundIds = new Set(this.items().map(i => i.issueId));
        this.candidateIssues.set(res.data.filter(i => i.status === 1 && !boundIds.has(i.id)));
        this.candidateSelection.clear();
        this.isSearchingIssues.set(false);
      },
      error: () => { this.isSearchingIssues.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); }
    });
  }
  confirmAddIssues(): void {
    const picked = this.candidateSelection.selected;
    if (!picked.length) { this.closeAddIssuesModal(); return; }
    const sub = this.pickedSubscription();
    const newItems: SerialBindingItem[] = picked.map(i => ({ issueId: i.id, serialSeq: i.serialSeq, publishedDate: i.publishedDate }));
    this.items.set([...this.items(), ...newItems]);
    if (sub && !this.dataForm.getRawValue().SubscriptionTitle) {
      this.dataForm.patchValue({ SubscriptionId: sub.id, SubscriptionTitle: sub.title || '' });
    }
    this.closeAddIssuesModal();
  }

  // in nhãn gáy tập
  printSingleLabel(item: SerialBinding): void { this.printTarget.set([item]); setTimeout(() => this.triggerPrint()); }
  printSelectedLabels(): void { if (!this.selection.selected.length) return; this.printTarget.set([...this.selection.selected]); setTimeout(() => this.triggerPrint()); }
  triggerPrint(): void { if (typeof window !== 'undefined') window.print(); }

  spineRange(item: SerialBinding): string {
    const list = item.items || []; if (!list.length) return '—';
    const first = list[0].serialSeq || ''; const last = list[list.length - 1].serialSeq || '';
    return first && last && first !== last ? `${first} — ${last}` : (first || last || '—');
  }
}
