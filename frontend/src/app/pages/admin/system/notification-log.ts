import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { NgSelectModule } from '@ng-select/ng-select';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { NotificationLogService } from '../../../services/system/notification-log.service';
import { NotificationLog } from '../../../models/system/notification-log';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

const EVENT_CODES = [
  'PRINT_HOLD_SUCCESS', 'EBOOK_RESERVATION_READY', 'PRINT_DUE_SOON', 'PRINT_OVERDUE',
  'EBOOK_DUE_SOON', 'BADGE_EARNED', 'ROOM_BOOKING_APPROVED', 'ROOM_BOOKING_REJECTED',
];

@Component({
  selector: 'app-notification-log',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, DateInputComponent, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './notification-log.html'
})
export class NotificationLogPage implements OnInit, OnDestroy {
  private notificationLogService = inject(NotificationLogService);
  private toastr = inject(ToastrService);
  public translate = inject(TranslateService);
  private auth = inject(Auth);
  private destroy$ = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns: string[] = ['index', 'channel', 'recipient', 'eventCode', 'success', 'errorMessage', 'sentAt'];
  dataSource: NotificationLog[] = [];
  channelOptions = [{ value: 'SMS', label: 'SMS' }, { value: 'ZALO', label: 'Zalo ZNS' }];
  eventCodeOptions = EVENT_CODES.map(c => ({ value: c, label: c }));
  successOptions = [{ value: 'true', label: 'Thành công' }, { value: 'false', label: 'Thất bại' }];

  totalRecords = 0;
  pageSize = 10;
  pageIndex = 0;
  pageSizeOptions = [5, 10, 25, 50, 100];

  isLoading = signal(false);

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  filterForm = new FormGroup({
    channel: new FormControl<string>('', { nonNullable: true }),
    eventCode: new FormControl<string>('', { nonNullable: true }),
    success: new FormControl<string>('', { nonNullable: true }),
    dateFrom: new FormControl<string>('', { nonNullable: true }),
    dateTo: new FormControl<string>('', { nonNullable: true })
  });

  ngOnInit() {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    this.loadData();
  }

  onTenantChange(): void { this.triggerSearch(); }

  onPageChange(event: PageEvent) {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadData();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadData() {
    const vals = this.filterForm.getRawValue();
    this.isLoading.set(true);
    this.notificationLogService.search({
      channel: vals.channel || undefined,
      eventCode: vals.eventCode || undefined,
      success: vals.success === '' ? undefined : vals.success === 'true',
      dateFrom: vals.dateFrom || undefined,
      dateTo: vals.dateTo || undefined,
      tenantId: this.tenantId,
      pageIndex: this.pageIndex + 1,
      pageSize: this.pageSize
    }).pipe(takeUntil(this.destroy$)).subscribe({
      next: (res) => {
        this.dataSource = res.data;
        this.totalRecords = res.total;
        this.isLoading.set(false);
      },
      error: () => {
        this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
        this.isLoading.set(false);
      }
    });
  }

  triggerSearch() {
    if (this.paginator) this.paginator.pageIndex = 0;
    this.pageIndex = 0;
    this.loadData();
  }

  rowIndex(i: number): number {
    return this.pageIndex * this.pageSize + i + 1;
  }
}
