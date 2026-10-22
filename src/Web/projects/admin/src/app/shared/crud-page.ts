import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { LDatePipe, LNumberPipe, Language } from '../core/i18n';
import { Api, CrudClient, CrudPage as Page, STATUS_ACTIVE, errorMessage } from '../core/api';
import { Session } from '../core/session';
import { PaymentDialog } from '../pages/payment/payment-dialog';
import { ImportDialog } from './import-dialog';
import { ToastrService } from './toastr';
import { ConfirmDelete, Loading, Modal, Paginator, StatusBadge } from './ui';

/** Mọi nhãn (title, label, placeholder, hint, options[].label…) là khoá dịch hoặc câu sẵn — template luôn qua pipe translate. */
export interface CrudColumn {
  key: string;
  label: string;
  /** 'lookup': hiện nhãn lựa chọn của ô nhập cùng key (vd tên loại kho thay vì id). */
  type?: 'text' | 'number' | 'status' | 'bool' | 'mono' | 'lookup' | 'date';
  width?: string;
}

export interface CrudField {
  key: string;
  label: string;
  /**
   * 'multiselect': danh sách ô tích, giá trị là mảng (vd các kho được mượn tại điểm lưu thông).
   * 'password': không bao giờ hiện giá trị cũ; để trống khi sửa = giữ nguyên (gửi null).
   * 'rows': bảng con nhiều dòng (giá trị là mảng object) theo rowFields — vd luật theo nhóm tài liệu của chính sách.
   * 'date': ngày yyyy-MM-dd (để trống = null).
   */
  type: 'text' | 'number' | 'textarea' | 'status' | 'checkbox' | 'select' | 'multiselect' | 'password' | 'rows' | 'date';
  /** Cột của bảng con ('rows'). */
  rowFields?: CrudRowField[];
  /** Nhãn nút thêm dòng của bảng con. */
  addLabel?: string;
  /** Lựa chọn của ô 'select'. */
  options?: { value: string | number; label: string }[];
  /** Lựa chọn lấy từ API khi mở màn (vd danh sách loại kho); thêm sẵn lựa chọn trống "—". */
  optionsFrom?: (api: Api) => Promise<{ value: string | number; label: string }[]>;
  /** Nhãn của lựa chọn trống (mặc định "—"), vd "Mọi loại bạn đọc". */
  emptyLabel?: string;
  required?: boolean;
  placeholder?: string;
  hint?: string;
  /** Không sửa được khi chỉnh sửa (mã). */
  lockOnEdit?: boolean;
  /** Không sửa được khi bản ghi thoả điều kiện (ví dụ tham số có sẵn của hệ thống). */
  lockWhen?: (item: Row) => boolean;
  half?: boolean;
}

/** Một cột của bảng con: ô chọn (lựa chọn tĩnh hoặc từ API) hoặc ô số (trống = null). */
export interface CrudRowField {
  key: string;
  label: string;
  type: 'select' | 'number' | 'text';
  options?: { value: string | number; label: string }[];
  /** Ô chữ: số ký tự tối đa / độ rộng. */
  maxLength?: number;
  optionsFrom?: (api: Api) => Promise<{ value: string | number; label: string }[]>;
  emptyLabel?: string;
}

/** Cấu hình một màn danh mục — truyền qua data của route. */
/** Thông báo sau thao tác: khoá dịch, hoặc khoá kèm tham số. */
export type CrudMessage = string | { key: string; params?: Record<string, unknown> };

