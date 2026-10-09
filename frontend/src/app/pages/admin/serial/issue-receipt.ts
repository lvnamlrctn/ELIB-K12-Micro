import { Component, inject, OnInit, OnDestroy, signal, computed, ViewChild } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormGroup, FormControl } from '@angular/forms';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { SelectionModel } from '@angular/cdk/collections';
import { MatTableModule } from '@angular/material/table';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil, forkJoin, of } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { SerialIssueService } from '../../../services/serial/serial-issue.service';
import { SerialIssue, SerialStatistics } from '../../../models/serial/serial-issue';
import { PatternMagazineService } from '../../../services/serial/pattern-magazine.service';
import { PatternMagazineDetail } from '../../../models/serial/pattern-magazine';
import { CanDirective } from '../../../directives/can.directive';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';

interface MergeLevel { key: 'X' | 'Y' | 'Z'; caption: string; value: string; }

@Component({
  selector: 'app-serial-issue-receipt',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, FormsModule, TranslateModule, RouterLink, MatTableModule, MatCheckboxModule, MatPaginatorModule, MatIconModule, AppDatePipe, DateInputComponent, NgSelectModule],
  templateUrl: './issue-receipt.html'
})
export class IssueReceiptPage implements OnInit, OnDestroy {
  private service   = inject(SerialIssueService);
  private patternSvc = inject(PatternMagazineService);
  private route     = inject(ActivatedRoute);
  private router    = inject(Router);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private destroy$  = new Subject<void>();

  subscriptionId = signal<number | null>(null);
  subscriptionTitle = signal<string>('');
  patternId = signal<number | null>(null);
  stats = signal<SerialStatistics | null>(null);
  isPredicting = signal(false);
  showPredict = signal(false);
  predictCount = 12;

  // 0=dự kiến,1=đã nhận,2=khiếu nại,3=thiếu
  statusOptions = [ { id: 0, label: 'EXPECTED' }, { id: 1, label: 'RECEIVED' }, { id: 2, label: 'CLAIMED' }, { id: 3, label: 'MISSING' } ];

  displayedColumns = ['select', 'stt', 'serialSeq', 'plannedDate', 'publishedDate', 'quantity', 'status', 'claimCount', 'actions'];
  dataSource: SerialIssue[] = [];
  selection = new SelectionModel<SerialIssue>(true, []);
  totalRecords = 0; pageSize = 20; pageIndex = 0; pageSizeOptions = [10, 20, 50, 100];
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  // ghép số phát hành (cẩm nang mục VII.4)
  showMergeModal = signal(false);
  isMerging = signal(false);
  mergeLevels = signal<MergeLevel[]>([]);
  mergeForm = new FormGroup({
    Stt:           new FormControl<number>(1, { nonNullable: true }),
    PublishedDate: new FormControl<string>('', { nonNullable: true }),
    Quantity:      new FormControl<number>(1, { nonNullable: true }),
  });
  mergedLabel = computed(() => this.mergeLevels().filter(l => (l.value || '').trim()).map(l => `${l.caption} ${l.value.trim()}`).join(', '));

  showModal = signal(false);
  editMode = signal(false);
  currentId = signal<number | null>(null);
  isLoading = signal(false);
  isSaving = signal(false);
  showConfirmDelete = signal(false);
  confirmDeletePublicId = signal<string | null>(null);

  dataForm = new FormGroup({
    SerialSeq:     new FormControl<string>('', { nonNullable: true }),
    SerialSeqX:    new FormControl<number>(0, { nonNullable: true }),
    SerialSeqY:    new FormControl<number>(0, { nonNullable: true }),
    SerialSeqZ:    new FormControl<number>(0, { nonNullable: true }),
    PlannedDate:   new FormControl<string>('', { nonNullable: true }),
    PublishedDate: new FormControl<string>('', { nonNullable: true }),
    Quantity:      new FormControl<number>(1, { nonNullable: true }),
    Status:        new FormControl<number>(1, { nonNullable: true }),
    IsSpecial:     new FormControl<boolean>(false, { nonNullable: true }),
    Note:          new FormControl<string>('', { nonNullable: true }),
  });

