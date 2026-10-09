import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Api, PagedResult, Role, User, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { Loading, Modal, Paginator } from '../../shared/ui';

/** Cán bộ quản lý thư viện (người dùng nhân viên của đơn vị) — giao diện như pages/admin/system/user của admin cũ. */
@Component({
  selector: 'app-users',
  imports: [FormsModule, DatePipe, Paginator, Modal, Loading],
  template: `
    <div class="mb-5"><h4 class="page-title">Cán bộ quản lý thư viện</h4></div>

    <div class="panel">
      <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
        <div class="xl:col-span-2">
          <label for="kw" class="field-label">Tên đăng nhập / họ tên</label>
          <input id="kw" class="input" [(ngModel)]="search" (keydown.enter)="load(1)" placeholder="Nhập tên đăng nhập, họ tên..." />
        </div>
        <div class="hidden xl:block"></div>
        <div class="flex items-end">
          <button (click)="load(1)" class="btn-primary w-full"><span class="material-icons text-[18px]">search</span> Tìm kiếm</button>
        </div>
      </div>
    </div>

    <div class="panel flex gap-3 items-center justify-between flex-wrap">
      <div class="flex gap-2">
        @if (session.can('USER:add')) {
          <button (click)="openAdd()" class="btn-add"><span class="material-icons text-[18px]">person_add</span> Thêm mới</button>
        }
      </div>
      <span class="text-sm text-gray-500">{{ result()?.total ?? 0 }} tài khoản</span>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
      @if (loading()) { <app-loading /> }
      <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse min-w-[860px]">
          <thead>
            <tr>
              <th class="th w-16 !text-center">ID</th>
              <th class="th">Tên đăng nhập</th>
              <th class="th">Họ tên</th>
              <th class="th">Email / Điện thoại</th>
              <th class="th">Vai trò</th>
              <th class="th w-36 !text-center">Trạng thái</th>
              <th class="th w-44">Đăng nhập gần nhất</th>
            </tr>
          </thead>
          <tbody>
            @for (u of result()?.items ?? []; track u.publicId) {
              <tr class="hover:bg-gray-50/50 transition-colors">
                <td class="td text-center text-gray-500">{{ u.id }}</td>
                <td class="td font-medium text-gray-800">{{ u.userName }}</td>
                <td class="td">{{ u.fullName }}</td>
                <td class="td text-gray-500">{{ u.email || '—' }}@if (u.phone) { <br />{{ u.phone }} }</td>
                <td class="td">
                  @for (r of u.roles; track r.publicId) { <span class="badge-info mr-1 mb-1">{{ r.name }}</span> }
                  @empty { <span class="text-gray-300">—</span> }
                </td>
                <td class="td text-center">
                  @if (!u.isActive) { <span class="badge-danger">Vô hiệu hoá</span> }
                  @else if (u.lockedOut) { <span class="badge-warn">Tạm khoá</span> }
                  @else { <span class="badge-on"><span class="w-1.5 h-1.5 rounded-full bg-green-500"></span>Hoạt động</span> }
                </td>
                <td class="td text-gray-500">{{ u.lastLoginAt ? (u.lastLoginAt | date: 'dd/MM/yyyy HH:mm') : '—' }}</td>
              </tr>
            } @empty {
              <tr><td colspan="7" class="py-8 text-center text-gray-500">{{ loading() ? '' : 'Không có dữ liệu' }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      @if (result(); as r) {
        <app-paginator [total]="r.total" [pageIndex]="r.page" [pageSize]="r.pageSize" [sizes]="[20]" (pageChange)="load($event.pageIndex)" />
      }
    </div>

    @if (form(); as f) {
      <app-modal title="Thêm cán bộ quản lý" widthClass="max-w-2xl" (closed)="form.set(null)">
        <form id="user-form" (ngSubmit)="create()" class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div><label class="form-label" for="u">Tên đăng nhập <span class="text-red-500">*</span></label><input id="u" name="u" class="input" [(ngModel)]="f.userName" required autocomplete="off" /></div>
          <div><label class="form-label" for="n">Họ tên <span class="text-red-500">*</span></label><input id="n" name="n" class="input" [(ngModel)]="f.fullName" required /></div>
          <div><label class="form-label" for="e">Email</label><input id="e" name="e" type="email" class="input" [(ngModel)]="f.email" /></div>
          <div><label class="form-label" for="ph">Điện thoại</label><input id="ph" name="ph" class="input" [(ngModel)]="f.phone" /></div>
          <div class="sm:col-span-2">
            <label class="form-label" for="p">Mật khẩu tạm <span class="text-red-500">*</span></label>
            <input id="p" name="p" type="password" autocomplete="new-password" class="input" [(ngModel)]="f.password" required />
            <p class="text-xs text-gray-500 mt-1">Người dùng phải đổi mật khẩu ở lần đăng nhập đầu tiên.</p>
          </div>
          <div class="sm:col-span-2">
            <span class="form-label">Vai trò</span>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-2">
              @for (r of roles(); track r.publicId) {
                <label class="flex items-center gap-2 text-sm text-gray-700 p-2 rounded-lg border border-gray-100 hover:bg-gray-50 cursor-pointer">
                  <input type="checkbox" class="rounded border-gray-300" [checked]="f.roleIds.includes(r.publicId)" (change)="toggleRole(r.publicId, $any($event.target).checked)" />
                  {{ r.name }}
                </label>
              } @empty {
                <p class="text-sm text-gray-500">Không có quyền xem vai trò.</p>
              }
            </div>
          </div>
        </form>
        <ng-container footer>
          <button type="button" (click)="form.set(null)" class="btn-secondary">Huỷ</button>
          <button type="submit" form="user-form" [disabled]="busy()" class="btn-primary"><span class="material-icons text-[16px]">save</span> Lưu</button>
        </ng-container>
      </app-modal>
    }
  `,
})
export class Users implements OnInit {
  private readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  protected readonly session = inject(Session);

  protected search = '';
  protected readonly result = signal<PagedResult<User> | null>(null);
  protected readonly roles = signal<Role[]>([]);
  protected readonly form = signal<{ userName: string; fullName: string; email: string; phone: string; password: string; roleIds: string[] } | null>(null);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);

  async ngOnInit(): Promise<void> {
    await this.load(1);
    if (this.session.can('ROLE:view')) {
      try {
        this.roles.set(await this.api.roles());
      } catch {
        /* không xem được vai trò: form tạo không có lựa chọn vai trò */
      }
    }
  }

  protected async load(page: number): Promise<void> {
    this.loading.set(true);
    try {
      this.result.set(await this.api.users(this.search.trim(), page));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  protected openAdd(): void {
    this.form.set({ userName: '', fullName: '', email: '', phone: '', password: '', roleIds: [] });
  }

  protected toggleRole(id: string, checked: boolean): void {
    const f = this.form();
    if (f) f.roleIds = checked ? [...f.roleIds, id] : f.roleIds.filter((r) => r !== id);
  }

  protected async create(): Promise<void> {
    const f = this.form();
    if (!f) return;
    this.busy.set(true);
    try {
      const user = await this.api.createUser({ ...f, email: f.email || null, phone: f.phone || null });
      this.toastr.success(`Đã tạo tài khoản "${user.userName}".`);
      this.form.set(null);
      await this.load(1);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }
}
