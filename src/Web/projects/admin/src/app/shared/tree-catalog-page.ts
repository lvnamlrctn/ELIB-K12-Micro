import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, STATUS_ACTIVE, errorMessage } from '../core/api';
import { Session } from '../core/session';
import { CollectionPermissions } from './collection-permissions';
import { ToastrService } from './toastr';
import { ConfirmDelete, Loading, Modal, StatusBadge } from './ui';

export interface TreeExtraField {
  key: string;
  label: string;
  type: 'text' | 'textarea' | 'checkbox' | 'select';
  options?: { value: string | number | null; label: string }[];
  placeholder?: string;
  half?: boolean;
}

/** Cấu hình một danh mục cây tài liệu số (bộ sưu tập/môn loại/chủ đề) — truyền qua data của route. */
export interface TreeCatalogConfig {
  title: string;
  resource: string;
  service: string;
  perm: string;
  extraFields: TreeExtraField[];
  defaults?: Record<string, unknown>;
  /** Hiện ô tải ảnh bìa công khai (chỉ bộ sưu tập dùng) — purpose cố định 'ebook-cover'. */
  coverUpload?: boolean;
  /** Nút "Phân quyền" theo loại bạn đọc (chỉ bộ sưu tập) — hộp thoại đọc/ghi qua collections/{id}/permissions. */
  permissions?: boolean;
  modalWidth?: string;
}

type RawRow = { id: number; publicId: string; name: string; parentId: number | null; sortOrder: number; status: number; [extra: string]: unknown };
interface TreeNode extends RawRow { level: number; children: TreeNode[]; childCount: number; }
interface FlatNode { node: TreeNode; depth: number; }

interface TreeForm {
  publicId: string | null;
  id: number | null;
  name: string;
  parentId: number | null;
  sortOrder: number;
  status: number;
  [key: string]: unknown;
}

/**
 * Danh mục cây dùng chung cho bộ sưu tập/môn loại/chủ đề tài liệu số — giao diện như pages/tenant/orgs.ts (cây thụt lề, chọn cha
 * bằng listbox), vì CrudPage (shared/crud-page.ts) không có chỗ cho quan hệ cha/con. Trường riêng của từng danh mục khai báo qua
 * config().extraFields (text/textarea/checkbox/select tĩnh — không cần optionsFrom vì chỉ còn ngôn ngữ/mô tả/cờ bật-tắt).
 */
