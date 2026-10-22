import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, ApprovalRule, ApprovalService, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { ReasonDialog, ReasonResult } from '../../shared/reason-dialog';
import { ToastrService } from '../../shared/toastr';
import { Loading } from '../../shared/ui';

interface Row extends ApprovalRule { service: ApprovalService; }

const SERVICES: ApprovalService[] = ['circulation', 'holdings'];

/**
 * Thẩm quyền phê duyệt: với từng loại thao tác quan trọng, đơn vị chọn có yêu cầu duyệt không và ngưỡng nào thì phải chờ người có quyền
 * "Duyệt" (vai Quản trị đơn vị luôn áp dụng trực tiếp). Mọi ngưỡng để trống = luôn cần duyệt. Số tiền/tỉ lệ: VƯỢT ngưỡng mới cần duyệt;
 * số lượng: ĐẠT ngưỡng. Mỗi lần đổi có lý do và được ghi nhật ký.
 */
@Component({
  selector: 'app-approval-rules',
  imports: [FormsModule, Loading, ReasonDialog, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'APPROVAL_RULES_PAGE.TITLE' | translate }}</h4>
      <p class="text-sm text-gray-500 mt-1">{{ 'APPROVAL_RULES_PAGE.HINT' | translate }}</p></div>
    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (busy()) { <app-loading /> }
      <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse min-w-[860px]">
          <thead><tr>
            <th class="th">{{ 'APPROVAL_RULES_PAGE.KIND' | translate }}</th><th class="th w-28 !text-center">{{ 'APPROVAL_RULES_PAGE.ENABLED' | translate }}</th>
            <th class="th w-36">{{ 'APPROVAL_RULES_PAGE.AMOUNT' | translate }}</th><th class="th w-28">{{ 'APPROVAL_RULES_PAGE.PERCENT' | translate }}</th><th class="th w-28">{{ 'APPROVAL_RULES_PAGE.COUNT' | translate }}</th>
            <th class="th w-28 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (r of rows(); track r.kind) {
              <tr class="align-top">
                <td class="td"><div class="font-medium">{{ r.name }} @if (r.isCustom) { <span class="badge-info ml-1">{{ 'APPROVAL_RULES_PAGE.CUSTOM' | translate }}</span> }</div>
                  <div class="text-xs text-gray-500">{{ r.description }} <span class="font-mono">({{ r.module }}:approve)</span></div></td>
                <td class="td text-center"><input type="checkbox" [(ngModel)]="r.enabled" [disabled]="!canEdit()" [attr.aria-label]="r.name" /></td>
                <td class="td"><input type="number" min="0" class="input !py-1" [(ngModel)]="r.amount" [disabled]="!canEdit() || !r.enabled" [attr.aria-label]="'APPROVAL_RULES_PAGE.AMOUNT' | translate" /></td>
                <td class="td"><input type="number" min="0" max="100" class="input !py-1" [(ngModel)]="r.percent" [disabled]="!canEdit() || !r.enabled" [attr.aria-label]="'APPROVAL_RULES_PAGE.PERCENT' | translate" /></td>
                <td class="td"><input type="number" min="1" class="input !py-1" [(ngModel)]="r.count" [disabled]="!canEdit() || !r.enabled" [attr.aria-label]="'APPROVAL_RULES_PAGE.COUNT' | translate" /></td>
                <td class="td text-center">@if (canEdit()) { <button type="button" class="btn-primary !py-1 !px-3" (click)="saving.set(r)">{{ 'COMMON.SAVE' | translate }}</button> }</td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="py-10 text-center text-gray-500">{{ 'APPROVAL_RULES_PAGE.EMPTY' | translate }}</td></tr>
            }
          </tbody>
        </table>
      </div>
    </div>
    @if (saving(); as r) {
      <app-reason-dialog title="APPROVAL_RULES_PAGE.REASON_TITLE" message="APPROVAL_RULES_PAGE.REASON_MSG" [params]="{ name: r.name }" (confirmed)="save(r, $event)" (cancelled)="saving.set(null)" />
    }
  `,
})
export class ApprovalRules implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);

  protected readonly rows = signal<Row[]>([]);
  protected readonly busy = signal(false);
  protected readonly saving = signal<Row | null>(null);

  async ngOnInit(): Promise<void> {
    this.busy.set(true);
    try {
      const all = await Promise.all(SERVICES.map(async (service) => {
        try {
          return (await this.api.approvalRules(service)).map((r) => ({ ...r, service }));
        } catch {
          return []; // service chưa bật license của đơn vị
        }
      }));
      this.rows.set(all.flat());
    } finally {
      this.busy.set(false);
    }
  }

  protected canEdit(): boolean {
    return this.session.can('APPROVAL_RULES:edit');
  }

  protected async save(r: Row, result: ReasonResult): Promise<void> {
    this.saving.set(null);
    try {
      const saved = await this.api.saveApprovalRule(r.service, r.kind, {
        enabled: r.enabled, amount: this.num(r.amount), percent: this.num(r.percent), count: this.num(r.count),
      }, result.reason);
      this.rows.update((all) => all.map((x) => (x.kind === r.kind ? { ...saved, service: r.service } : x)));
      this.toastr.success('APPROVAL_RULES_PAGE.SAVED');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  private num(v: number | null): number | null {
    return v === null || (v as unknown) === '' || Number.isNaN(Number(v)) ? null : Number(v);
  }
}
