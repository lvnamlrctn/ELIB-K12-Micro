import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, ApprovalService, ChangeRequest, errorMessage } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { ReasonDialog, ReasonResult } from '../../shared/reason-dialog';
import { ToastrService } from '../../shared/toastr';
import { Loading } from '../../shared/ui';

/** Các service có hộp phê duyệt (holdings thêm khi xử lý báo mất/thanh lý). Service không cấp quyền hoặc chưa bật license trả lỗi → bỏ qua. */
const SERVICES: ApprovalService[] = ['circulation', 'holdings'];

const FIELD_LABELS: Record<string, string> = {
  status: 'FINE_TICKET_PAGE.H_STATUS', total: 'FINE_TICKET_PAGE.TOTAL', discount: 'FINE_TICKET_PAGE.DISCOUNT', paid: 'FINE_TICKET_PAGE.PAID',
  remaining: 'FINE_TICKET_PAGE.REMAINING', note: 'COMMON.NOTE', manualAmount: 'FINE_TICKET_PAGE.TOTAL', lines: 'FINE_TICKET_PAGE.H_LINES',
  copies: 'APPROVALS_PAGE.F_COPIES',
  state: 'LOAN_HISTORY_PAGE.H_STATE', closeKind: 'LOAN_HISTORY_PAGE.H_CLOSE_KIND', closeReason: 'LOAN_HISTORY_PAGE.H_CLOSE_REASON', returnedAt: 'LOAN_HISTORY_PAGE.RETURNED_AT',
};

/**
 * Chờ phê duyệt: yêu cầu thay đổi vượt thẩm quyền của người thao tác (miễn/giảm phạt lớn, mở lại phiếu, hoàn tiền…). Người có quyền duyệt
 * của loại thao tác đó xem trước/sau rồi Duyệt (hệ thống áp dụng ngay) hoặc Từ chối (bắt buộc ghi lý do); người đề nghị xem lại / rút yêu cầu.
 */
