import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api, CrudClient, CrudPage as Page, STATUS_ACTIVE, errorMessage } from '../core/api';
import { Session } from '../core/session';
import { ToastrService } from './toastr';
import { ConfirmDelete, Loading, Modal, Paginator, StatusBadge } from './ui';

export interface CrudColumn {
  key: string;
  label: string;
  type?: 'text' | 'number' | 'status' | 'bool' | 'mono';
  width?: string;
}

export interface CrudField {
  key: string;
  label: string;
  type: 'text' | 'number' | 'textarea' | 'status' | 'checkbox';
  required?: boolean;
  placeholder?: string;
  hint?: string;
  /** Không sửa được khi chỉnh sửa (mã). */
  lockOnEdit?: boolean;
  /** Không sửa được khi bản ghi thoả điều kiện (ví dụ tham số có sẵn của hệ thống). */
  lockWhen?: (item: Row) => boolean;
  half?: boolean;
}

/** Cấu hình một màn danh mục — truyền qua data của route. */
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
  modalWidth?: string;
  /** Huy hiệu thêm sau cột đầu tiên (ví dụ "Hệ thống" cho tham số có sẵn). */
  badge?: (item: Row) => string | null;
  /** Nút thêm trên thanh công cụ (ví dụ "Khôi phục mẫu mặc định"). */
  toolbar?: CrudToolbarAction[];
}

export interface CrudToolbarAction {
  label: string;
  icon: string;
  /** Hành động quyền cần có trên module của màn (add/edit…). */
  action: string;
  /** Trả về thông báo thành công; danh sách tải lại sau khi chạy. */
  run: (api: Api) => Promise<string>;
}

type Row = Record<string, unknown> & { publicId: string; id: number };

/**
 * Màn danh mục chuẩn — giao diện như pages/admin/shared/base-entity của frontend monolith: thẻ tìm kiếm, thanh nút
 * Thêm / Xoá mục đã chọn, bảng có ô chọn + ID + cột dữ liệu + thao tác, phân trang, modal thêm/sửa, xác nhận xoá.
 */
