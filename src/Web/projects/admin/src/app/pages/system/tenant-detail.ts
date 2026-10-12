import { Component, OnDestroy, OnInit, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Api, ModuleDef, Tenant, errorMessage } from '../../core/api';
import { LDatePipe } from '../../core/i18n';
import { ToastrService } from '../../shared/toastr';
import { Modal } from '../../shared/ui';
import { ModulePicker } from './module-picker';
import { TenantStatusBadge } from './tenant-status';

/** Nhãn bước khởi tạo: khoá dịch TENANT_ADMIN.STEP_<trạng thái>. */
const STEP_CSS: Record<string, string> = { Pending: 'badge-warn', Succeeded: 'badge-on', Failed: 'badge-danger' };

@Component({
  selector: 'app-tenant-detail',
  imports: [FormsModule, RouterLink, LDatePipe, ModulePicker, TenantStatusBadge, Modal, TranslatePipe],
  template: `
    <div class="mb-5 flex items-center gap-3 flex-wrap">
      <a routerLink="/tenant" class="icon-btn bg-white border border-gray-200 text-gray-600 hover:bg-gray-50"><span class="material-icons text-[18px]">arrow_back</span></a>
      @if (tenant(); as t) {
        <h4 class="page-title">{{ t.name }}</h4>
        <app-tenant-status [status]="t.status" />
        <span class="flex-1"></span>
        @if (t.status === 'Active') {
          <button class="btn-secondary !py-2 bg-white" (click)="viewReason.set('')" [disabled]="busy()"
                  [title]="'TENANT_ADMIN.VIEW_TENANT_TIP' | translate">
            <span class="material-icons text-[18px]">visibility</span>{{ 'TENANT_ADMIN.VIEW_TENANT' | translate }}
          </button>
          <button class="btn-outline-danger !py-2" (click)="suspendReason.set('')" [disabled]="busy()"><span class="material-icons text-[18px]">block</span>{{ 'TENANT_ADMIN.SUSPEND' | translate }}</button>
        }
        @if (t.status === 'Suspended') {
          <button class="btn-primary" (click)="run(api.activateTenant(t.publicId), 'TENANT_ADMIN.REACTIVATED')" [disabled]="busy()"><span class="material-icons text-[18px]">play_circle</span>{{ 'TENANT_ADMIN.REACTIVATE' | translate }}</button>
        }
        @if (t.status === 'ProvisioningFailed') {
          <button class="btn-primary" (click)="run(api.retryProvisioning(t.publicId), 'TENANT_ADMIN.RETRYING')" [disabled]="busy()"><span class="material-icons text-[18px]">replay</span>{{ 'TENANT_ADMIN.RETRY' | translate }}</button>
        }
      } @else {
        <h4 class="page-title">{{ 'TENANT_ADMIN.TITLE' | translate }}</h4>
      }
    </div>

    @if (tenant(); as t) {
      <div class="grid grid-cols-1 xl:grid-cols-3 gap-5">
        <div class="xl:col-span-2 space-y-5">
          <section class="bg-white rounded-xl shadow-sm border border-gray-100">
            <div class="px-6 py-4 border-b border-gray-100 font-bold text-gray-800">{{ 'TENANT_ADMIN.INFO' | translate }}</div>
            <dl class="p-6 grid grid-cols-1 sm:grid-cols-3 gap-y-3 text-sm">
              <dt class="text-gray-500">{{ 'TENANT_ADMIN.CODE' | translate }}</dt><dd class="sm:col-span-2 font-mono font-medium">{{ t.code }}</dd>
              <dt class="text-gray-500">{{ 'TENANT_ADMIN.ADMIN_URL' | translate }}</dt>
              <dd class="sm:col-span-2"><a [href]="adminUrl(t)" target="_blank" rel="noopener" class="text-blue-600 hover:underline break-all">{{ adminUrl(t) }}</a></dd>
              <dt class="text-gray-500">{{ 'TENANT_ADMIN.TIME_ZONE' | translate }}</dt><dd class="sm:col-span-2">{{ t.timeZone }}</dd>
              <dt class="text-gray-500">{{ 'TENANT_ADMIN.DATA_VERSION' | translate }}</dt><dd class="sm:col-span-2">{{ t.version }}</dd>
            </dl>
          </section>

          <section class="bg-white rounded-xl shadow-sm border border-gray-100">
            <div class="px-6 py-4 border-b border-gray-100 font-bold text-gray-800">{{ 'TENANT_ADMIN.BRANDING' | translate }}</div>
            <div class="p-6 flex flex-col sm:flex-row gap-6">
              <div class="shrink-0">
                <div class="w-28 h-28 rounded-xl border border-dashed border-gray-300 bg-gray-50 flex items-center justify-center overflow-hidden">
                  @if (logoUrl(); as url) {
                    <img [src]="url" [alt]="'TENANT_ADMIN.LOGO_ALT' | translate" class="max-w-full max-h-full object-contain" />
                  } @else {
                    <span class="material-icons text-gray-300 text-[40px]">image</span>
                  }
                </div>
                <input #logoInput type="file" accept="image/png,image/jpeg,image/webp" class="hidden" (change)="uploadLogo(t, logoInput)" />
                <div class="flex gap-1 mt-2">
                  <button type="button" class="btn-secondary bg-white !py-1 !px-2 text-xs" (click)="logoInput.click()" [disabled]="busy()">
                    <span class="material-icons text-[16px]">upload</span>{{ 'TENANT_ADMIN.CHOOSE_IMAGE' | translate }}
                  </button>
                  @if (logoUrl()) {
                    <button type="button" class="icon-btn text-red-600 hover:bg-red-50" [title]="'TENANT_ADMIN.REMOVE_LOGO' | translate" (click)="logoUrl.set(null)" [disabled]="busy()">
                      <span class="material-icons text-[18px]">delete</span>
                    </button>
                  }
                </div>
              </div>
              <div class="flex-1 space-y-3">
                <div>
                  <label class="form-label" for="logo-text">{{ 'TENANT_ADMIN.SHORT_NAME' | translate }}</label>
                  <input id="logo-text" class="input" maxlength="50" [(ngModel)]="logoText" [placeholder]="t.name" />
                  <p class="text-xs text-gray-500 mt-1">{{ 'TENANT_ADMIN.SHORT_NAME_HINT' | translate }}</p>
                </div>
                <p class="text-xs text-gray-500">{{ 'TENANT_ADMIN.LOGO_HINT' | translate }}</p>
              </div>
            </div>
            <div class="bg-gray-50 px-6 py-4 border-t border-gray-100 flex justify-end">
              <button class="btn-primary" (click)="saveBranding(t)" [disabled]="busy()"><span class="material-icons text-[18px]">save</span>{{ 'TENANT_ADMIN.SAVE_BRANDING' | translate }}</button>
            </div>
          </section>

          <section class="bg-white rounded-xl shadow-sm border border-gray-100">
            <div class="px-6 py-4 border-b border-gray-100 font-bold text-gray-800">{{ 'TENANT_ADMIN.MODULES' | translate }}</div>
            <div class="p-6">
              <app-module-picker [modules]="modules()" [(selected)]="selected" />
            </div>
            <div class="bg-gray-50 px-6 py-4 border-t border-gray-100 flex justify-end">
              <button class="btn-primary" (click)="saveLicenses(t)" [disabled]="busy()"><span class="material-icons text-[18px]">save</span>{{ 'TENANT_ADMIN.SAVE_MODULES' | translate }}</button>
            </div>
          </section>
        </div>

        <div class="space-y-5">
          <section class="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden">
            <div class="px-6 py-4 border-b border-gray-100 font-bold text-gray-800">{{ 'TENANT_ADMIN.PROVISIONING' | translate }}</div>
            <table class="w-full text-left border-collapse">
              <tbody>
                @for (s of t.provisioningSteps; track s.service) {
                  <tr>
                    <td class="td font-mono text-[13px]">{{ s.service }}</td>
                    <td class="td"><span [class]="stepCss(s.status)">{{ 'TENANT_ADMIN.STEP_' + s.status | translate }}</span></td>
                    <td class="td text-xs text-gray-500">{{ s.updatedAt | ldate: 'dd/MM HH:mm:ss' }}</td>
                  </tr>
                  @if (s.error) { <tr><td colspan="3" class="px-4 pb-3 text-xs text-red-600">{{ s.error }}</td></tr> }
                } @empty {
                  <tr><td class="py-6 text-center text-sm text-gray-500">{{ 'TENANT_ADMIN.NO_STEPS' | translate }}</td></tr>
                }
              </tbody>
            </table>
            @if (t.provisioningError) { <div class="m-4 alert-error">{{ t.provisioningError }}</div> }
            <div class="bg-gray-50 px-6 py-3 border-t border-gray-100 flex flex-wrap gap-2 justify-end">
              <button class="btn-secondary bg-white !py-1.5" (click)="resync(t)" [disabled]="busy()"
                      [title]="'TENANT_ADMIN.RESYNC_TIP' | translate">
                <span class="material-icons text-[18px]">sync</span>{{ 'TENANT_ADMIN.RESYNC' | translate }}
              </button>
              <button class="btn-secondary bg-white !py-1.5" (click)="seedDefaults(t)" [disabled]="busy()" [title]="'TENANT_ADMIN.SEED_TIP' | translate">
                <span class="material-icons text-[18px]">playlist_add</span>{{ 'TENANT_ADMIN.SEED' | translate }}
              </button>
            </div>
          </section>

          <section class="bg-white rounded-xl shadow-sm border border-gray-100">
            <div class="px-6 py-4 border-b border-gray-100 font-bold text-gray-800">{{ 'TENANT_ADMIN.FIRST_ADMIN' | translate }}</div>
            @if (t.status !== 'Active') {
              <p class="p-6 text-sm text-gray-500">{{ 'TENANT_ADMIN.FIRST_ADMIN_INACTIVE' | translate }}</p>
            } @else {
              <form (ngSubmit)="createAdmin(t)" class="p-6 space-y-4">
                <div><label class="form-label" for="u">{{ 'TENANT_ADMIN.USER_NAME' | translate }} <span class="text-red-500">*</span></label><input id="u" name="u" class="input" [(ngModel)]="admin.userName" required autocomplete="off" /></div>
                <div><label class="form-label" for="n">{{ 'TENANT_ADMIN.FULL_NAME' | translate }} <span class="text-red-500">*</span></label><input id="n" name="n" class="input" [(ngModel)]="admin.fullName" required /></div>
                <div><label class="form-label" for="e">Email</label><input id="e" name="e" type="email" class="input" [(ngModel)]="admin.email" /></div>
                <div>
                  <label class="form-label" for="p">{{ 'TENANT_ADMIN.TEMP_PASSWORD' | translate }} <span class="text-red-500">*</span></label>
                  <input id="p" name="p" type="password" autocomplete="new-password" class="input" [(ngModel)]="admin.password" required />
                  <p class="text-xs text-gray-500 mt-1">{{ 'TENANT_ADMIN.TEMP_PASSWORD_HINT' | translate }}</p>
                </div>
                <button type="submit" class="btn-primary w-full" [disabled]="busy()"><span class="material-icons text-[18px]">person_add</span>{{ 'TENANT_ADMIN.CREATE_ADMIN' | translate }}</button>
              </form>
            }
          </section>
        </div>
      </div>
    } @else {
      <p class="text-gray-500">{{ 'COMMON.LOADING' | translate }}</p>
    }

    @if (viewReason() !== null) {
      <app-modal title="TENANT_ADMIN.VIEW_TITLE" (closed)="viewReason.set(null)">
        <p class="text-sm text-gray-600 mb-3" [innerHTML]="'TENANT_ADMIN.VIEW_TEXT' | translate"></p>
        <label class="form-label" for="view-reason">{{ 'TENANT_ADMIN.REASON' | translate }} <span class="text-red-500">*</span></label>
        <textarea id="view-reason" rows="3" class="input" [ngModel]="viewReason()" (ngModelChange)="viewReason.set($event)"
                  [placeholder]="'TENANT_ADMIN.VIEW_REASON_PH' | translate"></textarea>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="viewReason.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-primary" [disabled]="busy() || (viewReason() ?? '').trim().length < 5" (click)="impersonate()">
            <span class="material-icons text-[18px]">open_in_new</span>{{ 'TENANT_ADMIN.OPEN_TENANT_APP' | translate }}
          </button>
        </ng-container>
      </app-modal>
    }

    @if (suspendReason() !== null) {
      <app-modal title="TENANT_ADMIN.SUSPEND_TITLE" (closed)="suspendReason.set(null)">
        <label class="form-label" for="reason">{{ 'TENANT_ADMIN.SUSPEND_REASON' | translate }}</label>
        <textarea id="reason" rows="3" class="input" [ngModel]="suspendReason()" (ngModelChange)="suspendReason.set($event)" [placeholder]="'TENANT_ADMIN.SUSPEND_REASON_PH' | translate"></textarea>
        <p class="text-xs text-gray-500 mt-2">{{ 'TENANT_ADMIN.SUSPEND_HINT' | translate }}</p>
        <ng-container footer>
          <button type="button" class="btn-secondary" (click)="suspendReason.set(null)">{{ 'COMMON.CANCEL' | translate }}</button>
          <button type="button" class="btn-danger" [disabled]="busy()" (click)="suspend()"><span class="material-icons text-[18px]">block</span>{{ 'TENANT_ADMIN.SUSPEND' | translate }}</button>
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

  protected stepCss(status: string): string {
    return STEP_CSS[status] ?? 'badge-off';
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
    await this.run(this.api.suspendTenant(t.publicId, this.suspendReason() ?? ''), 'TENANT_ADMIN.SUSPENDED');
    this.suspendReason.set(null);
  }

  protected saveLicenses(t: Tenant): Promise<void> {
    const current = new Map(t.licenses.map((l) => [l.moduleCode, l]));
    // Giữ nguyên trạng thái/thời hạn của license đang có, module mới thì Active.
    const licenses = this.selected.map((code) => {
      const l = current.get(code);
      return l ? { moduleCode: code, status: l.status, validFrom: l.validFrom, validTo: l.validTo } : { moduleCode: code };
    });
    return this.run(this.api.setLicenses(t.publicId, licenses), 'TENANT_ADMIN.MODULES_SAVED');
  }

  protected async uploadLogo(t: Tenant, input: HTMLInputElement): Promise<void> {
    const file = input.files?.[0];
    input.value = ''; // chọn lại cùng file vẫn kích hoạt change
    if (!file) return;
    if (file.size > 2 * 1024 * 1024) {
      this.toastr.error('TENANT_ADMIN.LOGO_TOO_BIG');
      return;
    }
    this.busy.set(true);
    try {
      const uploaded = await this.api.uploadForTenant(t.id, 'tenant-logo', file);
      this.logoUrl.set(uploaded.url);
      this.toastr.success('TENANT_ADMIN.LOGO_UPLOADED');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected saveBranding(t: Tenant): Promise<void> {
    return this.run(this.api.setTenantBranding(t.publicId, { logoText: this.logoText.trim() || null, logoUrl: this.logoUrl() }), 'TENANT_ADMIN.BRANDING_SAVED');
  }

  protected resync(t: Tenant): Promise<void> {
    return this.run(this.api.resyncTenant(t.publicId), 'TENANT_ADMIN.RESYNCED');
  }

  protected async seedDefaults(t: Tenant): Promise<void> {
    this.busy.set(true);
    try {
      const { added } = await this.api.seedTenantDefaults(t.publicId);
      if (added) this.toastr.success('TENANT_ADMIN.SEEDED', { count: added });
      else this.toastr.success('TENANT_ADMIN.SEED_NOTHING');
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
      this.toastr.success('TENANT_ADMIN.ADMIN_CREATED', { user: user.userName, url: this.adminUrl(t) });
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
