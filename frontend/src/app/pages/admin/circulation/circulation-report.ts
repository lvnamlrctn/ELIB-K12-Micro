import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { CirculationReportService } from '../../../services/circulation/circulation-report.service';
import { CircPlaceService } from '../../../services/printbook/circ-place.service';
import { ReaderTypeService } from '../../../services/circulation/reader-type.service';
import { OrgService } from '../../../services/circulation/org.service';
import { AcademicClassService } from '../../../services/circulation/academic-class.service';
import { CourseService } from '../../../services/circulation/course.service';
import { CircPlace } from '../../../models/printbook/circ-place';
import { CanDirective } from '../../../directives/can.directive';
import { ReportSignoff } from '../../../shared/report-signoff/report-signoff';

interface LookupOption { id: number; name: string; }

// Tiêu đề in cố định tiếng Việt — khớp đúng ReportTitle() phía backend (CirculationReportController).
const PRINT_TITLES: Record<number, string> = {
  1:  'HOẠT ĐỘNG PHỤC VỤ TẠI THƯ VIỆN',
  2:  'DANH SÁCH TÀI LIỆU ĐANG MƯỢN',
  3:  'TÀI LIỆU ĐANG MƯỢN THEO NGÀY TRẢ',
  4:  'DANH SÁCH TÀI LIỆU ĐANG MƯỢN QUÁ HẠN',
  5:  'DANH SÁCH TÀI LIỆU TRẢ QUÁ HẠN',
  6:  'BẠN ĐỌC HẾT HẠN THẺ CHƯA TRẢ SÁCH',
  7:  'BẠN ĐỌC QUÁ HẠN SÁCH',
  8:  'THỐNG KÊ TÀI LIỆU MƯỢN NHIỀU',
  9:  'THỐNG KÊ TÀI LIỆU KHÔNG ĐƯỢC MƯỢN',
  10: 'DANH SÁCH TÀI LIỆU ĐÃ TRẢ',
  11: 'DANH SÁCH TÀI LIỆU MẤT',
};

const PRINT_ROW_CAP = 5000; // khớp RowCap phía backend

@Component({
  selector: 'app-circulation-report',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, NgSelectModule, TranslateModule, MatIconModule, MatPaginatorModule, DateInputComponent, ReportSignoff],
  templateUrl: './circulation-report.html'
})
export class CirculationReportPage implements OnInit, OnDestroy {
  private service       = inject(CirculationReportService);
  private circPlaceSvc  = inject(CircPlaceService);
  private readerTypeSvc = inject(ReaderTypeService);
  private orgSvc        = inject(OrgService);
  private classSvc      = inject(AcademicClassService);
  private courseSvc     = inject(CourseService);
  private toastr    = inject(ToastrService);
  private router    = inject(Router);
  public  translate = inject(TranslateService);
  private destroy$  = new Subject<void>();

  reportTypeOptions = [
    { value: 1,  label: 'CIRC_REPORT.RT_1' },
    { value: 2,  label: 'CIRC_REPORT.RT_2' },
    { value: 3,  label: 'CIRC_REPORT.RT_3' },
    { value: 4,  label: 'CIRC_REPORT.RT_4' },
    { value: 5,  label: 'CIRC_REPORT.RT_5' },
    { value: 6,  label: 'CIRC_REPORT.RT_6' },
    { value: 7,  label: 'CIRC_REPORT.RT_7' },
    { value: 8,  label: 'CIRC_REPORT.RT_8' },
    { value: 9,  label: 'CIRC_REPORT.RT_9' },
    { value: 10, label: 'CIRC_REPORT.RT_10' },
    { value: 11, label: 'CIRC_REPORT.RT_11' },
  ];

  circPlaces  = signal<CircPlace[]>([]);
  readerTypes = signal<LookupOption[]>([]);
  orgs        = signal<LookupOption[]>([]);
  classes     = signal<LookupOption[]>([]);
  courses     = signal<LookupOption[]>([]);

  headers     = signal<string[]>([]);
  rows        = signal<string[][]>([]);
  totalRow    = signal<string[] | null>(null);
  totalCount  = signal(0);
  hasSearched = signal(false);

  parentLibrary = signal('');
  libraryName   = signal('');
  printHeaders  = signal<string[]>([]);
  printRows     = signal<string[][]>([]);
  printTotalRow = signal<string[] | null>(null);

  isLoading         = signal(false);
  isExportingExcel  = signal(false);
  isPrinting        = signal(false);

  pageSize = 20; pageIndex = 0; pageSizeOptions = [10, 20, 50, 100];
  @ViewChild(MatPaginator) paginator?: MatPaginator;