export interface CrudConfig {
  title: string;
  /** Tài nguyên dưới /api/admin/<service>/ */
  resource: string;
  service?: string;
  /** Mã module quyền như monolith (NATIONALITIES, SYSTEM_PARAMS…). */
  perm: string;
  searchLabel: string;
  searchPlaceholder: string;
  columns: CrudColumn[];
  fields: CrudField[];
  /** Có lọc/đổi trạng thái (entity có Status). */
  hasStatus?: boolean;
  defaults?: Record<string, unknown>;
  /** Giá trị mặc định khi thêm mới lấy từ API (ví dụ mẫu quy tắc ISBD) — gộp đè lên defaults. */
  defaultsFrom?: (api: Api) => Promise<Record<string, unknown>>;
  modalWidth?: string;
  /** Huy hiệu thêm sau cột đầu tiên (ví dụ "Hệ thống" cho tham số có sẵn). */
  badge?: (item: Row) => string | null;
  /** Nút thêm trên thanh công cụ (ví dụ "Khôi phục mẫu mặc định"). */
  toolbar?: CrudToolbarAction[];
  /** Danh mục có endpoint ImportTemplate/Import (resource cài ICrudImportable) → hiện nút "Nhập Excel" (quyền add). */
  importable?: boolean;
  /** Nút thêm trên từng dòng (ví dụ "Kiểm tra kết nối"). */
  rowActions?: CrudRowAction[];
}

export interface CrudRowAction {
  icon: string;
  title: string;
  /** Hành động quyền cần có trên module của màn. */
  action: string;
  /** Mở hộp thoại thu phí bằng mã QR cho dòng này (nguồn khoản thu = service của màn, id = publicId) — cần thêm quyền PAYMENT_TRANSACTIONS:add. */
  payment?: boolean;
  /** Chỉ hiện ở dòng thoả điều kiện (ví dụ một tham số cụ thể). */
  show?: (row: Record<string, unknown>) => boolean;
  /** Ô nhập trước khi chạy (mở hộp thoại nhỏ) — ví dụ tài khoản thử để kiểm tra kết nối. */
  inputs?: { key: string; label: string; type?: 'text' | 'password' }[];
  /** Trả về thông báo kết quả (ném lỗi = báo lỗi). */
  run: (api: Api, row: Record<string, unknown> & { publicId: string }, values: Record<string, string>) => Promise<CrudMessage>;
}

export interface CrudToolbarAction {
  label: string;
  icon: string;
  /** Hành động quyền cần có trên module của màn (add/edit…). */
  action: string;
  /** Trả về thông báo thành công; danh sách tải lại sau khi chạy. */
  run: (api: Api) => Promise<CrudMessage>;
}

type Row = Record<string, unknown> & { publicId: string; id: number };

/**
 * Màn danh mục chuẩn — giao diện như pages/admin/shared/base-entity của frontend monolith: thẻ tìm kiếm, thanh nút
 * Thêm / Xoá mục đã chọn, bảng có ô chọn + ID + cột dữ liệu + thao tác, phân trang, modal thêm/sửa, xác nhận xoá.
 */
