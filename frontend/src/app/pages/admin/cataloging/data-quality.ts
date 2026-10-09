import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { TranslateModule } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import { DataQualityService, QUALITY_RULES, QualityIssue, QualityRuleKey, QualitySummary } from '../../../services/system/data-quality.service';

/**
 * Cảnh báo chất lượng dữ liệu (Đợt 21 — port từ ELIB-LRC `/admin/data-quality`). Chỉ đọc; mỗi dòng có nút
 * "Sửa ngay" mở đúng màn hình sửa với quyền sẵn có. Chỉ hiện quy tắc mà tài khoản có quyền xem (server không trả số
 * đếm của quy tắc không có quyền). Phạm vi = đơn vị của tài khoản (tài khoản hệ thống: toàn bộ).
 */
@Component({
  selector: 'app-data-quality',
  standalone: true,
  imports: [DatePipe, RouterLink, MatIconModule, MatPaginatorModule, TranslateModule],
  templateUrl: './data-quality.html'
})
export class DataQualityPage implements OnInit, OnDestroy {
  private service = inject(DataQualityService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private destroy$ = new Subject<void>();

  readonly pageSize = 25;
  summary = signal<QualitySummary | null>(null);
  summaryError = signal(false);
  rule = signal<QualityRuleKey | null>(null);
  items = signal<QualityIssue[]>([]);
  total = signal(0);
  pageIndex = signal(0);
  checkedAt = signal<string | null>(null);
  isLoading = signal(false);
  listError = signal(false);

  /** Quy tắc tài khoản được xem, theo thứ tự cố định. */
  rules = computed(() => {
    const counts = this.summary()?.counts ?? {};
    return QUALITY_RULES.filter(r => counts[r.key] !== undefined);
  });
  currentRule = computed(() => QUALITY_RULES.find(r => r.key === this.rule()) ?? null);
  isBarcodeRule = computed(() => this.rule() === 'duplicate-barcode' || this.rule() === 'unshelved');

  ngOnInit(): void { this.refresh(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  count(key: QualityRuleKey): number { return this.summary()?.counts[key] ?? 0; }

  refresh(): void {
    this.summaryError.set(false);
    this.service.summary().pipe(takeUntil(this.destroy$)).subscribe({
      next: s => {
        this.summary.set(s);
        const wanted = this.route.snapshot.queryParamMap.get('rule') as QualityRuleKey | null;
        const available = this.rules().map(r => r.key);
        const pick = (wanted && available.includes(wanted)) ? wanted
          : (this.rule() && available.includes(this.rule()!)) ? this.rule()!
          : available[0] ?? null;
        this.select(pick, this.rule() === pick ? this.pageIndex() : 0);
      },
      error: () => { this.summaryError.set(true); this.summary.set(null); }
    });
  }

  select(rule: QualityRuleKey | null, pageIndex = 0): void {
    this.rule.set(rule);
    this.pageIndex.set(pageIndex);
    if (rule) this.router.navigate([], { relativeTo: this.route, queryParams: { rule }, replaceUrl: true });
    this.loadList();
  }

  onPage(e: PageEvent): void { this.pageIndex.set(e.pageIndex); this.loadList(); }

  private loadList(): void {
    const rule = this.rule();
    this.items.set([]); this.total.set(0); this.listError.set(false);
    if (!rule) return;
    this.isLoading.set(true);
    this.service.list(rule, this.pageIndex() + 1, this.pageSize).pipe(takeUntil(this.destroy$)).subscribe({
      next: res => {
        this.isLoading.set(false);
        if (this.rule() !== rule) return; // đã chuyển tab trong lúc chờ
        this.items.set(res?.items ?? []); this.total.set(res?.total ?? 0); this.checkedAt.set(res?.checkedAt ?? null);
      },
      error: () => { this.isLoading.set(false); this.listError.set(true); }
    });
  }

  /** Link "Sửa ngay" — null khi không mở được đúng đối tượng (vd biểu ghi không có MFN). */
  fixLink(item: QualityIssue): { path: string[]; query?: Record<string, string> } | null {
    const rule = this.rule();
    if (!rule) return null;
    if (rule === 'duplicate-barcode') return item.barcode ? { path: ['/admin/re-register-barcode'], query: { barcode: item.barcode } } : null;
    if (rule === 'unshelved') return { path: ['/admin/map-shelving'] };
    return item.mfn != null ? { path: ['/admin/catalog-bibs/edit', String(item.mfn)] } : null;
  }
}
