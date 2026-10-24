import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CrudPage, StoreOption, errorMessage } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { Session } from '../../core/session';
import { DateInput } from '../../shared/date-input';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal, Paginator } from '../../shared/ui';
import { Binding, BindingSave, ISSUE_STATUS, SerialsApi, Subscription } from './serials-api';

interface PickIssue { publicId: string; label: string; receivedDate: string | null; quantity: number; }
interface Form {
  publicId: string | null; hasItem: boolean; subscriptionPublicId: string | null; accessionNo: string; volumeTitle: string; bindingDate: string | null; storeId: number | null;
  note: string; issues: PickIssue[]; chosen: Record<string, boolean>;
}

/**
 * Đóng tập báo/tạp chí (monolith: /admin/serial-binding, quyền SERIAL_BINDING): gom các kỳ ĐÃ NHẬN của một đăng ký (đã liên kết biểu ghi) thành một tập
 * và tạo ĐKCB thật ở phân hệ Kho gắn biểu ghi đầu báo. Một kỳ chỉ thuộc một tập; sau khi có ĐKCB thì số ĐKCB và kho bị khoá.
 */
@Component({
  selector: 'app-serial-bindings',
  imports: [DateInput, FormsModule, LDatePipe, Loading, Paginator, Modal, ConfirmDelete, TranslatePipe],
  template: `
    <div class="mb-5 flex justify-between items-center">
      <h4 class="page-title">{{ 'SERIALS.BINDINGS_TITLE' | translate }}</h4>
      @if (session.can('SERIAL_BINDING:add')) {
        <button class="btn-primary" (click)="edit(null)"><span class="material-icons text-[18px]">add</span> {{ 'SERIALS.BINDING_ADD' | translate }}</button>
      }
    </div>
    <div class="panel flex flex-wrap gap-3 items-end">
      <div class="w-48"><label for="sb-ac" class="field-label">{{ 'SERIALS.ACCESSION_NO' | translate }}</label><input id="sb-ac" class="input" [(ngModel)]="accessionNo" (keydown.enter)="find()" /></div>
      <div class="flex-1 min-w-[200px]"><label for="sb-vt" class="field-label">{{ 'SERIALS.VOLUME_TITLE' | translate }}</label><input id="sb-vt" class="input" [(ngModel)]="volumeTitle" (keydown.enter)="find()" /></div>
      <div class="w-56"><label for="sb-st" class="field-label">{{ 'SERIALS.STORE' | translate }}</label>
        <select id="sb-st" class="input" [(ngModel)]="storeId" (change)="find()"><option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
          @for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }</select></div>
      <button class="btn-primary" (click)="find()"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[860px]">
          <thead><tr>
            <th class="th w-36">{{ 'SERIALS.ACCESSION_NO' | translate }}</th><th class="th">{{ 'SERIALS.VOLUME_TITLE' | translate }}</th><th class="th w-56">{{ 'SERIALS.SUBSCRIPTION' | translate }}</th>
            <th class="th w-28">{{ 'SERIALS.BINDING_DATE' | translate }}</th><th class="th w-36">{{ 'SERIALS.STORE' | translate }}</th><th class="th w-20 !text-center">{{ 'SERIALS.ISSUES_COUNT' | translate }}</th>
            <th class="th w-28 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (b of page()?.items ?? []; track b.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td font-mono text-[13px]">{{ b.accessionNo }}</td><td class="td">{{ b.volumeTitle }}</td><td class="td">{{ b.subscriptionTitle }}</td>
                <td class="td">{{ b.bindingDate | ldate: 'dd/MM/yyyy' }}</td><td class="td">{{ b.storeName ?? '' }}</td><td class="td text-center">{{ b.issueCount }}</td>
                <td class="td">
                  <div class="flex items-center justify-center gap-1.5">
                    @if (session.can('SERIAL_BINDING:edit')) {
                      <button class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" [title]="'COMMON.EDIT' | translate" (click)="edit(b)"><span class="material-icons text-[18px]">edit</span></button>
                    }
                    @if (session.can('SERIAL_BINDING:delete')) {
                      <button class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" [title]="'COMMON.DELETE' | translate" (click)="deleting.set(b)"><span class="material-icons text-[18px]">delete</span></button>
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

    @if (form(); as f) {
      <app-modal [title]="f.publicId ? 'SERIALS.BINDING_EDIT' : 'SERIALS.BINDING_ADD'" widthClass="max-w-3xl" (closed)="form.set(null)">
        <div class="grid grid-cols-2 gap-4">
          <div class="col-span-2"><label for="bf-su" class="form-label">{{ 'SERIALS.SUBSCRIPTION' | translate }} *</label>
            <select id="bf-su" class="input" [disabled]="!!f.publicId" [ngModel]="f.subscriptionPublicId" (ngModelChange)="chooseSubscription(f, $event)">
              <option [ngValue]="null">—</option>@for (s of subscriptions(); track s.publicId) { <option [ngValue]="s.publicId">{{ s.title }}@if (s.issn) { ({{ s.issn }}) }</option> }</select>
            <p class="text-[11px] text-gray-400 mt-1">{{ 'SERIALS.BINDING_SUBSCRIPTION_HINT' | translate }}</p></div>
          <div><label for="bf-ac" class="form-label">{{ 'SERIALS.ACCESSION_NO' | translate }} *</label>
            <input id="bf-ac" class="input" maxlength="50" [disabled]="f.hasItem" [(ngModel)]="f.accessionNo" />
            @if (f.hasItem) { <p class="text-[11px] text-gray-400 mt-1">{{ 'SERIALS.ACCESSION_LOCKED' | translate }}</p> }</div>
          <div><label for="bf-dt" class="form-label">{{ 'SERIALS.BINDING_DATE' | translate }}</label><app-date-input fieldId="bf-dt" [(ngModel)]="f.bindingDate"></app-date-input></div>
          <div class="col-span-2"><label for="bf-vt" class="form-label">{{ 'SERIALS.VOLUME_TITLE' | translate }}</label><input id="bf-vt" class="input" maxlength="500" [(ngModel)]="f.volumeTitle" /></div>
          <div><label for="bf-sto" class="form-label">{{ 'SERIALS.STORE' | translate }}</label>
            <select id="bf-sto" class="input" [disabled]="f.hasItem" [(ngModel)]="f.storeId"><option [ngValue]="null">—</option>
              @for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }</select></div>
          <div><label for="bf-no" class="form-label">{{ 'COMMON.NOTE' | translate }}</label><input id="bf-no" class="input" maxlength="500" [(ngModel)]="f.note" /></div>
          <div class="col-span-2">
            <div class="form-label">{{ 'SERIALS.CHOOSE_ISSUES' | translate }}</div>
            <ul class="max-h-56 overflow-y-auto rounded-lg border border-gray-200 divide-y divide-gray-100 text-sm">
              @for (i of f.issues; track i.publicId) {
                <li><label class="flex items-center gap-3 px-3 py-2 cursor-pointer hover:bg-gray-50"><input type="checkbox" [(ngModel)]="f.chosen[i.publicId]" />
                  <span class="flex-1">{{ i.label }}</span><span class="text-[12px] text-gray-400">{{ i.receivedDate ? (i.receivedDate | ldate: 'dd/MM/yyyy') : '' }}</span></label></li>
              } @empty { <li class="px-3 py-4 text-center text-gray-500">{{ f.subscriptionPublicId ? ('SERIALS.NO_UNBOUND_ISSUES' | translate) : ('SERIALS.CHOOSE_SUBSCRIPTION_FIRST' | translate) }}</li> }
            </ul>
          </div>
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="form.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="busy()" (click)="save(f)">{{ 'COMMON.SAVE' | translate }}</button>
        </ng-container>
      </app-modal>
    }
    @if (deleting(); as b) {
      <app-confirm-delete [busy]="busy()" message="SERIALS.BINDING_DELETE_CONFIRM" [messageParams]="{ no: b.accessionNo }" warning="SERIALS.BINDING_DELETE_WARNING"
        (confirmed)="remove(b)" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class SerialBindings implements OnInit {
  private readonly api = inject(Api);
  private readonly serials = inject(SerialsApi);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);

  protected accessionNo = '';
  protected volumeTitle = '';
  protected storeId: number | null = null;
  protected pageIndex = 1;
  protected pageSize = 10;
  protected readonly page = signal<CrudPage<Binding> | null>(null);
  protected readonly stores = signal<StoreOption[]>([]);
  protected readonly subscriptions = signal<Subscription[]>([]);
  protected readonly form = signal<Form | null>(null);
  protected readonly deleting = signal<Binding | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    void this.load();
    this.api.storeOptions().then((r) => this.stores.set(r), () => undefined);
    this.serials.searchSubscriptions({ pageIndex: 1, pageSize: 200 }).then((p) => this.subscriptions.set(p.items), () => undefined);
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

  protected async edit(b: Binding | null): Promise<void> {
    if (!b) {
      this.form.set({ publicId: null, hasItem: false, subscriptionPublicId: null, accessionNo: '', volumeTitle: '', bindingDate: null, storeId: null, note: '', issues: [], chosen: {} });
      return;
    }
    try {
      const full = await this.serials.getBinding(b.publicId);
      const own = full.issues.map((i): PickIssue => ({ publicId: i.issuePublicId, label: i.label, receivedDate: i.receivedDate, quantity: i.quantity }));
      const free = await this.unboundIssues(full.subscriptionPublicId);
      this.form.set({
        publicId: full.publicId, hasItem: !!full.itemPublicId, subscriptionPublicId: full.subscriptionPublicId, accessionNo: full.accessionNo, volumeTitle: full.volumeTitle,
        bindingDate: full.bindingDate, storeId: full.storeId, note: full.note ?? '', issues: [...own, ...free.filter((i) => own.every((o) => o.publicId !== i.publicId))],
        chosen: Object.fromEntries(own.map((i) => [i.publicId, true])),
      });
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected async chooseSubscription(f: Form, publicId: string | null): Promise<void> {
    f.subscriptionPublicId = publicId;
    f.chosen = {};
    f.issues = publicId ? await this.unboundIssues(publicId).catch(() => []) : [];
  }

  private async unboundIssues(subscriptionPublicId: string): Promise<PickIssue[]> {
    const page = await this.serials.searchIssues({ subscriptionPublicId, status: ISSUE_STATUS.Received, bound: false, pageIndex: 1, pageSize: 500 });
    return page.items.map((i) => ({ publicId: i.publicId, label: i.label, receivedDate: i.receivedDate, quantity: i.quantity }));
  }

  protected async save(f: Form): Promise<void> {
    const issueIds = f.issues.filter((i) => f.chosen[i.publicId]).map((i) => i.publicId);
    if (!f.subscriptionPublicId || !issueIds.length) {
      this.toastr.warning('SERIALS.BINDING_NEEDS_ISSUES');
      return;
    }
    const store = this.stores().find((s) => s.id === f.storeId);
    const body: BindingSave = {
      subscriptionPublicId: f.subscriptionPublicId, issueIds, accessionNo: f.accessionNo.trim() || null, volumeTitle: f.volumeTitle.trim() || null,
      bindingDate: f.bindingDate || null, storeId: f.storeId, storeName: store?.name ?? null, note: f.note.trim() || null,
    };
    this.busy.set(true);
    try {
      if (f.publicId) await this.serials.updateBinding(f.publicId, body);
      else await this.serials.addBinding(body);
      this.toastr.success('COMMON.SAVE_SUCCESS');
      this.form.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async remove(b: Binding): Promise<void> {
    this.busy.set(true);
    try {
      await this.serials.deleteBinding(b.publicId);
      this.toastr.success('COMMON.DELETE_SUCCESS');
      this.deleting.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
      this.deleting.set(null);
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.page.set(await this.serials.searchBindings({
        accessionNo: this.accessionNo.trim() || null, volumeTitle: this.volumeTitle.trim() || null, storeId: this.storeId, pageIndex: this.pageIndex, pageSize: this.pageSize,
      }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
