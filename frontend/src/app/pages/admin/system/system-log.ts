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
import { SystemLogService } from '../../../services/system/system-log.service';
import { SystemLog, SystemUser } from '../../../models/system/system-log';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-system-log',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, DateInputComponent, NgSelectModule, TenantFilterSelectComponent],
  templateUrl: './system-log.html'
})
export class SystemLogPage implements OnInit, OnDestroy {
  private systemLogService = inject(SystemLogService);
  private toastr = inject(ToastrService);
  public translate = inject(TranslateService);
  private auth = inject(Auth);
  private destroy$ = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  displayedColumns: string[] = ['index', 'fullName', 'actionType', 'action', 'submited', 'ip'];
  dataSource: SystemLog[] = [];
  users: SystemUser[] = [];
  // đối tượng phẳng {value,label} cho ng-select, dẫn xuất từ `users` — không thay đổi logic nghiệp vụ
  userOptions: { value: string; label: string }[] = [];

  totalRecords = 0;
  pageSize = 10;
  pageIndex = 0;
  pageSizeOptions = [5, 10, 25, 50, 100];

  isLoading = signal(false);

  @ViewChild(MatPaginator) paginator!: MatPaginator;

  filterForm = new FormGroup({
    userId: new FormControl<string>('', { nonNullable: true }),
    action: new FormControl<string>('', { nonNullable: true }),
    dateFrom: new FormControl<string>('', { nonNullable: true }),
    dateTo: new FormControl<string>('', { nonNullable: true })
  });

  ngOnInit() {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    this.loadUsers();
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

  loadUsers() {
    this.systemLogService.getUsers().pipe(takeUntil(this.destroy$)).subscribe(users => {
      this.users = users;
      this.userOptions = users.map(u => ({ value: String(u.publicId || u.id || ''), label: u.fullName || u.userName || '' }));
    });
  }

  loadData() {
    const vals = this.filterForm.getRawValue();
    this.isLoading.set(true);
    this.systemLogService.search({
      userId: vals.userId || undefined,
      action: vals.action || undefined,
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
    if (this.paginator) {
      this.paginator.pageIndex = 0;
    }
    this.pageIndex = 0;
    this.loadData();
  }

  rowIndex(i: number): number {
    return this.pageIndex * this.pageSize + i + 1;
  }

  getActionTypeBadgeClass(actionType: string): string {
    const base = 'inline-block text-xs font-semibold px-2 py-0.5 rounded';
    switch (actionType?.toLowerCase()) {
      case 'add':    return `${base} bg-emerald-50 text-emerald-700`;
      case 'update': return `${base} bg-blue-50 text-blue-700`;
      case 'delete': return `${base} bg-red-50 text-red-700`;
      default:       return `${base} bg-gray-100 text-gray-600`;
    }
  }

  // Đợt 14 — đa ngữ hoá cột "Hành động": tập verb đóng (nguồn BaseRepository.WriteUserLogAsync/
  // AddRangeAsync) dịch qua khoá SYSTEM_LOG.VERB_*, verb lạ giữ nguyên. Tương tự cho tên entity — chỉ
  // dịch các entity hay gặp nhất, còn lại giữ nguyên tên gốc (nửa dịch vẫn đọc được).
  private static readonly KNOWN_VERBS: Set<string> = new Set(['Add', 'Update', 'Delete', 'ChangeStatus', 'Import']);
  private static readonly ENTITY_KEYS: Record<string, string> = {
    Reader: 'SYSTEM_LOG.ENTITY_READER', EbookItem: 'SYSTEM_LOG.ENTITY_EBOOK_ITEM',
    Category: 'SYSTEM_LOG.ENTITY_CATEGORY', PhotoAlbum: 'SYSTEM_LOG.ENTITY_PHOTO_ALBUM',
    News: 'SYSTEM_LOG.ENTITY_NEWS', Bib: 'SYSTEM_LOG.ENTITY_BIB', Module: 'SYSTEM_LOG.ENTITY_MODULE',
    Users: 'SYSTEM_LOG.ENTITY_USERS', RoomBookingConfig: 'SYSTEM_LOG.ENTITY_ROOM_BOOKING',
    Badge: 'SYSTEM_LOG.ENTITY_BADGE', DonVi: 'SYSTEM_LOG.ENTITY_DON_VI',
    ScheduledReport: 'SYSTEM_LOG.ENTITY_SCHEDULED_REPORT', EbookCollection: 'SYSTEM_LOG.ENTITY_EBOOK_COLLECTION',
    StoreType: 'SYSTEM_LOG.ENTITY_STORE_TYPE', CircPlace: 'SYSTEM_LOG.ENTITY_CIRC_PLACE',
    ReaderType: 'SYSTEM_LOG.ENTITY_READER_TYPE', SystemParameter: 'SYSTEM_LOG.ENTITY_SYSTEM_PARAMETER',
    NotificationChannelConfig: 'SYSTEM_LOG.ENTITY_NOTIFICATION_CHANNEL', AdminTask: 'SYSTEM_LOG.ENTITY_ADMIN_TASK',
  };
  private static readonly RE_GENERIC = /^(Add|Update|Delete|ChangeStatus)\s+(\w+)\s+#(.+)$/;
  private static readonly RE_IMPORT = /^Import\s+(\d+)\s+(\w+)\s+items$/;

  translateActionType(actionType: string | null | undefined): string {
    if (!actionType) return '—';
    return SystemLogPage.KNOWN_VERBS.has(actionType)
      ? this.translate.instant('SYSTEM_LOG.VERB_' + actionType.toUpperCase())
      : actionType;
  }

  private entityLabel(entityName: string): string {
    const key = SystemLogPage.ENTITY_KEYS[entityName];
    return key ? this.translate.instant(key) : entityName;
  }

  describeAction(action: string | null | undefined): string {
    if (!action) return '—';
    const generic = action.match(SystemLogPage.RE_GENERIC);
    if (generic) {
      const [, verb, entity, id] = generic;
      if (SystemLogPage.KNOWN_VERBS.has(verb))
        return `${this.translateActionType(verb)} ${this.entityLabel(entity)} #${id}`;
    }
    const imp = action.match(SystemLogPage.RE_IMPORT);
    if (imp) {
      const [, count, entity] = imp;
      return `${this.translate.instant('SYSTEM_LOG.VERB_IMPORT')} ${count} ${this.entityLabel(entity)}`;
    }
    // Câu lạ chưa lường tới — giữ nguyên văn, không mất thông tin (đúng hành vi LRC).
    return action;
  }
}
