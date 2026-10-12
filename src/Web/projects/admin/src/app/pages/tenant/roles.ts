import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, PermissionModule, Role, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal } from '../../shared/ui';

const ADMIN_ROLE = 'Quản trị đơn vị'; // i18n-data: tên vai trò dựng sẵn do identity tạo
const ACTIONS = [
  { key: 'view', label: 'ROLE.ACTION_view' },
  { key: 'add', label: 'ROLE.ACTION_add' },
  { key: 'edit', label: 'ROLE.ACTION_edit' },
  { key: 'delete', label: 'ROLE.ACTION_delete' },
] as const;

/** Một dòng của bảng phân quyền: tiêu đề nhóm (theo menu) hoặc một module quyền. */
interface MatrixRow {
  key: string;
  depth: number;
  title: string;
  module: PermissionModule | null;
  /** Nhóm: mọi module nằm dưới nhóm (để bật/tắt cả nhóm). */
  modules: PermissionModule[];
}

/**
 * Phân quyền — vai trò của đơn vị và quyền theo module (monolith: Roles + ModuleRoles, bảng Xem/Thêm/Sửa/Xoá).
 * Bản mới gán quyền cho VAI TRÒ (người dùng nhận quyền qua vai trò), danh mục module lấy từ identity theo license.
 */
