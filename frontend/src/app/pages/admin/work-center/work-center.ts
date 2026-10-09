import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { ToastrService } from '../../../services/shared/toastr.service';
import {
  WorkCenterService, WorkSourceInfo, WorkItemRow, WorkItemsResponse, WorkItemAssigneeOption
} from '../../../services/admin/work-center.service';
import { AppDatePipe } from '../../../shared/pipes/app-date.pipe';

type ViewFilter = 'all' | 'mine' | 'unassigned' | 'overdue';

/** Trung tâm công việc (Đợt 15 — port từ ELIB-LRC, bản đầu). Tổng hợp 3 nguồn hồ sơ chờ duyệt (đặt
 * phòng học nhóm/tài liệu nộp/đánh giá). Đặt phòng KHÔNG có dialog quyết định riêng — gọi thẳng
 * RoomBookingAdmin/Approve|Reject sẵn có qua service, giữ nguyên cơ chế thông báo hiện tại. Danh sách cán
 * bộ trong dialog phân công tải 1 lần khi mở (tối đa 100, đúng giới hạn backend) rồi lọc client-side qua
 * ô tìm kiếm có sẵn của ng-select — đơn giản hoá có chủ đích so với typeahead server-side đầy đủ. */
@Component({
  selector: 'app-work-center',
  standalone: true,
  imports: [CommonModule, FormsModule, NgSelectModule, TranslateModule, AppDatePipe],
  templateUrl: './work-center.html'
})
export class WorkCenterPage implements OnInit, OnDestroy {
  private service   = inject(WorkCenterService);
  private route      = inject(ActivatedRoute);
  private router      = inject(Router);
  private toastr    = inject(ToastrService);
  public  translate = inject(TranslateService);
  private destroy$  = new Subject<void>();

  sources = signal<WorkSourceInfo[]>([]);
  activeType = signal<string>('');
  view = signal<ViewFilter>('all');
  page = signal(1);
  pageSize = signal(10);
  pageSizeOptions = [10, 20, 50];
  data = signal<WorkItemsResponse | null>(null);
  isLoading = signal(false);

  activeSource = computed<WorkSourceInfo | undefined>(() => this.sources().find(s => s.type === this.activeType()));
  totalPages = computed(() => {
    const d = this.data();
    return d ? Math.max(1, Math.ceil(d.totalCount / this.pageSize())) : 1;
  });

  // ── Chi tiết ──────────────────────────────────────────────────────────────
  showDetail = signal(false);
  detail = signal<WorkItemRow | null>(null);

  // ── Dialog phân công/hạn ─────────────────────────────────────────────────
  showAssignDialog = signal(false);
  assignTarget = signal<WorkItemRow | null>(null);
  assignAssigneeId = signal<number | null>(null);
  assignDueLocal = signal<string>('');
  assignReason = signal('');
  assigneeOptions = signal<WorkItemAssigneeOption[]>([]);
  isSavingAssign = signal(false);
  assignDueWarning = computed(() => {
    const t = this.assignTarget();
    if (!t || t.type !== 'room-booking' || !t.roomStartAt || !this.assignDueLocal()) return false;
    const due = new Date(this.assignDueLocal());
    return due.getTime() > new Date(t.roomStartAt).getTime();
  });

  // ── Dialog quyết định (submission/review/room-booking reject) ───────────
  showDecisionDialog = signal(false);
  decisionTarget = signal<WorkItemRow | null>(null);
  decisionApprove = signal(true);
  decisionReason = signal('');
  isSavingDecision = signal(false);
  decisionNeedsReason = computed(() => {
    const t = this.decisionTarget();
    if (!t) return true;
    return t.type !== 'room-booking' || !this.decisionApprove();
  });

  ngOnInit(): void {
    this.service.getSources().pipe(takeUntil(this.destroy$)).subscribe(sources => {
      this.sources.set(sources);
      const qp = this.route.snapshot.queryParamMap;
      const qType = qp.get('type');
      const qView = qp.get('view') as ViewFilter | null;
      const qId   = qp.get('id');
      const initial = sources.find(s => s.type === qType && s.canView) ?? sources.find(s => s.canView);
      if (qView && ['all', 'mine', 'unassigned', 'overdue'].includes(qView)) this.view.set(qView);
      if (initial) {
        this.activeType.set(initial.type);
        this.loadItems();
        if (qId) this.openDetail({ type: initial.type, publicId: qId } as WorkItemRow);
      }
    });
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  selectType(type: string): void {
    this.activeType.set(type);
    this.page.set(1);
    this.loadItems();
  }
  viewFilters: ViewFilter[] = ['all', 'mine', 'unassigned', 'overdue'];

  selectView(v: string): void {
    this.view.set(v as ViewFilter);
    this.page.set(1);
    this.loadItems();
  }
  changePage(p: number): void {
    if (p < 1 || p > this.totalPages()) return;
    this.page.set(p);
    this.loadItems();
  }
  changePageSize(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
    this.loadItems();
  }

  loadItems(): void {
    const type = this.activeType();
    if (!type) return;
    this.isLoading.set(true);
    this.service.getItems(type, this.view(), this.page(), this.pageSize()).pipe(takeUntil(this.destroy$)).subscribe(res => {
      this.isLoading.set(false);
      if (!res) { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); return; }
      this.data.set(res);
    });
  }