@Component({
  selector: 'app-tree-catalog-page',
  imports: [CollectionPermissions, FormsModule, Modal, ConfirmDelete, Loading, StatusBadge, TranslatePipe],
  template: `
    @let c = config();
    <div class="mb-5"><h4 class="page-title">{{ c.title | translate }}</h4></div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4 items-end">
        <div class="xl:col-span-2">
          <input type="text" class="input" [(ngModel)]="keyword" (input)="onSearch()" [placeholder]="'DIGITAL_CAT.SEARCH_PH' | translate" />
        </div>
        <div class="flex gap-2">
          <button (click)="setAll(true)" class="btn flex-1 bg-gray-100 hover:bg-gray-200 text-gray-700 py-2 px-3">
            <span class="material-icons text-[16px]">unfold_more</span>{{ 'DIGITAL_CAT.EXPAND_ALL' | translate }}
          </button>
          <button (click)="setAll(false)" class="btn flex-1 bg-gray-100 hover:bg-gray-200 text-gray-700 py-2 px-3">
            <span class="material-icons text-[16px]">unfold_less</span>{{ 'DIGITAL_CAT.COLLAPSE_ALL' | translate }}
          </button>
        </div>
        @if (can('add')) {
          <button (click)="openCreate(null)" class="btn-add !py-2"><span class="material-icons text-[16px]">add</span>{{ 'DIGITAL_CAT.ADD_ROOT' | translate }}</button>
        }
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse min-w-[760px]">
          <thead>
            <tr>
              <th class="th">{{ 'COMMON.NAME' | translate }}</th>
              <th class="th w-24 !text-center">{{ 'DIGITAL_CAT.LEVEL' | translate }}</th>
              <th class="th w-24 !text-center">{{ 'DIGITAL_CAT.SORT_ORDER' | translate }}</th>
              <th class="th w-36 !text-center">{{ 'COMMON.STATUS' | translate }}</th>
              <th class="th w-24 !text-center">{{ 'DIGITAL_CAT.CHILDREN' | translate }}</th>
              <th class="th w-32 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
            </tr>
          </thead>
          <tbody>
            @for (row of visible(); track row.node.publicId) {
              @let n = row.node;
              <tr class="hover:bg-gray-50/60 transition-colors border-b border-gray-100">
                <td class="py-2.5 px-4">
                  <div class="flex items-center" [style.paddingLeft.px]="row.depth * 22">
                    @if (n.children.length) {
                      <button (click)="toggle(n)" class="w-5 h-5 flex items-center justify-center mr-1 text-gray-400 hover:text-blue-600 transition-transform duration-150" [class.rotate-90]="isExpanded(n)">
                        <span class="material-icons text-[16px] leading-none">chevron_right</span>
                      </button>
                    } @else {
                      <span class="w-5 h-5 mr-1 inline-block shrink-0"></span>
                    }
                    <span class="material-icons text-[16px] mr-1.5 shrink-0" [style.color]="row.depth === 0 ? '#3b82f6' : n.children.length ? '#60a5fa' : '#9ca3af'">
                      {{ row.depth === 0 ? 'folder' : n.children.length ? 'folder_open' : 'article' }}
                    </span>
                    <span class="text-sm text-gray-800 font-medium">{{ n.name }}</span>
                  </div>
                </td>
                <td class="py-2.5 px-4 text-center">
                  <span class="text-xs px-2 py-0.5 rounded-full font-medium" [class]="row.depth === 0 ? 'bg-blue-100 text-blue-700' : row.depth === 1 ? 'bg-purple-100 text-purple-700' : 'bg-green-100 text-green-700'">
                    {{ 'DIGITAL_CAT.LEVEL_N' | translate: { level: row.depth + 1 } }}
                  </span>
                </td>
                <td class="py-2.5 px-4 text-center text-sm text-gray-600">{{ n.sortOrder }}</td>
                <td class="py-2.5 px-4 text-center"><app-status-badge [status]="n.status" inactiveText="DIGITAL_CAT.STATUS_HIDDEN" /></td>
                <td class="py-2.5 px-4 text-center">
                  @if (n.childCount > 0) { <span class="text-xs px-2 py-0.5 bg-gray-100 text-gray-600 rounded-full">{{ n.childCount }}</span> } @else { <span class="text-gray-300 text-xs">—</span> }
                </td>
                <td class="py-2.5 px-4">
                  <div class="flex items-center justify-center gap-1">
                    @if (can('add')) {
                      <button (click)="openCreate(n)" class="w-7 h-7 rounded-lg bg-emerald-50 text-emerald-600 hover:bg-emerald-100 flex items-center justify-center" [title]="'DIGITAL_CAT.ADD_CHILD' | translate">
                        <span class="material-icons text-[15px] leading-none">add</span>
                      </button>
                    }
                    @if (c.permissions && can('edit')) {
                      <button (click)="permissionsFor.set(n)" class="w-7 h-7 rounded-lg bg-purple-50 text-purple-600 hover:bg-purple-100 flex items-center justify-center" [title]="'DIGITAL_DOC.PERMISSION_BTN' | translate">
                        <span class="material-icons text-[15px] leading-none">manage_accounts</span>
                      </button>
                    }
                    @if (can('edit')) {
                      <button (click)="openEdit(n)" class="w-7 h-7 rounded-lg bg-blue-50 text-blue-600 hover:bg-blue-100 flex items-center justify-center" [title]="'COMMON.EDIT' | translate">
                        <span class="material-icons text-[15px] leading-none">edit</span>
                      </button>
                    }
                    @if (can('delete')) {
                      <button (click)="deleting.set(n)" class="w-7 h-7 rounded-lg bg-red-50 text-red-600 hover:bg-red-100 flex items-center justify-center" [title]="'COMMON.DELETE' | translate">
                        <span class="material-icons text-[15px] leading-none">delete</span>
                      </button>
                    }
                  </div>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="py-14 text-center text-gray-500 text-sm">{{ loading() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr>
            }
          </tbody>
        </table>
      </div>
    </div>

    @if (form(); as f) {
      <app-modal [title]="f.publicId ? 'COMMON.EDIT' : 'COMMON.ADD_BTN'" [widthClass]="c.modalWidth ?? 'max-w-xl'" (closed)="form.set(null)">
        <form id="tree-cat-form" (ngSubmit)="save()" class="space-y-4">
          <div>
            <label class="form-label" for="tc-parent">{{ 'DIGITAL_CAT.PARENT' | translate }}</label>
            <select id="tc-parent" name="parent" size="6" class="input !p-2 h-32" [(ngModel)]="f.parentId">
              <option [ngValue]="null">{{ 'DIGITAL_CAT.ROOT_OPTION' | translate }}</option>
              @for (opt of parentOptions(); track opt.id) { <option [ngValue]="opt.id" [disabled]="opt.blocked">{{ opt.label }}</option> }
            </select>
          </div>
          <div>
            <label class="form-label" for="tc-name">{{ 'COMMON.NAME' | translate }} <span class="text-red-500">*</span></label>
            <input id="tc-name" name="name" class="input" [(ngModel)]="f.name" required />
          </div>
          <div class="grid grid-cols-2 gap-4">
            <div>
              <label class="form-label" for="tc-order">{{ 'DIGITAL_CAT.SORT_ORDER' | translate }}</label>
              <input id="tc-order" name="order" type="number" class="input" [(ngModel)]="f.sortOrder" />
            </div>
            <div>
              <label class="form-label" for="tc-status">{{ 'COMMON.STATUS' | translate }}</label>
              <select id="tc-status" name="status" class="input" [(ngModel)]="f.status">
                <option [ngValue]="2">{{ 'COMMON.ACTIVE' | translate }}</option>
                <option [ngValue]="1">{{ 'DIGITAL_CAT.STATUS_HIDDEN' | translate }}</option>
              </select>
            </div>
          </div>

          @for (xf of c.extraFields; track xf.key) {
            <div>
              @if (xf.type === 'checkbox') {
                <label class="flex items-center gap-2 text-sm text-gray-700 cursor-pointer select-none">
                  <input type="checkbox" class="rounded border-gray-300" [name]="xf.key" [(ngModel)]="f[xf.key]" />
                  {{ xf.label | translate }}
                </label>
              } @else {
                <label class="form-label" [for]="'tc-' + xf.key">{{ xf.label | translate }}</label>
                @switch (xf.type) {
                  @case ('textarea') {
                    <textarea [id]="'tc-' + xf.key" [name]="xf.key" rows="3" class="input" [(ngModel)]="f[xf.key]" [placeholder]="(xf.placeholder ?? '') | translate"></textarea>
                  }
                  @case ('select') {
                    <select [id]="'tc-' + xf.key" [name]="xf.key" class="input" [(ngModel)]="f[xf.key]">
                      @for (o of xf.options ?? []; track o.value) { <option [ngValue]="o.value">{{ o.label | translate }}</option> }
                    </select>
                  }
                  @default {
                    <input [id]="'tc-' + xf.key" [name]="xf.key" type="text" class="input" [(ngModel)]="f[xf.key]" [placeholder]="(xf.placeholder ?? '') | translate" />
                  }
                }
              }
            </div>
          }

          @if (c.coverUpload) {
            <div>
              <label class="form-label">{{ 'DIGITAL_CAT.COVER' | translate }}</label>
              <div class="flex items-center gap-3">
                @if (f['coverUrl']) { <img [src]="f['coverUrl']" class="w-14 h-18 object-cover rounded border border-gray-200" referrerpolicy="no-referrer" alt="" /> }
                <input type="file" accept="image/png,image/jpeg,image/webp" (change)="onCoverSelect($event, f)" />
                @if (uploadingCover()) { <span class="material-icons animate-spin text-[18px] text-blue-600">autorenew</span> }
              </div>
            </div>
          }
        </form>
        <ng-container footer>
          <button type="button" (click)="form.set(null)" class="btn-secondary">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="submit" form="tree-cat-form" [disabled]="saving()" class="btn-primary">
            <span class="material-icons text-[16px]" [class.animate-spin]="saving()">{{ saving() ? 'autorenew' : 'save' }}</span> {{ 'COMMON.SAVE' | translate }}
          </button>
        </ng-container>
      </app-modal>
    }

    @if (permissionsFor(); as p) {
      <app-collection-permissions [collectionPublicId]="p.publicId" [collectionName]="p.name" (closed)="permissionsFor.set(null)" />
    }

    @if (deleting(); as d) {
      <app-confirm-delete [busy]="saving()" message="DIGITAL_CAT.DELETE_CONFIRM" [messageParams]="{ name: d.name }"
                          (confirmed)="doDelete(d)" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class TreeCatalogPage implements OnInit {
  readonly config = input.required<TreeCatalogConfig>();

  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);

  protected keyword = '';
  protected readonly tree = signal<TreeNode[]>([]);
  protected readonly expanded = signal<Set<number> | 'all'>('all');
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly uploadingCover = signal(false);
  protected readonly form = signal<TreeForm | null>(null);
  protected readonly deleting = signal<TreeNode | null>(null);
  protected readonly permissionsFor = signal<TreeNode | null>(null);

  private readonly flat = computed(() => {
    const out: FlatNode[] = [];
    const walk = (nodes: TreeNode[], depth: number) => nodes.forEach((n) => (out.push({ node: n, depth }), walk(n.children, depth + 1)));
    walk(this.tree(), 0);
    return out;
  });

  protected readonly visible = computed(() => {
    const out: FlatNode[] = [];
    const walk = (nodes: TreeNode[], depth: number) =>
      nodes.forEach((n) => {
        out.push({ node: n, depth });
        if (this.isExpanded(n)) walk(n.children, depth + 1);
      });
    walk(this.tree(), 0);
    return out;
  });

  protected readonly parentOptions = computed(() => {
    const f = this.form();
    const blocked = new Set<number>();
    if (f?.id) {
      const mark = (n: TreeNode) => (blocked.add(n.id), n.children.forEach(mark));
      const self = this.flat().find((x) => x.node.id === f.id);
      if (self) mark(self.node);
    }
    return this.flat().map((x) => ({ id: x.node.id, label: `${'    '.repeat(x.depth)}${x.depth ? '└ ' : ''}${x.node.name}`, blocked: blocked.has(x.node.id) }));
  });

  ngOnInit(): void {
    void this.load();
  }

  protected can(action: string): boolean {
    return this.session.can(`${this.config().perm}:${action}`);
  }

  protected isExpanded(n: TreeNode): boolean {
    const e = this.expanded();
    return e === 'all' || e.has(n.id);
  }

  protected toggle(n: TreeNode): void {
    const e = this.expanded();
    const next = new Set(e === 'all' ? this.flat().map((x) => x.node.id) : e);
    if (next.has(n.id)) next.delete(n.id);
    else next.add(n.id);
    this.expanded.set(next);
  }

  protected setAll(open: boolean): void {
    this.expanded.set(open ? 'all' : new Set());
  }

  protected onSearch(): void {
    const q = this.keyword.trim().toLowerCase();
    if (!q) {
      this.expanded.set('all');
      return;
    }
    const matchIds = new Set(this.flat().filter((x) => x.node.name.toLowerCase().includes(q)).map((x) => x.node.id));
    const next = new Set<number>();
    const addAncestors = (id: number) => {
      const self = this.flat().find((x) => x.node.id === id);
      if (self?.node.parentId) {
        next.add(self.node.parentId);
        addAncestors(self.node.parentId);
      }
    };
    matchIds.forEach((id) => addAncestors(id));
    this.expanded.set(next);
  }

  protected openCreate(parent: TreeNode | null): void {
    const extra: Record<string, unknown> = {};
    for (const xf of this.config().extraFields) extra[xf.key] = xf.type === 'checkbox' ? false : (this.config().defaults?.[xf.key] ?? null);
    if (this.config().coverUpload) extra['coverUrl'] = null;
    this.form.set({ publicId: null, id: null, name: '', parentId: parent?.id ?? null, sortOrder: (parent?.children.length ?? this.tree().length) + 1, status: STATUS_ACTIVE, ...extra });
  }

  protected openEdit(n: TreeNode): void {
    const extra: Record<string, unknown> = {};
    for (const xf of this.config().extraFields) extra[xf.key] = n[xf.key] ?? (xf.type === 'checkbox' ? false : null);
    if (this.config().coverUpload) extra['coverUrl'] = n['coverUrl'] ?? null;
    this.form.set({ publicId: n.publicId, id: n.id, name: n.name, parentId: n.parentId, sortOrder: n.sortOrder, status: n.status, ...extra });
  }

  protected async onCoverSelect(event: Event, f: TreeForm): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.uploadingCover.set(true);
    try {
      const uploaded = await this.api.upload('ebook-cover', file);
      f['coverUrl'] = uploaded.url;
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.uploadingCover.set(false);
    }
  }

  protected async save(): Promise<void> {
    const f = this.form();
    if (!f) return;
    if (!f.name.trim()) {
      this.toastr.warning('DIGITAL_CAT.NAME_REQUIRED');
      return;
    }
    this.saving.set(true);
    try {
      const body: Record<string, unknown> = { name: f.name, parentId: f.parentId, sortOrder: f.sortOrder, status: f.status };
      for (const xf of this.config().extraFields) body[xf.key] = f[xf.key];
      if (this.config().coverUpload) body['coverUrl'] = f['coverUrl'] ?? null;
      const client = this.api.crud<RawRow>(this.config().resource, this.config().service);
      if (f.publicId) await client.update(f.publicId, body);
      else await client.add(body);
      this.toastr.success(f.publicId ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS');
      this.form.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }

  protected async doDelete(n: TreeNode): Promise<void> {
    this.saving.set(true);
    try {
      await this.api.crud<RawRow>(this.config().resource, this.config().service).delete(n.publicId);
      this.toastr.success('COMMON.DELETE_SUCCESS');
      this.deleting.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      const rows = await this.api.crud<RawRow>(this.config().resource, this.config().service).searchAll({});
      this.tree.set(this.buildTree(rows));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  private buildTree(rows: RawRow[]): TreeNode[] {
    const byParent = new Map<number | null, RawRow[]>();
    for (const r of rows) {
      const key = r.parentId ?? null;
      const list = byParent.get(key);
      if (list) list.push(r);
      else byParent.set(key, [r]);
    }
    const build = (parentId: number | null): TreeNode[] =>
      (byParent.get(parentId) ?? [])
        .slice()
        .sort((a, b) => a.sortOrder - b.sortOrder || a.id - b.id)
        .map((r) => {
          const children = build(r.id);
          return { ...r, level: 0, children, childCount: children.length };
        });
    return build(null);
  }
}