  ngOnInit(): void {
    this.route.queryParams.pipe(takeUntil(this.destroy$)).subscribe(p => {
      const sid = p['subscriptionId'] ? Number(p['subscriptionId']) : null;
      this.subscriptionId.set(sid);
      this.subscriptionTitle.set(p['title'] || '');
      const pid = p['patternId'] ? Number(p['patternId']) : null;
      this.patternId.set(pid);
      if (sid) this.loadData();
    });
  }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  getStatusLabel(id: number | null | undefined): string {
    const o = this.statusOptions.find(s => s.id === (id ?? 0));
    return this.translate.instant('SERIAL_ISSUE.STATUS_' + (o ? o.label : 'EXPECTED'));
  }
  statusClass(id: number | null | undefined): string {
    switch (id) { case 1: return 'bg-green-100 text-green-700'; case 2: return 'bg-amber-100 text-amber-700'; case 3: return 'bg-red-100 text-red-700'; default: return 'bg-gray-100 text-gray-600'; }
  }

  loadData(): void {
    const sid = this.subscriptionId(); if (!sid) return;
    this.isLoading.set(true);
    this.service.search({ subscriptionId: sid, pageIndex: this.pageIndex + 1, pageSize: this.pageSize })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
    this.service.getStatistics(sid).pipe(takeUntil(this.destroy$)).subscribe({ next: s => this.stats.set(s), error: () => {} });
  }