  // ── Nhận việc ─────────────────────────────────────────────────────────────
  claim(row: WorkItemRow): void {
    this.service.claim(row.type, row.publicId, row.version).pipe(takeUntil(this.destroy$)).subscribe(res => {
      if (res.ok) { this.toastr.success(this.translate.instant('WORK_CENTER.CLAIMED')); this.loadItems(); }
      else if (res.status === 409) { this.toastr.warning(this.translate.instant('WORK_CENTER.CLAIM_CONFLICT')); this.loadItems(); }
      else { this.toastr.error(res.message || this.translate.instant('COMMON.LOAD_ERROR')); }
    });
  }

  // ── Dialog phân công ──────────────────────────────────────────────────────
  openAssignDialog(row: WorkItemRow): void {
    this.assignTarget.set(row);
    this.assignAssigneeId.set(row.assigneeId ?? null);
    this.assignDueLocal.set(this.toLocalInputValue(row.dueAtUtc));
    this.assignReason.set('');
    this.assigneeOptions.set([]);
    this.showAssignDialog.set(true);
    this.service.getAssignees(row.type, '', 0).pipe(takeUntil(this.destroy$)).subscribe(res => this.assigneeOptions.set(res.items));
  }
  closeAssignDialog(): void { this.showAssignDialog.set(false); this.assignTarget.set(null); }

  confirmAssign(): void {
    const t = this.assignTarget();
    if (!t || this.isSavingAssign()) return;
    if (!this.assignReason().trim()) { this.toastr.warning(this.translate.instant('WORK_CENTER.REASON_REQUIRED')); return; }
    this.isSavingAssign.set(true);
    this.service.setAssignment(t.type, t.publicId, {
      version: t.version,
      assigneeId: this.assignAssigneeId(),
      dueAtUtc: this.toUtcIso(this.assignDueLocal()),
      reason: this.assignReason().trim(),
    }).pipe(takeUntil(this.destroy$)).subscribe(res => {
      this.isSavingAssign.set(false);
      if (res.ok) { this.toastr.success(this.translate.instant('WORK_CENTER.ASSIGN_SAVED')); this.closeAssignDialog(); this.loadItems(); }
      else if (res.status === 409) { this.toastr.warning(this.translate.instant('WORK_CENTER.VERSION_CONFLICT')); this.closeAssignDialog(); this.loadItems(); }
      else { this.toastr.error(res.message || this.translate.instant('COMMON.LOAD_ERROR')); }
    });
  }

  // ── Mở xử lý (chi tiết) ───────────────────────────────────────────────────
  openDetail(row: WorkItemRow): void {
    this.showDetail.set(true);
    this.detail.set(null);
    this.service.getDetail(row.type, row.publicId).pipe(takeUntil(this.destroy$)).subscribe(d => {
      if (!d) { this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); this.showDetail.set(false); return; }
      this.detail.set(d);
    });
  }
  closeDetail(): void { this.showDetail.set(false); this.detail.set(null); }

  submissionFileUrl(row: WorkItemRow): string { return this.service.submissionFileUrl(row.publicId); }

  // ── Dialog quyết định ─────────────────────────────────────────────────────
  openDecisionDialog(row: WorkItemRow, approve: boolean): void {
    this.decisionTarget.set(row);
    this.decisionApprove.set(approve);
    this.decisionReason.set('');
    this.showDecisionDialog.set(true);
  }
  closeDecisionDialog(): void { this.showDecisionDialog.set(false); this.decisionTarget.set(null); }

  confirmDecision(): void {
    const t = this.decisionTarget();
    if (!t || this.isSavingDecision()) return;
    if (this.decisionNeedsReason() && !this.decisionReason().trim()) {
      this.toastr.warning(this.translate.instant('WORK_CENTER.REASON_REQUIRED'));
      return;
    }
    this.isSavingDecision.set(true);
    const approve = this.decisionApprove();
    const reason = this.decisionReason().trim();
    const obs = t.type === 'room-booking'
      ? (approve ? this.service.approveRoomBooking(t.publicId) : this.service.rejectRoomBooking(t.publicId, reason))
      : this.service.decide(t.type, t.publicId, approve, reason);
    obs.pipe(takeUntil(this.destroy$)).subscribe(res => {
      this.isSavingDecision.set(false);
      if (res.ok) {
        this.toastr.success(this.translate.instant('WORK_CENTER.DECISION_SAVED'));
        this.closeDecisionDialog();
        this.closeDetail();
        this.loadItems();
      } else if (res.status === 409) {
        this.toastr.warning(this.translate.instant('WORK_CENTER.ALREADY_CLOSED'));
        this.closeDecisionDialog();
        this.loadItems();
      } else {
        this.toastr.error(res.message || this.translate.instant('COMMON.LOAD_ERROR'));
      }
    });
  }

  // ── Helper thời gian ──────────────────────────────────────────────────────
  private toLocalInputValue(utcIso?: string | null): string {
    if (!utcIso) return '';
    const d = new Date(utcIso);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }
  private toUtcIso(local: string): string | null {
    if (!local) return null;
    const d = new Date(local);
    return isNaN(d.getTime()) ? null : d.toISOString();
  }
}