@Component({
  selector: 'app-crud-page',
  imports: [FormsModule, LDatePipe, LNumberPipe, TranslatePipe, Paginator, Modal, ConfirmDelete, Loading, StatusBadge, ImportDialog, PaymentDialog],
  template: `
    @let c = config();
    <div class="mb-5 flex flex-col sm:flex-row justify-between items-start sm:items-center">
      <h4 class="page-title">{{ c.title | translate }}</h4>
    </div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
        <div>
          <label for="kw" class="field-label">{{ c.searchLabel | translate }}</label>
          <input id="kw" type="text" class="input" [(ngModel)]="keyword" (keydown.enter)="search()" [placeholder]="c.searchPlaceholder | translate" />
        </div>
        @if (c.hasStatus) {
          <div>
            <label for="st" class="field-label">{{ 'COMMON.STATUS' | translate }}</label>
            <select id="st" class="input" [(ngModel)]="status" (change)="search()">
              <option [ngValue]="null">{{ 'COMMON.ALL' | translate }}</option>
              <option [ngValue]="2">{{ 'COMMON.ACTIVE' | translate }}</option>
              <option [ngValue]="1">{{ 'COMMON.STATUS_INACTIVE' | translate }}</option>
            </select>
          </div>
        } @else {
          <div class="hidden xl:block"></div>
        }
        <div class="hidden xl:block"></div>
        <div class="flex items-end">
          <button (click)="search()" class="btn-primary w-full">
            <span class="material-icons text-[18px]">search</span> {{ 'COMMON.SEARCH_BTN' | translate }}
          </button>
        </div>
      </div>
    </div>

    <div class="panel flex gap-3 items-center justify-between flex-wrap">
      <div class="flex gap-2">
        @if (can('add')) {
          <button (click)="openAdd()" class="btn-add"><span class="material-icons text-[18px]">add</span> {{ 'COMMON.ADD_BTN' | translate }}</button>
          @if (c.importable) {
            <button (click)="importing.set(true)" class="btn-secondary !py-1.5 !px-3"><span class="material-icons text-[18px]">upload_file</span> {{ 'COMMON.IMPORT_EXCEL' | translate }}</button>
          }
        }
        @if (can('delete')) {
          <button (click)="confirmBulk()" [disabled]="selected().size === 0" class="btn-outline-danger">
            <span class="material-icons text-[18px]">delete</span> {{ 'COMMON.DELETE_SELECTED' | translate }}
          </button>
        }
        @for (t of c.toolbar ?? []; track t.label) {
          @if (can(t.action)) {
            <button (click)="runTool(t)" [disabled]="saving()" class="btn-secondary !py-1.5 !px-3">
              <span class="material-icons text-[18px]">{{ t.icon }}</span> {{ t.label | translate }}
            </button>
          }
        }
      </div>
      <span class="text-sm text-gray-500">{{ 'COMMON.RECORDS_N' | translate: { count: page()?.totalCount ?? 0 } }}</span>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse">
          <thead>
            <tr>
              <th class="th w-12 text-center">
                <input type="checkbox" class="rounded border-gray-300 cursor-pointer" [checked]="allSelected()" (change)="toggleAll()" />
              </th>
              <th class="th w-16 !text-center">ID</th>
              @for (col of c.columns; track col.key) { <th class="th" [style.width]="col.width">{{ col.label | translate }}</th> }
              @if (c.hasStatus) { <th class="th w-36 !text-center">{{ 'COMMON.STATUS' | translate }}</th> }
              <th class="th w-28 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
            </tr>
          </thead>
          <tbody>
            @for (row of page()?.items ?? []; track row.publicId) {
              <tr class="hover:bg-gray-50/50 transition-colors cursor-pointer" (click)="toggle(row)">
                <td class="td text-center">
                  <input type="checkbox" class="rounded border-gray-300 cursor-pointer" [checked]="selected().has(row.publicId)"
                         (click)="$event.stopPropagation()" (change)="toggle(row)" />
                </td>
                <td class="td text-center text-gray-500">{{ row.id }}</td>
                @for (col of c.columns; track col.key; let first = $first) {
                  <td class="td" [class.font-medium]="first" [class.text-gray-800]="first">
                    @switch (col.type) {
                      @case ('number') { {{ $any(row[col.key]) | lnumber: '1.0-4' }} }
                      @case ('bool') {
                        @if (row[col.key]) { <span class="badge-info">{{ 'COMMON.YES' | translate }}</span> } @else { <span class="text-gray-300">—</span> }
                      }
                      @case ('mono') { <span class="font-mono text-[13px]">{{ row[col.key] }}</span> }
                      @case ('lookup') { {{ optionLabel(col.key, row[col.key]) | translate }} }
                      @case ('date') { <span class="whitespace-nowrap">{{ $any(row[col.key]) | ldate }}</span> }
                      @default { <span class="line-clamp-2 break-all">{{ row[col.key] ?? '—' }}</span> }
                    }
                    @if (first && c.badge?.(row); as b) { <span class="ml-2 text-[11px] text-blue-600 bg-blue-50 px-1.5 py-0.5 rounded whitespace-nowrap">{{ b | translate }}</span> }
                  </td>
                }
                @if (c.hasStatus) {
                  <td class="td text-center" (click)="$event.stopPropagation()">
                    <button type="button" [disabled]="!can('edit')" (click)="flipStatus(row)" [title]="'COMMON.CLICK_TO_TOGGLE_STATUS' | translate" class="disabled:cursor-default">
                      <app-status-badge [status]="$any(row['status'])" />
                    </button>
                  </td>
                }
                <td class="td">
                  <div class="flex items-center justify-center space-x-2">
                    @for (a of c.rowActions ?? []; track a.title) {
                      @if (can(a.action) && (!a.payment || session.can('PAYMENT_TRANSACTIONS:add')) && (!a.show || a.show(row))) {
                        <button (click)="runRowAction(a, row); $event.stopPropagation()" [disabled]="busyRow() === row.publicId"
                                class="icon-btn bg-gray-50 text-gray-600 hover:bg-gray-100" [title]="a.title | translate">
                          <span class="material-icons text-[18px]" [class.animate-spin]="busyRow() === row.publicId">{{ busyRow() === row.publicId ? 'autorenew' : a.icon }}</span>
                        </button>
                      }
                    }
                    @if (can('edit')) {
                      <button (click)="openEdit(row); $event.stopPropagation()" class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" [title]="'COMMON.EDIT' | translate">
                        <span class="material-icons text-[18px]">edit</span>
                      </button>
                    }
                    @if (can('delete')) {
                      <button (click)="confirmOne(row); $event.stopPropagation()" class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" [title]="'COMMON.DELETE' | translate">
                        <span class="material-icons text-[18px]">delete</span>
                      </button>
                    }
                  </div>
                </td>
              </tr>
            } @empty {
              <tr><td class="py-8 px-4 text-center text-gray-500" [attr.colspan]="colspan()">{{ loading() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (page(); as p) {
        <app-paginator [total]="p.totalCount" [pageIndex]="pageIndex" [pageSize]="pageSize" (pageChange)="onPage($event)" />
      }
    </div>

    @if (actionDialog(); as d) {
      <app-modal [title]="d.action.title | translate" widthClass="max-w-md" (closed)="actionDialog.set(null)">
        <form id="row-action-form" (ngSubmit)="submitRowAction()" class="space-y-4">
          @for (i of d.action.inputs ?? []; track i.key) {
            <div>
              <label class="form-label" [for]="'ra-' + i.key">{{ i.label | translate }}</label>
              <input [id]="'ra-' + i.key" [name]="i.key" class="input" [type]="i.type ?? 'text'" autocomplete="off" [(ngModel)]="d.values[i.key]" required />
            </div>
          }
        </form>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="actionDialog.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="submit" form="row-action-form" class="btn-primary" [disabled]="busyRow() !== null">
            <span class="material-icons text-[16px]">{{ d.action.icon }}</span> {{ d.action.title | translate }}</button>
        </ng-container>
      </app-modal>
    }

    @if (editing(); as item) {
      <app-modal [title]="language.t(item.publicId ? 'COMMON.UPDATE_NAMED' : 'COMMON.ADD_NAMED', { name: language.t(c.title) })" [widthClass]="c.modalWidth ?? 'max-w-md'" (closed)="editing.set(null)">
        <form id="crud-form" (ngSubmit)="save()" class="grid grid-cols-2 gap-4">
          @for (f of c.fields; track f.key) {
            <div [class.col-span-2]="!f.half">
              @if (f.type === 'checkbox') {
                <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer select-none">
                  <input type="checkbox" class="rounded border-gray-300" [name]="f.key" [(ngModel)]="item[f.key]" [disabled]="locked(f, item)" />
                  {{ f.label | translate }}
                </label>
              } @else {
                <label class="form-label" [for]="'f-' + f.key">{{ f.label | translate }} @if (f.required) { <span class="text-red-500">*</span> }</label>
                @switch (f.type) {
                  @case ('textarea') {
                    <textarea [id]="'f-' + f.key" [name]="f.key" rows="5" class="input font-mono text-[13px]" [(ngModel)]="item[f.key]"
                              [placeholder]="(f.placeholder ?? '') | translate" [disabled]="locked(f, item)"></textarea>
                  }
                  @case ('number') {
                    <input [id]="'f-' + f.key" [name]="f.key" type="number" step="any" class="input" [(ngModel)]="item[f.key]"
                           [placeholder]="(f.placeholder ?? '') | translate" [required]="!!f.required" [disabled]="locked(f, item)" />
                  }
                  @case ('date') {
                    <input [id]="'f-' + f.key" [name]="f.key" type="date" class="input" [(ngModel)]="item[f.key]" [required]="!!f.required" [disabled]="locked(f, item)" />
                  }
                  @case ('select') {
                    <select [id]="'f-' + f.key" [name]="f.key" class="input" [(ngModel)]="item[f.key]" [disabled]="locked(f, item)">
                      @for (o of optionsOf(f); track o.value) { <option [ngValue]="o.value">{{ o.label | translate }}</option> }
                    </select>
                  }
                  @case ('multiselect') {
                    <div class="grid grid-cols-2 gap-x-4 gap-y-1.5 max-h-40 overflow-y-auto border border-gray-200 rounded-lg p-2.5">
                      @for (o of optionsOf(f); track o.value) {
                        @if (o.value !== null) {
                          <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer select-none">
                            <input type="checkbox" class="rounded border-gray-300" [checked]="isChecked(item, f.key, o.value)" (change)="toggleValue(item, f.key, o.value)" />
                            {{ o.label | translate }}
                          </label>
                        }
                      } @empty { <span class="text-sm text-gray-400">{{ 'COMMON.NO_OPTIONS' | translate }}</span> }
                    </div>
                  }
                  @case ('password') {
                    <input [id]="'f-' + f.key" [name]="f.key" type="password" autocomplete="new-password" class="input" [(ngModel)]="item[f.key]"
                           [placeholder]="(item.publicId && item['has' + f.key[0].toUpperCase() + f.key.slice(1)] ? 'COMMON.PASSWORD_KEEP' : (f.placeholder ?? '')) | translate"
                           [disabled]="locked(f, item)" />
                  }
                  @case ('rows') {
                    <div class="border border-gray-200 rounded-lg overflow-x-auto">
                      <table class="w-full text-sm">
                        <thead class="bg-gray-50 text-xs text-gray-600">
                          <tr>
                            @for (sf of f.rowFields ?? []; track sf.key) { <th class="px-2 py-1.5 text-left font-medium">{{ sf.label | translate }}</th> }
                            <th class="w-8"></th>
                          </tr>
                        </thead>
                        <tbody>
                          @for (r of rowsOf(item, f.key); track $index; let i = $index) {
                            <tr class="border-t border-gray-100">
                              @for (sf of f.rowFields ?? []; track sf.key) {
                                <td class="px-1.5 py-1">
                                  @if (sf.type === 'select') {
                                    <select class="input py-1 min-w-[10rem]" [name]="f.key + '-' + i + '-' + sf.key" [(ngModel)]="r[sf.key]" [attr.aria-label]="sf.label | translate">
                                      @for (o of rowOptions(f, sf); track o.value) { <option [ngValue]="o.value">{{ o.label | translate }}</option> }
                                    </select>
                                  } @else if (sf.type === 'text') {
                                    <input type="text" class="input py-1 font-mono min-w-[3.5rem]" [attr.maxlength]="sf.maxLength ?? null" [name]="f.key + '-' + i + '-' + sf.key"
                                           [(ngModel)]="r[sf.key]" [attr.aria-label]="sf.label | translate" />
                                  } @else {
                                    <input type="number" min="0" step="1" class="input py-1 min-w-[4.5rem]" [name]="f.key + '-' + i + '-' + sf.key"
                                           [(ngModel)]="r[sf.key]" [attr.aria-label]="sf.label | translate" />
                                  }
                                </td>
                              }
                              <td class="px-1 text-center">
                                <button type="button" class="text-gray-400 hover:text-red-600" [attr.aria-label]="'COMMON.DELETE' | translate" (click)="removeRow(item, f.key, i)">
                                  <span class="material-icons text-[18px] align-middle">close</span></button>
                              </td>
                            </tr>
                          } @empty {
                            <tr><td [attr.colspan]="(f.rowFields?.length ?? 0) + 1" class="px-2 py-2 text-xs text-gray-400">{{ 'COMMON.NO_ROWS' | translate }}</td></tr>
                          }
                        </tbody>
                      </table>
                    </div>
                    <button type="button" class="text-blue-600 hover:underline text-sm mt-1.5" (click)="addRow(item, f)">
                      <span class="material-icons text-[16px] align-middle">add</span> {{ (f.addLabel ?? 'COMMON.ADD_ROW') | translate }}</button>
                  }
                  @case ('status') {
                    <select [id]="'f-' + f.key" [name]="f.key" class="input" [(ngModel)]="item[f.key]">
                      <option [ngValue]="2">{{ 'COMMON.ACTIVE' | translate }}</option>
                      <option [ngValue]="1">{{ 'COMMON.STATUS_INACTIVE' | translate }}</option>
                    </select>
                  }
                  @default {
                    <input [id]="'f-' + f.key" [name]="f.key" type="text" class="input" [(ngModel)]="item[f.key]"
                           [placeholder]="(f.placeholder ?? '') | translate" [required]="!!f.required" [disabled]="locked(f, item)" />
                  }
                }
              }
              @if (f.hint) { <p class="text-xs text-gray-500 mt-1">{{ f.hint | translate }}</p> }
            </div>
          }
        </form>
        <ng-container footer>
          <button type="button" (click)="editing.set(null)" class="btn-secondary">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="submit" form="crud-form" [disabled]="saving()" class="btn-primary">
            <span class="material-icons text-[16px]" [class.animate-spin]="saving()">{{ saving() ? 'autorenew' : 'save' }}</span>
            {{ (item.publicId ? 'COMMON.UPDATE' : 'COMMON.SAVE') | translate }}
          </button>
        </ng-container>
      </app-modal>
    }

    @if (importing()) {
      <app-import-dialog [title]="c.title" [resource]="c.resource" [service]="c.service ?? 'tenant'" (closed)="importing.set(false)" (imported)="search()" />
    }

    @if (paying(); as p) {
      <app-payment-dialog [sourceService]="c.service ?? 'circulation'" [sourceId]="p.publicId" (closed)="paying.set(null)" (paid)="search()" />
    }

    @if (deleting().length) {
      <app-confirm-delete [busy]="saving()"
                          [message]="deleting().length > 1 ? 'COMMON.DELETE_SELECTED_CONFIRM' : 'COMMON.DELETE_CONFIRM_IRREVERSIBLE'" [messageParams]="{ count: deleting().length }"
                          (confirmed)="doDelete()" (cancelled)="deleting.set([])" />
    }
  `,
})
export class CrudPage implements OnInit {
  readonly config = input.required<CrudConfig>();

