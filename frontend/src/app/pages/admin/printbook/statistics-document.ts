import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatTableModule } from '@angular/material/table';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { StatisticsDocumentService, StatisticRow, StatCriterion } from '../../../services/printbook/statistics-document.service';
import { StoreService } from '../../../services/printbook/store.service';
import { CanDirective } from '../../../directives/can.directive';

@Component({
  selector: 'app-statistics-document',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatIconModule, DateInputComponent, NgSelectModule],
  templateUrl: './statistics-document.html'
})
export class StatisticsDocumentPage implements OnInit, OnDestroy {
  private service   = inject(StatisticsDocumentService);
  private storeSvc  = inject(StoreService);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private destroy$  = new Subject<void>();

  criteria: { id: StatCriterion; label: string }[] = [
    { id: 'bibType',  label: 'BY_BIB_TYPE' },
    { id: 'status',   label: 'BY_STATUS' },
    { id: 'language', label: 'BY_LANGUAGE' },
    { id: 'ddc',      label: 'BY_DDC' },
    { id: 'store',    label: 'BY_STORE' },
  ];

  displayedColumns = ['stt', 'label', 'count', 'detail'];
  rows = signal<StatisticRow[]>([]);
  stores = signal<{ id: number; name: string }[]>([]);
  isLoading = signal(false);

  searchForm = new FormGroup({
    criterion:       new FormControl<StatCriterion>('bibType', { nonNullable: true }),
    storeId:         new FormControl<number | null>(null),
    receiptDateFrom: new FormControl<string>('', { nonNullable: true }),
    receiptDateTo:   new FormControl<string>('', { nonNullable: true }),
  });

  ngOnInit(): void {
    this.storeSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: list => this.stores.set(list.map(s => ({ id: Number(s.id), name: s.name || '' }))), error: () => {} });
    this.run();
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  get totalCount(): number { return this.rows().reduce((sum, r) => sum + (r.count || 0), 0); }
  get totalDetail(): number { return this.rows().reduce((sum, r) => sum + (r.detail || 0), 0); }

  run(): void {
    const f = this.searchForm.getRawValue();
    this.isLoading.set(true);
    this.service.statistics({ criterion: f.criterion, storeId: f.storeId, receiptDateFrom: f.receiptDateFrom || null, receiptDateTo: f.receiptDateTo || null })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: rows => { this.rows.set(rows); this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }

  exportExcel(): void {
    const f = this.searchForm.getRawValue();
    this.service.export({ criterion: f.criterion, storeId: f.storeId, receiptDateFrom: f.receiptDateFrom || null, receiptDateTo: f.receiptDateTo || null })
      .pipe(takeUntil(this.destroy$)).subscribe(blob => {
        if (!blob || blob.size === 0) { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); return; }
        const url = URL.createObjectURL(blob); const a = document.createElement('a');
        a.href = url; a.download = `statistics-${f.criterion}.xlsx`; a.click(); URL.revokeObjectURL(url);
      });
  }
}
