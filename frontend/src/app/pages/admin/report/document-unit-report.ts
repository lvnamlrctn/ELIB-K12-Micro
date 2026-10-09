import { Component, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { Auth } from '../../../services/auth';
import { DepartmentService } from '../../../services/system/department.service';
import { DocumentUnitReportService, DocumentReportType } from '../../../services/report/document-unit-report.service';
import { DocumentUnitReportRow } from '../../../models/report/document-unit-report';
import { ReportSignoff } from '../../../shared/report-signoff/report-signoff';

@Component({
  selector: 'app-document-unit-report',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule, MatIconModule, DateInputComponent, ReportSignoff, NgSelectModule],
  templateUrl: './document-unit-report.html'
})
export class DocumentUnitReportPage implements OnInit, OnDestroy {
  private service       = inject(DocumentUnitReportService);
  private departmentSvc = inject(DepartmentService);
  private auth          = inject(Auth);
  private toastr        = inject(ToastrService);
  public  translate     = inject(TranslateService);
  private destroy$      = new Subject<void>();

  // Đơn vị (chỉ super-admin xem/lọc được nhiều đơn vị — theo đúng quy ước của org.ts)
  isPrivileged = this.auth.isPrivileged();
  tenantOptions: { id: number; name: string }[] = [];

  reportTypes: { value: DocumentReportType; label: string }[] = [
    { value: 'summary', label: 'DOC_UNIT_REPORT.T_SUMMARY' },
    { value: 'print',   label: 'DOC_UNIT_REPORT.T_PRINT' },
    { value: 'digital', label: 'DOC_UNIT_REPORT.T_DIGITAL' },
  ];
  reportType = signal<DocumentReportType>('summary');

  rows = signal<DocumentUnitReportRow[]>([]);
  isLoading = signal(false);

  totalBib     = computed(() => this.rows().reduce((s, r) => s + r.bibCount, 0));
  totalBarcode = computed(() => this.rows().reduce((s, r) => s + r.barcodeCount, 0));
  totalEbook   = computed(() => this.rows().reduce((s, r) => s + r.ebookCount, 0));
  totalAll     = computed(() => this.totalBib() + this.totalEbook());

  searchForm = new FormGroup({
    tenantId: new FormControl<number | null>(null),
    fromDate: new FormControl<string>('', { nonNullable: true }),
    toDate:   new FormControl<string>('', { nonNullable: true }),
  });

  ngOnInit(): void {
    if (this.isPrivileged) {
      this.departmentSvc.getAll({ draw: 1, start: 0, length: 500, search: { value: '', regex: false } })
        .pipe(takeUntil(this.destroy$))
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        .subscribe({ next: res => this.tenantOptions = res.data.map((t: any) => ({ id: t.id, name: t.name })), error: () => {} });
    }
    this.load();
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  private buildParams() {
    const f = this.searchForm.getRawValue();
    return { tenantId: f.tenantId, fromDate: f.fromDate || null, toDate: f.toDate || null, reportType: this.reportType() };
  }

  changeReportType(type: DocumentReportType): void { this.reportType.set(type); }

  load(): void {
    this.isLoading.set(true);
    this.service.summary(this.buildParams()).pipe(takeUntil(this.destroy$)).subscribe({
      next: rows => { this.rows.set(rows); this.isLoading.set(false); },
      error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
    });
  }

  exportExcel(): void {
    this.service.export(this.buildParams()).pipe(takeUntil(this.destroy$)).subscribe(blob => {
      if (!blob || blob.size === 0) { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); return; }
      const url = URL.createObjectURL(blob); const a = document.createElement('a');
      a.href = url; a.download = `document-unit-report-${this.reportType()}.xlsx`; a.click(); URL.revokeObjectURL(url);
    });
  }

  print(): void { if (typeof window !== 'undefined') window.print(); }
}
