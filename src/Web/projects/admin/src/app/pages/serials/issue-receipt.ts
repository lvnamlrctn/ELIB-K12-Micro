import { Component, OnInit, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { CrudPage, errorMessage } from '../../core/api';
import { LDatePipe, LNumberPipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { DateInput } from '../../shared/date-input';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal, Paginator } from '../../shared/ui';
import { ISSUE_STATUS, IssueSave, IssueStatistics, SerialIssue, SerialsApi, Subscription } from './serials-api';

const STATUSES = [
  { code: ISSUE_STATUS.Planned, name: 'SERIALS.ISSUE_PLANNED', badge: 'badge-info' },
  { code: ISSUE_STATUS.Received, name: 'SERIALS.ISSUE_RECEIVED', badge: 'badge-on' },
  { code: ISSUE_STATUS.Claimed, name: 'SERIALS.ISSUE_CLAIMED', badge: 'badge-warn' },
  { code: ISSUE_STATUS.Missing, name: 'SERIALS.ISSUE_MISSING', badge: 'badge-off' },
];

interface IssueForm {
  publicId: string | null; label: string; x: number | null; y: number | null; z: number | null; isSpecial: boolean; quantity: number; plannedDate: string | null;
  receivedDate: string | null; status: number; note: string;
}

/**
 * Nhận kỳ của một đăng ký (monolith: /admin/serial-issues?subscriptionId=…, quyền SERIAL_ISSUES): sinh kỳ dự kiến theo mẫu đánh số + tần suất, nhận nhanh,
 * thêm/sửa tay, khiếu nại kỳ chưa đến, ghép nhiều kỳ thành một số (xử lý trong một giao dịch phía server) và xoá kỳ chưa đóng tập.
 */
@Component({
  selector: 'app-serial-issues',
  imports: [DateInput, FormsModule, RouterLink, LDatePipe, LNumberPipe, Loading, Paginator, Modal, ConfirmDelete, TranslatePipe],
  template: `
    <div class="mb-5 flex flex-wrap justify-between items-center gap-3">
      <div>
        <h4 class="page-title">{{ 'SERIALS.ISSUES_TITLE' | translate }}</h4>
        @if (subscription(); as s) { <p class="text-sm text-gray-500 mt-1">{{ s.title }}@if (s.issn) { · ISSN {{ s.issn }} }</p> }
      </div>
      <a routerLink="/serial-subscriptions" class="btn-secondary"><span class="material-icons text-[18px]">arrow_back</span> {{ 'SERIALS.BACK_TO_SUBSCRIPTIONS' | translate }}</a>
    </div>

    @if (!subscription()) {
      <div class="panel">
        <div class="flex gap-3 items-end">
          <div class="flex-1"><label for="si-pick" class="field-label">{{ 'SERIALS.CHOOSE_SUBSCRIPTION' | translate }}</label>
            <input id="si-pick" class="input" [(ngModel)]="pickKeyword" (keydown.enter)="searchSubscriptions()" [placeholder]="'SERIALS.SEARCH_TITLE_ISSN' | translate" /></div>
          <button class="btn-primary" (click)="searchSubscriptions()"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
        </div>
        <ul class="mt-3 divide-y divide-gray-100 rounded-lg border border-gray-200 text-sm">
          @for (s of candidates(); track s.publicId) {
            <li><a class="block px-3 py-2 hover:bg-gray-50" [routerLink]="['/serial-issues']" [queryParams]="{ subscriptionId: s.publicId }">{{ s.title }}<span class="text-[11px] text-gray-400"> · {{ s.frequencyName ?? '' }} · {{ s.receivedCount }}/{{ s.issueCount }}</span></a></li>
          } @empty { <li class="px-3 py-4 text-center text-gray-500">{{ 'COMMON.NO_DATA' | translate }}</li> }
        </ul>
      </div>
    } @else {
      @if (stats(); as st) {
        <div class="grid grid-cols-2 md:grid-cols-5 gap-3 mb-4">
          @for (c of cards(st); track c.label) {
            <div class="bg-white rounded-xl border border-gray-100 shadow-sm p-4"><div class="text-[12px] text-gray-500">{{ c.label | translate }}</div>
              <div class="text-2xl font-bold" [class]="c.tone">{{ c.value | lnumber }}</div></div>
          }
        </div>
      }

      <div class="panel flex flex-wrap gap-3 items-end">
        <div class="w-44"><label for="si-st" class="field-label">{{ 'COMMON.STATUS' | translate }}</label>
          <select id="si-st" class="input" [(ngModel)]="status" (change)="find()"><option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
            @for (s of statuses; track s.code) { <option [ngValue]="s.code">{{ s.name | translate }}</option> }</select></div>
        <div class="flex-1"></div>
        @if (session.can('SERIAL_ISSUES:add')) {
          <div class="w-28"><label for="si-cnt" class="field-label">{{ 'SERIALS.PREDICT_COUNT' | translate }}</label><input id="si-cnt" type="number" min="1" max="366" class="input" [(ngModel)]="predictCount" /></div>
          <button class="btn-secondary" [disabled]="busy()" (click)="predict()"><span class="material-icons text-[18px]">auto_awesome</span> {{ 'SERIALS.PREDICT' | translate }}</button>
          <button class="btn-primary" (click)="edit(null)"><span class="material-icons text-[18px]">add</span> {{ 'SERIALS.ISSUE_ADD' | translate }}</button>
        }
        @if (session.can('SERIAL_ISSUES:edit')) {
          <button class="btn-secondary" [disabled]="busy() || selected().length < 2" (click)="mergeOpen.set(true)"><span class="material-icons text-[18px]">merge</span> {{ 'SERIALS.MERGE' | translate }} ({{ selected().length }})</button>
        }
      </div>

      <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
        @if (loading()) { <app-loading /> }
        <div class="overflow-x-auto w-full">
          <table class="w-full text-left border-collapse min-w-[900px]">
            <thead><tr>
              <th class="th w-10"></th><th class="th">{{ 'SERIALS.ISSUE_LABEL' | translate }}</th><th class="th w-32">{{ 'SERIALS.PLANNED_DATE' | translate }}</th>
              <th class="th w-32">{{ 'SERIALS.RECEIVED_DATE' | translate }}</th><th class="th w-20 !text-right">{{ 'SERIALS.QUANTITY' | translate }}</th>
              <th class="th w-32 !text-center">{{ 'COMMON.STATUS' | translate }}</th><th class="th w-44 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
            </tr></thead>
            <tbody>
              @for (i of page()?.items ?? []; track i.publicId) {
                <tr class="hover:bg-gray-50/50">
                  <td class="td"><input type="checkbox" [disabled]="i.bound" [checked]="isSelected(i)" [attr.aria-label]="i.label" (change)="toggle(i)" /></td>
                  <td class="td">{{ i.label }}@if (i.isSpecial) { <span class="badge-info ml-2">{{ 'SERIALS.SPECIAL' | translate }}</span> }@if (i.isMerged) { <span class="badge-off ml-2">{{ 'SERIALS.MERGED' | translate }}</span> }
                    @if (i.bound) { <span class="badge-on ml-2">{{ 'SERIALS.BOUND' | translate }}</span> }
                    @if (i.note) { <div class="text-[11px] text-gray-400">{{ i.note }}</div> }</td>
                  <td class="td">{{ i.plannedDate ? (i.plannedDate | ldate: 'dd/MM/yyyy') : '' }}</td>
                  <td class="td">{{ i.receivedDate ? (i.receivedDate | ldate: 'dd/MM/yyyy') : '' }}</td>
                  <td class="td text-right">{{ i.quantity | lnumber }}</td>
                  <td class="td text-center"><span [class]="statusOf(i.status).badge">{{ statusOf(i.status).name | translate }}</span>
                    @if (i.claimCount > 0) { <div class="text-[11px] text-gray-400">{{ 'SERIALS.CLAIM_N' | translate: { count: i.claimCount } }}</div> }</td>
                  <td class="td">
                    <div class="flex items-center justify-center gap-1.5">
                      @if (session.can('SERIAL_ISSUES:edit') && i.status !== 1) {
                        <button class="icon-btn bg-green-50 text-green-700 hover:bg-green-100" [title]="'SERIALS.RECEIVE' | translate" [disabled]="busy()" (click)="receive(i)"><span class="material-icons text-[18px]">move_to_inbox</span></button>
                        <button class="icon-btn bg-amber-50 text-amber-700 hover:bg-amber-100" [title]="'SERIALS.CLAIM' | translate" [disabled]="busy()" (click)="claim(i)"><span class="material-icons text-[18px]">campaign</span></button>
                      }
                      @if (session.can('SERIAL_ISSUES:edit')) {
                        <button class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" [title]="'COMMON.EDIT' | translate" (click)="edit(i)"><span class="material-icons text-[18px]">edit</span></button>
                      }
                      @if (session.can('SERIAL_ISSUES:delete') && !i.bound) {
                        <button class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" [title]="'COMMON.DELETE' | translate" (click)="deleting.set(i)"><span class="material-icons text-[18px]">delete</span></button>
                      }
                    </div>
                  </td>
                </tr>
              } @empty {
                <tr><td colspan="7" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr>
              }
            </tbody>
          </table>
        </div>
        @if (page(); as p) { <app-paginator [total]="p.totalCount" [pageIndex]="pageIndex" [pageSize]="pageSize" (pageChange)="onPage($event)" /> }
      </div>
    }

    @if (form(); as f) {
      <app-modal [title]="f.publicId ? 'SERIALS.ISSUE_EDIT' : 'SERIALS.ISSUE_ADD'" widthClass="max-w-2xl" (closed)="form.set(null)">
        <div class="grid grid-cols-2 gap-4">
          <div class="col-span-2"><label for="if-lb" class="form-label">{{ 'SERIALS.ISSUE_LABEL' | translate }}</label><input id="if-lb" class="input" maxlength="250" [(ngModel)]="f.label" />
            <p class="text-[11px] text-gray-400 mt-1">{{ 'SERIALS.ISSUE_LABEL_HINT' | translate }}</p></div>
          <div class="col-span-2 grid grid-cols-3 gap-4">
            <div><label for="if-x" class="form-label">X</label><input id="if-x" type="number" min="0" class="input" [(ngModel)]="f.x" /></div>
            <div><label for="if-y" class="form-label">Y</label><input id="if-y" type="number" min="0" class="input" [(ngModel)]="f.y" /></div>
            <div><label for="if-z" class="form-label">Z</label><input id="if-z" type="number" min="0" class="input" [(ngModel)]="f.z" /></div>
          </div>
          <div><label for="if-pd" class="form-label">{{ 'SERIALS.PLANNED_DATE' | translate }}</label><app-date-input fieldId="if-pd" [(ngModel)]="f.plannedDate"></app-date-input></div>
          <div><label for="if-rd" class="form-label">{{ 'SERIALS.RECEIVED_DATE' | translate }}</label><app-date-input fieldId="if-rd" [(ngModel)]="f.receivedDate"></app-date-input></div>
          <div><label for="if-q" class="form-label">{{ 'SERIALS.QUANTITY' | translate }}</label><input id="if-q" type="number" min="1" class="input" [(ngModel)]="f.quantity" /></div>
          <div><label for="if-s" class="form-label">{{ 'COMMON.STATUS' | translate }}</label>
            <select id="if-s" class="input" [(ngModel)]="f.status">@for (s of statuses; track s.code) { <option [ngValue]="s.code">{{ s.name | translate }}</option> }</select></div>
          <div class="col-span-2"><label class="inline-flex items-center gap-2 text-sm"><input type="checkbox" [(ngModel)]="f.isSpecial" /> {{ 'SERIALS.SPECIAL_ISSUE' | translate }}</label></div>
          <div class="col-span-2"><label for="if-n" class="form-label">{{ 'COMMON.NOTE' | translate }}</label><input id="if-n" class="input" maxlength="500" [(ngModel)]="f.note" /></div>
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="form.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="busy()" (click)="save(f)">{{ 'COMMON.SAVE' | translate }}</button>
        </ng-container>
      </app-modal>
    }
    @if (mergeOpen()) {
      <app-modal title="SERIALS.MERGE" widthClass="max-w-md" (closed)="mergeOpen.set(false)">
        <p class="text-sm text-gray-600 mb-3">{{ 'SERIALS.MERGE_HINT' | translate: { count: selected().length } }}</p>
        <label for="mg-lb" class="form-label">{{ 'SERIALS.MERGE_LABEL' | translate }}</label>
        <input id="mg-lb" class="input" maxlength="250" [(ngModel)]="mergeLabel" [placeholder]="'SERIALS.MERGE_LABEL_PH' | translate" />
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="mergeOpen.set(false)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="busy()" (click)="merge()">{{ 'SERIALS.MERGE' | translate }}</button>
        </ng-container>
      </app-modal>
    }
    @if (deleting(); as i) {
      <app-confirm-delete [busy]="busy()" message="SERIALS.ISSUE_DELETE_CONFIRM" [messageParams]="{ label: i.label }" (confirmed)="remove(i)" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class SerialIssues implements OnInit {
  private readonly serials = inject(SerialsApi);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);

  /** Mã (publicId) đăng ký — từ query ?subscriptionId=… (router bind vào input). */
  readonly subscriptionId = input<string | undefined>(undefined);

  protected readonly statuses = STATUSES;
  protected status: number | null = null;
  protected pageIndex = 1;
  protected pageSize = 20;
  protected predictCount = 12;
  protected pickKeyword = '';
  protected mergeLabel = '';
  protected readonly subscription = signal<Subscription | null>(null);
  protected readonly candidates = signal<Subscription[]>([]);
  protected readonly stats = signal<IssueStatistics | null>(null);
  protected readonly page = signal<CrudPage<SerialIssue> | null>(null);
  protected readonly picked = signal<Record<string, SerialIssue>>({});
  protected readonly selected = computed(() => Object.values(this.picked()));
  protected readonly form = signal<IssueForm | null>(null);
  protected readonly mergeOpen = signal(false);
  protected readonly deleting = signal<SerialIssue | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);

  constructor() {
    effect(() => {
      const id = this.subscriptionId();
      if (id) void this.open(id);
      else this.subscription.set(null);
    });
  }

  ngOnInit(): void {
    if (!this.subscriptionId()) void this.searchSubscriptions();
  }

  protected statusOf(code: number) {
    return STATUSES.find((s) => s.code === code) ?? STATUSES[0];
  }

  protected cards(st: IssueStatistics): { label: string; value: number; tone: string }[] {
    return [
      { label: 'SERIALS.STAT_TOTAL', value: st.total, tone: 'text-gray-800' },
      { label: 'SERIALS.STAT_RECEIVED', value: st.received, tone: 'text-green-700' },
      { label: 'SERIALS.STAT_CLAIMED', value: st.claimed, tone: 'text-amber-600' },
      { label: 'SERIALS.STAT_MISSING', value: st.missing, tone: 'text-red-600' },
      { label: 'SERIALS.STAT_LATE', value: st.late, tone: 'text-orange-600' },
    ];
  }

  protected async searchSubscriptions(): Promise<void> {
    try {
      const page = await this.serials.searchSubscriptions({ keyword: this.pickKeyword.trim() || undefined, pageIndex: 1, pageSize: 30 });
      this.candidates.set(page.items);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected find(): void {
    this.pageIndex = 1;
    void this.load();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.pageIndex = e.pageIndex;
    this.pageSize = e.pageSize;
    void this.load();
  }

  protected isSelected(i: SerialIssue): boolean {
    return !!this.picked()[i.publicId];
  }

  protected toggle(i: SerialIssue): void {
    this.picked.update((p) => {
      const next = { ...p };
      if (next[i.publicId]) delete next[i.publicId];
      else next[i.publicId] = i;
      return next;
    });
  }

  protected edit(i: SerialIssue | null): void {
    this.form.set({
      publicId: i?.publicId ?? null, label: i?.label ?? '', x: i?.x ?? null, y: i?.y ?? null, z: i?.z ?? null, isSpecial: i?.isSpecial ?? false, quantity: i?.quantity ?? 1,
      plannedDate: i?.plannedDate ?? null, receivedDate: i?.receivedDate ?? null, status: i?.status ?? ISSUE_STATUS.Planned, note: i?.note ?? '',
    });
  }

  protected async save(f: IssueForm): Promise<void> {
    const sub = this.subscription();
    if (!sub) return;
    if (!f.label.trim() && f.x === null) {
      this.toastr.warning('SERIALS.ISSUE_LABEL_REQUIRED');
      return;
    }
    const body: IssueSave = {
      subscriptionPublicId: sub.publicId, publicId: f.publicId, label: f.label.trim() || null, x: f.x, y: f.y, z: f.z, isSpecial: f.isSpecial,
      quantity: Math.max(1, Number(f.quantity) || 1), plannedDate: f.plannedDate || null, receivedDate: f.receivedDate || null, status: f.status, note: f.note.trim() || null,
    };
    await this.run(async () => {
      await this.serials.saveIssue(body);
      this.toastr.success('COMMON.SAVE_SUCCESS');
      this.form.set(null);
      await this.refresh();
    });
  }

  protected async receive(i: SerialIssue): Promise<void> {
    await this.run(async () => {
      await this.serials.receiveIssue(i.publicId);
      this.toastr.success('SERIALS.RECEIVED_OK');
      await this.refresh();
    });
  }

  protected async claim(i: SerialIssue): Promise<void> {
    await this.run(async () => {
      await this.serials.claimIssue(i.publicId);
      this.toastr.success('SERIALS.CLAIMED_OK');
      await this.refresh();
    });
  }

  protected async predict(): Promise<void> {
    const sub = this.subscription();
    if (!sub) return;
    await this.run(async () => {
      const r = await this.serials.predictIssues(sub.publicId, Math.min(366, Math.max(1, Number(this.predictCount) || 1)));
      if (r.skipped > 0) this.toastr.warning('SERIALS.PREDICT_RESULT_SKIPPED', { generated: r.generated, skipped: r.skipped });
      else this.toastr.success('SERIALS.PREDICT_RESULT', { generated: r.generated });
      await this.refresh();
    });
  }

  protected async merge(): Promise<void> {
    const sub = this.subscription();
    if (!sub) return;
    await this.run(async () => {
      await this.serials.mergeIssues(sub.publicId, this.selected().map((i) => i.publicId), this.mergeLabel.trim());
      this.toastr.success('SERIALS.MERGED_OK');
      this.mergeOpen.set(false);
      this.mergeLabel = '';
      this.picked.set({});
      await this.refresh();
    });
  }

  protected async remove(i: SerialIssue): Promise<void> {
    await this.run(async () => {
      await this.serials.deleteIssue(i.publicId);
      this.toastr.success('COMMON.DELETE_SUCCESS');
      this.deleting.set(null);
      await this.refresh();
    }, () => this.deleting.set(null));
  }

  private async open(id: string): Promise<void> {
    try {
      this.subscription.set(await this.serials.getSubscription(id));
      this.picked.set({});
      this.pageIndex = 1;
      await this.refresh();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  private async refresh(): Promise<void> {
    await Promise.all([this.load(), this.loadStats()]);
  }

  private async loadStats(): Promise<void> {
    const sub = this.subscription();
    if (!sub || !this.session.can('SERIAL_ISSUES:view')) return;
    try {
      this.stats.set(await this.serials.issueStatistics(sub.publicId));
    } catch {
      this.stats.set(null);
    }
  }

  private async load(): Promise<void> {
    const sub = this.subscription();
    if (!sub) return;
    this.loading.set(true);
    try {
      this.page.set(await this.serials.searchIssues({ subscriptionPublicId: sub.publicId, status: this.status, pageIndex: this.pageIndex, pageSize: this.pageSize }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  private async run(action: () => Promise<void>, onError?: () => void): Promise<void> {
    this.busy.set(true);
    try {
      await action();
    } catch (e) {
      this.toastr.error(errorMessage(e));
      onError?.();
    } finally {
      this.busy.set(false);
    }
  }
}
