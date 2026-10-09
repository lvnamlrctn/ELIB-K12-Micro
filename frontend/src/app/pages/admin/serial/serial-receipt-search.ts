import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { Router } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { SubscriptionService } from '../../../services/serial/subscription.service';
import { Subscription } from '../../../models/serial/subscription';
import { SerialIssueService } from '../../../services/serial/serial-issue.service';
import { SerialIssue } from '../../../models/serial/serial-issue';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-serial-receipt-search',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, DateInputComponent, TenantFilterSelectComponent],
  templateUrl: './serial-receipt-search.html'
})
export class SerialReceiptSearchPage implements OnInit, OnDestroy {
  private service   = inject(SubscriptionService);
  private issueSvc  = inject(SerialIssueService);
  private router    = inject(Router);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private auth      = inject(Auth);
  private destroy$  = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  // in nhanh "Danh mục tạp chí nhận" (cẩm nang mục VII.6)
  printSubject = signal<Subscription | null>(null);
  printIssues = signal<SerialIssue[]>([]);

  displayedColumns = ['stt', 'title', 'issn', 'startTime', 'endTime', 'actions'];
  dataSource: Subscription[] = [];
  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  isLoading = signal(false);

  searchForm = new FormGroup({
    title:         new FormControl<string>('', { nonNullable: true }),
    author:        new FormControl<string>('', { nonNullable: true }),
    issn:          new FormControl<string>('', { nonNullable: true }),
    startTimeFrom: new FormControl<string>('', { nonNullable: true }),
    startTimeTo:   new FormControl<string>('', { nonNullable: true }),
  });

  ngOnInit(): void { applyTenantColumn(this.displayedColumns, this.isPrivileged); this.loadData(); }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  loadData(): void {
    this.isLoading.set(true);
    const f = this.searchForm.getRawValue();
    this.service.search({ title: f.title || null, author: f.author || null, issn: f.issn || null, startTimeFrom: f.startTimeFrom || null, startTimeTo: f.startTimeTo || null, pageIndex: this.pageIndex + 1, pageSize: this.pageSize, tenantId: this.tenantId })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  onTenantChange(): void { this.triggerSearch(); }
  resetSearch(): void { this.searchForm.reset({ title: '', author: '', issn: '', startTimeFrom: '', startTimeTo: '' }); this.triggerSearch(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  openIssues(item: Subscription): void { this.router.navigate(['/admin/serial-issues'], { queryParams: { subscriptionId: item.id, title: item.title || '', patternId: item.patternId ?? '' } }); }

  printReceipt(item: Subscription): void {
    this.printSubject.set(item);
    this.issueSvc.search({ subscriptionId: item.id, pageSize: 1000 }).pipe(takeUntil(this.destroy$)).subscribe(res => {
      this.printIssues.set(res.data);
      setTimeout(() => { if (typeof window !== 'undefined') window.print(); });
    });
  }
}