@Component({
  selector: 'app-crud-page',
  imports: [FormsModule, DecimalPipe, Paginator, Modal, ConfirmDelete, Loading, StatusBadge],
  template: `
    @let c = config();
    <div class="mb-5 flex flex-col sm:flex-row justify-between items-start sm:items-center">
      <h4 class="page-title">{{ c.title }}</h4>
    </div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
        <div>
          <label for="kw" class="field-label">{{ c.searchLabel }}</label>
          <input id="kw" type="text" class="input" [(ngModel)]="keyword" (keydown.enter)="search()" [placeholder]="c.searchPlaceholder" />
        </div>
        @if (c.hasStatus) {
          <div>
            <label for="st" class="field-label">Trạng thái</label>
            <select id="st" class="input" [(ngModel)]="status" (change)="search()">
              <option [ngValue]="null">Tất cả</option>
              <option [ngValue]="2">Hoạt động</option>
              <option [ngValue]="1">Không hoạt động</option>
            </select>
          </div>
        } @else {
          <div class="hidden xl:block"></div>
        }
        <div class="hidden xl:block"></div>
        <div class="flex items-end">
          <button (click)="search()" class="btn-primary w-full">
            <span class="material-icons text-[18px]">search</span> Tìm kiếm
          </button>
        </div>
      </div>
    </div>

    <div class="panel flex gap-3 items-center justify-between flex-wrap">
      <div class="flex gap-2">
        @if (can('add')) {
          <button (click)="openAdd()" class="btn-add"><span class="material-icons text-[18px]">add</span> Thêm mới</button>
        }
        @if (can('delete')) {
          <button (click)="confirmBulk()" [disabled]="selected().size === 0" class="btn-outline-danger">
            <span class="material-icons text-[18px]">delete</span> Xoá mục đã chọn
          </button>
        }
        @for (t of c.toolbar ?? []; track t.label) {
          @if (can(t.action)) {
            <button (click)="runTool(t)" [disabled]="saving()" class="btn-secondary !py-1.5 !px-3">
              <span class="material-icons text-[18px]">{{ t.icon }}</span> {{ t.label }}
            </button>
          }
        }
      </div>
      <span class="text-sm text-gray-500">{{ page()?.totalCount ?? 0 }} bản ghi</span>
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
              @for (col of c.columns; track col.key) { <th class="th" [style.width]="col.width">{{ col.label }}</th> }
              @if (c.hasStatus) { <th class="th w-36 !text-center">Trạng thái</th> }
              <th class="th w-28 !text-center">Thao tác</th>
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
                      @case ('number') { {{ $any(row[col.key]) | number: '1.0-4' }} }
                      @case ('bool') {
                        @if (row[col.key]) { <span class="badge-info">Có</span> } @else { <span class="text-gray-300">—</span> }
                      }
                      @case ('mono') { <span class="font-mono text-[13px]">{{ row[col.key] }}</span> }
                      @default { <span class="line-clamp-2 break-all">{{ row[col.key] ?? '—' }}</span> }
                    }
                    @if (first && c.badge?.(row); as b) { <span class="ml-2 text-[11px] text-blue-600 bg-blue-50 px-1.5 py-0.5 rounded whitespace-nowrap">{{ b }}</span> }
                  </td>
                }
                @if (c.hasStatus) {
                  <td class="td text-center" (click)="$event.stopPropagation()">
                    <button type="button" [disabled]="!can('edit')" (click)="flipStatus(row)" title="Bấm để đổi trạng thái" class="disabled:cursor-default">
                      <app-status-badge [status]="$any(row['status'])" />
                    </button>
                  </td>
                }
                <td class="td">
                  <div class="flex items-center justify-center space-x-2">
                    @if (can('edit')) {
                      <button (click)="openEdit(row); $event.stopPropagation()" class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" title="Sửa">
                        <span class="material-icons text-[18px]">edit</span>
                      </button>
                    }
                    @if (can('delete')) {
                      <button (click)="confirmOne(row); $event.stopPropagation()" class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" title="Xoá">
                        <span class="material-icons text-[18px]">delete</span>
                      </button>
                    }
                  </div>
                </td>
              </tr>
            } @empty {
              <tr><td class="py-8 px-4 text-center text-gray-500" [attr.colspan]="colspan()">{{ loading() ? '' : 'Không có dữ liệu' }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (page(); as p) {
        <app-paginator [total]="p.totalCount" [pageIndex]="pageIndex" [pageSize]="pageSize" (pageChange)="onPage($event)" />
      }
    </div>

    @if (editing(); as item) {
      <app-modal [title]="item.publicId ? 'Cập nhật ' + c.title.toLowerCase() : 'Thêm mới ' + c.title.toLowerCase()" [widthClass]="c.modalWidth ?? 'max-w-md'" (closed)="editing.set(null)">
        <form id="crud-form" (ngSubmit)="save()" class="grid grid-cols-2 gap-4">
          @for (f of c.fields; track f.key) {
            <div [class.col-span-2]="!f.half">
              @if (f.type === 'checkbox') {
                <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer select-none">
                  <input type="checkbox" class="rounded border-gray-300" [name]="f.key" [(ngModel)]="item[f.key]" [disabled]="locked(f, item)" />
                  {{ f.label }}
                </label>
              } @else {
                <label class="form-label" [for]="'f-' + f.key">{{ f.label }} @if (f.required) { <span class="text-red-500">*</span> }</label>
                @switch (f.type) {
                  @case ('textarea') {
                    <textarea [id]="'f-' + f.key" [name]="f.key" rows="5" class="input font-mono text-[13px]" [(ngModel)]="item[f.key]"
                              [placeholder]="f.placeholder ?? ''" [disabled]="locked(f, item)"></textarea>
                  }
                  @case ('number') {
                    <input [id]="'f-' + f.key" [name]="f.key" type="number" step="any" class="input" [(ngModel)]="item[f.key]"
                           [placeholder]="f.placeholder ?? ''" [required]="!!f.required" [disabled]="locked(f, item)" />
                  }
                  @case ('status') {
                    <select [id]="'f-' + f.key" [name]="f.key" class="input" [(ngModel)]="item[f.key]">
                      <option [ngValue]="2">Hoạt động</option>
                      <option [ngValue]="1">Không hoạt động</option>
                    </select>
                  }
                  @default {
                    <input [id]="'f-' + f.key" [name]="f.key" type="text" class="input" [(ngModel)]="item[f.key]"
                           [placeholder]="f.placeholder ?? ''" [required]="!!f.required" [disabled]="locked(f, item)" />
                  }
                }
              }
              @if (f.hint) { <p class="text-xs text-gray-500 mt-1">{{ f.hint }}</p> }
            </div>
          }
        </form>
        <ng-container footer>
          <button type="button" (click)="editing.set(null)" class="btn-secondary">Huỷ</button>
          <button type="submit" form="crud-form" [disabled]="saving()" class="btn-primary">
            <span class="material-icons text-[16px]" [class.animate-spin]="saving()">{{ saving() ? 'autorenew' : 'save' }}</span>
            {{ item.publicId ? 'Cập nhật' : 'Lưu' }}
          </button>
        </ng-container>
      </app-modal>
    }

    @if (deleting().length) {
      <app-confirm-delete [busy]="saving()"
                          [message]="deleting().length > 1 ? 'Xoá ' + deleting().length + ' mục đã chọn? Thao tác này không thể hoàn tác.' : 'Bạn có chắc chắn muốn xoá mục này? Thao tác này không thể hoàn tác.'"
                          (confirmed)="doDelete()" (cancelled)="deleting.set([])" />
    }
  `,
})
export class CrudPage implements OnInit {
  readonly config = input.required<CrudConfig>();

  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
  private client!: CrudClient<Row>;

