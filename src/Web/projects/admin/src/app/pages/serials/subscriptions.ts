import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, CrudPage, StoreOption, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { DateInput } from '../../shared/date-input';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal, Paginator } from '../../shared/ui';
import { BibOption, SUBSCRIPTION_STATUS, SerialsApi, Subscription, SubscriptionRequest, SupplierOption } from './serials-api';

interface Named { id: number; name: string; }
interface Form extends SubscriptionRequest { publicId: string | null; bib: BibOption | null; approved: boolean; }

const STATUSES = [
  { code: SUBSCRIPTION_STATUS.Draft, name: 'SERIALS.STATUS_DRAFT', badge: 'badge-info' },
  { code: SUBSCRIPTION_STATUS.Active, name: 'SERIALS.STATUS_ACTIVE', badge: 'badge-on' },
  { code: SUBSCRIPTION_STATUS.Closed, name: 'SERIALS.STATUS_CLOSED', badge: 'badge-off' },
];

const emptyForm = (): Form => ({
  publicId: null, bib: null, approved: false, title: '', issn: '', bibPublicId: null, publicationTypeId: null, frequencyId: null, patternId: null, storeId: null,
  storeName: null, supplierPublicId: null, supplierName: null, startsOn: null, endsOn: null, firstIssueDate: null, startX: null, startY: null, startZ: null,
  callNumber: '', publicNote: '', internalNote: '', graceDays: 0, status: SUBSCRIPTION_STATUS.Active,
});

/**
 * Đăng ký đặt báo/tạp chí (monolith: /admin/serial-subscriptions, quyền SUBSCRIPTIONS): danh sách, thêm/sửa/xoá, liên kết biểu ghi thư mục, duyệt
 * (một chiều — đã duyệt thì khoá sửa/xoá) và mở màn nhận kỳ. Duyệt và đóng tập cần đăng ký đã liên kết biểu ghi.
 */
