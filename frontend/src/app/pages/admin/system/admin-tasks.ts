import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Subject, EMPTY, timer } from 'rxjs';
import { exhaustMap, switchMap, takeUntil } from 'rxjs/operators';
import { CanDirective } from '../../../directives/can.directive';
import { AdminTaskService } from '../../../services/system/admin-task.service';
import { AdminTaskView, AdminTaskMonitorItem, AdminTaskMonitorDetail, RetentionPreview, RetentionRun } from '../../../models/system/admin-task';
import { ToastrService } from '../../../services/shared/toastr.service';

type Tab = 'mine' | 'monitor' | 'retention';

// Đợt 10 — "Tác vụ nền" (AdminTask v2), tab "tác vụ của tôi". Đợt 13 — thêm tab "Giám sát" (tác vụ của
// người khác, gate quyền ADMIN_TASK_MONITOR/ADMIN_TASK_CONTROL) và "Dọn dữ liệu" (gate ADMIN_TASK_RETENTION).
// Polling tab "của tôi" giữ nguyên 2 tầng: kiểm tra AdminTasks:Enabled mỗi 15 giây trước, chỉ khi bật mới
// poll danh sách mỗi 3 giây — đúng cơ chế ELIB-LRC. 2 tab mới KHÔNG polling liên tục (tải khi chuyển tab,
// tải lại thủ công sau mỗi thao tác) — tần suất truy cập thấp hơn nhiều so với theo dõi tiến độ 1 tác vụ.
@Component({
  selector: 'app-admin-tasks',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslateModule, MatIconModule, CanDirective],
  templateUrl: './admin-tasks.html'
})
export class AdminTasksPage implements OnInit, OnDestroy {
  private service   = inject(AdminTaskService);
  public  translate = inject(TranslateService);
  private toastr    = inject(ToastrService);
  private destroy$  = new Subject<void>();

  tab = signal<Tab>('mine');

  enabled = signal(true);
  tasks   = signal<AdminTaskView[]>([]);
  detail  = signal<AdminTaskView | null>(null);
  busy    = signal(false);

