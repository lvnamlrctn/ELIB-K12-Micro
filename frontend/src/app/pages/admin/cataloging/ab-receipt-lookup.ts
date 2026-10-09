import { Component, inject, OnInit, OnDestroy, signal, ViewChild, ViewEncapsulation } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ReceiptLookupService, ReceiptLookupParams, ReceiptLookupRow } from '../../../services/cataloging/receipt-lookup.service';
import { SupplierService } from '../../../services/acquisition/supplier.service';
import { FundService } from '../../../services/acquisition/fund.service';
import { AbSourceService } from '../../../services/acquisition/ab-source.service';
import { StoreService } from '../../../services/printbook/store.service';
import { UserService } from '../../../services/system/user.service';
import { ClassLabelService } from '../../../services/cataloging/class-label.service';
import { AcquisitionReportService, AcquisitionReportType, AcquisitionReportParams, AcquisitionPrintResult, StoreAllocationPrintResult, AccessionRegisterPrintResult, ClassLabelAccessionPrintResult } from '../../../services/acquisition/acquisition-report.service';
import { ToastrService } from '../../../services/shared/toastr.service';
import { Supplier } from '../../../models/acquisition/supplier';
import { Fund } from '../../../models/acquisition/fund';
import { Store } from '../../../models/printbook/store';
import { User } from '../../../models/system/user';
import { CanDirective } from '../../../directives/can.directive';
import { BarcodeDirective } from '../../../components/barcode.directive';

interface PrintLabelEntry {
  key:          string;
  title?:       string;
  author?:      string;
  classSymbol?: string;
  authorMark?:  string;
}

@Component({
  selector: 'app-ab-receipt-lookup',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, BarcodeDirective, DateInputComponent, NgSelectModule],
  templateUrl: './ab-receipt-lookup.html',
  styles: [`@media print { @page { size: landscape; } }`],
  encapsulation: ViewEncapsulation.None
})
export class AbReceiptLookupPage implements OnInit, OnDestroy {
  private service      = inject(ReceiptLookupService);
  private supplierSvc  = inject(SupplierService);
  private fundSvc      = inject(FundService);
  private sourceSvc    = inject(AbSourceService);
  private storeSvc     = inject(StoreService);
  private userSvc      = inject(UserService);
  private classLabelSvc = inject(ClassLabelService);
  private acquisitionReportSvc = inject(AcquisitionReportService);
  private toastr       = inject(ToastrService);
  public  translate    = inject(TranslateService);
  private router       = inject(Router);
  private destroy$     = new Subject<void>();

  displayedColumns = ['stt', 'mfn', 'receiptCode', 'isbd', 'createdDate', 'sourceName', 'storeName'];
  dataSource: ReceiptLookupRow[] = [];

  suppliers = signal<Supplier[]>([]);
  funds     = signal<Fund[]>([]);
  sources   = signal<{ id: number; name: string }[]>([]);
  stores    = signal<Store[]>([]);
  users     = signal<User[]>([]);

  statusOptions = [
    { value: 1, label: 'AB_RECEIPT.ORDER_ST_PENDING' },
    { value: 2, label: 'AB_RECEIPT.ORDER_ST_DONE' },
  ];

  isLoading = signal(false);
  isExporting = signal(false);
  filtersCollapsed = signal(false);
  isPrintingLabels = signal(false);
  printLabels = signal<PrintLabelEntry[]>([]);
  libraryHeader = signal<{ parentLibrary: string; libraryName: string }>({ parentLibrary: '', libraryName: '' });
  acquisitionPrint = signal<AcquisitionPrintResult | null>(null);
  storeAllocationPrint = signal<StoreAllocationPrintResult | null>(null);
  accessionRegisterPrint = signal<AccessionRegisterPrintResult | null>(null);
  classLabelAccessionPrint = signal<ClassLabelAccessionPrintResult | null>(null);

  totalRecords    = 0;
  pageSize        = 20;
  pageIndex       = 0;
  pageSizeOptions = [10, 20, 50, 100];

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  reportType = new FormControl<'CLASS_LABEL' | 'CLASS_LABEL_ACCESSION' | AcquisitionReportType>('CLASS_LABEL', { nonNullable: true });