@Component({
  selector: 'app-approvals',
  imports: [FormsModule, LDatePipe, Loading, ReasonDialog, TranslatePipe],
  template: `
    <div class="mb-5 flex items-center gap-3 flex-wrap">
      <h4 class="page-title">{{ 'APPROVALS_PAGE.TITLE' | translate }}</h4>
      <div class="ml-auto flex gap-2">
        <button type="button" class="px-3 py-1.5 rounded-lg text-sm border" [class]="scope() === 'inbox' ? 'bg-teal-600 text-white border-teal-600' : 'bg-white text-gray-600 border-gray-200'" (click)="setScope('inbox')">{{ 'APPROVALS_PAGE.INBOX' | translate }}</button>
        <button type="button" class="px-3 py-1.5 rounded-lg text-sm border" [class]="scope() === 'mine' ? 'bg-teal-600 text-white border-teal-600' : 'bg-white text-gray-600 border-gray-200'" (click)="setScope('mine')">{{ 'APPROVALS_PAGE.MINE' | translate }}</button>
        <select class="input !w-auto !py-1.5" [ngModel]="status()" (ngModelChange)="setStatus($event)" [attr.aria-label]="'APPROVALS_PAGE.STATUS' | translate">
          <option [ngValue]="1">{{ 'APPROVALS_PAGE.S_Pending' | translate }}</option>
          <option [ngValue]="null">{{ 'APPROVALS_PAGE.S_ALL' | translate }}</option>
          <option [ngValue]="2">{{ 'APPROVALS_PAGE.S_Approved' | translate }}</option>
          <option [ngValue]="3">{{ 'APPROVALS_PAGE.S_Rejected' | translate }}</option>
          <option [ngValue]="5">{{ 'APPROVALS_PAGE.S_Failed' | translate }}</option>
        </select>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (busy()) { <app-loading /> }
      <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse min-w-[900px]">
          <thead><tr>
            <th class="th w-44">{{ 'APPROVALS_PAGE.KIND' | translate }}</th><th class="th">{{ 'APPROVALS_PAGE.CONTENT' | translate }}</th>
            <th class="th w-52">{{ 'APPROVALS_PAGE.REQUESTER' | translate }}</th><th class="th w-28">{{ 'APPROVALS_PAGE.STATUS' | translate }}</th><th class="th w-44 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (r of rows(); track r.publicId) {
              <tr class="align-top">
                <td class="td"><div class="font-medium">{{ r.kindName }}</div><div class="text-[11px] text-gray-500">{{ r.targetLabel }}</div></td>
                <td class="td">
                  <div>{{ r.summary }}</div>
                  <div class="text-xs text-gray-600 mt-0.5"><b>{{ 'REASON_DIALOG.REASON' | translate }}:</b> {{ r.reason }}</div>
                  @if (r.decisionNote) { <div class="text-xs text-gray-600"><b>{{ 'APPROVALS_PAGE.DECISION' | translate }}:</b> {{ r.decisionNote }} ({{ r.decidedByName }}, {{ r.decidedAt | ldate: 'dd/MM/yyyy HH:mm' }})</div> }
                  <button type="button" class="text-xs text-teal-700 underline mt-1" (click)="toggle(r.publicId)">{{ (open().has(r.publicId) ? 'APPROVALS_PAGE.HIDE_DIFF' : 'APPROVALS_PAGE.SHOW_DIFF') | translate }}</button>
                  @if (open().has(r.publicId)) {
                    <table class="mt-2 text-xs border border-gray-100">
                      <thead class="bg-gray-50 text-gray-500"><tr><th class="px-2 py-1 text-left">{{ 'READER_HISTORY.FIELD' | translate }}</th><th class="px-2 py-1 text-left">{{ 'APPROVALS_PAGE.BEFORE' | translate }}</th><th class="px-2 py-1 text-left">{{ 'APPROVALS_PAGE.AFTER' | translate }}</th></tr></thead>
                      <tbody>
                        @for (f of fields(r); track f) {
                          <tr class="border-t border-gray-100">
                            <td class="px-2 py-1 text-gray-600">{{ label(f) | translate }}</td>
                            <td class="px-2 py-1 text-red-700">{{ show(r.before?.[f]) }}</td>
                            <td class="px-2 py-1 text-green-700 font-medium">{{ show(r.after?.[f]) }}</td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  }
                </td>
                <td class="td text-[13px]">{{ r.requestedByName ?? '—' }}<div class="text-[11px] text-gray-500">{{ r.requestedAt | ldate: 'dd/MM/yyyy HH:mm' }}</div></td>
                <td class="td"><span [class]="badge(r.status)">{{ statusKey(r.status) | translate }}</span></td>
                <td class="td text-center">
                  @if (r.status === 'Pending' && r.canDecide) {
                    <button type="button" class="btn-primary !py-1 !px-2 mr-1" (click)="deciding.set({ r, approve: true })">{{ 'APPROVALS_PAGE.APPROVE' | translate }}</button>
                    <button type="button" class="btn-secondary !py-1 !px-2 text-red-700" (click)="deciding.set({ r, approve: false })">{{ 'APPROVALS_PAGE.REJECT' | translate }}</button>
                  } @else if (r.status === 'Pending' && scope() === 'mine') {
                    <button type="button" class="btn-secondary !py-1 !px-2" (click)="cancel(r)">{{ 'APPROVALS_PAGE.CANCEL' | translate }}</button>
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="5" class="py-10 text-center text-gray-500">{{ 'APPROVALS_PAGE.EMPTY' | translate }}</td></tr>
            }
          </tbody>
        </table>
      </div>
    </div>

    @if (deciding(); as d) {
      <app-reason-dialog [title]="d.approve ? 'APPROVALS_PAGE.APPROVE_TITLE' : 'APPROVALS_PAGE.REJECT_TITLE'" [message]="d.approve ? 'APPROVALS_PAGE.APPROVE_MSG' : 'APPROVALS_PAGE.REJECT_MSG'"
                         [params]="{ summary: d.r.summary }" [required]="!d.approve" [confirmText]="d.approve ? 'APPROVALS_PAGE.APPROVE' : 'APPROVALS_PAGE.REJECT'"
                         (confirmed)="decide(d.r, d.approve, $event)" (cancelled)="deciding.set(null)" />
    }
  `,
})
export class Approvals implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);

  protected readonly rows = signal<ChangeRequest[]>([]);
  protected readonly busy = signal(false);
  protected readonly scope = signal<'inbox' | 'mine'>('inbox');
  protected readonly status = signal<number | null>(1);
  protected readonly open = signal<Set<string>>(new Set());
  protected readonly deciding = signal<{ r: ChangeRequest; approve: boolean } | null>(null);

  ngOnInit(): void {
    void this.load();
  }

  protected setScope(scope: 'inbox' | 'mine'): void {
    this.scope.set(scope);
    void this.load();
  }

  protected setStatus(status: number | null): void {
    this.status.set(status);
    void this.load();
  }

  protected async load(): Promise<void> {
    this.busy.set(true);
    try {
      const all = await Promise.all(SERVICES.map(async (service) => {
        try {
          return (await this.api.searchApprovals(service, this.scope(), this.status())).items.map((r) => ({ ...r, service }));
        } catch {
          return []; // service không có quyền / chưa bật license
        }
      }));
      this.rows.set(all.flat().sort((a, b) => b.requestedAt.localeCompare(a.requestedAt)));
    } finally {
      this.busy.set(false);
    }
  }

  protected toggle(id: string): void {
    this.open.update((s) => {
      const next = new Set(s);
      if (!next.delete(id)) next.add(id);
      return next;
    });
  }

  protected fields(r: ChangeRequest): string[] {
    return [...new Set([...Object.keys(r.before ?? {}), ...Object.keys(r.after ?? {})])].filter((f) => JSON.stringify(r.before?.[f]) !== JSON.stringify(r.after?.[f]));
  }

  protected label(field: string): string {
    return FIELD_LABELS[field] ?? field;
  }

  protected show(value: unknown): string {
    if (value == null) return '—';
    return Array.isArray(value) ? (value.length ? value.join('; ') : '—') : String(value);
  }

  protected statusKey(status: string): string {
    switch (status) {
      case 'Pending': return 'APPROVALS_PAGE.S_Pending';
      case 'Approved': return 'APPROVALS_PAGE.S_Approved';
      case 'Rejected': return 'APPROVALS_PAGE.S_Rejected';
      case 'Cancelled': return 'APPROVALS_PAGE.S_Cancelled';
      default: return 'APPROVALS_PAGE.S_Failed';
    }
  }

  protected badge(status: string): string {
    return status === 'Approved' ? 'badge-on' : status === 'Pending' ? 'badge-info' : status === 'Rejected' || status === 'Failed' ? 'badge-danger' : 'badge-off';
  }

  protected async decide(r: ChangeRequest, approve: boolean, result: ReasonResult): Promise<void> {
    this.deciding.set(null);
    const service = r.service ?? 'circulation';
    try {
      if (approve) await this.api.approve(service, r.publicId, result.reason || null);
      else await this.api.rejectApproval(service, r.publicId, result.reason);
      this.toastr.success(approve ? 'APPROVALS_PAGE.APPROVED' : 'APPROVALS_PAGE.REJECTED');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
    await this.load();
  }

  protected async cancel(r: ChangeRequest): Promise<void> {
    try {
      await this.api.cancelApproval(r.service ?? 'circulation', r.publicId);
      this.toastr.success('APPROVALS_PAGE.CANCELLED');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
    await this.load();
  }
}
