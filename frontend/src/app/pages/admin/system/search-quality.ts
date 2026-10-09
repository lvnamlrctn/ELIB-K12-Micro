import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import { SearchQualityService } from '../../../services/system/search-quality.service';
import { SearchQualityReport } from '../../../models/system/search-quality';
import { ToastrService } from '../../../services/shared/toastr.service';

// Đợt 9 — báo cáo chất lượng tìm kiếm OPAC (port từ ELIB-LRC). Đọc-only, không có Add/Edit/Delete.
@Component({
  selector: 'app-search-quality',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, MatIconModule],
  templateUrl: './search-quality.html'
})
export class SearchQualityPage implements OnInit, OnDestroy {
  private service   = inject(SearchQualityService);
  public  translate = inject(TranslateService);
  private toastr    = inject(ToastrService);
  private destroy$  = new Subject<void>();

  loading = signal(false);
  report  = signal<SearchQualityReport | null>(null);
  days    = signal<number>(30);
  dayOptions = [7, 30, 90];

  ngOnInit(): void { this.load(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  setDays(d: number): void { this.days.set(d); this.load(); }

  load(): void {
    this.loading.set(true);
    this.service.getReport(this.days()).pipe(takeUntil(this.destroy$)).subscribe({
      next: r => {
        this.report.set(r);
        this.loading.set(false);
        if (!r) this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
      },
      error: () => { this.loading.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); }
    });
  }

  round(n: number): number { return Math.round(n * 10) / 10; }
}