  private readonly api = inject(Api);
  protected readonly session = inject(Session);
  protected readonly paying = signal<Row | null>(null);
  private readonly toastr = inject(ToastrService);
  protected readonly language = inject(Language);
  private client!: CrudClient<Row>;

  protected keyword = '';
  protected status: number | null = null;
  protected pageIndex = 1;
  protected pageSize = 10;

  protected readonly page = signal<Page<Row> | null>(null);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly busyRow = signal<string | null>(null);
  protected readonly selected = signal<Set<string>>(new Set());
  protected readonly editing = signal<Row | null>(null);
  protected readonly deleting = signal<string[]>([]);
  protected readonly importing = signal(false);
  private readonly loadedOptions = signal<Record<string, { value: string | number | null; label: string }[]>>({});

  protected readonly colspan = computed(() => this.config().columns.length + (this.config().hasStatus ? 4 : 3));
  protected readonly allSelected = computed(() => {
    const items = this.page()?.items ?? [];
    return items.length > 0 && items.every((r) => this.selected().has(r.publicId));
  });

  ngOnInit(): void {
    this.client = this.api.crud<Row>(this.config().resource, this.config().service);
    void this.load();
    void this.loadOptions();
  }

  protected optionsOf(field: CrudField): { value: string | number | null; label: string }[] {
    return this.loadedOptions()[field.key] ?? field.options ?? [];
  }

