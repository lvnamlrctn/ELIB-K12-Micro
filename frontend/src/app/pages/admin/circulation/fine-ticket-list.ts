import { Component, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { DateInputComponent } from '../../../components/date-input/date-input';
import { Router } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator, PageEvent } from '@angular/material/paginator';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import { FineTicketService } from '../../../services/circulation/fine-ticket.service';
import { CFineMethodService } from '../../../services/printbook/cfine-method.service';
import { UserService } from '../../../services/system/user.service';
import { FineTicketListRow, FineTicketTotals } from '../../../models/circulation/fine-ticket';
import { CFineMethod } from '../../../models/printbook/cfine-method';
import { User } from '../../../models/system/user';
import { CanDirective } from '../../../directives/can.directive';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';
import { ReportSignoff } from '../../../shared/report-signoff/report-signoff';
import { ListPageStateService } from '../../../services/shared/list-page-state.service';
import { Auth } from '../../../services/auth';
import { TenantFilterSelectComponent } from '../../../shared/components/tenant-filter-select/tenant-filter-select';
import { applyTenantColumn } from '../../../shared/utils/tenant-column.util';

@Component({
  selector: 'app-fine-ticket-list',
  standalone: true,
  imports: [CanDirective, CommonModule, ReactiveFormsModule, NgSelectModule, TranslateModule, MatTableModule, MatPaginatorModule, MatIconModule, AppDatePipe, DateInputComponent, ReportSignoff, TenantFilterSelectComponent],
  templateUrl: './fine-ticket-list.html'
})
export class FineTicketListPage implements OnInit, OnDestroy {
  private service    = inject(FineTicketService);
  private listState = inject(ListPageStateService);
  private methodSvc   = inject(CFineMethodService);
  private userSvc     = inject(UserService);
  private toastr      = inject(ToastrService);
  private router       = inject(Router);
  public  translate   = inject(TranslateService);
  private auth        = inject(Auth);
  private destroy$    = new Subject<void>();

  isPrivileged = this.auth.isPrivileged();
  tenantId: string | null = null;

  statusOptions = [
    { value: null, label: 'FINE_TICKET.FILTER_ALL' },
    { value: 1,    label: 'FINE_TICKET.ST_PROCESSING' },
    { value: 2,    label: 'FINE_TICKET.ST_DONE' },
  ];
  debtStatusOptions = [
    { value: null, label: 'FINE_TICKET.FILTER_ALL' },
    { value: 1,    label: 'FINE_TICKET.DEBT_OWING' },
    { value: 2,    label: 'FINE_TICKET.DEBT_PAID' },
  ];

  methods = signal<CFineMethod[]>([]);
  creators = signal<User[]>([]);

  displayedColumns = ['stt', 'code', 'fineDate', 'reason', 'cardNo', 'readerName', 'totalAmount', 'paidAmount', 'status'];
  dataSource: FineTicketListRow[] = [];
  totals = signal<FineTicketTotals>({ totalReceivable: 0, totalReceived: 0, remaining: 0 });

  totalRecords = 0; pageSize = 10; pageIndex = 0; pageSizeOptions = [5, 10, 25, 50];
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  isLoading = signal(false);
  reportMode = signal<'none' | 'summary' | 'detail'>('none');

  searchForm = new FormGroup({
    code:         new FormControl<string>('', { nonNullable: true }),
    fineDateFrom: new FormControl<string>('', { nonNullable: true }),
    fineDateTo:   new FormControl<string>('', { nonNullable: true }),
    cardNo:       new FormControl<string>('', { nonNullable: true }),
    readerName:   new FormControl<string>('', { nonNullable: true }),
    statusFilter: new FormControl<number | null>(null),
    debtStatus:   new FormControl<number | null>(null),
    fineMethodId: new FormControl<number | null>(null),
    createdRowBy: new FormControl<number | null>(null),
  });

  ngOnInit(): void {
    applyTenantColumn(this.displayedColumns, this.isPrivileged);
    const saved = this.listState.recall('circulation.fine-ticket-list'); if (saved) { this.pageIndex = saved.pageIndex; this.pageSize = saved.pageSize; }
    this.methodSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.methods.set(r), error: () => {} });
    this.userSvc.searchAll().pipe(takeUntil(this.destroy$)).subscribe({ next: r => this.creators.set(r), error: () => {} });
    this.loadData();
  }
  onPageChange(event: PageEvent): void { this.pageIndex = event.pageIndex; this.pageSize = event.pageSize; this.loadData(); }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  private buildFilterParams() {
    const f = this.searchForm.getRawValue();
    return {
      code:         f.code || null,
      fineDateFrom: f.fineDateFrom || null,
      fineDateTo:   f.fineDateTo || null,
      cardNo:       f.cardNo || null,
      readerName:   f.readerName || null,
      statusFilter: f.statusFilter ?? null,
      debtStatus:   f.debtStatus ?? null,
      fineMethodId: f.fineMethodId ?? null,
      createdRowBy: f.createdRowBy ?? null,
      tenantId:     this.tenantId,
    };
  }

  loadData(): void {
    this.listState.remember('circulation.fine-ticket-list', this.pageIndex, this.pageSize);
    this.isLoading.set(true);
    const filters = this.buildFilterParams();
    this.service.search({ ...filters, pageIndex: this.pageIndex + 1, pageSize: this.pageSize })
      .pipe(takeUntil(this.destroy$)).subscribe({
        next: res => { this.dataSource = res.data; this.totalRecords = res.recordsTotal; this.isLoading.set(false); },
        error: () => { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.isLoading.set(false); }
      });
    this.service.getTotals(filters).pipe(takeUntil(this.destroy$)).subscribe({ next: t => this.totals.set(t), error: () => {} });
  }
  triggerSearch(): void { this.pageIndex = 0; if (this.paginator) this.paginator.pageIndex = 0; this.loadData(); }
  onTenantChange(): void { this.triggerSearch(); }
  getRowIndex(i: number): number { return this.pageIndex * this.pageSize + i + 1; }

  goToNew(): void { this.router.navigate(['/admin/fine-ticket', 'new']); }
  goToDetail(row: FineTicketListRow): void { this.router.navigate(['/admin/fine-ticket', row.publicId]); }

  statusClass(s: number | undefined): string { return s === 2 ? 'bg-green-50 text-green-700' : 'bg-amber-50 text-amber-700'; }
  statusLabel(s: number | undefined): string { return this.translate.instant(s === 2 ? 'FINE_TICKET.ST_DONE' : 'FINE_TICKET.ST_PROCESSING'); }

  printSummary(): void { this.reportMode.set('summary'); setTimeout(() => { window.print(); this.reportMode.set('none'); }, 0); }
  printDetail(): void { this.reportMode.set('detail'); setTimeout(() => { window.print(); this.reportMode.set('none'); }, 0); }
}