@Component({
  selector: 'app-roles',
  imports: [FormsModule, Loading, Modal, ConfirmDelete, TranslatePipe],
  template: `
    <div class="mb-5"><h4 class="page-title">{{ 'ROLE.PAGE_TITLE' | translate }}</h4></div>

    <div class="panel flex gap-3 items-center justify-between flex-wrap">
      <div class="flex gap-2">
        @if (session.can('ROLE:add')) {
          <button (click)="openRole(null)" class="btn-add"><span class="material-icons text-[18px]">add</span> {{ 'ROLE.ADD_ROLE' | translate }}</button>
        }
      </div>
      <span class="text-sm text-gray-500">{{ 'ROLE.COUNT' | translate: { count: roles().length } }}</span>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse">
          <thead>
            <tr>
              <th class="th">{{ 'ROLE.ROLE_NAME' | translate }}</th>
              <th class="th">{{ 'COMMON.DESCRIPTION' | translate }}</th>
              <th class="th w-40">{{ 'ROLE.PERMISSIONS' | translate }}</th>
              <th class="th w-36 !text-center">{{ 'COMMON.ACTIONS' | translate }}</th>
            </tr>
          </thead>
          <tbody>
            @for (r of roles(); track r.publicId) {
              <tr class="hover:bg-gray-50/50 transition-colors">
                <td class="td font-medium text-gray-800">
                  <span class="material-icons text-[18px] align-middle mr-1.5 text-blue-500">admin_panel_settings</span>{{ r.name }}
                  @if (r.isBuiltIn) { <span class="ml-2 text-[11px] text-blue-600 bg-blue-50 px-1.5 py-0.5 rounded">{{ 'ROLE.BUILT_IN' | translate }}</span> }
                </td>
                <td class="td text-gray-500">{{ r.description || '—' }}</td>
                <td class="td">
                  @if (r.permissions.includes('*')) { <span class="badge-info">{{ 'ROLE.ALL_PERMISSIONS' | translate }}</span> }
                  @else if (r.permissions.length) { <span class="badge-on">{{ 'ROLE.N_PERMISSIONS' | translate: { count: r.permissions.length } }}</span> }
                  @else { <span class="badge-off">{{ 'ROLE.NO_PERMISSIONS' | translate }}</span> }
                </td>
                <td class="td">
                  <div class="flex items-center justify-center space-x-2">
                    <button (click)="openMatrix(r)" class="icon-btn bg-teal-50 text-teal-600 hover:bg-teal-100" [title]="'ROLE.PAGE_TITLE' | translate">
                      <span class="material-icons text-[18px]">security</span>
                    </button>
                    @if (session.can('ROLE:edit')) {
                      <button (click)="openRole(r)" class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" [title]="'COMMON.EDIT' | translate">
                        <span class="material-icons text-[18px]">edit</span>
                      </button>
                    }
                    @if (session.can('ROLE:delete') && !r.isBuiltIn) {
                      <button (click)="deleting.set(r)" class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" [title]="'COMMON.DELETE' | translate">
                        <span class="material-icons text-[18px]">delete</span>
                      </button>
                    }
                  </div>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="4" class="py-8 text-center text-gray-500">{{ loading() ? '' : ('COMMON.NO_DATA' | translate) }}</td></tr>
            }
          </tbody>
        </table>
      </div>
    </div>

    @if (editing(); as e) {
      <app-modal [title]="e.publicId ? 'ROLE.EDIT_ROLE_TITLE' : 'ROLE.ADD_ROLE_TITLE'" (closed)="editing.set(null)">
        <form id="role-form" (ngSubmit)="saveRole()" class="space-y-4">
          <div>
            <label class="form-label" for="r-name">{{ 'ROLE.ROLE_NAME' | translate }} <span class="text-red-500">*</span></label>
            <input id="r-name" name="name" class="input" [(ngModel)]="e.name" [disabled]="e.locked" [placeholder]="'ROLE.ROLE_NAME_PH' | translate" />
          </div>
          <div>
            <label class="form-label" for="r-desc">{{ 'COMMON.DESCRIPTION' | translate }}</label>
            <textarea id="r-desc" name="description" rows="3" class="input" [(ngModel)]="e.description"></textarea>
          </div>
          @if (!e.publicId) { <p class="text-xs text-gray-500" [innerHTML]="'ROLE.AFTER_CREATE_HINT' | translate"></p> }
        </form>
        <ng-container footer>
          <button type="button" (click)="editing.set(null)" class="btn-secondary">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="submit" form="role-form" [disabled]="saving()" class="btn-primary">
            <span class="material-icons text-[16px]">save</span>{{ (e.publicId ? 'COMMON.UPDATE' : 'COMMON.SAVE') | translate }}
          </button>
        </ng-container>
      </app-modal>
    }

    @if (matrixRole(); as role) {
      <app-modal [title]="'ROLE.MATRIX_TITLE' | translate: { name: role.name }" widthClass="max-w-4xl" (closed)="matrixRole.set(null)">
        @if (role.permissions.includes('*')) {
          <div class="alert-info" [innerHTML]="'ROLE.ADMIN_LOCKED' | translate: { name: role.name }"></div>
        } @else if (!canEditMatrix()) {
          <div class="alert-info">{{ 'ROLE.VIEW_ONLY' | translate }}</div>
        }
        <div class="border border-gray-100 rounded-lg overflow-auto max-h-[60vh] relative">
          @if (catalogLoading()) { <app-loading /> }
          <table class="w-full text-sm border-collapse min-w-[640px]">
            <thead class="sticky top-0 z-10">
              <tr class="bg-teal-600 text-white">
                <th class="py-3 px-4 text-left font-medium">{{ 'ROLE.FUNCTION' | translate }}</th>
                @for (a of actions; track a.key) { <th class="py-3 px-2 text-center font-medium w-24">{{ a.label }}</th> }
              </tr>
            </thead>
            <tbody>
              @for (row of rows(); track row.key) {
                <tr class="border-b border-gray-100 hover:bg-gray-50/60" [class.bg-gray-50]="!row.module">
                  <td class="py-2.5 px-4">
                    <div class="flex items-center" [style.paddingLeft.px]="row.depth * 20">
                      @if (row.module) {
                        <span class="text-gray-700">{{ row.title }}</span>
                        <span class="ml-2 font-mono text-[11px] text-gray-400">{{ row.module.code }}</span>
                      } @else {
                        <span class="font-semibold text-gray-800">{{ row.title }}</span>
                      }
                    </div>
                  </td>
                  @for (a of actions; track a.key) {
                    <td class="py-2.5 px-2 text-center">
                      @if (supports(row, a.key)) {
                        <button type="button" (click)="toggle(row, a.key)" [disabled]="!canEditMatrix()"
                                class="relative inline-flex h-5 w-9 items-center rounded-full transition-colors duration-200 disabled:cursor-default disabled:opacity-70"
                                [class]="isOn(row, a.key) ? (a.key === 'delete' ? 'bg-red-400' : 'bg-teal-500') : 'bg-gray-200'"
                                [title]="row.module ? (a.label | translate) : ('ROLE.WHOLE_GROUP' | translate: { action: (a.label | translate) })">
                          <span class="inline-block h-3.5 w-3.5 transform rounded-full bg-white shadow transition-transform duration-200"
                                [class]="isOn(row, a.key) ? 'translate-x-4' : 'translate-x-0.5'"></span>
                        </button>
                      } @else {
                        <span class="text-gray-300">—</span>
                      }
                    </td>
                  }
                </tr>
              } @empty {
                <tr><td colspan="5" class="py-12 text-center text-gray-400">{{ catalogLoading() ? '' : ('ROLE.NO_MODULES' | translate) }}</td></tr>
              }
            </tbody>
          </table>
        </div>
        <ng-container footer>
          <span class="mr-auto text-sm text-gray-500">{{ 'ROLE.SELECTED_N' | translate: { count: granted().size } }}</span>
          <button type="button" (click)="matrixRole.set(null)" class="btn-secondary">{{ (canEditMatrix() ? 'COMMON.CANCEL' : 'COMMON.CLOSE') | translate }}</button>
          @if (canEditMatrix()) {
            <button type="button" (click)="saveMatrix()" [disabled]="saving()" class="btn-primary !bg-teal-600 hover:!bg-teal-700">
              <span class="material-icons text-[16px]" [class.animate-spin]="saving()">{{ saving() ? 'autorenew' : 'save' }}</span>{{ 'ROLE.SAVE_MATRIX' | translate }}
            </button>
          }
        </ng-container>
      </app-modal>
    }

    @if (deleting(); as d) {
      <app-confirm-delete [busy]="saving()" message="ROLE.DELETE_CONFIRM" [messageParams]="{ name: d.name }"
                          (confirmed)="deleteRole(d)" (cancelled)="deleting.set(null)" />
    }
  `,
})
export class Roles implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);
  protected readonly actions = ACTIONS;

  protected readonly roles = signal<Role[]>([]);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly editing = signal<{ publicId: string | null; name: string; description: string | null; locked: boolean } | null>(null);
  protected readonly deleting = signal<Role | null>(null);

  protected readonly matrixRole = signal<Role | null>(null);
  protected readonly catalog = signal<PermissionModule[]>([]);
  protected readonly catalogLoading = signal(false);
  protected readonly granted = signal<Set<string>>(new Set());
  protected readonly canEditMatrix = computed(() => {
    const role = this.matrixRole();
    return !!role && !role.permissions.includes('*') && this.session.can('ROLE:edit');
  });
  protected readonly rows = computed(() => buildRows(this.catalog()));

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  protected openRole(role: Role | null): void {
    this.editing.set(role
      ? { publicId: role.publicId, name: role.name, description: role.description, locked: role.isBuiltIn && role.name === ADMIN_ROLE }
      : { publicId: null, name: '', description: null, locked: false });
  }

  protected async saveRole(): Promise<void> {
    const e = this.editing();
    if (!e) return;
    if (!e.name.trim()) {
      this.toastr.warning('ROLE.NAME_REQUIRED');
      return;
    }
    this.saving.set(true);
    try {
      if (e.publicId) {
        const current = this.roles().find((r) => r.publicId === e.publicId)!;
        await this.api.updateRole(e.publicId, { name: e.name, description: e.description, permissions: current.permissions });
      } else {
        await this.api.createRole({ name: e.name, description: e.description, permissions: [] });
      }
      this.toastr.success(e.publicId ? 'COMMON.UPDATE_SUCCESS' : 'COMMON.ADD_SUCCESS');
      this.editing.set(null);
      await this.load();
    } catch (err) {
      this.toastr.error(errorMessage(err));
    } finally {
      this.saving.set(false);
    }
  }

  protected async deleteRole(role: Role): Promise<void> {
    this.saving.set(true);
    try {
      await this.api.deleteRole(role.publicId);
      this.toastr.success('COMMON.DELETE_SUCCESS');
      this.deleting.set(null);
      await this.load();
    } catch (err) {
      this.toastr.error(errorMessage(err));
    } finally {
      this.saving.set(false);
    }
  }

  protected async openMatrix(role: Role): Promise<void> {
    this.granted.set(new Set(role.permissions));
    this.matrixRole.set(role);
    if (this.catalog().length) return;
    this.catalogLoading.set(true);
    try {
      this.catalog.set(await this.api.permissionCatalog());
    } catch (err) {
      this.toastr.error(errorMessage(err));
    } finally {
      this.catalogLoading.set(false);
    }
  }

  protected supports(row: MatrixRow, action: string): boolean {
    return row.modules.some((m) => m.actions.includes(action));
  }

  protected isOn(row: MatrixRow, action: string): boolean {
    if (this.matrixRole()?.permissions.includes('*')) return true;
    const targets = row.modules.filter((m) => m.actions.includes(action));
    return targets.length > 0 && targets.every((m) => this.granted().has(`${m.code}:${action}`));
  }

  /** Bật Thêm/Sửa/Xoá thì bật luôn Xem; tắt Xem thì tắt các quyền còn lại của module. Dòng nhóm áp cho mọi module trong nhóm. */
  protected toggle(row: MatrixRow, action: string): void {
    const on = !this.isOn(row, action);
    const next = new Set(this.granted());
    for (const m of row.modules.filter((x) => x.actions.includes(action))) {
      if (on) {
        next.add(`${m.code}:${action}`);
        if (action !== 'view' && m.actions.includes('view')) next.add(`${m.code}:view`);
      } else {
        next.delete(`${m.code}:${action}`);
        if (action === 'view') for (const a of m.actions) next.delete(`${m.code}:${a}`);
      }
    }
    this.granted.set(next);
  }

  protected async saveMatrix(): Promise<void> {
    const role = this.matrixRole();
    if (!role) return;
    this.saving.set(true);
    try {
      await this.api.updateRole(role.publicId, { name: role.name, description: role.description, permissions: [...this.granted()].sort() });
      this.toastr.success('ROLE.MATRIX_SAVED');
      this.matrixRole.set(null);
      await this.load();
    } catch (err) {
      this.toastr.error(errorMessage(err));
    } finally {
      this.saving.set(false);
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.roles.set(await this.api.roles());
    } catch (err) {
      this.toastr.error(errorMessage(err));
    } finally {
      this.loading.set(false);
    }
  }
}

/** Dựng cây theo nhóm "A / B" (giữ thứ tự của danh mục): dòng nhóm trước, module bên dưới, thụt lề theo cấp. */
function buildRows(catalog: PermissionModule[]): MatrixRow[] {
  const rows: MatrixRow[] = [];
  const groups = new Map<string, MatrixRow>();
  for (const m of catalog) {
    const parts = m.group.split(' / ');
    for (let i = 0; i < parts.length; i++) {
      const path = parts.slice(0, i + 1).join(' / ');
      let g = groups.get(path);
      if (!g) {
        g = { key: 'g:' + path, depth: i, title: parts[i], module: null, modules: [] };
        groups.set(path, g);
        rows.push(g);
      }
      g.modules.push(m);
    }
    rows.push({ key: 'm:' + m.code, depth: parts.length, title: m.name, module: m, modules: [m] });
  }
  return rows;
}