  protected rowsOf(item: Row, key: string): Record<string, unknown>[] {
    return Array.isArray(item[key]) ? (item[key] as Record<string, unknown>[]) : [];
  }

  protected rowOptions(field: CrudField, sub: CrudRowField): { value: string | number | null; label: string }[] {
    return this.loadedOptions()[`${field.key}.${sub.key}`] ?? sub.options ?? [];
  }

  protected addRow(item: Row, field: CrudField): void {
    const row: Record<string, unknown> = {};
    for (const sf of field.rowFields ?? []) row[sf.key] = null;
    item[field.key] = [...this.rowsOf(item, field.key), row];
  }

  protected removeRow(item: Row, key: string, index: number): void {
    item[key] = this.rowsOf(item, key).filter((_, i) => i !== index);
  }

  protected isChecked(item: Row, key: string, value: unknown): boolean {
    return Array.isArray(item[key]) && (item[key] as unknown[]).includes(value);
  }

  protected toggleValue(item: Row, key: string, value: unknown): void {
    const current = Array.isArray(item[key]) ? (item[key] as unknown[]) : [];
    item[key] = current.includes(value) ? current.filter((v) => v !== value) : [...current, value];
  }

  protected optionLabel(key: string, value: unknown): string {
    const field = this.config().fields.find((f) => f.key === key);
    if (Array.isArray(value)) return value.length ? value.map((v) => this.optionLabel(key, v)).join(', ') : (field?.emptyLabel ?? '—');
    if (value == null || value === '') return field?.emptyLabel ?? '—';
    return (field ? this.optionsOf(field) : []).find((o) => o.value === value)?.label ?? String(value);
  }

