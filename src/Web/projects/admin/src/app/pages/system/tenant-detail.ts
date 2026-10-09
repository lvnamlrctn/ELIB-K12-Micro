import { Component, OnDestroy, OnInit, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, ModuleDef, Tenant, errorMessage } from '../../core/api';
import { ToastrService } from '../../shared/toastr';
import { Modal } from '../../shared/ui';
import { ModulePicker } from './module-picker';
import { TenantStatusBadge } from './tenant-status';

const STEP_LABELS: Record<string, [string, string]> = {
  Pending: ['Đang chờ', 'badge-warn'],
  Succeeded: ['Hoàn tất', 'badge-on'],
  Failed: ['Lỗi', 'badge-danger'],
};

@Component({
  selector: 'app-tenant-detail',
  imports: [FormsModule, RouterLink, DatePipe, ModulePicker, TenantStatusBadge, Modal],
  template: `
    <div class="mb-5 flex items-center gap-3 flex-wrap">
      <a routerLink="/tenant" class="icon-btn bg-white border border-gray-200 text-gray-600 hover:bg-gray-50"><span class="material-icons text-[18px]">arrow_back</span></a>
      @if (tenant(); as t) {
        <h4 class="page-title">{{ t.name }}</h4>
        <app-tenant-status [status]="t.status" />
        <span class="flex-1"></span>
        @if (t.status === 'Active') {
          <button class="btn-secondary !py-2 bg-white" (click)="viewReason.set('')" [disabled]="busy()"
                  title="Mở app quản trị của đơn vị với quyền chỉ đọc (30 phút, ghi nhật ký)">
            <span class="material-icons text-[18px]">visibility</span>Vào xem đơn vị
          </button>
          <button class="btn-outline-danger !py-2" (click)="suspendReason.set('')" [disabled]="busy()"><span class="material-icons text-[18px]">block</span>Tạm ngưng</button>
        }
        @if (t.status === 'Suspended') {
          <button class="btn-primary" (click)="run(api.activateTenant(t.publicId), 'Đã kích hoạt lại đơn vị.')" [disabled]="busy()"><span class="material-icons text-[18px]">play_circle</span>Kích hoạt lại</button>
        }
        @if (t.status === 'ProvisioningFailed') {
          <button class="btn-primary" (click)="run(api.retryProvisioning(t.publicId), 'Đang khởi tạo lại.')" [disabled]="busy()"><span class="material-icons text-[18px]">replay</span>Khởi tạo lại</button>
        }
      } @else {
        <h4 class="page-title">Đơn vị</h4>
      }
    </div>

    @if (tenant(); as t) {
      <div class="grid grid-cols-1 xl:grid-cols-3 gap-5">
        <div class="xl:col-span-2 space-y-5">
          <section class="bg-white rounded-xl shadow-sm border border-gray-100">
            <div class="px-6 py-4 border-b border-gray-100 font-bold text-gray-800">Thông tin đơn vị</div>
            <dl class="p-6 grid grid-cols-1 sm:grid-cols-3 gap-y-3 text-sm">
              <dt class="text-gray-500">Mã đơn vị</dt><dd class="sm:col-span-2 font-mono font-medium">{{ t.code }}</dd>
              <dt class="text-gray-500">Địa chỉ quản trị</dt>
              <dd class="sm:col-span-2"><a [href]="adminUrl(t)" target="_blank" rel="noopener" class="text-blue-600 hover:underline break-all">{{ adminUrl(t) }}</a></dd>
              <dt class="text-gray-500">Múi giờ</dt><dd class="sm:col-span-2">{{ t.timeZone }}</dd>
              <dt class="text-gray-500">Phiên bản dữ liệu</dt><dd class="sm:col-span-2">{{ t.version }}</dd>
            </dl>
          </section>

          <section class="bg-white rounded-xl shadow-sm border border-gray-100">
            <div class="px-6 py-4 border-b border-gray-100 font-bold text-gray-800">Nhận diện</div>
            <div class="p-6 flex flex-col sm:flex-row gap-6">
              <div class="shrink-0">
                <div class="w-28 h-28 rounded-xl border border-dashed border-gray-300 bg-gray-50 flex items-center justify-center overflow-hidden">
                  @if (logoUrl(); as url) {
                    <img [src]="url" alt="Logo đơn vị" class="max-w-full max-h-full object-contain" />
                  } @else {
                    <span class="material-icons text-gray-300 text-[40px]">image</span>
                  }
                </div>
                <input #logoInput type="file" accept="image/png,image/jpeg,image/webp" class="hidden" (change)="uploadLogo(t, logoInput)" />
                <div class="flex gap-1 mt-2">
                  <button type="button" class="btn-secondary bg-white !py-1 !px-2 text-xs" (click)="logoInput.click()" [disabled]="busy()">
                    <span class="material-icons text-[16px]">upload</span>Chọn ảnh
                  </button>
                  @if (logoUrl()) {
                    <button type="button" class="icon-btn text-red-600 hover:bg-red-50" title="Bỏ logo" (click)="logoUrl.set(null)" [disabled]="busy()">
                      <span class="material-icons text-[18px]">delete</span>
                    </button>
                  }
                </div>
              </div>
              <div class="flex-1 space-y-3">
                <div>
                  <label class="form-label" for="logo-text">Tên ngắn</label>
                  <input id="logo-text" class="input" maxlength="50" [(ngModel)]="logoText" [placeholder]="t.name" />
                  <p class="text-xs text-gray-500 mt-1">Hiện cạnh logo và khi cài OPAC như ứng dụng trên điện thoại. Bỏ trống = tên đơn vị.</p>
                </div>
                <p class="text-xs text-gray-500">Logo: PNG, JPG hoặc WEBP, tối đa 2 MB, nên là ảnh vuông nền trong suốt.</p>
              </div>
            </div>
            <div class="bg-gray-50 px-6 py-4 border-t border-gray-100 flex justify-end">
              <button class="btn-primary" (click)="saveBranding(t)" [disabled]="busy()"><span class="material-icons text-[18px]">save</span>Lưu nhận diện</button>
            </div>
          </section>

          <section class="bg-white rounded-xl shadow-sm border border-gray-100">
            <div class="px-6 py-4 border-b border-gray-100 font-bold text-gray-800">Phân hệ đã bán</div>
            <div class="p-6">
              <app-module-picker [modules]="modules()" [(selected)]="selected" />
            </div>
            <div class="bg-gray-50 px-6 py-4 border-t border-gray-100 flex justify-end">
              <button class="btn-primary" (click)="saveLicenses(t)" [disabled]="busy()"><span class="material-icons text-[18px]">save</span>Lưu phân hệ</button>
            </div>
          </section>
        </div>

        <div class="space-y-5">
          <section class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden">
            <div class="px-6 py-4 border-b border-gray-100 font-bold text-gray-800">Khởi tạo đơn vị</div>
            <table class="w-full text-left border-collapse">
              <tbody>
                @for (s of t.provisioningSteps; track s.service) {
                  <tr>
                    <td class="td font-mono text-[13px]">{{ s.service }}</td>
                    <td class="td"><span [class]="stepLabel(s.status)[1]">{{ stepLabel(s.status)[0] }}</span></td>
                    <td class="td text-xs text-gray-500">{{ s.updatedAt | date: 'dd/MM HH:mm:ss' }}</td>
                  </tr>
                  @if (s.error) { <tr><td colspan="3" class="px-4 pb-3 text-xs text-red-600">{{ s.error }}</td></tr> }
                } @empty {
                  <tr><td class="py-6 text-center text-sm text-gray-500">Không có bước nào.</td></tr>
                }
              </tbody>
            </table>
            @if (t.provisioningError) { <div class="m-4 alert-error">{{ t.provisioningError }}</div> }
            <div class="bg-gray-50 px-6 py-3 border-t border-gray-100 flex flex-wrap gap-2 justify-end">
              <button class="btn-secondary bg-white !py-1.5" (click)="resync(t)" [disabled]="busy()"
                      title="Gửi lại thông tin đơn vị và phân hệ cho mọi service — dùng khi nền tảng có service mới hoặc dữ liệu bị lệch">
                <span class="material-icons text-[18px]">sync</span>Đồng bộ lại sang các service
              </button>
              <button class="btn-secondary bg-white !py-1.5" (click)="seedDefaults(t)" [disabled]="busy()" title="Chép bổ sung danh mục và tham số mặc định còn thiếu">
                <span class="material-icons text-[18px]">playlist_add</span>Bổ sung danh mục mặc định
              </button>
            </div>
          </section>

          <section class="bg-white rounded-xl shadow-sm border border-gray-100">
            <div class="px-6 py-4 border-b border-gray-100 font-bold text-gray-800">Quản trị viên đầu tiên</div>
            @if (t.status !== 'Active') {
              <p class="p-6 text-sm text-gray-500">Đơn vị phải ở trạng thái Hoạt động mới tạo được quản trị viên.</p>
            } @else {
              <form (ngSubmit)="createAdmin(t)" class="p-6 space-y-4">
                <div><label class="form-label" for="u">Tên đăng nhập <span class="text-red-500">*</span></label><input id="u" name="u" class="input" [(ngModel)]="admin.userName" required autocomplete="off" /></div>
                <div><label class="form-label" for="n">Họ tên <span class="text-red-500">*</span></label><input id="n" name="n" class="input" [(ngModel)]="admin.fullName" required /></div>
                <div><label class="form-label" for="e">Email</label><input id="e" name="e" type="email" class="input" [(ngModel)]="admin.email" /></div>
                <div>
                  <label class="form-label" for="p">Mật khẩu tạm <span class="text-red-500">*</span></label>
                  <input id="p" name="p" type="password" autocomplete="new-password" class="input" [(ngModel)]="admin.password" required />
                  <p class="text-xs text-gray-500 mt-1">Người dùng phải đổi ở lần đăng nhập đầu.</p>
                </div>
                <button type="submit" class="btn-primary w-full" [disabled]="busy()"><span class="material-icons text-[18px]">person_add</span>Tạo quản trị viên</button>
              </form>
            }
          </section>
        </div>
      </div>
    } @else {
      <p class="text-gray-500">Đang tải…</p>
    }

    @if (viewReason() !== null) {
      <app-modal title="Vào xem đơn vị (chỉ đọc)" (closed)="viewReason.set(null)">
        <p class="text-sm text-gray-600 mb-3">
          Mở app quản trị của đơn vị trong tab mới với quyền <b>chỉ xem</b> (không thêm/sửa/xoá được), tối đa 30 phút.
          Lần xem và lý do được ghi vào nhật ký của đơn vị.
        </p>
        <label class="form-label" for="view-reason">Lý do <span class="text-red-500">*</span></label>
        <textarea id="view-reason" rows="3" class="input" [ngModel]="viewReason()" (ngModelChange)="viewReason.set($event)"
                  placeholder="VD: hỗ trợ trường kiểm tra cấu hình email theo yêu cầu ngày 09/10"></textarea>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="viewReason.set(null)">Huỷ</button>
          <button type="button" class="btn-primary" [disabled]="busy() || (viewReason() ?? '').trim().length < 5" (click)="impersonate()">
            <span class="material-icons text-[18px]">open_in_new</span>Mở app của đơn vị
          </button>
        </ng-container>
      </app-modal>
    }

    @if (suspendReason() !== null) {
      <app-modal title="Tạm ngưng đơn vị" (closed)="suspendReason.set(null)">
        <label class="form-label" for="reason">Lý do tạm ngưng</label>
        <textarea id="reason" rows="3" class="input" [ngModel]="suspendReason()" (ngModelChange)="suspendReason.set($event)" placeholder="VD: hết hạn hợp đồng"></textarea>
        <p class="text-xs text-gray-500 mt-2">Người dùng của đơn vị sẽ không đăng nhập được cho tới khi kích hoạt lại.</p>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="suspendReason.set(null)">Huỷ</button>
          <button type="button" class="btn-danger" [disabled]="busy()" (click)="suspend()"><span class="material-icons text-[18px]">block</span>Tạm ngưng</button>
        </ng-container>
      </app-modal>
    }
  `,
})
export class TenantDetail implements OnInit, OnDestroy {
  protected readonly api = inject(Api);
  private readonly toastr = inject(ToastrService);
  readonly id = input.required<string>();

