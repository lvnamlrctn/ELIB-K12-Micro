import { Component, OnInit, WritableSignal, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, BibTypeRule, CircPlace, LoanPolicy, WorkSession, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading } from '../../shared/ui';

type Option = { id: number; label: string };
type PlaceForm = Omit<CircPlace, 'id' | 'publicId'>;
type PolicyForm = Omit<LoanPolicy, 'id' | 'publicId' | 'bibTypeRules'> & { bibTypeRules: BibTypeRule[] };

const KIND_BORROW = 1;
const KIND_READING = 2;

/**
 * Điểm lưu thông như ELIB-LRC (/admin/circ-places): cây điểm lưu thông → chính sách theo loại bạn đọc. Chọn điểm: thông tin, giờ làm việc,
 * kho được mượn và loại bạn đọc được phục vụ (gán loại mới thì có sẵn chính sách riêng chép từ luật đang áp dụng). Chọn chính sách: sửa
 * ngay số ngày mượn, gia hạn, đặt mượn, phạt và luật theo nhóm tài liệu. Dữ liệu theo đơn vị đang đăng nhập (kho, loại bạn đọc, loại biểu
 * ghi đều lấy từ danh mục của đơn vị).
 */
@Component({
  selector: 'app-circ-places',
  imports: [FormsModule, TranslatePipe, Loading, ConfirmDelete],
  template: `
    <div class="mb-5 flex items-center gap-3 flex-wrap">
      <h4 class="page-title">{{ 'CIRC_PLACE_PAGE.TITLE' | translate }}</h4>
    </div>

    <div class="grid grid-cols-1 lg:grid-cols-3 gap-5 items-start">
      <!-- Cây điểm lưu thông -->
      <div class="lg:col-span-1 bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
        @if (loading()) { <app-loading /> }
        <div class="p-3 border-b border-gray-100 flex items-center justify-between bg-gray-50">
          <h5 class="text-sm font-semibold text-gray-700">{{ 'CIRC_PLACE_PAGE.LIST' | translate }}</h5>
          @if (can('CIRC_PLACES', 'add')) {
            <button class="btn-primary !py-1 !px-2 !text-xs" (click)="newPlace()"><span class="material-icons text-[16px]">add</span> {{ 'COMMON.ADD' | translate }}</button>
          }
        </div>
        <div class="max-h-[70vh] overflow-y-auto">
          @if (canPolicies() && generalPolicies().length) {
            <div class="border-b border-gray-100">
              <button type="button" class="w-full flex items-center gap-1 px-2 py-2 text-left hover:bg-gray-50" (click)="toggle(0)">
                <span class="material-icons text-[18px] text-gray-400">{{ expanded().has(0) ? 'expand_more' : 'chevron_right' }}</span>
                <span class="material-icons text-[16px] text-indigo-500">public</span>
                <span class="text-sm flex-1 text-gray-800">{{ 'CIRC_PLACE_PAGE.GENERAL' | translate }}</span>
                <span class="text-[11px] text-gray-400">{{ generalPolicies().length }}</span>
              </button>
              @if (expanded().has(0)) {
                @for (p of generalPolicies(); track p.publicId) {
                  <button type="button" class="w-full flex items-center gap-1.5 pl-10 pr-2 py-1.5 text-left text-sm hover:bg-gray-50"
                          [class.bg-blue-50]="selectedPolicy()?.publicId === p.publicId" (click)="selectPolicy(p)">
                    <span class="material-icons text-[15px] text-gray-400">person</span>
                    <span class="truncate text-gray-700">{{ readerTypeName(p.readerTypeId) | translate }}</span>
                    <span class="ml-auto text-[11px] text-gray-400 whitespace-nowrap">{{ 'CIRC_PLACE_PAGE.DAYS' | translate: { n: p.loanDays } }}</span>
                  </button>
                }
              }
            </div>
          }
          @for (place of places(); track place.publicId) {
            <div class="border-b border-gray-50">
              <div class="flex items-center gap-1 px-2 py-2 cursor-pointer hover:bg-gray-50" [class.bg-blue-50]="selectedPlace()?.publicId === place.publicId"
                   (click)="selectPlace(place)" (keydown.enter)="selectPlace(place)" tabindex="0" role="button">
                <button type="button" class="p-0.5 text-gray-400 hover:text-gray-600" (click)="toggle(place.id); $event.stopPropagation()" [attr.aria-label]="'CIRC_PLACE_PAGE.EXPAND' | translate">
                  <span class="material-icons text-[18px]">{{ expanded().has(place.id) ? 'expand_more' : 'chevron_right' }}</span>
                </button>
                <span class="material-icons text-[16px]" [class]="place.kind === kindReading ? 'text-teal-500' : 'text-amber-500'">{{ place.kind === kindReading ? 'chair' : 'place' }}</span>
                <span class="text-sm flex-1 truncate text-gray-800"><span class="font-mono text-[12px] text-gray-500">{{ place.code }}</span> {{ place.name }}</span>
                @if (place.kind === kindReading) { <span class="badge-off !text-[10px]">{{ 'CIRC_PLACE_PAGE.KIND_READING' | translate }}</span> }
              </div>
              @if (expanded().has(place.id)) {
                @if (!canPolicies()) {
                  <div class="pl-10 py-1.5 text-xs text-gray-400 italic">{{ 'CIRC_PLACE_PAGE.NO_POLICY_PERM' | translate }}</div>
                } @else {
                  @for (p of policiesOf(place.id); track p.publicId) {
                    <button type="button" class="w-full flex items-center gap-1.5 pl-10 pr-2 py-1.5 text-left text-sm hover:bg-gray-50"
                            [class.bg-blue-50]="selectedPolicy()?.publicId === p.publicId" (click)="selectPolicy(p)">
                      <span class="material-icons text-[15px] text-gray-400">person</span>
                      <span class="truncate" [class]="served(place, p.readerTypeId) ? 'text-gray-700' : 'text-gray-400 line-through'">{{ readerTypeName(p.readerTypeId) | translate }}</span>
                      <span class="ml-auto text-[11px] text-gray-400 whitespace-nowrap">{{ 'CIRC_PLACE_PAGE.DAYS' | translate: { n: p.loanDays } }}</span>
                    </button>
                  } @empty {
                    <div class="pl-10 pr-2 py-1.5 text-xs text-gray-400 italic">{{ 'CIRC_PLACE_PAGE.NO_OWN_POLICY' | translate }}</div>
                  }
                }
              }
            </div>
          } @empty {
            @if (!loading()) {
              <div class="p-8 text-center text-sm text-gray-400"><span class="material-icons text-4xl text-gray-300">place</span><p>{{ 'COMMON.NO_DATA' | translate }}</p></div>
            }
          }
        </div>
      </div>

      <!-- Chi tiết -->
      <div class="lg:col-span-2 space-y-5">
        @if (!placeForm() && !policyForm()) {
          <div class="bg-white rounded-xl shadow-sm border border-gray-100 p-12 text-center text-gray-400">
            <span class="material-icons text-5xl text-gray-300">touch_app</span>
            <p class="text-sm">{{ 'CIRC_PLACE_PAGE.SELECT_HINT' | translate }}</p>
          </div>
        }

        @if (placeForm(); as f) {
          <div class="panel !mb-0">
            <h5 class="text-base font-bold text-gray-800 mb-4 flex items-center gap-2">
              <span class="material-icons text-blue-600">place</span>
              {{ (selectedPlace() ? 'CIRC_PLACE_PAGE.INFO' : 'CIRC_PLACE_PAGE.NEW') | translate }}
            </h5>
            <fieldset [disabled]="!canEditPlace()" class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <label for="cp-code" class="form-label">{{ 'COMMON.CODE' | translate }} <span class="text-red-500">*</span></label>
                <input id="cp-code" class="input font-mono uppercase" maxlength="20" [(ngModel)]="f.code" [placeholder]="'CIRC_CAT.PLACE_CODE_EG' | translate" />
              </div>
              <div>
                <label for="cp-kind" class="form-label">{{ 'CIRC_PLACE_PAGE.KIND' | translate }}</label>
                <select id="cp-kind" class="input" [(ngModel)]="f.kind">
                  <option [ngValue]="kindBorrow">{{ 'CIRC_PLACE_PAGE.KIND_BORROW' | translate }}</option>
                  <option [ngValue]="kindReading">{{ 'CIRC_PLACE_PAGE.KIND_READING' | translate }}</option>
                </select>
                @if (f.kind === kindReading) { <p class="text-[11px] text-gray-500 mt-1">{{ 'CIRC_PLACE_PAGE.KIND_READING_HINT' | translate }}</p> }
              </div>
              <div class="md:col-span-2">
                <label for="cp-name" class="form-label">{{ 'CIRC_CAT.PLACE_NAME' | translate }} <span class="text-red-500">*</span></label>
                <input id="cp-name" class="input" maxlength="250" [(ngModel)]="f.name" [placeholder]="'CIRC_CAT.PLACE_NAME_EG' | translate" />
              </div>
              <div>
                <label for="cp-max" class="form-label">{{ 'CIRC_PLACE_PAGE.MAX_LOANS' | translate }}</label>
                <input id="cp-max" type="number" min="0" class="input" [(ngModel)]="f.maxLoans" />
                <p class="text-[11px] text-gray-500 mt-1">{{ 'CIRC_PLACE_PAGE.MAX_LOANS_HINT' | translate }}</p>
              </div>
              <div>
                <label for="cp-note" class="form-label">{{ 'COMMON.NOTE' | translate }}</label>
                <input id="cp-note" class="input" maxlength="500" [(ngModel)]="f.note" />
              </div>
            </fieldset>

            <div class="mt-5">
              <div class="flex items-center justify-between mb-2">
                <span class="form-label !mb-0">{{ 'CIRC_PLACE_PAGE.WORK_SESSIONS' | translate }}</span>
                @if (canEditPlace()) {
                  <button type="button" class="text-sm text-blue-600 hover:text-blue-700 flex items-center gap-1" (click)="addSession(f)"><span class="material-icons text-[16px]">add</span> {{ 'CIRC_PLACE_PAGE.ADD_SESSION' | translate }}</button>
                }
              </div>
              @for (s of f.workSessions; track $index) {
                <div class="flex items-center gap-2 mb-2">
                  <select class="input !w-36" [(ngModel)]="s.day" [disabled]="!canEditPlace()" [attr.aria-label]="'CIRC_PLACE_PAGE.DAY' | translate">
                    @for (d of days; track d) { <option [ngValue]="d">{{ 'CIRC_PLACE_PAGE.DAY_' + d | translate }}</option> }
                  </select>
                  <input type="time" class="input !w-32" [(ngModel)]="s.from" [disabled]="!canEditPlace()" [attr.aria-label]="'CIRC_PLACE_PAGE.FROM' | translate" />
                  <span class="text-gray-400">–</span>
                  <input type="time" class="input !w-32" [(ngModel)]="s.to" [disabled]="!canEditPlace()" [attr.aria-label]="'CIRC_PLACE_PAGE.TO' | translate" />
                  @if (canEditPlace()) {
                    <button type="button" class="icon-btn text-gray-400 hover:text-red-600" (click)="f.workSessions.splice($index, 1)" [title]="'COMMON.DELETE' | translate"><span class="material-icons text-[18px]">close</span></button>
                  }
                </div>
              } @empty {
                <p class="text-xs text-gray-400">{{ 'CIRC_PLACE_PAGE.NO_SESSIONS' | translate }}</p>
              }
              @if (canEditPlace()) {
                <button type="button" class="text-xs text-gray-500 hover:text-blue-600 underline" (click)="officeHours(f)">{{ 'CIRC_PLACE_PAGE.OFFICE_HOURS' | translate }}</button>
              }
            </div>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-5">
            <div class="panel !mb-0">
              <h5 class="text-sm font-semibold text-gray-700 mb-1">{{ 'CIRC_CAT.PLACE_STORES_FULL' | translate }}</h5>
              <p class="text-[11px] text-gray-500 mb-2">{{ 'CIRC_CAT.PLACE_STORES_HINT' | translate }}</p>
              <div class="max-h-56 overflow-y-auto border border-gray-200 rounded-lg divide-y divide-gray-100">
                @for (s of stores(); track s.id) {
                  <label class="flex items-center gap-2 px-3 py-1.5 text-sm hover:bg-gray-50 cursor-pointer">
                    <input type="checkbox" class="rounded text-blue-500 border-gray-300" [checked]="f.storeIds.includes(s.id)" [disabled]="!canEditPlace()" (change)="flip(f.storeIds, s.id)" />
                    <span class="text-gray-700">{{ s.label }}</span>
                  </label>
                } @empty { <p class="px-3 py-4 text-xs text-gray-400 text-center">{{ 'COMMON.NO_DATA' | translate }}</p> }
              </div>
            </div>
            <div class="panel !mb-0">
              <h5 class="text-sm font-semibold text-gray-700 mb-1">{{ 'CIRC_PLACE_PAGE.READER_TYPES' | translate }}</h5>
              <p class="text-[11px] text-gray-500 mb-2">{{ 'CIRC_PLACE_PAGE.READER_TYPES_HINT' | translate }}</p>
              <div class="max-h-56 overflow-y-auto border border-gray-200 rounded-lg divide-y divide-gray-100">
                @for (t of readerTypes(); track t.id) {
                  <label class="flex items-center gap-2 px-3 py-1.5 text-sm hover:bg-gray-50 cursor-pointer">
                    <input type="checkbox" class="rounded text-blue-500 border-gray-300" [checked]="f.readerTypeIds.includes(t.id)" [disabled]="!canEditPlace()" (change)="flip(f.readerTypeIds, t.id)" />
                    <span class="text-gray-700">{{ t.label }}</span>
                  </label>
                } @empty { <p class="px-3 py-4 text-xs text-gray-400 text-center">{{ 'COMMON.NO_DATA' | translate }}</p> }
              </div>
            </div>
          </div>

          <div class="flex justify-end gap-3">
            @if (selectedPlace() && can('CIRC_PLACES', 'delete')) {
              <button type="button" class="btn-secondary !text-red-600" (click)="deleting.set('place')"><span class="material-icons text-[18px]">delete</span> {{ 'COMMON.DELETE' | translate }}</button>
            }
            @if (canEditPlace()) {
              <button type="button" class="btn-primary" [disabled]="busy() || !f.code.trim() || !f.name.trim()" (click)="savePlace(f)">
                <span class="material-icons text-[18px]">save</span> {{ 'COMMON.SAVE' | translate }}
              </button>
            }
          </div>
        }

        @if (policyForm(); as p) {
          <div class="panel !mb-0">
            <h5 class="text-base font-bold text-gray-800 mb-1 flex items-center gap-2">
              <span class="material-icons text-blue-600">rule</span>
              {{ 'CIRC_PLACE_PAGE.POLICY_FOR' | translate: { type: (readerTypeName(p.readerTypeId) | translate), place: placeLabel(p.circPlaceId) | translate } }}
            </h5>
            <p class="text-xs text-gray-500 mb-4">{{ 'CIRC_PLACE_PAGE.POLICY_HINT' | translate }}</p>
            <fieldset [disabled]="!can('CIRC_POLICIES', 'edit')" class="grid grid-cols-1 md:grid-cols-3 gap-4">
              <div>
                <label for="lp-days" class="form-label">{{ 'CIRC_CAT.LOAN_DAYS' | translate }}</label>
                <input id="lp-days" type="number" min="1" class="input" [(ngModel)]="p.loanDays" />
              </div>
              <div>
                <label for="lp-max" class="form-label">{{ 'CIRC_CAT.MAX_LOANS_FULL' | translate }}</label>
                <input id="lp-max" type="number" min="0" class="input" [(ngModel)]="p.maxLoans" [placeholder]="'CIRC_PLACE_PAGE.UNLIMITED' | translate" />
              </div>
              <div>
                <label for="lp-fine" class="form-label">{{ 'CIRC_CAT.FINE_PER_DAY_FULL' | translate }}</label>
                <input id="lp-fine" type="number" min="0" class="input" [(ngModel)]="p.finePerDay" />
              </div>
              <div>
                <label for="lp-renews" class="form-label">{{ 'CIRC_CAT.MAX_RENEWALS_FULL' | translate }}</label>
                <input id="lp-renews" type="number" min="0" class="input" [(ngModel)]="p.maxRenewals" [placeholder]="'CIRC_PLACE_PAGE.UNLIMITED' | translate" />
              </div>
              <div>
                <label for="lp-renew-days" class="form-label">{{ 'CIRC_CAT.RENEW_DAYS_FULL' | translate }}</label>
                <input id="lp-renew-days" type="number" min="1" class="input" [(ngModel)]="p.renewDays" />
              </div>
              <div class="flex items-end pb-2">
                <label class="flex items-center gap-2 text-sm text-gray-700"><input type="checkbox" class="rounded text-blue-500 border-gray-300" [(ngModel)]="p.renewFromToday" /> {{ 'CIRC_CAT.RENEW_FROM_TODAY' | translate }}</label>
              </div>
              <div>
                <label for="lp-holds" class="form-label">{{ 'CIRC_CAT.MAX_HOLDS' | translate }}</label>
                <input id="lp-holds" type="number" min="0" class="input" [(ngModel)]="p.maxHolds" [placeholder]="'CIRC_PLACE_PAGE.UNLIMITED' | translate" />
              </div>
              <div>
                <label for="lp-hold-days" class="form-label">{{ 'CIRC_CAT.HOLD_DAYS' | translate }}</label>
                <input id="lp-hold-days" type="number" min="1" max="30" class="input" [(ngModel)]="p.holdDays" />
              </div>
              <div></div>
              <div>
                <label for="lp-tech" class="form-label">{{ 'CIRC_CAT.TECH_FEE' | translate }}</label>
                <input id="lp-tech" type="number" min="0" class="input" [(ngModel)]="p.techFee" />
              </div>
              <div>
                <label for="lp-ship" class="form-label">{{ 'CIRC_CAT.SHIPPING_FEE' | translate }}</label>
                <input id="lp-ship" type="number" min="0" class="input" [(ngModel)]="p.shippingFee" />
              </div>
              <p class="text-[11px] text-gray-500 self-end pb-2">{{ 'CIRC_CAT.DOC_FEES_HINT' | translate }}</p>
            </fieldset>

            <div class="mt-6 border-t border-gray-100 pt-4">
              <h6 class="text-sm font-semibold text-gray-700">{{ 'CIRC_CAT.BIB_TYPE_RULES' | translate }}</h6>
              <p class="text-[11px] text-gray-500 mb-3">{{ 'CIRC_CAT.BIB_TYPE_RULES_HINT' | translate }}</p>
              <div class="overflow-x-auto">
                <table class="w-full text-sm min-w-[560px]">
                  <thead><tr>
                    <th class="th">{{ 'CIRC_CAT.BIB_TYPE' | translate }}</th><th class="th w-28">{{ 'CIRC_CAT.LOAN_DAYS' | translate }}</th>
                    <th class="th w-28">{{ 'CIRC_CAT.MAX_LOANS' | translate }}</th><th class="th w-28">{{ 'CIRC_CAT.MAX_RENEWALS' | translate }}</th>
                    <th class="th w-28">{{ 'CIRC_CAT.MAX_HOLDS_SHORT' | translate }}</th><th class="th w-10"></th>
                  </tr></thead>
                  <tbody>
                    @for (r of p.bibTypeRules; track $index) {
                      <tr>
                        <td class="td"><select class="input" [(ngModel)]="r.bibTypeId" [disabled]="!can('CIRC_POLICIES', 'edit')" [attr.aria-label]="'CIRC_CAT.BIB_TYPE' | translate">
                          @for (b of bibTypes(); track b.id) { <option [ngValue]="b.id">{{ b.label }}</option> }
                        </select></td>
                        <td class="td"><input type="number" min="1" class="input" [(ngModel)]="r.loanDays" [disabled]="!can('CIRC_POLICIES', 'edit')" [attr.aria-label]="'CIRC_CAT.LOAN_DAYS' | translate" /></td>
                        <td class="td"><input type="number" min="0" class="input" [(ngModel)]="r.maxLoans" [disabled]="!can('CIRC_POLICIES', 'edit')" [attr.aria-label]="'CIRC_CAT.MAX_LOANS' | translate" /></td>
                        <td class="td"><input type="number" min="0" class="input" [(ngModel)]="r.maxRenewals" [disabled]="!can('CIRC_POLICIES', 'edit')" [attr.aria-label]="'CIRC_CAT.MAX_RENEWALS' | translate" /></td>
                        <td class="td"><input type="number" min="0" class="input" [(ngModel)]="r.maxHolds" [disabled]="!can('CIRC_POLICIES', 'edit')" [attr.aria-label]="'CIRC_CAT.MAX_HOLDS_SHORT' | translate" /></td>
                        <td class="td text-right">
                          @if (can('CIRC_POLICIES', 'edit')) {
                            <button type="button" class="icon-btn text-gray-400 hover:text-red-600" (click)="p.bibTypeRules.splice($index, 1)" [title]="'COMMON.DELETE' | translate"><span class="material-icons text-[18px]">delete</span></button>
                          }
                        </td>
                      </tr>
                    } @empty {
                      <tr><td colspan="6" class="py-4 text-center text-gray-400 text-xs">{{ 'COMMON.NO_DATA' | translate }}</td></tr>
                    }
                  </tbody>
                </table>
              </div>
              @if (can('CIRC_POLICIES', 'edit') && bibTypes().length) {
                <button type="button" class="mt-2 text-sm text-blue-600 hover:text-blue-700 flex items-center gap-1" (click)="addRule(p)"><span class="material-icons text-[16px]">add</span> {{ 'CIRC_CAT.BIB_TYPE_RULE_ADD' | translate }}</button>
              }
            </div>
          </div>

          <div class="flex justify-end gap-3">
            @if (can('CIRC_POLICIES', 'delete') && p.circPlaceId !== null) {
              <button type="button" class="btn-secondary !text-red-600" (click)="deleting.set('policy')"><span class="material-icons text-[18px]">delete</span> {{ 'COMMON.DELETE' | translate }}</button>
            }
            @if (can('CIRC_POLICIES', 'edit')) {
              <button type="button" class="btn-primary" [disabled]="busy()" (click)="savePolicy(p)"><span class="material-icons text-[18px]">save</span> {{ 'COMMON.SAVE' | translate }}</button>
            }
          </div>
        }
      </div>
    </div>

    @if (deleting(); as what) {
      <app-confirm-delete [busy]="busy()"
        [message]="what === 'place' ? 'CIRC_PLACE_PAGE.DELETE_PLACE' : 'CIRC_PLACE_PAGE.DELETE_POLICY'"
        [messageParams]="{ name: selectedPlace()?.name }"
        (confirmed)="what === 'place' ? deletePlace() : deletePolicy()" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class CircPlaces implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
  private readonly placesApi = this.api.crud<CircPlace>('circ-places', 'circulation');
  private readonly policiesApi = this.api.crud<LoanPolicy>('loan-policies', 'circulation');

  protected readonly kindBorrow = KIND_BORROW;
  protected readonly kindReading = KIND_READING;
  protected readonly days = [1, 2, 3, 4, 5, 6, 7];

  protected readonly places = signal<CircPlace[]>([]);
  protected readonly policies = signal<LoanPolicy[]>([]);
  protected readonly stores = signal<Option[]>([]);
  protected readonly readerTypes = signal<Option[]>([]);
  protected readonly bibTypes = signal<Option[]>([]);
  protected readonly expanded = signal<Set<number>>(new Set());
  protected readonly selectedPlace = signal<CircPlace | null>(null);
  protected readonly selectedPolicy = signal<LoanPolicy | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly deleting = signal<'place' | 'policy' | null>(null);
  protected readonly generalPolicies = computed(() => this.policies().filter((p) => p.circPlaceId === null));
  protected readonly canPolicies = computed(() => this.can('CIRC_POLICIES', 'view'));

  // Bản nháp đang sửa (ngModel ghi thẳng vào object; chỉ một trong hai khác null).
  protected readonly placeForm = signal<PlaceForm | null>(null);
  protected readonly policyForm = signal<PolicyForm | null>(null);

  ngOnInit(): void {
    void this.load();
    // Danh mục của đơn vị ở service khác — đơn vị chưa mua phân hệ thì danh sách trống, màn vẫn dùng được.
    void this.lookup(this.readerTypes, async () => (await this.api.crud<{ publicId: string; id: number; name: string }>('reader-types', 'patron').searchAll())
      .map((t) => ({ id: t.id, label: t.name })));
    void this.lookup(this.stores, async () => (await this.api.crud<{ publicId: string; id: number; code: string; name: string }>('stores', 'holdings').searchAll())
      .map((s) => ({ id: s.id, label: `${s.code} — ${s.name}` })));
    void this.lookup(this.bibTypes, async () => (await this.api.crud<{ publicId: string; id: number; name: string }>('bib-types', 'catalog').searchAll())
      .map((b) => ({ id: b.id, label: b.name })));
  }

  protected can(module: string, action: string): boolean {
    return this.session.can(`${module}:${action}`);
  }

  protected canEditPlace(): boolean {
    return this.can('CIRC_PLACES', this.selectedPlace() ? 'edit' : 'add');
  }

  protected policiesOf(placeId: number): LoanPolicy[] {
    return this.policies().filter((p) => p.circPlaceId === placeId);
  }

  protected served(place: CircPlace, readerTypeId: number | null): boolean {
    return place.readerTypeIds.length === 0 || (readerTypeId !== null && place.readerTypeIds.includes(readerTypeId));
  }

  /** Tên loại bạn đọc, hoặc khoá dịch "mọi loại" — template dịch lại (tên thật không có trong file dịch nên giữ nguyên). */
  protected readerTypeName(id: number | null): string {
    if (id === null) return 'CIRC_CAT.ALL_READER_TYPES';
    return this.readerTypes().find((t) => t.id === id)?.label ?? `#${id}`;
  }

  protected placeLabel(id: number | null): string {
    if (id === null) return 'CIRC_CAT.ALL_PLACES';
    const place = this.places().find((p) => p.id === id);
    return place ? place.name : `#${id}`;
  }

  protected toggle(id: number): void {
    const next = new Set(this.expanded());
    if (next.has(id)) next.delete(id);
    else next.add(id);
    this.expanded.set(next);
  }

  protected newPlace(): void {
    this.selectedPlace.set(null);
    this.selectedPolicy.set(null);
    this.policyForm.set(null);
    this.placeForm.set({ code: '', name: '', storeIds: [], readerTypeIds: [], kind: KIND_BORROW, maxLoans: null, workSessions: [], note: null });
  }

  protected selectPlace(place: CircPlace): void {
    this.selectedPlace.set(place);
    this.selectedPolicy.set(null);
    this.policyForm.set(null);
    this.placeForm.set({
      code: place.code, name: place.name, storeIds: [...place.storeIds], readerTypeIds: [...(place.readerTypeIds ?? [])], kind: place.kind ?? KIND_BORROW,
      maxLoans: place.maxLoans, workSessions: (place.workSessions ?? []).map((s) => ({ ...s })), note: place.note,
    });
    if (!this.expanded().has(place.id)) this.toggle(place.id);
  }

  protected selectPolicy(policy: LoanPolicy): void {
    this.selectedPolicy.set(policy);
    this.selectedPlace.set(this.places().find((p) => p.id === policy.circPlaceId) ?? null);
    this.placeForm.set(null);
    const { id: _id, publicId: _publicId, bibTypeRules, ...rest } = policy;
    this.policyForm.set({ ...rest, bibTypeRules: (bibTypeRules ?? []).map((r) => ({ ...r })) });
  }

  protected flip(list: number[], id: number): void {
    const i = list.indexOf(id);
    if (i >= 0) list.splice(i, 1);
    else list.push(id);
  }

  protected addSession(f: PlaceForm): void {
    const last = f.workSessions.at(-1);
    f.workSessions.push({ day: last ? Math.min(7, last.day + 1) : 1, from: last?.from ?? '07:00', to: last?.to ?? '17:00' });
  }

  /** Giờ hành chính thứ Hai – thứ Sáu (sáng + chiều) — điểm bắt đầu nhanh cho thư viện trường. */
  protected officeHours(f: PlaceForm): void {
    f.workSessions = [1, 2, 3, 4, 5].flatMap((day): WorkSession[] => [{ day, from: '07:00', to: '11:30' }, { day, from: '13:30', to: '17:00' }]);
  }

  protected addRule(p: PolicyForm): void {
    const used = new Set(p.bibTypeRules.map((r) => r.bibTypeId));
    const free = this.bibTypes().find((b) => !used.has(b.id)) ?? this.bibTypes()[0];
    p.bibTypeRules.push({ bibTypeId: free.id, loanDays: null, maxLoans: null, maxRenewals: null, maxHolds: null });
  }

  protected async savePlace(f: PlaceForm): Promise<void> {
    await this.run(async () => {
      const body = { ...f, code: f.code.trim(), name: f.name.trim(), note: f.note?.trim() || null, maxLoans: blank(f.maxLoans) };
      const current = this.selectedPlace();
      const saved = current ? await this.placesApi.update(current.publicId, body) : await this.placesApi.add(body);
      this.toastr.success(current ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS');
      await this.load();
      this.selectPlace(this.places().find((p) => p.publicId === saved.publicId) ?? saved);
    });
  }

  protected async savePolicy(p: PolicyForm): Promise<void> {
    const current = this.selectedPolicy();
    if (!current) return;
    await this.run(async () => {
      const body = {
        ...p, maxLoans: blank(p.maxLoans), maxRenewals: blank(p.maxRenewals), maxHolds: blank(p.maxHolds),
        finePerDay: p.finePerDay ?? 0, techFee: p.techFee ?? 0, shippingFee: p.shippingFee ?? 0,
        bibTypeRules: p.bibTypeRules.map((r) => ({ ...r, loanDays: blank(r.loanDays), maxLoans: blank(r.maxLoans), maxRenewals: blank(r.maxRenewals), maxHolds: blank(r.maxHolds) })),
      };
      const saved = await this.policiesApi.update(current.publicId, body);
      this.toastr.success('COMMON.UPDATE_SUCCESS');
      await this.load();
      this.selectPolicy(this.policies().find((x) => x.publicId === saved.publicId) ?? saved);
    });
  }

  protected async deletePlace(): Promise<void> {
    const place = this.selectedPlace();
    if (!place) return;
    await this.run(async () => {
      await this.placesApi.delete(place.publicId);
      this.toastr.success('COMMON.DELETE_SUCCESS');
      this.clearSelection();
      await this.load();
    });
  }

  protected async deletePolicy(): Promise<void> {
    const policy = this.selectedPolicy();
    if (!policy) return;
    await this.run(async () => {
      await this.policiesApi.delete(policy.publicId);
      this.toastr.success('COMMON.DELETE_SUCCESS');
      this.clearSelection();
      await this.load();
    });
  }

  private clearSelection(): void {
    this.deleting.set(null);
    this.selectedPlace.set(null);
    this.selectedPolicy.set(null);
    this.placeForm.set(null);
    this.policyForm.set(null);
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const [places, policies] = await Promise.all([
        this.placesApi.searchAll(),
        this.canPolicies() ? this.policiesApi.searchAll() : Promise.resolve([] as LoanPolicy[]),
      ]);
      this.places.set(places);
      this.policies.set(policies);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  private async lookup(target: WritableSignal<Option[]>, fetch: () => Promise<Option[]>): Promise<void> {
    try {
      target.set(await fetch());
    } catch {
      target.set([]);
    }
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    try {
      await action();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}

/** Ô số để trống (ngModel trả null hoặc "") → null = không giới hạn. */
function blank(value: number | null | undefined): number | null {
  return value === null || value === undefined || (value as unknown) === '' ? null : Number(value);
}