@Component({
  selector: 'app-serial-subscriptions',
  imports: [DateInput, FormsModule, RouterLink, Loading, Paginator, Modal, ConfirmDelete, TranslatePipe],
  template: `
    <div class="mb-5 flex justify-between items-center">
      <h4 class="page-title">{{ 'SERIALS.SUBSCRIPTIONS_TITLE' | translate }}</h4>
      @if (session.can('SUBSCRIPTIONS:add')) {
        <button class="btn-primary" (click)="edit(null)"><span class="material-icons text-[18px]">add</span> {{ 'SERIALS.SUBSCRIPTION_ADD' | translate }}</button>
      }
    </div>
    <div class="panel flex flex-wrap gap-3 items-end">
      <div class="flex-1 min-w-[220px]">
        <label for="ss-kw" class="field-label">{{ 'SERIALS.SEARCH_TITLE_ISSN' | translate }}</label>
        <input id="ss-kw" class="input" [(ngModel)]="keyword" (keydown.enter)="find()" />
      </div>
      <div class="w-44">
        <label for="ss-ty" class="field-label">{{ 'SERIALS.TYPE' | translate }}</label>
        <select id="ss-ty" class="input" [(ngModel)]="typeId" (change)="find()"><option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
          @for (t of types(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }</select>
      </div>
      <div class="w-44">
        <label for="ss-fr" class="field-label">{{ 'SERIALS.FREQUENCY' | translate }}</label>
        <select id="ss-fr" class="input" [(ngModel)]="frequencyId" (change)="find()"><option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
          @for (f of frequencies(); track f.id) { <option [ngValue]="f.id">{{ f.name }}</option> }</select>
      </div>
      <div class="w-40">
        <label for="ss-ap" class="field-label">{{ 'SERIALS.APPROVAL' | translate }}</label>
        <select id="ss-ap" class="input" [(ngModel)]="approved" (change)="find()"><option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
          <option [ngValue]="true">{{ 'SERIALS.APPROVED' | translate }}</option><option [ngValue]="false">{{ 'SERIALS.NOT_APPROVED' | translate }}</option></select>
      </div>
      <button class="btn-primary" (click)="find()"><span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}</button>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse min-w-[1000px]">
          <thead><tr>
            <th class="th">{{ 'SERIALS.TITLE_COL' | translate }}</th><th class="th w-28">ISSN</th><th class="th w-32">{{ 'SERIALS.TYPE' | translate }}</th>
            <th class="th w-32">{{ 'SERIALS.FREQUENCY' | translate }}</th><th class="th w-36 !text-center">{{ 'SERIALS.ISSUES_RECEIVED' | translate }}</th>
            <th class="th w-28 !text-center">{{ 'COMMON.STATUS' | translate }}</th><th class="th w-44 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
          </tr></thead>
          <tbody>
            @for (s of page()?.items ?? []; track s.publicId) {
              <tr class="hover:bg-gray-50/50">
                <td class="td">{{ s.title }}
                  <div class="text-[11px]" [class]="s.bibPublicId ? 'text-gray-400' : 'text-amber-600'">
                    @if (s.bibPublicId) { MFN {{ s.bibMfn }} } @else { {{ 'SERIALS.NO_BIB' | translate }} }
                    @if (s.supplierName) { · {{ s.supplierName }} }
                  </div></td>
                <td class="td font-mono text-[13px]">{{ s.issn ?? '' }}</td>
                <td class="td">{{ s.publicationTypeName ?? '' }}</td><td class="td">{{ s.frequencyName ?? '' }}</td>
                <td class="td text-center">{{ s.receivedCount }} / {{ s.issueCount }}@if (s.missingCount > 0) { <div class="text-[11px] text-red-600">{{ 'SERIALS.MISSING_N' | translate: { count: s.missingCount } }}</div> }</td>
                <td class="td text-center">
                  <span [class]="statusOf(s.status).badge">{{ statusOf(s.status).name | translate }}</span>
                  @if (s.approved) { <div class="mt-1"><span class="badge-on">{{ 'SERIALS.APPROVED' | translate }}</span></div> }
                </td>
                <td class="td">
                  <div class="flex items-center justify-center gap-1.5 flex-wrap">
                    @if (session.can('SERIAL_ISSUES:view')) {
                      <a class="icon-btn bg-emerald-50 text-emerald-700 hover:bg-emerald-100" [routerLink]="['/serial-issues']" [queryParams]="{ subscriptionId: s.publicId }" [title]="'SERIALS.OPEN_ISSUES' | translate">
                        <span class="material-icons text-[18px]">move_to_inbox</span></a>
                    }
                    @if (session.can('SUBSCRIPTIONS:edit') && !s.approved) {
                      <button class="icon-btn bg-indigo-50 text-indigo-600 hover:bg-indigo-100" [title]="'SERIALS.LINK_BIB' | translate" (click)="openLink(s)"><span class="material-icons text-[18px]">link</span></button>
                      <button class="icon-btn bg-green-50 text-green-700 hover:bg-green-100" [title]="'SERIALS.APPROVE' | translate" (click)="approving.set(s)"><span class="material-icons text-[18px]">verified</span></button>
                    }
                    @if (session.can('SUBSCRIPTIONS:edit') && !s.approved) {
                      <button class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" [title]="'COMMON.EDIT' | translate" (click)="edit(s)"><span class="material-icons text-[18px]">edit</span></button>
                    }
                    @if (session.can('SUBSCRIPTIONS:delete') && !s.approved) {
                      <button class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" [title]="'COMMON.DELETE' | translate" (click)="deleting.set(s)"><span class="material-icons text-[18px]">delete</span></button>
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
      <app-modal [title]="f.publicId ? 'SERIALS.SUBSCRIPTION_EDIT' : 'SERIALS.SUBSCRIPTION_ADD'" widthClass="max-w-3xl" (closed)="form.set(null)">
        <h5 class="font-semibold text-gray-700 mb-3">{{ 'SERIALS.SECTION_PUBLICATION' | translate }}</h5>
        <div class="grid grid-cols-2 gap-4 mb-6">
          <div class="col-span-2">
            <label class="form-label">{{ 'SERIALS.BIB' | translate }}</label>
            @if (f.bib; as b) {
              <div class="flex items-center gap-2 rounded-lg border border-gray-200 bg-gray-50 px-3 py-2 text-sm">
                <span class="flex-1">{{ b.title }}<span class="text-[11px] text-gray-400"> · MFN {{ b.mfn }}@if (b.author) { · {{ b.author }} }</span></span>
                <button type="button" class="icon-btn" [title]="'SERIALS.UNLINK_BIB' | translate" (click)="pickBib(null)"><span class="material-icons text-[18px]">close</span></button>
              </div>
            } @else {
              <div class="flex gap-2">
                <input class="input" [(ngModel)]="bibKeyword" [placeholder]="'SERIALS.BIB_SEARCH_PH' | translate" [attr.aria-label]="'SERIALS.BIB' | translate" (keydown.enter)="lookup()" />
                <button type="button" class="btn-secondary" [disabled]="busy()" (click)="lookup()"><span class="material-icons text-[18px]">search</span></button>
              </div>
              @if (bibResults().length) { <ul class="mt-2 max-h-40 overflow-y-auto rounded-lg border border-gray-200 divide-y divide-gray-100 text-sm">
                @for (b of bibResults(); track b.bibPublicId) {
                  <li><button type="button" class="w-full text-left px-3 py-2 hover:bg-gray-50" (click)="pickBib(b)">{{ b.title }}<span class="text-[11px] text-gray-400"> · MFN {{ b.mfn }}@if (b.issn) { · ISSN {{ b.issn }} }</span></button></li>
                } </ul> }
              <p class="text-[11px] text-gray-400 mt-1">{{ 'SERIALS.BIB_HINT' | translate }}</p>
            }
          </div>
          <div class="col-span-2"><label for="sf-ti" class="form-label">{{ 'SERIALS.TITLE_COL' | translate }}</label><input id="sf-ti" class="input" maxlength="1000" [(ngModel)]="f.title" /></div>
          <div><label for="sf-is" class="form-label">ISSN</label><input id="sf-is" class="input" maxlength="20" [(ngModel)]="f.issn" /></div>
          <div><label for="sf-ty" class="form-label">{{ 'SERIALS.TYPE' | translate }}</label>
            <select id="sf-ty" class="input" [(ngModel)]="f.publicationTypeId"><option [ngValue]="null">—</option>@for (t of types(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }</select></div>
        </div>

        <h5 class="font-semibold text-gray-700 mb-3">{{ 'SERIALS.SECTION_SCHEDULE' | translate }}</h5>
        <div class="grid grid-cols-2 gap-4 mb-6">
          <div><label for="sf-fr" class="form-label">{{ 'SERIALS.FREQUENCY' | translate }}</label>
            <select id="sf-fr" class="input" [(ngModel)]="f.frequencyId"><option [ngValue]="null">—</option>@for (t of frequencies(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }</select></div>
          <div><label for="sf-pa" class="form-label">{{ 'SERIALS.PATTERN' | translate }}</label>
            <select id="sf-pa" class="input" [(ngModel)]="f.patternId"><option [ngValue]="null">—</option>@for (t of patterns(); track t.id) { <option [ngValue]="t.id">{{ t.name }}</option> }</select></div>
          <div><label for="sf-st" class="form-label">{{ 'SERIALS.STARTS_ON' | translate }}</label><app-date-input fieldId="sf-st" [(ngModel)]="f.startsOn"></app-date-input></div>
          <div><label for="sf-en" class="form-label">{{ 'SERIALS.ENDS_ON' | translate }}</label><app-date-input fieldId="sf-en" [(ngModel)]="f.endsOn"></app-date-input></div>
          <div class="col-span-2"><label for="sf-fi" class="form-label">{{ 'SERIALS.FIRST_ISSUE_DATE' | translate }}</label><app-date-input fieldId="sf-fi" [(ngModel)]="f.firstIssueDate"></app-date-input></div>
          <div class="col-span-2 grid grid-cols-3 gap-4">
            <div><label for="sf-x" class="form-label">{{ 'SERIALS.START_X' | translate }}</label><input id="sf-x" type="number" min="0" class="input" [(ngModel)]="f.startX" /></div>
            <div><label for="sf-y" class="form-label">{{ 'SERIALS.START_Y' | translate }}</label><input id="sf-y" type="number" min="0" class="input" [(ngModel)]="f.startY" /></div>
            <div><label for="sf-z" class="form-label">{{ 'SERIALS.START_Z' | translate }}</label><input id="sf-z" type="number" min="0" class="input" [(ngModel)]="f.startZ" /></div>
          </div>
        </div>

        <h5 class="font-semibold text-gray-700 mb-3">{{ 'SERIALS.SECTION_ORDER' | translate }}</h5>
        <div class="grid grid-cols-2 gap-4">
          <div><label for="sf-sto" class="form-label">{{ 'SERIALS.STORE' | translate }}</label>
            <select id="sf-sto" class="input" [(ngModel)]="f.storeId" (ngModelChange)="onStore(f)"><option [ngValue]="null">—</option>
              @for (s of stores(); track s.id) { <option [ngValue]="s.id">{{ s.code }} — {{ s.name }}</option> }</select></div>
          @if (suppliers().length) {
            <div><label for="sf-su" class="form-label">{{ 'SERIALS.SUPPLIER' | translate }}</label>
              <select id="sf-su" class="input" [(ngModel)]="f.supplierPublicId" (ngModelChange)="onSupplier(f)"><option [ngValue]="null">—</option>
                @for (s of suppliers(); track s.publicId) { <option [ngValue]="s.publicId">{{ s.name }}</option> }</select></div>
          }
          <div><label for="sf-gr" class="form-label">{{ 'SERIALS.GRACE_DAYS' | translate }}</label><input id="sf-gr" type="number" min="0" class="input" [(ngModel)]="f.graceDays" />
            <p class="text-[11px] text-gray-400 mt-1">{{ 'SERIALS.GRACE_HINT' | translate }}</p></div>
          <div><label for="sf-cn" class="form-label">{{ 'SERIALS.CALL_NUMBER' | translate }}</label><input id="sf-cn" class="input" maxlength="100" [(ngModel)]="f.callNumber" /></div>
          <div><label for="sf-ss" class="form-label">{{ 'COMMON.STATUS' | translate }}</label>
            <select id="sf-ss" class="input" [(ngModel)]="f.status">@for (s of statuses; track s.code) { <option [ngValue]="s.code">{{ s.name | translate }}</option> }</select></div>
          <div class="col-span-2"><label for="sf-pn" class="form-label">{{ 'SERIALS.PUBLIC_NOTE' | translate }}</label><input id="sf-pn" class="input" maxlength="500" [(ngModel)]="f.publicNote" /></div>
          <div class="col-span-2"><label for="sf-in" class="form-label">{{ 'SERIALS.INTERNAL_NOTE' | translate }}</label><input id="sf-in" class="input" maxlength="500" [(ngModel)]="f.internalNote" /></div>
        </div>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="form.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="busy()" (click)="save(f)">{{ 'COMMON.SAVE' | translate }}</button>
        </ng-container>
      </app-modal>
    }

    @if (linking(); as s) {
      <app-modal title="SERIALS.LINK_BIB" widthClass="max-w-xl" (closed)="linking.set(null)">
        <p class="text-sm text-gray-600 mb-3">{{ s.title }}</p>
        <div class="flex gap-2">
          <input class="input" [(ngModel)]="bibKeyword" [placeholder]="'SERIALS.BIB_SEARCH_PH' | translate" [attr.aria-label]="'SERIALS.BIB' | translate" (keydown.enter)="lookup()" />
          <button type="button" class="btn-secondary" [disabled]="busy()" (click)="lookup()"><span class="material-icons text-[18px]">search</span></button>
        </div>
        <ul class="mt-3 max-h-64 overflow-y-auto rounded-lg border border-gray-200 divide-y divide-gray-100 text-sm">
          @for (b of bibResults(); track b.bibPublicId) {
            <li><button type="button" class="w-full text-left px-3 py-2 hover:bg-gray-50" [disabled]="busy()" (click)="link(s, b)">{{ b.title }}<span class="text-[11px] text-gray-400"> · MFN {{ b.mfn }}@if (b.author) { · {{ b.author }} }</span></button></li>
          } @empty { <li class="px-3 py-4 text-center text-gray-500">{{ 'COMMON.NO_DATA' | translate }}</li> }
        </ul>
        <ng-container footer><button type="button" class="btn-secondary" (click)="linking.set(null)">{{ 'COMMON.CLOSE' | translate }}</button></ng-container>
      </app-modal>
    }
    @if (approving(); as s) {
      <app-confirm-delete title="SERIALS.APPROVE" message="SERIALS.APPROVE_CONFIRM" [messageParams]="{ title: s.title }" warning="SERIALS.APPROVE_WARNING" confirmText="SERIALS.APPROVE"
        [busy]="busy()" (confirmed)="approve(s)" (cancelled)="approving.set(null)" />
    }
    @if (deleting(); as s) {
      <app-confirm-delete [busy]="busy()" message="SERIALS.SUBSCRIPTION_DELETE_CONFIRM" [messageParams]="{ title: s.title }" (confirmed)="remove(s)" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class SerialSubscriptions implements OnInit {
  private readonly api = inject(Api);
  private readonly serials = inject(SerialsApi);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);

  protected readonly statuses = STATUSES;
  protected keyword = '';
  protected typeId: number | null = null;
  protected frequencyId: number | null = null;
  protected approved: boolean | null = null;
  protected pageIndex = 1;
  protected pageSize = 10;
  protected bibKeyword = '';
  protected readonly page = signal<CrudPage<Subscription> | null>(null);
  protected readonly types = signal<Named[]>([]);
  protected readonly frequencies = signal<Named[]>([]);
  protected readonly patterns = signal<Named[]>([]);
  protected readonly stores = signal<StoreOption[]>([]);
  protected readonly suppliers = signal<SupplierOption[]>([]);
  protected readonly bibResults = signal<BibOption[]>([]);
  protected readonly form = signal<Form | null>(null);
  protected readonly linking = signal<Subscription | null>(null);
  protected readonly approving = signal<Subscription | null>(null);
  protected readonly deleting = signal<Subscription | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);

  ngOnInit(): void {
    void this.load();
    const ignore = () => undefined;
    this.api.crud<Named & { publicId: string }>('types', 'serials').searchAll().then((r) => this.types.set(r), ignore);
    this.api.crud<Named & { publicId: string }>('frequencies', 'serials').searchAll().then((r) => this.frequencies.set(r), ignore);
    this.api.crud<Named & { publicId: string }>('patterns', 'serials').searchAll().then((r) => this.patterns.set(r), ignore);
    this.api.storeOptions().then((r) => this.stores.set(r), ignore);
    // Nhà cung cấp là tuỳ chọn (cần phân hệ Bổ sung): rỗng/lỗi thì ẩn ô chọn.
    this.serials.suppliers().then((r) => this.suppliers.set(r), ignore);
  }

  protected statusOf(code: number) {
    return STATUSES.find((s) => s.code === code) ?? STATUSES[0];
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

  protected edit(s: Subscription | null): void {
    this.bibKeyword = '';
    this.bibResults.set([]);
    if (!s) {
      this.form.set(emptyForm());
      return;
    }
    this.form.set({
      publicId: s.publicId, approved: s.approved, bib: s.bibPublicId ? { bibPublicId: s.bibPublicId, mfn: s.bibMfn ?? 0, title: s.title, author: null, publisher: null, issn: s.issn } : null,
      title: s.title, issn: s.issn ?? '', bibPublicId: s.bibPublicId, publicationTypeId: s.publicationTypeId, frequencyId: s.frequencyId, patternId: s.patternId,
      storeId: s.storeId, storeName: s.storeName, supplierPublicId: s.supplierPublicId, supplierName: s.supplierName, startsOn: s.startsOn, endsOn: s.endsOn,
      firstIssueDate: s.firstIssueDate, startX: s.startX, startY: s.startY, startZ: s.startZ, callNumber: s.callNumber ?? '', publicNote: s.publicNote ?? '',
      internalNote: s.internalNote ?? '', graceDays: s.graceDays, status: s.status,
    });
  }

  protected pickBib(b: BibOption | null): void {
    const f = this.form();
    if (!f) return;
    f.bib = b;
    f.bibPublicId = b?.bibPublicId ?? null;
    if (b) {
      if (!f.title?.trim()) f.title = b.title;
      if (!f.issn?.trim() && b.issn) f.issn = b.issn;
      this.bibResults.set([]);
    }
  }

  protected onStore(f: Form): void {
    f.storeName = this.stores().find((s) => s.id === f.storeId)?.name ?? null;
  }

  protected onSupplier(f: Form): void {
    f.supplierName = this.suppliers().find((s) => s.publicId === f.supplierPublicId)?.name ?? null;
  }

  protected async lookup(): Promise<void> {
    const keyword = this.bibKeyword.trim();
    if (!keyword) return;
    this.busy.set(true);
    try {
      this.bibResults.set(await this.serials.lookupBibs(keyword));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected openLink(s: Subscription): void {
    this.bibKeyword = s.title;
    this.bibResults.set([]);
    this.linking.set(s);
    void this.lookup();
  }

  protected async link(s: Subscription, b: BibOption): Promise<void> {
    await this.run(async () => {
      await this.serials.linkBib(s.publicId, b.bibPublicId);
      this.toastr.success('SERIALS.LINKED');
      this.linking.set(null);
      await this.load();
    });
  }

  protected async approve(s: Subscription): Promise<void> {
    await this.run(async () => {
      await this.serials.approveSubscription(s.publicId);
      this.toastr.success('SERIALS.APPROVED_OK');
      this.approving.set(null);
      await this.load();
    }, () => this.approving.set(null));
  }

  protected async save(f: Form): Promise<void> {
    if (!f.bibPublicId && !f.title?.trim()) {
      this.toastr.warning('SERIALS.TITLE_REQUIRED');
      return;
    }
    const text = (v: string | null) => v?.trim() || null;
    const body: SubscriptionRequest = {
      title: text(f.title), issn: text(f.issn), bibPublicId: f.bibPublicId, publicationTypeId: f.publicationTypeId, frequencyId: f.frequencyId, patternId: f.patternId,
      storeId: f.storeId, storeName: f.storeName, supplierPublicId: f.supplierPublicId, supplierName: f.supplierName, startsOn: f.startsOn || null, endsOn: f.endsOn || null,
      firstIssueDate: f.firstIssueDate || null, startX: f.startX, startY: f.startY, startZ: f.startZ, callNumber: text(f.callNumber), publicNote: text(f.publicNote),
      internalNote: text(f.internalNote), graceDays: Math.max(0, Number(f.graceDays) || 0), status: f.status,
    };
    await this.run(async () => {
      if (f.publicId) await this.serials.updateSubscription(f.publicId, body);
      else await this.serials.addSubscription(body);
      this.toastr.success('COMMON.SAVE_SUCCESS');
      this.form.set(null);
      await this.load();
    });
  }

  protected async remove(s: Subscription): Promise<void> {
    await this.run(async () => {
      await this.serials.deleteSubscription(s.publicId);
      this.toastr.success('COMMON.DELETE_SUCCESS');
      this.deleting.set(null);
      await this.load();
    }, () => this.deleting.set(null));
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

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.page.set(await this.serials.searchSubscriptions({
        keyword: this.keyword.trim() || undefined, publicationTypeId: this.typeId, frequencyId: this.frequencyId, approved: this.approved,
        pageIndex: this.pageIndex, pageSize: this.pageSize,
      }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