  protected readonly tenant = signal<Tenant | null>(null);
  protected readonly modules = signal<ModuleDef[]>([]);
  protected readonly suspendReason = signal<string | null>(null);
  protected readonly viewReason = signal<string | null>(null);
  protected selected: string[] = [];
  /** Logo đang hiển thị/sẽ lưu (ảnh vừa upload chỉ áp dụng khi bấm "Lưu nhận diện"). */
  protected readonly logoUrl = signal<string | null>(null);
  protected logoText = '';
  protected admin = { userName: '', fullName: '', email: '', password: '' };
  protected readonly busy = signal(false);
  private timer: ReturnType<typeof setInterval> | undefined;

  async ngOnInit(): Promise<void> {
    try {
      const [tenant, modules] = await Promise.all([this.api.tenant(this.id()), this.api.modules()]);
      this.modules.set(modules);
      this.show(tenant);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
    // Saga khởi tạo chạy bất đồng bộ — tự làm mới khi đang khởi tạo.
    this.timer = setInterval(async () => {
      if (this.tenant()?.status !== 'Provisioning') return;
      try {
        this.show(await this.api.tenant(this.id()));
      } catch {
        /* bỏ qua, lần sau thử lại */
      }
    }, 3000);
  }

  ngOnDestroy(): void {
    clearInterval(this.timer);
  }

  protected stepLabel(status: string): [string, string] {
    return STEP_LABELS[status] ?? [status, 'badge-off'];
  }

  protected adminUrl(t: Tenant): string {
    return `${location.protocol}//${t.subdomain}.${location.hostname}${location.port ? ':' + location.port : ''}/admin/`;
  }

  protected async impersonate(): Promise<void> {
    const t = this.tenant();
    if (!t) return;
    this.busy.set(true);
    try {
      const { url } = await this.api.impersonate(t.id, (this.viewReason() ?? '').trim());
      this.viewReason.set(null);
      window.open(url, '_blank', 'noopener');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async suspend(): Promise<void> {
    const t = this.tenant();
    if (!t) return;
    await this.run(this.api.suspendTenant(t.publicId, this.suspendReason() ?? ''), 'Đã tạm ngưng đơn vị.');
    this.suspendReason.set(null);
  }

  protected saveLicenses(t: Tenant): Promise<void> {
    const current = new Map(t.licenses.map((l) => [l.moduleCode, l]));
    // Giữ nguyên trạng thái/thời hạn của license đang có, module mới thì Active.
    const licenses = this.selected.map((code) => {
      const l = current.get(code);
      return l ? { moduleCode: code, status: l.status, validFrom: l.validFrom, validTo: l.validTo } : { moduleCode: code };
    });
    return this.run(this.api.setLicenses(t.publicId, licenses), 'Đã lưu phân hệ.');
  }

  protected async uploadLogo(t: Tenant, input: HTMLInputElement): Promise<void> {
    const file = input.files?.[0];
    input.value = ''; // chọn lại cùng file vẫn kích hoạt change
    if (!file) return;
    if (file.size > 2 * 1024 * 1024) {
      this.toastr.error('Logo tối đa 2 MB.');
      return;
    }
    this.busy.set(true);
    try {
      const uploaded = await this.api.uploadForTenant(t.id, 'tenant-logo', file);
      this.logoUrl.set(uploaded.url);
      this.toastr.success('Đã tải ảnh lên — bấm "Lưu nhận diện" để áp dụng.');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected saveBranding(t: Tenant): Promise<void> {
    return this.run(this.api.setTenantBranding(t.publicId, { logoText: this.logoText.trim() || null, logoUrl: this.logoUrl() }), 'Đã lưu nhận diện đơn vị.');
  }

  protected resync(t: Tenant): Promise<void> {
    return this.run(this.api.resyncTenant(t.publicId), 'Đã gửi lại thông tin đơn vị cho các service.');
  }

  protected async seedDefaults(t: Tenant): Promise<void> {
    this.busy.set(true);
    try {
      const { added } = await this.api.seedTenantDefaults(t.publicId);
      this.toastr.success(added ? `Đã bổ sung ${added} mục mặc định.` : 'Danh mục mặc định đã đủ.');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async createAdmin(t: Tenant): Promise<void> {
    this.busy.set(true);
    try {
      const user = await this.api.createTenantAdmin(t.id, { ...this.admin, email: this.admin.email || null });
      this.admin = { userName: '', fullName: '', email: '', password: '' };
      this.toastr.success(`Đã tạo quản trị viên "${user.userName}". Đăng nhập tại ${this.adminUrl(t)}`);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async run(action: Promise<Tenant>, success: string): Promise<void> {
    this.busy.set(true);
    try {
      this.show(await action);
      this.toastr.success(success);
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private show(t: Tenant): void {
    this.tenant.set(t);
    this.selected = t.licenses.map((l) => l.moduleCode);
    this.logoUrl.set(t.logoUrl);
    this.logoText = t.logoText ?? '';
  }
}