  private async loadOptions(): Promise<void> {
    for (const field of this.config().fields.filter((f) => f.optionsFrom)) {
      try {
        const options = await field.optionsFrom!(this.api);
        this.loadedOptions.update((all) => ({ ...all, [field.key]: [{ value: null, label: field.emptyLabel ?? '—' }, ...options] }));
      } catch {
        /* thiếu quyền xem danh mục liên quan — ô chọn để trống */
      }
    }
    for (const field of this.config().fields.filter((f) => f.type === 'rows')) {
      for (const sub of (field.rowFields ?? []).filter((s) => s.optionsFrom)) {
        try {
          const options = await sub.optionsFrom!(this.api);
          this.loadedOptions.update((all) => ({ ...all, [`${field.key}.${sub.key}`]: [{ value: null, label: sub.emptyLabel ?? '—' }, ...options] }));
        } catch {
          /* thiếu quyền xem danh mục liên quan */
        }
      }
    }
  }

  protected can(action: string): boolean {
    return this.session.can(`${this.config().perm}:${action}`);
  }

  protected locked(field: CrudField, item: Row): boolean {
    return (!!field.lockOnEdit && !!item.publicId) || (!!field.lockWhen && !!item.publicId && field.lockWhen(item));
  }

  protected search(): void {
    this.pageIndex = 1;
    this.selected.set(new Set());
    void this.load();
  }

