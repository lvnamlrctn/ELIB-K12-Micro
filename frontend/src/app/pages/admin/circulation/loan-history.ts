import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { HistoryBorrowService } from '../../../services/circulation/history-borrow.service';
import { SystemLogService } from '../../../services/system/system-log.service';
import { CircPlaceService } from '../../../services/printbook/circ-place.service';
import { ReaderTypeService } from '../../../services/circulation/reader-type.service';
import { OrgService } from '../../../services/circulation/org.service';
import { AcademicClassService } from '../../../services/circulation/academic-class.service';
import { CourseService } from '../../../services/circulation/course.service';
import { Borrow } from '../../../models/circulation/borrow';
import { CircPlace } from '../../../models/printbook/circ-place';
import { CanDirective } from '../../../directives/can.directive';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { ListPageStateService } from '../../../services/shared/list-page-state.service';
import { StoreService } from '../../../services/printbook/store.service';

interface LookupOption { id: number; name: string; }

@Component({
  selector: 'app-loan-history',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, NgSelectModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, DateInputComponent],
  templateUrl: './loan-history.html'
})
export class LoanHistoryPage implements OnInit, OnDestroy {
  private service        = inject(HistoryBorrowService);
  private listState = inject(ListPageStateService);
  private systemLogSvc   = inject(SystemLogService);
  private circPlaceSvc   = inject(CircPlaceService);
  private readerTypeSvc  = inject(ReaderTypeService);
  private orgSvc         = inject(OrgService);
  private storeSvc       = inject(StoreService);
  private classSvc       = inject(AcademicClassService);
  private courseSvc      = inject(CourseService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private router    = inject(Router);
  private destroy$  = new Subject<void>();

  statusOptions = [
    { value: null, label: 'LOAN_HISTORY.ST_ALL' },
    { value: 1, label: 'BORROW.ST_1' },
    { value: 2, label: 'BORROW.ST_2' },
    { value: 3, label: 'BORROW.ST_3' },
    { value: 4, label: 'LOAN_HISTORY.ST_LATE' },
  ];

  circPlaces  = signal<CircPlace[]>([]);
  readerTypes = signal<LookupOption[]>([]);
  orgs        = signal<LookupOption[]>([]);
  stores      = signal<LookupOption[]>([]);
  classes     = signal<LookupOption[]>([]);
  courses     = signal<LookupOption[]>([]);
  operatorOptions = signal<{ value: string; label: string }[]>([]);

  displayedColumns = ['stt', 'cardNo', 'readerName', 'barcode', 'bibTitle', 'borrowDate', 'dueDate', 'returnDate', 'staffName', 'status', 'actions'];
  dataSource: Borrow[] = [];

  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  isLoading = signal(false);
  isExportingExcel = signal(false);
  isExportingPdf = signal(false);

  searchForm = new FormGroup({
    cardNo:  new FormControl<string>('', { nonNullable: true }),
    title:   new FormControl<string>('', { nonNullable: true }),
    barcode: new FormControl<string>('', { nonNullable: true }),
    borrowDateFrom: new FormControl<string>('', { nonNullable: true }),
    borrowDateTo:   new FormControl<string>('', { nonNullable: true }),
    status:  new FormControl<number | null>(null),
    circPlaceId:  new FormControl<number | null>(null),
    readerTypeId: new FormControl<number | null>(null),
    orgId:        new FormControl<number | null>(null),
    storeId:      new FormControl<number | null>(null),
    classId:      new FormControl<number | null>(null),
    courseId:     new FormControl<number | null>(null),
    operatorId:   new FormControl<string>('', { nonNullable: true }),
    isOverdue:    new FormControl<boolean>(false, { nonNullable: true }),
    dueDateFrom:    new FormControl<string>('', { nonNullable: true }),
    dueDateTo:      new FormControl<string>('', { nonNullable: true }),
    returnDateFrom: new FormControl<string>('', { nonNullable: true }),
    returnDateTo:   new FormControl<string>('', { nonNullable: true }),
  });

  ngOnInit(): void {
    const saved = this.listState.recall('circulation.loan-history'); if (saved) { this.pageIndex = saved.pageIndex; this.pageSize = saved.pageSize; }
    this.loadData(); this.loadLookups(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadLookups(): void {
    this.circPlaceSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.circPlaces.set(list), error: () => {} });
    const allParams = { draw: 0, start: 0, length: 1000, search: { value: '' } };
    this.readerTypeSvc.getAll(allParams).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => this.readerTypes.set((res.data || []).map(d => ({ id: Number(d.id), name: d.name }))), error: () => {}
    });
    this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({
      next: list => this.stores.set(list.map(s => ({ id: Number(s.id), name: s.name || '' }))), error: () => {}
    });
    this.orgSvc.getAll(allParams).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => this.orgs.set((res.data || []).map(d => ({ id: Number(d.id), name: d.name }))), error: () => {}
    });
    this.classSvc.getAll(allParams).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => this.classes.set((res.data || []).map(d => ({ id: Number(d.id), name: d.name }))), error: () => {}
    });
    this.courseSvc.getAll(allParams).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => this.courses.set((res.data || []).map(d => ({ id: Number(d.id), name: d.name }))), error: () => {}
    });
    // Đợt 14 — lọc theo người thực hiện (BookOut.CreatedRowBy), tái dùng nguồn user sẵn có của Theo dõi hệ thống.
    this.systemLogSvc.getUsers().pipe(takeUntil(this.destroy$)).subscribe({
      next: users => this.operatorOptions.set(users.map(u => ({ value: String(u.id ?? ''), label: u.fullName || u.userName || '' }))),
      error: () => {}
    });
  }

  private params() {
    const s = this.searchForm.getRawValue();
    // operatorId đến từ ng-select dạng chuỗi (bindValue) — backend nhận long?, phải ép số trước khi gửi.
    return { ...s, operatorId: s.operatorId ? Number(s.operatorId) : null, pageIndex: this.pageIndex + 1, pageSize: this.pageSize };
  }
  loadData(): void {
    this.listState.remember('circulation.loan-history', this.pageIndex, this.pageSize);
    this.isLoading.set(true);
    this.service.search(this.params()).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
      error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
    });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  resetForm(): void { this.searchForm.reset(); this.triggerSearch(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  private downloadBlob(blob: Blob, filename: string): void {
    if (!blob.size) { this.toastr.warning(this.translate.instant('COMMON.NO_DATA')); return; }
    const url = URL.createObjectURL(blob);
    const a   = document.createElement('a');
    a.href     = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }

  exportExcel(): void {
    if (this.isExportingExcel()) return;
    this.isExportingExcel.set(true);
    this.service.exportExcel(this.params()).pipe(takeUntil(this.destroy$)).subscribe({
      next: blob => { this.downloadBlob(blob, `lich-su-muon-tra_${Date.now()}.xlsx`); this.isExportingExcel.set(false); },
      error: () => { this.isExportingExcel.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); }
    });
  }

  exportPdf(): void {
    if (this.isExportingPdf()) return;
    this.isExportingPdf.set(true);
    this.service.exportPdf(this.params()).pipe(takeUntil(this.destroy$)).subscribe({
      next: blob => { this.downloadBlob(blob, `lich-su-muon-tra_${Date.now()}.pdf`); this.isExportingPdf.set(false); },
      error: () => { this.isExportingPdf.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); }
    });
  }

  statusClass(s: number | undefined): string { switch (s) { case 3: return 'bg-red-50 text-red-700'; case 2: return 'bg-gray-100 text-gray-600'; default: return 'bg-green-50 text-green-700'; } }

  /** Đợt 14 — thẻ thông tin bạn đọc/sách khi đã lọc hẹp về 1 số thẻ/1 barcode cụ thể. Lấy trực tiếp từ
   * dataSource đã tải (không gọi thêm API) — đơn giản hoá có chủ đích, đủ ngữ cảnh cho cán bộ. */
  infoCard(): { readerName?: string; cardNo?: string; count: number; bookTitle?: string; barcode?: string } | null {
    if (this.dataSource.length === 0) return null;
    const cardNo = this.searchForm.get('cardNo')!.value?.trim();
    const barcode = this.searchForm.get('barcode')!.value?.trim();
    if (!cardNo && !barcode) return null;
    const card: { readerName?: string; cardNo?: string; count: number; bookTitle?: string; barcode?: string } = { count: this.totalRecords };
    if (cardNo) { card.readerName = this.dataSource[0].readerName; card.cardNo = this.dataSource[0].cardNo; }
    if (barcode) { card.bookTitle = this.dataSource[0].bibTitle; card.barcode = this.dataSource[0].barcode; }
    return card;
  }

  // Đợt 16 — mở Lịch sử thay đổi cho 1 giao dịch. Phiếu cũ không có publicId (chưa có liên kết giao dịch
  // gốc) thì ẩn nút, không suy đoán từ barcode/bạn đọc.
  openHistory(row: Borrow): void {
    if (!row.publicId) return;
    this.router.navigate(['/admin/entity-history'], { queryParams: { type: 'LoanTransaction', id: row.publicId } });
  }
}