  searchForm = new FormGroup({
    dateFrom:     new FormControl<string>('', { nonNullable: true }),
    dateTo:       new FormControl<string>('', { nonNullable: true }),
    classId:      new FormControl<number | null>(null),
    courseId:     new FormControl<number | null>(null),
    orgId:        new FormControl<number | null>(null),
    readerTypeId: new FormControl<number | null>(null),
    circPlaceId:  new FormControl<number | null>(null),
    reportType:   new FormControl<number | null>(null),
  });

  private route = inject(ActivatedRoute);
  /** Loại báo cáo được mở thẳng qua ?reportType= (Đợt 21 — lối vào từ tab Lưu thông/Biên mục của dashboard):
   *  2 đang mượn, 7 quá hạn, 9 không được mượn, 10 đã trả. Giá trị khác bị bỏ qua. */
  private static readonly LINKABLE_REPORT_TYPES = [2, 7, 9, 10];

  ngOnInit(): void {
    this.loadLookups();
    const rt = Number(this.route.snapshot.queryParamMap.get('reportType'));
    if (CirculationReportPage.LINKABLE_REPORT_TYPES.includes(rt)) {
      this.searchForm.patchValue({ reportType: rt });
      this.runReport();
    }
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadLookups(): void {
    this.circPlaceSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.circPlaces.set(list), error: () => {} });
    const allParams = { draw: 0, start: 0, length: 1000, search: { value: '' } };
    this.readerTypeSvc.getAll(allParams).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => this.readerTypes.set((res.data || []).map(d => ({ id: Number(d.id), name: d.name }))), error: () => {}
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
  }

  private params() {
    const s = this.searchForm.getRawValue();
    return { ...s, pageIndex: this.pageIndex + 1, pageSize: this.pageSize };
  }

  runReport(): void {
    if (!this.searchForm.value.reportType) {
      this.toastr.warning(this.translate.instant('CIRC_REPORT.CHOOSE_REPORT_TYPE_FIRST'));
      return;
    }
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.fetchPage();
  }

  onPageChange(e: PageEvent): void {
    this.pageIndex = e.pageIndex;
    this.pageSize  = e.pageSize;
    this.fetchPage();
  }

  private fetchPage(): void {
    this.isLoading.set(true);
    this.hasSearched.set(true);
    this.service.search(this.params()).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        this.headers.set(res.headers); this.rows.set(res.rows); this.totalCount.set(res.totalCount); this.totalRow.set(res.totalRow);
        this.parentLibrary.set(res.parentLibrary); this.libraryName.set(res.libraryName);
        this.isLoading.set(false);
      },
      error: () => { this.isLoading.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); }
    });
  }

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
    if (!this.searchForm.value.reportType) {
      this.toastr.warning(this.translate.instant('CIRC_REPORT.CHOOSE_REPORT_TYPE_FIRST'));
      return;
    }
    if (this.isExportingExcel()) return;
    this.isExportingExcel.set(true);
    this.service.exportExcel(this.params()).pipe(takeUntil(this.destroy$)).subscribe({
      next: blob => { this.downloadBlob(blob, `bao-cao-luu-thong_${Date.now()}.xlsx`); this.isExportingExcel.set(false); },
      error: () => { this.isExportingExcel.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); }
    });
  }

  exit(): void { this.router.navigate(['/admin']); }

  printTitle(): string {
    const type = this.searchForm.value.reportType;
    return type ? (PRINT_TITLES[type] ?? '') : '';
  }

  printDateRange(): string {
    const { dateFrom, dateTo } = this.searchForm.value;
    if (!dateFrom && !dateTo) return '';
    const fmt = (d?: string | null) => d ? d.split('-').reverse().join('/') : '…';
    return `Từ ngày ${fmt(dateFrom)} đến ngày ${fmt(dateTo)}`;
  }

  printReport(): void {
    if (!this.searchForm.value.reportType) {
      this.toastr.warning(this.translate.instant('CIRC_REPORT.CHOOSE_REPORT_TYPE_FIRST'));
      return;
    }
    if (this.isPrinting()) return;
    this.isPrinting.set(true);
    this.service.search({ ...this.params(), pageIndex: 1, pageSize: PRINT_ROW_CAP }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        this.printHeaders.set(res.headers); this.printRows.set(res.rows); this.printTotalRow.set(res.totalRow);
        this.parentLibrary.set(res.parentLibrary); this.libraryName.set(res.libraryName);
        setTimeout(() => { window.print(); this.isPrinting.set(false); }, 0);
      },
      error: () => { this.isPrinting.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); }
    });
  }
}