  protected onPage(e: { pageIndex: number; pageSize: number }): void {
    this.pageIndex = e.pageIndex;
    this.pageSize = e.pageSize;
    void this.load();
  }

  protected toggle(row: Row): void {
    const next = new Set(this.selected());
    if (next.has(row.publicId)) next.delete(row.publicId);
    else next.add(row.publicId);
    this.selected.set(next);
  }

  protected toggleAll(): void {
    this.selected.set(this.allSelected() ? new Set() : new Set((this.page()?.items ?? []).map((r) => r.publicId)));
  }

  protected async openAdd(): Promise<void> {
    let extra: Record<string, unknown> = {};
    try {
      extra = (await this.config().defaultsFrom?.(this.api)) ?? {};
    } catch {
      /* không lấy được mẫu — form trống */
    }
    const blank: Row = { publicId: '', id: 0, ...(this.config().hasStatus ? { status: STATUS_ACTIVE } : {}), ...structuredClone(this.config().defaults ?? {}), ...extra };
    this.editing.set(blank);
  }

  protected async openEdit(row: Row): Promise<void> {
    try {
      this.editing.set({ ...(await this.client.get(row.publicId)) });
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected async save(): Promise<void> {
    const item = this.editing();
    if (!item) return;
    const missing = this.config().fields.find((f) => f.required && (item[f.key] === undefined || item[f.key] === null || String(item[f.key]).trim() === ''));
    if (missing) {
      this.toastr.warning('COMMON.REQUIRED_FIELD', { field: this.language.t(missing.label) });
      return;
    }
    this.saving.set(true);
    try {
      const { publicId, id: _id, ...body } = item;
      for (const f of this.config().fields)
        if ((f.type === 'password' || f.type === 'date') && (body[f.key] === undefined || body[f.key] === '')) body[f.key] = null;
      if (publicId) await this.client.update(publicId, body);
      else await this.client.add(body);
      this.toastr.success(publicId ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS');
      this.editing.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }

  private notify(m: CrudMessage): void {
    if (typeof m === 'string') this.toastr.success(m);
    else this.toastr.success(m.key, m.params);
  }

  /** Nút trên dòng có ô nhập: mở hộp thoại, bấm chạy mới gọi run. */
  protected readonly actionDialog = signal<{ action: CrudRowAction; row: Row; values: Record<string, string> } | null>(null);

  protected async submitRowAction(): Promise<void> {
    const d = this.actionDialog();
    if (!d) return;
    await this.runRowAction(d.action, d.row, d.values);
  }

  protected async runRowAction(action: CrudRowAction, row: Row, values?: Record<string, string>): Promise<void> {
    if (action.payment) {
      this.paying.set(row);
      return;
    }
    if (action.inputs?.length && !values) {
      this.actionDialog.set({ action, row, values: {} });
      return;
    }
    this.busyRow.set(row.publicId);
    try {
      this.notify(await action.run(this.api, row, values ?? {}));
      this.actionDialog.set(null);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busyRow.set(null);
    }
  }

  protected async flipStatus(row: Row): Promise<void> {
    const next = row['status'] === STATUS_ACTIVE ? 1 : STATUS_ACTIVE;
    try {
      await this.client.changeStatus(row.publicId, next);
      this.toastr.success('COMMON.STATUS_CHANGED');
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected async runTool(tool: CrudToolbarAction): Promise<void> {
    this.saving.set(true);
    try {
      this.notify(await tool.run(this.api));
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }

  protected confirmOne(row: Row): void {
    this.deleting.set([row.publicId]);
  }

  protected confirmBulk(): void {
    this.deleting.set([...this.selected()]);
  }

  protected async doDelete(): Promise<void> {
    this.saving.set(true);
    const ids = this.deleting();
    const results = await Promise.allSettled(ids.map((id) => this.client.delete(id)));
    const failed = results.filter((r): r is PromiseRejectedResult => r.status === 'rejected');
    if (failed.length === 0) this.toastr.success(ids.length > 1 ? 'COMMON.DELETED_N' : 'COMMON.DELETE_SUCCESS', { count: ids.length });
    else this.toastr.error('COMMON.DELETE_PARTIAL_FAILED', { failed: failed.length, total: ids.length, error: errorMessage(failed[0].reason) });
    this.saving.set(false);
    this.deleting.set([]);
    this.selected.set(new Set());
    await this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.page.set(await this.client.search({ keyword: this.keyword.trim() || undefined, status: this.status, pageIndex: this.pageIndex, pageSize: this.pageSize }));
    } catch (e) {
      this.toastr.error(errorMessage(e));
      this.page.set({ items: [], totalCount: 0, pageIndex: 1, pageSize: this.pageSize, totalPages: 0 });
    } finally {
      this.loading.set(false);
    }
  }
}