  openPredict(): void { this.predictCount = 12; this.showPredict.set(true); }
  closePredict(): void { this.showPredict.set(false); }
  runPredict(): void {
    const sid = this.subscriptionId(); if (!sid) return;
    const count = Math.max(1, Math.min(this.predictCount || 1, 366));
    this.isPredicting.set(true);
    this.service.predict(sid, count).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isPredicting.set(false); this.closePredict(); this.loadData(); this.toastr.success(this.translate.instant('SERIAL_ISSUE.PREDICT_OK')); },
      error: () => { this.isPredicting.set(false); }
    });
  }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  openAddModal(): void {
    const today = new Date().toISOString().substring(0, 10);
    this.editMode.set(false); this.currentId.set(null);
    this.dataForm.reset({ SerialSeq: '', SerialSeqX: 0, SerialSeqY: 0, SerialSeqZ: 0, PlannedDate: today, PublishedDate: today, Quantity: 1, Status: 1, IsSpecial: false, Note: '' });
    this.showModal.set(true);
  }
  openEditModal(item: SerialIssue): void {
    this.editMode.set(true); this.currentId.set(item.id);
    this.dataForm.patchValue({
      SerialSeq: item.serialSeq ?? '', SerialSeqX: item.serialSeqX ?? 0, SerialSeqY: item.serialSeqY ?? 0, SerialSeqZ: item.serialSeqZ ?? 0,
      PlannedDate: (item.plannedDate || '').substring(0, 10), PublishedDate: (item.publishedDate || '').substring(0, 10),
      Quantity: item.quantity ?? 1, Status: item.status ?? 1, IsSpecial: (item.isSpecial ?? 0) === 1, Note: item.note ?? '',
    });
    this.showModal.set(true);
  }
  closeModal(): void { this.showModal.set(false); }

  onSubmit(): void {
    const sid = this.subscriptionId(); if (!sid) return;
    const v = this.dataForm.getRawValue();
    this.isSaving.set(true);
    const payload: Partial<SerialIssue> = {
      id: this.currentId() ?? 0, subscriptionId: sid,
      serialSeq: v.SerialSeq, serialSeqX: v.SerialSeqX, serialSeqY: v.SerialSeqY, serialSeqZ: v.SerialSeqZ,
      plannedDate: v.PlannedDate || undefined, publishedDate: v.PublishedDate || undefined,
      quantity: v.Quantity, status: (v.Status as SerialIssue['status']), isSpecial: v.IsSpecial ? 1 : 0, note: v.Note,
    };
    this.service.save(payload).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.isSaving.set(false); this.closeModal(); this.loadData(); this.toastr.success(this.translate.instant(this.editMode() ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS')); },
      error: () => { this.isSaving.set(false); }
    });
  }

  // nhận nhanh một kỳ dự kiến
  receive(item: SerialIssue): void {
    const payload: Partial<SerialIssue> = { ...item, status: 1, publishedDate: new Date().toISOString().substring(0, 10) };
    this.service.save(payload).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('SERIAL_ISSUE.RECEIVED_OK')); this.loadData(); },
      error: () => {}
    });
  }
  // khiếu nại kỳ thiếu
  claim(item: SerialIssue): void {
    this.service.claim({ id: item.id }).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('SERIAL_ISSUE.CLAIMED_OK')); this.loadData(); },
      error: () => {}
    });
  }

  handleDelete(item: SerialIssue): void { if (!item.publicId) return; this.confirmDeletePublicId.set(item.publicId); this.showConfirmDelete.set(true); }
  closeConfirm(): void { this.showConfirmDelete.set(false); this.confirmDeletePublicId.set(null); }
  confirmActionExecute(): void {
    const publicId = this.confirmDeletePublicId(); if (publicId == null) return;
    this.service.delete(publicId).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => { this.toastr.success(this.translate.instant('COMMON.DELETE_SUCCESS')); this.closeConfirm(); this.loadData(); },
      error: () => { this.closeConfirm(); }
    });
  }

  // chọn nhiều dòng để ghép số
  isAllSelected(): boolean { return this.dataSource.length > 0 && this.selection.selected.length === this.dataSource.length; }
  masterToggle(): void { this.isAllSelected() ? this.selection.clear() : this.dataSource.forEach(row => this.selection.select(row)); }

  // ghép số phát hành (cẩm nang mục VII.4) — theo cấu trúc 3 cấp X/Y/Z của mẫu phát hành
  openMergeModal(): void {
    const sources = this.selection.selected;
    if (sources.length < 2) return;
    const buildLevel = (key: 'X' | 'Y' | 'Z', caption: string, getVal: (i: SerialIssue) => number | undefined): MergeLevel => {
      const vals = Array.from(new Set(sources.map(getVal).filter((v): v is number => v !== undefined && v !== null)));
      return { key, caption, value: vals.length ? vals.join('+') : '' };
    };
    const applyLevels = (det: PatternMagazineDetail | null) => {
      this.mergeLevels.set([
        buildLevel('X', det?.x || 'Cấp 1', i => i.serialSeqX),
        buildLevel('Y', det?.y || 'Cấp 2', i => i.serialSeqY),
        buildLevel('Z', det?.z || 'Cấp 3', i => i.serialSeqZ),
      ]);
    };
    const minStt = Math.min(...sources.map(s => s.sortOrder ?? s.id));
    const sumQty = sources.reduce((sum, s) => sum + (s.quantity ?? 0), 0);
    const lastDate = sources.map(s => s.publishedDate || s.plannedDate).filter(Boolean).sort().pop() || '';
    this.mergeForm.reset({ Stt: minStt, PublishedDate: lastDate.substring(0, 10), Quantity: sumQty || 1 });
    const pid = this.patternId();
    if (pid) this.patternSvc.getByNumericId(pid).pipe(takeUntil(this.destroy$)).subscribe(det => applyLevels(det?.detail ?? null));
    else applyLevels(null);
    this.showMergeModal.set(true);
  }
  closeMergeModal(): void { this.showMergeModal.set(false); }
  updateMergeLevelValue(key: 'X' | 'Y' | 'Z', value: string): void {
    this.mergeLevels.set(this.mergeLevels().map(l => l.key === key ? { ...l, value } : l));
  }

  mergeIssues(): void {
    const sid = this.subscriptionId(); if (!sid) return;
    const label = this.mergedLabel();
    if (!label) { this.toastr.error(this.translate.instant('SERIAL_ISSUE.MERGE_EMPTY_LABEL')); return; }
    const sources = this.selection.selected;
    const v = this.mergeForm.getRawValue();
    this.isMerging.set(true);
    const payload: Partial<SerialIssue> = {
      id: 0, subscriptionId: sid, serialSeq: label,
      plannedDate: v.PublishedDate || undefined, publishedDate: v.PublishedDate || undefined,
      quantity: v.Quantity, status: 1, isMerged: true, sortOrder: v.Stt,
      note: `Gộp từ: ${sources.map(s => s.serialSeq || `#${s.id}`).join(', ')}`,
    };
    this.service.save(payload).pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        const toDelete = sources.filter(s => s.publicId).map(s => this.service.delete(s.publicId!));
        (toDelete.length ? forkJoin(toDelete) : of([])).pipe(takeUntil(this.destroy$)).subscribe(() => {
          this.isMerging.set(false); this.selection.clear(); this.closeMergeModal(); this.loadData();
          this.toastr.success(this.translate.instant('SERIAL_ISSUE.MERGE_OK'));
        });
      },
      error: () => { this.isMerging.set(false); }
    });
  }
}
