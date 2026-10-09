import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { SelectionModel } from '@angular/cdk/collections';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { BookRecordService, BookSearchParams } from '../../../services/printbook/book-record.service';
import { BibTypeService } from '../../../services/cataloging/bib-type.service';
import { DBibStatusService } from '../../../services/printbook/dbib-status.service';
import { ClassLabelService } from '../../../services/cataloging/class-label.service';
import { ToastrService } from '../../../services/shared/toastr.service';
import { BookTitleRecord } from '../../../models/printbook/book-title-record';
import { BibType } from '../../../models/cataloging/bib-type';
import { DBibStatus } from '../../../models/printbook/dbib-status';
import { CanDirective } from '../../../directives/can.directive';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

interface PrintLabelEntry {
  key:          string;
  title?:       string;
  author?:      string;
  classSymbol?: string;
  authorMark?:  string;
}

@Component({
  selector: 'app-book-title-search',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './book-title-search.html',
  styles: [`@media print { @page { size: landscape; } }`]
})
export class BookTitleSearchPage implements OnInit, OnDestroy {
  private bookSvc      = inject(BookRecordService);
  private bibTypeSvc   = inject(BibTypeService);
  private statusSvc    = inject(DBibStatusService);
  private classLabelSvc = inject(ClassLabelService);
  private toastr       = inject(ToastrService);
  public  translate    = inject(TranslateService);
  private auth         = inject(Auth);
  private destroy$     = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns = ['select', 'stt', 'mfn', 'title', 'author', 'publisher', 'publishYear', 'quantity'];
  dataSource: BookTitleRecord[] = [];
  selection = new SelectionModel<BookTitleRecord>(true, []);

  bibTypes  = signal<BibType[]>([]);
  statuses  = signal<DBibStatus[]>([]);
  isLoading = signal(false);
  isExporting = signal(false);
  isPrintingLabels = signal(false);
  printLabels = signal<PrintLabelEntry[]>([]);
  libraryHeader = signal<{ parentLibrary: string; libraryName: string }>({ parentLibrary: '', libraryName: '' });
  filtersCollapsed = signal(false);
  toggleFilters(): void { this.filtersCollapsed.update(v => !v); }

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
    keyword:    new FormControl<string>('', { nonNullable: true }),
    summary:    new FormControl<string>('', { nonNullable: true }),
  });

  ngOnInit(): void {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    this.bibTypeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.bibTypes.set(l), error: () => {} });
    this.statusSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.statuses.set(l), error: () => {} });
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
      keyword:    v.keyword    || null,
      summary:    v.summary    || null,
      pageIndex:  this.pageIndex + 1,
      pageSize:   this.pageSize,
      tenantId:   this.tenantId,
    };
  }

  loadData(): void {
    this.isLoading.set(true);
    this.bookSvc.searchTitles(this.buildParams()).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        this.dataSource   = res.data;
        this.totalRecords = res.recordsTotal;
        this.selection.clear();
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

  exportTitles(): void {
    this.isExporting.set(true);
    const params = { ...this.buildParams(), pageIndex: 1, pageSize: 99999 };
    this.bookSvc.exportTitles(params).pipe(takeUntil(this.destroy$)).subscribe({
      next: blob => {
        this.triggerDownload(blob, 'TaiLieu_TheoDauSach.xlsx');
        this.isExporting.set(false);
      },
      error: () => {
        this.toastr.error(this.translate.instant('COMMON.EXPORT_ERROR'));
        this.isExporting.set(false);
      }
    });
  }

  isAllSelected(): boolean {
    return this.selection.selected.length > 0 && this.selection.selected.length === this.dataSource.length;
  }
  toggleAllRows(): void {
    this.isAllSelected() ? this.selection.clear() : this.selection.select(...this.dataSource);
  }

  printClassLabels(): void {
    if (this.selection.selected.length === 0) {
      this.toastr.warning(this.translate.instant('BOOK_TITLE_SEARCH.SELECT_AT_LEAST_ONE'));
      return;
    }
    const selected = this.selection.selected;
    const publicIds = selected.map(r => r.publicId).filter((x): x is string => !!x);
    this.isPrintingLabels.set(true);
    this.classLabelSvc.searchByBib(publicIds).pipe(takeUntil(this.destroy$)).subscribe({
      next: result => {
        this.isPrintingLabels.set(false);
        this.libraryHeader.set({ parentLibrary: result.parentLibrary, libraryName: result.libraryName });

        const entries: PrintLabelEntry[] = [];
        result.items.forEach(item => {
          const rec = selected.find(r => r.publicId === item.bibPublicId);
          const qty = rec?.quantity ?? 0;
          for (let i = 0; i < qty; i++) {
            entries.push({ key: `${item.id}-${i}`, title: item.title, author: item.author, classSymbol: item.classSymbol, authorMark: item.authorMark });
          }
        });

        if (entries.length === 0) {
          this.toastr.warning(this.translate.instant('BOOK_TITLE_SEARCH.NO_COPIES_TO_PRINT'));
          return;
        }
        this.printLabels.set(entries);
        setTimeout(() => this.print());
      },
      error: () => {
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
        this.isPrintingLabels.set(false);
      }
    });
  }

  caption(author?: string, title?: string): string {
    return `${author ? author : ''}-${title ?? ''}`;
  }

  print(): void {
    if (typeof window !== 'undefined') window.print();
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