  ngOnInit(): void {
    timer(0, 15000).pipe(
      takeUntil(this.destroy$),
      exhaustMap(() => this.service.health()),
      switchMap(health => {
        this.enabled.set(health.enabled);
        if (!health.enabled) return EMPTY;
        return timer(0, 3000).pipe(exhaustMap(() => this.service.list()));
      })
    ).subscribe(res => {
      this.tasks.set(res.items);
      const open = this.detail();
      if (open && (open.state === 'Queued' || open.state === 'Running'))
        this.service.get(open.id).subscribe(t => { if (t) this.detail.set(t); });
    });
  }

  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }

  setTab(t: Tab): void {
    this.tab.set(t);
    if (t === 'monitor') this.loadMonitor();
    if (t === 'retention') this.loadRetention();
  }

  open(task: AdminTaskView): void { this.detail.set(task); }
  closeDetail(): void { this.detail.set(null); }

  progress(t: { totalItems: number; completedItems: number }): number {
    return t.totalItems > 0 ? Math.round((100 * t.completedItems) / t.totalItems) : 0;
  }

  private static readonly KindKeys: Record<string, string> = {
    'reader-import': 'ADMIN_TASKS.KIND_READER_IMPORT',
    'barcode-reregister-import': 'ADMIN_TASKS.KIND_BARCODE_REREGISTER_IMPORT',
    'inventory-import': 'ADMIN_TASKS.KIND_INVENTORY_IMPORT',
  };

  kindLabel(kind: string): string {
    const key = AdminTasksPage.KindKeys[kind];
    return key ? this.translate.instant(key) : kind;
  }

  stateClass(state: string): string {
    switch (state) {
      case 'Completed': return 'bg-green-100 text-green-700';
      case 'Running':   return 'bg-blue-100 text-blue-700';
      case 'Queued':    return 'bg-gray-100 text-gray-600';
      case 'Paused':    return 'bg-amber-100 text-amber-700';
      case 'NeedsReview': return 'bg-orange-100 text-orange-700';
      case 'Failed':    return 'bg-red-100 text-red-700';
      case 'Cancelled': return 'bg-gray-100 text-gray-400';
      default:          return 'bg-gray-100 text-gray-600';
    }
  }

  private refresh(id: string): void {
    this.service.get(id).subscribe(t => { if (t) this.detail.set(t); });
  }

  pause(t: AdminTaskView): void {
    this.busy.set(true);
    this.service.pause(t.id).subscribe({
      next: () => { this.busy.set(false); this.refresh(t.id); },
      error: () => this.busy.set(false),
    });
  }

  resume(t: AdminTaskView): void {
    this.busy.set(true);
    this.service.resume(t.id).subscribe({
      next: () => { this.busy.set(false); this.refresh(t.id); },
      error: () => this.busy.set(false),
    });
  }

  cancel(t: AdminTaskView): void {
    if (!confirm(this.translate.instant('ADMIN_TASKS.CONFIRM_CANCEL'))) return;
    this.busy.set(true);
    this.service.cancel(t.id).subscribe({
      next: () => { this.busy.set(false); this.refresh(t.id); },
      error: () => this.busy.set(false),
    });
  }

  /** Kết quả tác vụ được lưu nguyên JSON phía server — tên thuộc tính có thể là camelCase (xem trước) hoặc
   *  PascalCase (ghi thật, ReaderImportResult); đọc được cả 2. */
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  resultValue(t: AdminTaskView, key: string): any {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const r = t.result as any;
    if (!r) return undefined;
    return r[key] ?? r[key.charAt(0).toUpperCase() + key.slice(1)];
  }
  resultList(t: AdminTaskView, key: string): string[] { const v = this.resultValue(t, key); return Array.isArray(v) ? v : []; }

  downloadErrors(t: AdminTaskView): void {
    this.service.importErrors(t.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = `doc-gia-dong-loi-${t.id}.xlsx`; a.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.toastr.error(this.translate.instant('ADMIN_TASKS.ERRORS_UNAVAILABLE')),
    });
  }

  confirmTask(t: AdminTaskView): void {
    this.busy.set(true);
    this.service.confirm(t.id).subscribe({
      next: task => {
        this.busy.set(false);
        if (task) { this.detail.set(task); this.toastr.success(this.translate.instant('ADMIN_TASKS.CONFIRM')); }
        else this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
      },
      error: () => { this.busy.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); },
    });
  }

  previewRemaining(t: AdminTaskView): void {
    this.busy.set(true);
    this.service.previewRemaining(t.id).subscribe({
      next: task => { this.busy.set(false); if (task) this.detail.set(task); },
      error: () => this.busy.set(false),
    });
  }

  // ── Đợt 13 — Giám sát ────────────────────────────────────────────────────
  monitorTasks   = signal<AdminTaskMonitorItem[]>([]);
  monitorDetail  = signal<AdminTaskMonitorDetail | null>(null);
  monitorBusy    = signal(false);
  monitorState   = signal('');

  loadMonitor(): void {
    this.service.monitorList({ state: this.monitorState() || undefined }).subscribe(res => this.monitorTasks.set(res.items));
  }

  filterMonitor(): void { this.loadMonitor(); }

  openMonitor(t: { id: string }): void {
    this.service.monitorDetail(t.id).subscribe(d => this.monitorDetail.set(d));
  }

  closeMonitorDetail(): void { this.monitorDetail.set(null); }

  monitorPauseTask(t: { id: string }): void {
    const reason = window.prompt(this.translate.instant('ADMIN_TASKS.MONITOR_REASON_PROMPT'));
    if (!reason) return;
    this.monitorBusy.set(true);
    this.service.monitorPause(t.id, reason).subscribe({
      next: res => {
        this.monitorBusy.set(false);
        if (res) { this.loadMonitor(); this.openMonitor(t); }
        else this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
      },
      error: () => { this.monitorBusy.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); },
    });
  }

  monitorResumeTask(t: { id: string }): void {
    const reason = window.prompt(this.translate.instant('ADMIN_TASKS.MONITOR_REASON_PROMPT'));
    if (!reason) return;
    this.monitorBusy.set(true);
    this.service.monitorResume(t.id, reason).subscribe({
      next: res => {
        this.monitorBusy.set(false);
        if (res) { this.loadMonitor(); this.openMonitor(t); }
        else this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR'));
      },
      error: () => { this.monitorBusy.set(false); this.toastr.error(this.translate.instant('COMMON.LOAD_ERROR')); },
    });
  }

  toggleHold(t: { id: string }, hold: boolean): void {
    this.monitorBusy.set(true);
    this.service.retentionHold(t.id, hold).subscribe({
      next: () => { this.monitorBusy.set(false); this.openMonitor(t); },
      error: () => this.monitorBusy.set(false),
    });
  }

  // ── Đợt 13 — Dọn dữ liệu ─────────────────────────────────────────────────
  retentionPreviewData = signal<RetentionPreview | null>(null);
  retentionRunsData    = signal<RetentionRun[]>([]);
  retentionBusy        = signal(false);

  loadRetention(): void {
    this.service.retentionPreview().subscribe(p => this.retentionPreviewData.set(p));
    this.service.retentionRuns().subscribe(r => this.retentionRunsData.set(r));
  }

  runRetention(): void {
    if (!confirm(this.translate.instant('ADMIN_TASKS.RETENTION_CONFIRM_RUN'))) return;
    this.retentionBusy.set(true);
    this.service.retentionRun().subscribe({
      next: () => { this.retentionBusy.set(false); this.loadRetention(); },
      error: () => this.retentionBusy.set(false),
    });
  }
}