  protected keyword = '';
  protected status: number | null = null;
  protected pageIndex = 1;
  protected pageSize = 10;

  protected readonly page = signal<Page<Row> | null>(null);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly selected = signal<Set<string>>(new Set());
  protected readonly editing = signal<Row | null>(null);
  protected readonly deleting = signal<string[]>([]);

  protected readonly colspan = computed(() => this.config().columns.length + (this.config().hasStatus ? 4 : 3));
  protected readonly allSelected = computed(() => {
    const items = this.page()?.items ?? [];
    return items.length > 0 && items.every((r) => this.selected().has(r.publicId));
  });

  ngOnInit(): void {
    this.client = this.api.crud<Row>(this.config().resource, this.config().service);
    void this.load();
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

  protected openAdd(): void {
    const blank: Row = { publicId: '', id: 0, ...(this.config().hasStatus ? { status: STATUS_ACTIVE } : {}), ...this.config().defaults };
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
      this.toastr.warning(`Vui lòng nhập ${missing.label.toLowerCase()}.`);
      return;
    }
    this.saving.set(true);
    try {
      const { publicId, id: _id, ...body } = item;
      if (publicId) await this.client.update(publicId, body);
      else await this.client.add(body);
      this.toastr.success(publicId ? 'Cập nhật thành công' : 'Thêm mới thành công');
      this.editing.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }

  protected async flipStatus(row: Row): Promise<void> {
    const next = row['status'] === STATUS_ACTIVE ? 1 : STATUS_ACTIVE;
    try {
      await this.client.changeStatus(row.publicId, next);
      this.toastr.success('Đã đổi trạng thái');
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  protected async runTool(tool: CrudToolbarAction): Promise<void> {
    this.saving.set(true);
    try {
      this.toastr.success(await tool.run(this.api));
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
    if (failed.length === 0) this.toastr.success(ids.length > 1 ? `Đã xoá ${ids.length} mục` : 'Xoá thành công');
    else this.toastr.error(`${failed.length}/${ids.length} mục không xoá được: ${errorMessage(failed[0].reason)}`);
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
