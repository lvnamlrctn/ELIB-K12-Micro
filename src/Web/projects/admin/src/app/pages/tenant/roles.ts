import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, PermissionModule, Role, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading, Modal } from '../../shared/ui';

const ADMIN_ROLE = 'Quản trị đơn vị';
const ACTIONS = [
  { key: 'view', label: 'Xem' },
  { key: 'add', label: 'Thêm mới' },
  { key: 'edit', label: 'Sửa' },
  { key: 'delete', label: 'Xoá' },
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
  imports: [FormsModule, Loading, Modal, ConfirmDelete],
  template: `
    <div class="mb-5"><h4 class="page-title">Phân quyền</h4></div>

    <div class="panel flex gap-3 items-center justify-between flex-wrap">
      <div class="flex gap-2">
        @if (session.can('ROLE:add')) {
          <button (click)="openRole(null)" class="btn-add"><span class="material-icons text-[18px]">add</span> Thêm vai trò</button>
        }
      </div>
      <span class="text-sm text-gray-500">{{ roles().length }} vai trò</span>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto w-full">
        <table class="w-full text-left border-collapse">
          <thead>
            <tr>
              <th class="th">Tên vai trò</th>
              <th class="th">Mô tả</th>
              <th class="th w-40">Quyền</th>
              <th class="th w-36 !text-center">Thao tác</th>
            </tr>
          </thead>
          <tbody>
            @for (r of roles(); track r.publicId) {
              <tr class="hover:bg-gray-50/50 transition-colors">
                <td class="td font-medium text-gray-800">
                  <span class="material-icons text-[18px] align-middle mr-1.5 text-blue-500">admin_panel_settings</span>{{ r.name }}
                  @if (r.isBuiltIn) { <span class="ml-2 text-[11px] text-blue-600 bg-blue-50 px-1.5 py-0.5 rounded">Mặc định</span> }
                </td>
                <td class="td text-gray-500">{{ r.description || '—' }}</td>
                <td class="td">
                  @if (r.permissions.includes('*')) { <span class="badge-info">Toàn quyền</span> }
                  @else if (r.permissions.length) { <span class="badge-on">{{ r.permissions.length }} quyền</span> }
                  @else { <span class="badge-off">Chưa có quyền</span> }
                </td>
                <td class="td">
                  <div class="flex items-center justify-center space-x-2">
                    <button (click)="openMatrix(r)" class="icon-btn bg-teal-50 text-teal-600 hover:bg-teal-100" title="Phân quyền">
                      <span class="material-icons text-[18px]">security</span>
                    </button>
                    @if (session.can('ROLE:edit')) {
                      <button (click)="openRole(r)" class="icon-btn bg-blue-50 text-blue-600 hover:bg-blue-100" title="Sửa">
                        <span class="material-icons text-[18px]">edit</span>
                      </button>
                    }
                    @if (session.can('ROLE:delete') && !r.isBuiltIn) {
                      <button (click)="deleting.set(r)" class="icon-btn bg-red-50 text-red-600 hover:bg-red-100" title="Xoá">
                        <span class="material-icons text-[18px]">delete</span>
                      </button>
                    }
                  </div>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="4" class="py-8 text-center text-gray-500">{{ loading() ? '' : 'Không có dữ liệu' }}</td></tr>
            }
          </tbody>
        </table>
      </div>
    </div>

    @if (editing(); as e) {
      <app-modal [title]="e.publicId ? 'Cập nhật vai trò' : 'Thêm mới vai trò'" (closed)="editing.set(null)">
        <form id="role-form" (ngSubmit)="saveRole()" class="space-y-4">
          <div>
            <label class="form-label" for="r-name">Tên vai trò <span class="text-red-500">*</span></label>
            <input id="r-name" name="name" class="input" [(ngModel)]="e.name" [disabled]="e.locked" placeholder="VD: Thủ thư lưu thông" />
          </div>
          <div>
            <label class="form-label" for="r-desc">Mô tả</label>
            <textarea id="r-desc" name="description" rows="3" class="input" [(ngModel)]="e.description"></textarea>
          </div>
          @if (!e.publicId) { <p class="text-xs text-gray-500">Sau khi tạo, bấm nút <b>Phân quyền</b> để chọn quyền cho vai trò.</p> }
        </form>
        <ng-container footer>
          <button type="button" (click)="editing.set(null)" class="btn-secondary">Huỷ</button>
          <button type="submit" form="role-form" [disabled]="saving()" class="btn-primary">
            <span class="material-icons text-[16px]">save</span>{{ e.publicId ? 'Cập nhật' : 'Lưu' }}
          </button>
        </ng-container>
      </app-modal>
    }

    @if (matrixRole(); as role) {
      <app-modal [title]="'Phân quyền — ' + role.name" widthClass="max-w-4xl" (closed)="matrixRole.set(null)">
        @if (role.permissions.includes('*')) {
          <div class="alert-info">Vai trò <b>{{ role.name }}</b> có toàn quyền trong đơn vị — không chỉnh được.</div>
        } @else if (!canEditMatrix()) {
          <div class="alert-info">Bạn chỉ có quyền xem phân quyền.</div>
        }
        <div class="border border-gray-100 rounded-lg overflow-auto max-h-[60vh] relative">
          @if (catalogLoading()) { <app-loading /> }
          <table class="w-full text-sm border-collapse min-w-[640px]">
            <thead class="sticky top-0 z-10">
              <tr class="bg-teal-600 text-white">
                <th class="py-3 px-4 text-left font-medium">Chức năng</th>
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
                                [title]="a.label + (row.module ? '' : ' — cả nhóm')">
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
                <tr><td colspan="5" class="py-12 text-center text-gray-400">{{ catalogLoading() ? '' : 'Không có dữ liệu module' }}</td></tr>
              }
            </tbody>
          </table>
        </div>
        <ng-container footer>
          <span class="mr-auto text-sm text-gray-500">{{ granted().size }} quyền được chọn</span>
          <button type="button" (click)="matrixRole.set(null)" class="btn-secondary">{{ canEditMatrix() ? 'Huỷ' : 'Đóng' }}</button>
          @if (canEditMatrix()) {
            <button type="button" (click)="saveMatrix()" [disabled]="saving()" class="btn-primary !bg-teal-600 hover:!bg-teal-700">
              <span class="material-icons text-[16px]" [class.animate-spin]="saving()">{{ saving() ? 'autorenew' : 'save' }}</span>Lưu phân quyền
            </button>
          }
        </ng-container>
      </app-modal>
    }

    @if (deleting(); as d) {
      <app-confirm-delete [busy]="saving()" [message]="'Xoá vai trò \\'' + d.name + '\\'? Người dùng đang có vai trò này sẽ mất các quyền tương ứng.'"
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
      this.toastr.warning('Vui lòng nhập tên vai trò.');
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
      this.toastr.success(e.publicId ? 'Cập nhật thành công' : 'Thêm mới thành công');
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
      this.toastr.success('Xoá thành công');
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
      this.toastr.success('Đã lưu phân quyền');
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
