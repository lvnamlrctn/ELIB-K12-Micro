import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, OrgNode, STATUS_ACTIVE, errorMessage } from '../../core/api';
import { Language } from '../../core/i18n';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { ImportDialog } from '../../shared/import-dialog';
import { ConfirmDelete, Loading, Modal, StatusBadge } from '../../shared/ui';

interface FlatNode {
  node: OrgNode;
  depth: number;
}

interface OrgForm {
  publicId: string | null;
  id: number | null;
  name: string;
  parentId: number | null;
  sortOrder: number;
  status: number;
  link: string;
}

/** Phòng ban (cơ cấu tổ chức dạng cây) — giao diện như pages/admin/system/org của admin cũ. */
@Component({
  selector: 'app-orgs',
  imports: [FormsModule, Modal, ConfirmDelete, Loading, StatusBadge, ImportDialog, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'ORG.PAGE_TITLE' | translate }}</h4></div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4 items-end">
        <div class="xl:col-span-2">
          <input type="text" class="input" [(ngModel)]="keyword" (keydown.enter)="load()" [placeholder]="'ORG.SEARCH_ENTER' | translate" />
        </div>
        <div class="flex gap-2">
          <button (click)="setAll(true)" class="btn flex-1 bg-gray-100 hover:bg-gray-200 text-gray-700 py-2 px-3">
            <span class="material-icons text-[16px]">unfold_more</span>{{ 'ORG.EXPAND_ALL' | translate }}
          </button>
          <button (click)="setAll(false)" class="btn flex-1 bg-gray-100 hover:bg-gray-200 text-gray-700 py-2 px-3">
            <span class="material-icons text-[16px]">unfold_less</span>{{ 'ORG.COLLAPSE_ALL' | translate }}
          </button>
        </div>
        @if (can('add')) {
          <div class="flex gap-2">
            <button (click)="openCreate(null)" class="btn-add flex-1 !py-2"><span class="material-icons text-[16px]">add</span>{{ 'ORG.ADD_ROOT' | translate }}</button>
            <button (click)="importing.set(true)" class="btn-secondary !py-2 !px-3 bg-white" [title]="'ORG.IMPORT_TIP' | translate">
              <span class="material-icons text-[16px]">upload_file</span>{{ 'COMMON.IMPORT_EXCEL' | translate }}
            </button>
          </div>
        }
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse min-w-[760px]">
          <thead>
            <tr>
              <th class="th">{{ 'ORG.NAME' | translate }}</th>
              <th class="th w-20 !text-center">ID</th>
              <th class="th w-24 !text-center">{{ 'ORG.LEVEL' | translate }}</th>
              <th class="th w-24 !text-center">{{ 'ORG.ORDER' | translate }}</th>
              <th class="th w-36 !text-center">{{ 'COMMON.STATUS' | translate }}</th>
              <th class="th w-24 !text-center">{{ 'ORG.SUB_UNITS' | translate }}</th>
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
                    <span class="material-icons text-[16px] mr-1.5 shrink-0"
                          [style.color]="row.depth === 0 ? '#3b82f6' : n.children.length ? '#60a5fa' : '#9ca3af'">
                      {{ row.depth === 0 ? 'corporate_fare' : n.children.length ? 'account_tree' : 'apartment' }}
                    </span>
                    <span class="text-sm text-gray-800 font-medium">{{ n.name }}</span>
                  </div>
                </td>
                <!-- Số id — điền cột Đơn vị khi nhập bạn đọc "theo mã" (như ELIB-LRC). -->
                <td class="py-2.5 px-4 text-center font-mono text-xs text-gray-500">{{ n.id }}</td>
                <td class="py-2.5 px-4 text-center">
                  <span class="text-xs px-2 py-0.5 rounded-full font-medium"
                        [class]="row.depth === 0 ? 'bg-blue-100 text-blue-700' : row.depth === 1 ? 'bg-purple-100 text-purple-700' : 'bg-green-100 text-green-700'">
                    {{ 'ORG.LEVEL_N' | translate: { level: n.level } }}
                  </span>
                </td>
                <td class="py-2.5 px-4 text-center text-sm text-gray-600">{{ n.sortOrder }}</td>
                <td class="py-2.5 px-4 text-center"><app-status-badge [status]="n.status" inactiveText="ORG.STATUS_HIDDEN" /></td>
                <td class="py-2.5 px-4 text-center">
                  @if (n.childCount > 0) {
                    <span class="text-xs px-2 py-0.5 bg-gray-100 text-gray-600 rounded-full">{{ n.childCount }}</span>
                  } @else {
                    <span class="text-gray-300 text-xs">—</span>
                  }
                </td>
                <td class="py-2.5 px-4">
                  <div class="flex items-center justify-center gap-1">
                    @if (can('add')) {
                      <button (click)="openCreate(n)" class="w-7 h-7 rounded-lg bg-emerald-50 text-emerald-600 hover:bg-emerald-100 flex items-center justify-center" [title]="'ORG.ADD_CHILD' | translate">
                        <span class="material-icons text-[15px] leading-none">add</span>
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
              <tr>
                <td colspan="7" class="py-14 text-center text-gray-500 text-sm">
                  <span class="material-icons text-4xl text-gray-300 mb-2 block">corporate_fare</span>
                  {{ loading() ? '' : ('COMMON.NO_DATA' | translate) }}
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>

    @if (form(); as f) {
      <app-modal [title]="f.publicId ? 'ORG.EDIT_TITLE' : 'ORG.ADD_TITLE'" widthClass="max-w-xl" (closed)="form.set(null)">
        <form id="org-form" (ngSubmit)="save()" class="space-y-4">
          <div>
            <label class="form-label" for="parent">{{ 'ORG.PARENT' | translate }}</label>
            <select id="parent" name="parent" size="6" class="input !p-2 h-32" [(ngModel)]="f.parentId">
              <option [ngValue]="null">{{ 'ORG.ROOT_NO_PARENT' | translate }}</option>
              @for (opt of parentOptions(); track opt.id) {
                <option [ngValue]="opt.id" [disabled]="opt.blocked">{{ opt.label }}</option>
              }
            </select>
          </div>
          <div>
            <label class="form-label" for="name">{{ 'ORG.NAME' | translate }} <span class="text-red-500">*</span></label>
            <input id="name" name="name" class="input" [(ngModel)]="f.name" required />
          </div>
          <div class="grid grid-cols-2 gap-4">
            <div>
              <label class="form-label" for="order">{{ 'ORG.ORDER' | translate }}</label>
              <input id="order" name="order" type="number" class="input" [(ngModel)]="f.sortOrder" />
            </div>
            <div>
              <label class="form-label" for="status">{{ 'COMMON.STATUS' | translate }}</label>
              <select id="status" name="status" class="input" [(ngModel)]="f.status">
                <option [ngValue]="2">{{ 'ORG.STATUS_ACTIVE' | translate }}</option>
                <option [ngValue]="1">{{ 'ORG.STATUS_HIDDEN' | translate }}</option>
              </select>
            </div>
          </div>
          <div>
            <label class="form-label" for="link">{{ 'ORG.LINK' | translate }}</label>
            <input id="link" name="link" class="input" [(ngModel)]="f.link" [placeholder]="'ORG.OPTIONAL' | translate" />
          </div>
        </form>
        <ng-container footer>
          <button type="button" (click)="form.set(null)" class="btn-secondary">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="submit" form="org-form" [disabled]="saving()" class="btn-primary">
            <span class="material-icons text-[16px]" [class.animate-spin]="saving()">{{ saving() ? 'autorenew' : 'save' }}</span> {{ 'COMMON.SAVE' | translate }}
          </button>
        </ng-container>
      </app-modal>
    }

    @if (importing()) {
      <app-import-dialog title="ORG.IMPORT_NAME" resource="orgs" (closed)="importing.set(false)" (imported)="load()" />
    }

    @if (deleting(); as d) {
      <app-confirm-delete [busy]="saving()" message="ORG.DELETE_CONFIRM" [messageParams]="{ name: d.name }"
                          [warning]="d.children.length ? language.t('ORG.DELETE_BRANCH_WARNING', { count: d.childCount }) : null"
                          (confirmed)="doDelete(d)" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class Orgs implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);
  protected readonly language = inject(Language);
  private readonly client = this.api.crud<OrgNode>('orgs');

  protected keyword = '';
  protected readonly tree = signal<OrgNode[]>([]);
  protected readonly expanded = signal<Set<number> | 'all'>('all');
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly form = signal<OrgForm | null>(null);
  protected readonly deleting = signal<OrgNode | null>(null);
  protected readonly importing = signal(false);

  private readonly flat = computed(() => {
    const out: FlatNode[] = [];
    const walk = (nodes: OrgNode[], depth: number) => nodes.forEach((n) => (out.push({ node: n, depth }), walk(n.children, depth + 1)));
    walk(this.tree(), 0);
    return out;
  });

  protected readonly visible = computed(() => {
    const out: FlatNode[] = [];
    const walk = (nodes: OrgNode[], depth: number) =>
      nodes.forEach((n) => {
        out.push({ node: n, depth });
        if (this.isExpanded(n)) walk(n.children, depth + 1);
      });
    walk(this.tree(), 0);
    return out;
  });

  /** Lựa chọn cha: thụt lề theo cấp; khi sửa thì chặn chính nó và nhánh con của nó. */
  protected readonly parentOptions = computed(() => {
    const f = this.form();
    const blocked = new Set<number>();
    if (f?.id) {
      const mark = (n: OrgNode) => (blocked.add(n.id), n.children.forEach(mark));
      const self = this.flat().find((x) => x.node.id === f.id);
      if (self) mark(self.node);
    }
    return this.flat().map((x) => ({ id: x.node.id, label: `${'    '.repeat(x.depth)}${x.depth ? '└ ' : ''}${x.node.name}`, blocked: blocked.has(x.node.id) }));
  });

  ngOnInit(): void {
    void this.load();
  }

  protected can(action: string): boolean {
    return this.session.can(`ORGS:${action}`);
  }

  protected isExpanded(n: OrgNode): boolean {
    const e = this.expanded();
    return e === 'all' || e.has(n.id);
  }

  protected toggle(n: OrgNode): void {
    const e = this.expanded();
    const next = new Set(e === 'all' ? this.flat().map((x) => x.node.id) : e);
    if (next.has(n.id)) next.delete(n.id);
    else next.add(n.id);
    this.expanded.set(next);
  }

  protected setAll(open: boolean): void {
    this.expanded.set(open ? 'all' : new Set());
  }

  protected openCreate(parent: OrgNode | null): void {
    this.form.set({ publicId: null, id: null, name: '', parentId: parent?.id ?? null, sortOrder: (parent?.children.length ?? this.tree().length) + 1, status: STATUS_ACTIVE, link: '' });
  }

  protected openEdit(n: OrgNode): void {
    this.form.set({ publicId: n.publicId, id: n.id, name: n.name, parentId: n.parentId, sortOrder: n.sortOrder, status: n.status, link: n.link ?? '' });
  }

  protected async save(): Promise<void> {
    const f = this.form();
    if (!f) return;
    if (!f.name.trim()) {
      this.toastr.warning('ORG.NAME_REQUIRED');
      return;
    }
    this.saving.set(true);
    try {
      const body = { name: f.name, parentId: f.parentId, sortOrder: f.sortOrder, status: f.status, link: f.link || null };
      if (f.publicId) await this.client.update(f.publicId, body);
      else await this.client.add(body);
      this.toastr.success(f.publicId ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS');
      this.form.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }

  protected async doDelete(n: OrgNode): Promise<void> {
    this.saving.set(true);
    try {
      if (n.children.length) await this.api.deleteOrgWithChildren(n.publicId);
      else await this.client.delete(n.publicId);
      this.toastr.success('COMMON.DELETE_SUCCESS');
      this.deleting.set(null);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.saving.set(false);
    }
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.tree.set(await this.api.orgTree(this.keyword.trim() || undefined));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }
}
