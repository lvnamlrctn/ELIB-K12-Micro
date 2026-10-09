import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { StoreBookReportService } from '../../../services/printbook/store-book-report.service';
import { StoreService } from '../../../services/printbook/store.service';
import { CanDirective } from '../../../directives/can.directive';
import { ReportSignoff } from '../../../shared/report-signoff/report-signoff';

interface LookupOption { id: number; name: string; }
interface PrintGroup { storeName: string; rows: string[][]; titleCount: number; copyCount: number; }

const PRINT_TITLE = 'DANH SÁCH SÁCH TRONG KHO';
const PRINT_ROW_CAP = 5000; // khớp RowCap phía backend

@Component({
  selector: 'app-store-book-report',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatIconModule, MatPaginatorModule, DateInputComponent, ReportSignoff, NgSelectModule],
  templateUrl: './store-book-report.html'
})
export class StoreBookReportPage implements OnInit, OnDestroy {
  private service   = inject(StoreBookReportService);
  private storeSvc  = inject(StoreService);
  private toastr    = inject(ToastrService);
  private router    = inject(Router);
  public  translate = inject(TranslateService);
  private destroy$  = new Subject<void>();

  stores = signal<LookupOption[]>([]);

  headers     = signal<string[]>([]);
  rows        = signal<string[][]>([]);
  totalCount  = signal(0);
  titleCount  = signal(0);
  hasSearched = signal(false);

  parentLibrary = signal('');
  libraryName   = signal('');
  storeName     = signal('');
  printHeaders  = signal<string[]>([]);
  printRows     = signal<string[][]>([]);
  storeStats    = signal<{ storeName: string; titleCount: number; copyCount: number }[]>([]);

  isLoading        = signal(false);
  isExportingExcel = signal(false);
  isPrinting       = signal(false);

  pageSize = 20; pageIndex = 0; pageSizeOptions = [10, 20, 50, 100];
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  searchForm = new FormGroup({
    dateFrom: new FormControl<string>('', { nonNullable: true }),
    dateTo:   new FormControl<string>('', { nonNullable: true }),
    storeId:  new FormControl<number | null>(null),
  });

  ngOnInit(): void { this.loadStores(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadPage(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadStores(): void {
    this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({
      next: list => this.stores.set(list.map(s => ({ id: Number(s.id), name: s.name || '' }))), error: () => {}
    });
  }

  private params() {
    const s = this.searchForm.getRawValue();
    return { ...s, pageIndex: this.pageIndex + 1, pageSize: this.pageSize };
  }

  runReport(): void {
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.hasSearched.set(true);
    this.loadPage();
  }

  loadPage(): void {
    this.isLoading.set(true);
    this.service.search(this.params()).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        this.headers.set(res.headers); this.rows.set(res.rows); this.totalCount.set(res.totalCount); this.titleCount.set(res.titleCount);
        this.parentLibrary.set(res.parentLibrary); this.libraryName.set(res.libraryName); this.storeName.set(res.storeName);
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
    if (this.isExportingExcel()) return;
    this.isExportingExcel.set(true);
    this.service.exportExcel(this.params()).pipe(takeUntil(this.destroy$)).subscribe({
      next: blob => { this.downloadBlob(blob, `sach-trong-kho_${Date.now()}.xlsx`); this.isExportingExcel.set(false); },
      error: () => { this.isExportingExcel.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); }
    });
  }

  printTitle(): string { return PRINT_TITLE; }

  printSubtitle(): string {
    const { dateFrom, dateTo, storeId } = this.searchForm.value;
    const fmt = (d?: string | null) => d ? d.split('-').reverse().join('/') : '…';
    const parts: string[] = [];
    if (storeId && this.storeName()) parts.push(`Kho: ${this.storeName()}`);
    if (dateFrom || dateTo) parts.push(`Từ ngày ${fmt(dateFrom)} đến ngày ${fmt(dateTo)}`);
    return parts.join('   —   ');
  }

  // Khi chọn 1 Kho cụ thể: 1 nhóm duy nhất (giữ hành vi cũ). Khi "Tất cả các kho": nhóm theo cột Kho (cột cuối).
  printGroups(): PrintGroup[] {
    if (this.searchForm.value.storeId) {
      return [{ storeName: this.storeName(), rows: this.printRows(), titleCount: this.titleCount(), copyCount: this.totalCount() }];
    }
    const koIdx = this.printHeaders().length - 1;
    const map = new Map<string, string[][]>();
    for (const row of this.printRows()) {
      const key = row[koIdx] || '(Không xác định)';
      if (!map.has(key)) map.set(key, []);
      map.get(key)!.push(row);
    }
    const stats = this.storeStats();
    return Array.from(map.entries()).map(([storeName, rows]) => {
      const s = stats.find(x => (x.storeName || '(Không xác định)') === storeName);
      return { storeName, rows, titleCount: s?.titleCount ?? 0, copyCount: s?.copyCount ?? rows.length };
    });
  }

  printReport(): void {
    if (this.isPrinting()) return;
    this.isPrinting.set(true);
    this.service.search({ ...this.params(), pageIndex: 1, pageSize: PRINT_ROW_CAP }).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        this.printHeaders.set(res.headers); this.printRows.set(res.rows);
        this.parentLibrary.set(res.parentLibrary); this.libraryName.set(res.libraryName); this.storeName.set(res.storeName);
        this.totalCount.set(res.totalCount); this.titleCount.set(res.titleCount); this.storeStats.set(res.storeStats);
        setTimeout(() => { window.print(); this.isPrinting.set(false); }, 0);
      },
      error: () => { this.isPrinting.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); }
    });
  }

  exit(): void { this.router.navigate(['/admin']); }
}
