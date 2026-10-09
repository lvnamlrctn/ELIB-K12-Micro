import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { BookRecordService, BookSearchParams } from '../../../services/printbook/book-record.service';
import { StoreService } from '../../../services/printbook/store.service';
import { BibTypeService } from '../../../services/cataloging/bib-type.service';
import { BarcodeStatusService } from '../../../services/printbook/barcode-status.service';
import { ToastrService } from '../../../services/shared/toastr.service';
import { BookRecord } from '../../../models/printbook/book-record';
import { Store } from '../../../models/printbook/store';
import { BibType } from '../../../models/cataloging/bib-type';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-book-search',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './book-search.html'
})
export class BookSearchPage implements OnInit, OnDestroy {
  private bookSvc  = inject(BookRecordService);
  private storeSvc = inject(StoreService);
  private bibTypeSvc = inject(BibTypeService);
  private barcodeStatusSvc = inject(BarcodeStatusService);
  private toastr   = inject(ToastrService);
  public  translate = inject(TranslateService);
  private auth     = inject(Auth);
  private destroy$ = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['stt', 'mfn', 'donNhan', 'dkcb', 'title', 'author', 'publisher', 'publishYear', 'storeName', 'status'];
  dataSource: BookRecord[] = [];

  stores  = signal<Store[]>([]);
  bibTypes = signal<BibType[]>([]);
  barcodeStatuses = signal<{ id: string; label: string }[]>([]);
  isLoading = signal(false);
  filtersCollapsed = signal(false);
  toggleFilters(): void { this.filtersCollapsed.update(v => !v); }
  isExporting = signal(false);

  totalRecords    = 0;
  pageSize        = 10;
  pageIndex       = 0;
  pageSizeOptions = [10, 25, 50, 100];

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  searchForm = new FormGroup({
    mfnFrom:    new FormControl<number | null>(null),
    mfnTo:      new FormControl<number | null>(null),
    publishYear: new FormControl<string>('', { nonNullable: true }),
    docTypeId:  new FormControl<number | null>(null),
    title:      new FormControl<string>('', { nonNullable: true }),
    publisher:  new FormControl<string>('', { nonNullable: true }),
    status:     new FormControl<string | null>(null),
    author:     new FormControl<string>('', { nonNullable: true }),
    callNumber: new FormControl<string>('', { nonNullable: true }),
    storeId:    new FormControl<number | null>(null),
    keyword:    new FormControl<string>('', { nonNullable: true }),
    summary:    new FormControl<string>('', { nonNullable: true }),
  });

  ngOnInit(): void {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe(data => this.stores.set(data));
    this.bibTypeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.bibTypes.set(l), error: () => {} });
    this.barcodeStatusSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({
      next: list => {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const arr = list as any[];
        this.barcodeStatuses.set(arr.map(x => ({ id: String(x.id ?? ''), label: x.commentStatus ?? x.name ?? String(x.id ?? '') })));
      },
      error: () => {}
    });
    this.loadData();
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize  = event.pageSize;
    this.loadData();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private buildParams(): BookSearchParams {
    const v = this.searchForm.getRawValue();
    return {
      mfnFrom:    v.mfnFrom    ?? null,
      mfnTo:      v.mfnTo      ?? null,
      publishYear: v.publishYear || null,
      docTypeId:  v.docTypeId  ?? null,
      title:      v.title      || null,
      publisher:  v.publisher  || null,
      status:     v.status     ?? null,
      author:     v.author     || null,
      callNumber: v.callNumber || null,
      storeId:    v.storeId    ?? null,
      keyword:    v.keyword    || null,
      summary:    v.summary    || null,
      pageIndex:  this.pageIndex + 1,
      pageSize:   this.pageSize,
      tenantId:   this.tenantId,
    };
  }

  loadData(): void {
    this.isLoading.set(true);
    this.bookSvc.search(this.buildParams()).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        this.dataSource   = res.data;
        this.totalRecords = res.recordsTotal;
        this.isLoading.set(false);
      },
      error: () => {
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
        this.isLoading.set(false);
      }
    });
  }

  triggerSearch(): void {
    this.pageIndex = 0;
    if (this.paginator) this.paginator.pageIndex = 0;
    this.loadData();
  }

  onTenantChange(): void {
    this.triggerSearch();
  }

  resetForm(): void {
    this.searchForm.reset();
    this.triggerSearch();
  }

  getRowIndex(i: number): number {
    return this.pageIndex * this.pageSize + i + 1;
  }

  getStatusLabel(status?: string): string {
    if (!status) return '—';
    return this.barcodeStatuses().find(s => s.id === status)?.label ?? status;
  }

  getStatusClass(_status?: string): string {
    return 'bg-gray-100 text-gray-700';
  }

  exportDetail(): void {
    this.isExporting.set(true);
    const params = { ...this.buildParams(), pageIndex: 1, pageSize: 99999 };
    this.bookSvc.exportDetail(params).pipe(takeUntil(this.destroy$)).subscribe({
      next: blob => {
        this.triggerDownload(blob, 'TaiLieu_ChiTiet.xlsx');
        this.isExporting.set(false);
      },
      error: () => {
        this.toastr.error(this.translate.instant('COMMON.EXPORT_ERROR'));
        this.isExporting.set(false);
      }
    });
  }

  exportSummary(): void {
    this.isExporting.set(true);
    const params = { ...this.buildParams(), pageIndex: 1, pageSize: 99999 };
    this.bookSvc.exportSummary(params).pipe(takeUntil(this.destroy$)).subscribe({
      next: blob => {
        this.triggerDownload(blob, 'TaiLieu_TomTat.xlsx');
        this.isExporting.set(false);
      },
      error: () => {
        this.toastr.error(this.translate.instant('COMMON.EXPORT_ERROR'));
        this.isExporting.set(false);
      }
    });
  }

  private triggerDownload(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const a   = document.createElement('a');
    a.href     = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }
}