  private readonly reportFileNames: Record<AcquisitionReportType, string> = {
    STORE_ALLOCATION:   'BaoCaoPhanBoKho.xlsx',
    ACQUISITION_LIST:   'DanhMucSachBoSung.xlsx',
    ACCESSION_REGISTER: 'SoDangKyCaBiet.xlsx',
    NEW_BOOK_CATALOG:   'ThuMucGioiThieuSachMoi.xlsx',
  };

  searchForm = new FormGroup({
    receiptCodeFrom: new FormControl<number | null>(null),
    receiptCodeTo:   new FormControl<number | null>(null),
    receiptName:     new FormControl<string>('', { nonNullable: true }),
    title:           new FormControl<string>('', { nonNullable: true }),
    mfnFrom:         new FormControl<number | null>(null),
    mfnTo:           new FormControl<number | null>(null),
    status:          new FormControl<number | null>(null),
    author:          new FormControl<string>('', { nonNullable: true }),
    receiptDateFrom: new FormControl<string>('', { nonNullable: true }),
    receiptDateTo:   new FormControl<string>('', { nonNullable: true }),
    sourceId:        new FormControl<number | null>(null),
    publishYear:     new FormControl<string>('', { nonNullable: true }),
    createdDateFrom: new FormControl<string>('', { nonNullable: true }),
    createdDateTo:   new FormControl<string>('', { nonNullable: true }),
    fundId:          new FormControl<number | null>(null),
    publisher:       new FormControl<string>('', { nonNullable: true }),
    supplierId:      new FormControl<number | null>(null),
    createdBy:       new FormControl<number | null>(null),
    storeId:         new FormControl<number | null>(null),
  });

