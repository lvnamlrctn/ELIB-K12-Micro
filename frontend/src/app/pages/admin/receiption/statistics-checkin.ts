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
import { StatisticsCheckInService, CheckInStatRow, CheckInStatCriterion } from '../../../services/receiption/statistics-checkin.service';
import { CircPlaceService } from '../../../services/printbook/circ-place.service';
import { CircPlace } from '../../../models/printbook/circ-place';
import { CanDirective } from '../../../directives/can.directive';

@Component({
  selector: 'app-statistics-checkin',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, NgSelectModule, TranslateModule, MatTableModule, MatIconModule, DateInputComponent],
  templateUrl: './statistics-checkin.html'
})
export class StatisticsCheckInPage implements OnInit, OnDestroy {
  private service     = inject(StatisticsCheckInService);
  private circPlaceSvc = inject(CircPlaceService);
  private toastr      = inject(ToastrService);
  public  translate   = inject(TranslateService);
  private destroy$    = new Subject<void>();

  criteria: { id: CheckInStatCriterion; label: string }[] = [
    { id: 'readerType', label: 'BY_READER_TYPE' },
    { id: 'class',      label: 'BY_CLASS' },
    { id: 'course',     label: 'BY_COURSE' },
    { id: 'department', label: 'BY_DEPARTMENT' },
  ];

  displayedColumns = ['stt', 'label', 'count'];
  rows = signal<CheckInStatRow[]>([]);
  circPlaces = signal<CircPlace[]>([]);
  isLoading = signal(false);

  searchForm = new FormGroup({
    criterion:       new FormControl<CheckInStatCriterion>('readerType', { nonNullable: true }),
    circPlaceId:     new FormControl<number | null>(null),
    receiptDateFrom: new FormControl<string>('', { nonNullable: true }),
    receiptDateTo:   new FormControl<string>('', { nonNullable: true }),
  });

  ngOnInit(): void {
    this.circPlaceSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: l => this.circPlaces.set(l), error: () => {} });
    this.run();
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  get totalCount(): number { return this.rows().reduce((sum, r) => sum + (r.count || 0), 0); }

  run(): void {
    const f = this.searchForm.getRawValue();
    this.isLoading.set(true);
    this.service.statistics({ criterion: f.criterion, circPlaceId: f.circPlaceId, receiptDateFrom: f.receiptDateFrom || null, receiptDateTo: f.receiptDateTo || null })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: rows => { this.rows.set(rows); this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }

  exportExcel(): void {
    const f = this.searchForm.getRawValue();
    this.service.export({ criterion: f.criterion, circPlaceId: f.circPlaceId, receiptDateFrom: f.receiptDateFrom || null, receiptDateTo: f.receiptDateTo || null })
      .pipe(takeUntil(this.destroy$)).subscribe(blob => {
        if (!blob || blob.size === 0) { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); return; }
        const url = URL.createObjectURL(blob); const a = document.createElement('a');
        a.href = url; a.download = `checkin-statistics-${f.criterion}.xlsx`; a.click(); URL.revokeObjectURL(url);
      });
  }
}