  ngOnInit(): void {
    this.supplierSvc.search({ pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.suppliers.set(r.data), error: () => {} });
    this.fundSvc.search({ pageSize: 500 }).pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.funds.set(r.data), error: () => {} });
    this.sourceSvc.getAll({ draw: 1, start: 0, length: 500, search: { value: '' } }).pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.sources.set(r.data as unknown as { id: number; name: string }[]), error: () => {} });
    this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.stores.set(l), error: () => {} });
    this.userSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.users.set(l), error: () => {} });
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

  private buildParams(): ReceiptLookupParams {
    const v = this.searchForm.getRawValue();
    return {
      receiptCodeFrom: v.receiptCodeFrom ?? null,
      receiptCodeTo:   v.receiptCodeTo   ?? null,
      receiptName:     v.receiptName     || null,
      title:           v.title           || null,
      mfnFrom:         v.mfnFrom         ?? null,
      mfnTo:           v.mfnTo           ?? null,
      status:          v.status          ?? null,
      author:          v.author          || null,
      receiptDateFrom: v.receiptDateFrom || null,
      receiptDateTo:   v.receiptDateTo   || null,
      sourceId:        v.sourceId        ?? null,
      publishYear:     v.publishYear     || null,
      createdDateFrom: v.createdDateFrom || null,
      createdDateTo:   v.createdDateTo   || null,
      fundId:          v.fundId          ?? null,
      publisher:       v.publisher       || null,
      supplierId:      v.supplierId      ?? null,
      createdBy:       v.createdBy       ?? null,
      storeId:         v.storeId         ?? null,
      pageIndex:       this.pageIndex + 1,
      pageSize:        this.pageSize,
    };
  }

  // Map đầy đủ searchForm sang tham số cho các API in báo cáo (AcquisitionReportController) —
  // dùng chung để "Báo cáo" luôn khớp với bộ lọc đang áp dụng trên lưới kết quả.
  private buildAcquisitionReportParams(): AcquisitionReportParams {
    const v = this.searchForm.getRawValue();
    return {
      fromDate:        v.receiptDateFrom || null,
      toDate:          v.receiptDateTo   || null,
      storeId:         v.storeId         ?? null,
      supplierId:      v.supplierId      ?? null,
      receiptCodeFrom: v.receiptCodeFrom ?? null,
      receiptCodeTo:   v.receiptCodeTo   ?? null,
      receiptName:     v.receiptName     || null,
      title:           v.title           || null,
      mfnFrom:         v.mfnFrom         ?? null,
      mfnTo:           v.mfnTo           ?? null,
      status:          v.status          ?? null,
      author:          v.author          || null,
      sourceId:        v.sourceId        ?? null,
      publishYear:     v.publishYear     || null,
      createdDateFrom: v.createdDateFrom || null,
      createdDateTo:   v.createdDateTo   || null,
      fundId:          v.fundId          ?? null,
      publisher:       v.publisher       || null,
      createdBy:       v.createdBy       ?? null,
    };
  }

  loadData(): void {
    this.isLoading.set(true);
    this.service.search(this.buildParams()).pipe(takeUntil(this.destroy$)).subscribe({
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

  resetForm(): void {
    this.searchForm.reset();
    this.triggerSearch();
  }

  toggleFilters(): void {
    this.filtersCollapsed.update(v => !v);
  }

  getRowIndex(i: number): number {
    return this.pageIndex * this.pageSize + i + 1;
  }

  exportExcel(): void {
    this.isExporting.set(true);
    const params = { ...this.buildParams(), pageIndex: 1, pageSize: 99999 };
    this.service.export(params).pipe(takeUntil(this.destroy$)).subscribe({
      next: blob => {
        this.triggerDownload(blob, 'TraCuuDonNhan.xlsx');
        this.isExporting.set(false);
      },
      error: () => {
        this.toastr.error(this.translate.instant('COMMON.EXPORT_ERROR'));
        this.isExporting.set(false);
      }
    });
  }

  // "Loại báo cáo" quyết định hành vi nút "Báo cáo":
  // - CLASS_LABEL: in nhãn môn loại cho TOÀN BỘ kết quả đang lọc (logic riêng, xem printClassLabels()).
  // - ACQUISITION_LIST: in "Danh mục sách bổ sung" theo mẫu vật lý (xem printAcquisitionList()).
  // - STORE_ALLOCATION: in "Báo cáo phân bổ kho" theo mẫu vật lý (xem printStoreAllocationReport()).
  // - ACCESSION_REGISTER: in "Sổ đăng ký cá biệt" theo mẫu vật lý (xem printAccessionRegisterReport()).
  // - Các loại còn lại: tái dùng AcquisitionReportController có sẵn (chỉ nhận fromDate/toDate/storeId/supplierId
  //   nên các bộ lọc khác của trang này không áp dụng cho các loại đó), xuất thẳng ra Excel.
  runReport(): void {
    const type = this.reportType.value;
    if (type === 'CLASS_LABEL') {
      this.printClassLabels();
      return;
    }
    if (type === 'ACQUISITION_LIST') {
      this.printAcquisitionList();
      return;
    }
    if (type === 'STORE_ALLOCATION') {
      this.printStoreAllocationReport();
      return;
    }
    if (type === 'ACCESSION_REGISTER') {
      this.printAccessionRegisterReport();
      return;
    }
    if (type === 'CLASS_LABEL_ACCESSION') {
      this.printClassLabelAccessionReport();
      return;
    }
    this.runAcquisitionReport(type);
  }

  private printClassLabelAccessionReport(): void {
    this.isPrintingLabels.set(true);
    this.acquisitionReportSvc.printClassLabelAccession(this.buildAcquisitionReportParams()).pipe(takeUntil(this.destroy$)).subscribe({
      next: result => {
        this.isPrintingLabels.set(false);
        if (result.items.length === 0) {
          this.toastr.warning(this.translate.instant('AB_RECEIPT_LOOKUP.NO_COPIES_TO_PRINT'));
          return;
        }
        this.classLabelAccessionPrint.set(result);
        setTimeout(() => this.print());
      },
      error: () => {
        this.isPrintingLabels.set(false);
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
      }
    });
  }

  private printAccessionRegisterReport(): void {
    this.isPrintingLabels.set(true);
    this.acquisitionReportSvc.printAccessionRegister(this.buildAcquisitionReportParams()).pipe(takeUntil(this.destroy$)).subscribe({
      next: result => {
        this.isPrintingLabels.set(false);
        if (result.items.length === 0) {
          this.toastr.warning(this.translate.instant('AB_RECEIPT_LOOKUP.NO_COPIES_TO_PRINT'));
          return;
        }
        this.accessionRegisterPrint.set(result);
        setTimeout(() => this.print());
      },
      error: () => {
        this.isPrintingLabels.set(false);
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
      }
    });
  }

  private printStoreAllocationReport(): void {
    this.isPrintingLabels.set(true);
    this.acquisitionReportSvc.printStoreAllocation(this.buildAcquisitionReportParams()).pipe(takeUntil(this.destroy$)).subscribe({
      next: result => {
        this.isPrintingLabels.set(false);
        if (result.groups.length === 0) {
          this.toastr.warning(this.translate.instant('AB_RECEIPT_LOOKUP.NO_COPIES_TO_PRINT'));
          return;
        }
        this.storeAllocationPrint.set(result);
        setTimeout(() => this.print());
      },
      error: () => {
        this.isPrintingLabels.set(false);
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
      }
    });
  }

  formatShortDate(iso?: string | null): string {
    const d = iso ? new Date(iso) : new Date();
    return `${d.getDate().toString().padStart(2, '0')}/${(d.getMonth() + 1).toString().padStart(2, '0')}/${d.getFullYear()}`;
  }

  private printAcquisitionList(): void {
    this.isPrintingLabels.set(true);
    this.acquisitionReportSvc.printAcquisitionList(this.buildAcquisitionReportParams()).pipe(takeUntil(this.destroy$)).subscribe({
      next: result => {
        this.isPrintingLabels.set(false);
        if (result.items.length === 0) {
          this.toastr.warning(this.translate.instant('AB_RECEIPT_LOOKUP.NO_COPIES_TO_PRINT'));
          return;
        }
        this.acquisitionPrint.set(result);
        setTimeout(() => this.print());
      },
      error: () => {
        this.isPrintingLabels.set(false);
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
      }
    });
  }

  formatVnDate(iso?: string | null): string {
    const d = iso ? new Date(iso) : new Date();
    return `Ngày ${d.getDate().toString().padStart(2, '0')} tháng ${(d.getMonth() + 1).toString().padStart(2, '0')} năm ${d.getFullYear()}`;
  }

  private runAcquisitionReport(type: AcquisitionReportType): void {
    this.isPrintingLabels.set(true);
    const v = this.searchForm.getRawValue();
    this.acquisitionReportSvc.export(type, {
      fromDate:   v.receiptDateFrom || null,
      toDate:     v.receiptDateTo   || null,
      storeId:    v.storeId         ?? null,
      supplierId: v.supplierId      ?? null,
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: blob => {
        this.triggerDownload(blob, this.reportFileNames[type]);
        this.isPrintingLabels.set(false);
      },
      error: () => {
        this.toastr.error(this.translate.instant('COMMON.EXPORT_ERROR'));
        this.isPrintingLabels.set(false);
      }
    });
  }

  private printClassLabels(): void {
    this.isPrintingLabels.set(true);
    this.service.searchForLabel(this.buildParams()).pipe(takeUntil(this.destroy$)).subscribe({
      next: rows => {
        if (rows.length === 0) {
          this.isPrintingLabels.set(false);
          this.toastr.warning(this.translate.instant('AB_RECEIPT_LOOKUP.NO_COPIES_TO_PRINT'));
          return;
        }
        const publicIds = rows.map(r => r.bibPublicId);
        this.classLabelSvc.searchByBib(publicIds).pipe(takeUntil(this.destroy$)).subscribe({
          next: result => {
            this.isPrintingLabels.set(false);
            this.libraryHeader.set({ parentLibrary: result.parentLibrary, libraryName: result.libraryName });

            const entries: PrintLabelEntry[] = [];
            result.items.forEach(item => {
              const amount = rows.find(r => r.bibPublicId === item.bibPublicId)?.amount ?? 0;
              for (let i = 0; i < amount; i++) {
                entries.push({ key: `${item.id}-${i}`, title: item.title, author: item.author, classSymbol: item.classSymbol, authorMark: item.authorMark });
              }
            });

            if (entries.length === 0) {
              this.toastr.warning(this.translate.instant('AB_RECEIPT_LOOKUP.NO_COPIES_TO_PRINT'));
              return;
            }
            this.printLabels.set(entries);
            setTimeout(() => this.print());
          },
          error: () => {
            this.isPrintingLabels.set(false);
            this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
          }
        });
      },
      error: () => {
        this.isPrintingLabels.set(false);
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
      }
    });
  }

  caption(author?: string, title?: string): string {
    return `${author ? author : ''}-${title ?? ''}`;
  }

  print(): void {
    if (typeof window !== 'undefined') window.print();
  }

  exit(): void {
    this.router.navigate(['/admin/ab-receipts']);
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
