import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, EmailSettings, EmailSettingsInput, NotificationLog, STATUS_ACTIVE, SmtpSecurity, errorMessage } from '../../core/api';
import { Session } from '../../core/session';
import { ToastrService } from '../../shared/toastr';
import { ConfirmDelete, Loading } from '../../shared/ui';

const PORTS: Record<SmtpSecurity, number> = { StartTls: 587, SslOnConnect: 465, None: 25 };

/**
 * Cấu hình máy chủ gửi email của đơn vị (monolith: cấu hình kênh thông báo email).
 * Không cấu hình = gửi bằng SMTP của nền tảng. Mật khẩu không bao giờ hiện lại — để trống khi lưu là giữ mật khẩu cũ.
 */
@Component({
  selector: 'app-email-settings',
  imports: [FormsModule, Loading, ConfirmDelete],
  template: `
    <div class="mb-5">
      <h4 class="page-title">Cấu hình email</h4>
      <p class="text-sm text-gray-500 mt-1">Máy chủ SMTP dùng để gửi mã OTP, thông báo cho cán bộ và bạn đọc</p>
    </div>

    @if (current(); as cur) {
      <div class="max-w-3xl">
        @if (!cur.configured) {
          <div class="alert-info flex gap-2">
            <span class="material-icons text-[20px]">info</span>
            <span>
              @if (cur.platformAvailable) {
                Đơn vị đang gửi email bằng <b>máy chủ chung của nền tảng</b>. Nhập cấu hình bên dưới nếu muốn gửi bằng địa chỉ email của trường.
              } @else {
                Chưa có máy chủ gửi email — nhập cấu hình SMTP để gửi được mã OTP và thông báo.
              }
            </span>
          </div>
        }

        <div class="bg-white rounded-xl shadow-sm border border-gray-100 relative">
          @if (busy()) { <app-loading /> }
          <form (ngSubmit)="save()" class="p-6 grid grid-cols-1 sm:grid-cols-6 gap-4">
            <div class="sm:col-span-4">
              <label class="form-label" for="host">Máy chủ SMTP <span class="text-red-500">*</span></label>
              <input id="host" name="host" class="input" [(ngModel)]="form.host" placeholder="VD: smtp.gmail.com" [disabled]="!canEdit" />
            </div>
            <div class="sm:col-span-2">
              <label class="form-label" for="port">Cổng <span class="text-red-500">*</span></label>
              <input id="port" name="port" type="number" class="input" [(ngModel)]="form.port" [disabled]="!canEdit" />
            </div>
            <div class="sm:col-span-3">
              <label class="form-label" for="sec">Mã hoá</label>
              <select id="sec" name="security" class="input" [(ngModel)]="form.security" (ngModelChange)="onSecurity($event)" [disabled]="!canEdit">
                <option value="StartTls">STARTTLS (cổng 587)</option>
                <option value="SslOnConnect">SSL/TLS (cổng 465)</option>
                <option value="None">Không mã hoá</option>
              </select>
            </div>
            <div class="sm:col-span-3">
              <label class="form-label" for="st">Trạng thái</label>
              <select id="st" name="status" class="input" [(ngModel)]="form.status" [disabled]="!canEdit">
                <option [ngValue]="2">Đang dùng</option>
                <option [ngValue]="1">Tạm tắt (dùng máy chủ nền tảng)</option>
              </select>
            </div>
            <div class="sm:col-span-3">
              <label class="form-label" for="user">Tên đăng nhập</label>
              <input id="user" name="userName" class="input" [(ngModel)]="form.userName" autocomplete="off" placeholder="Thường là địa chỉ email" [disabled]="!canEdit" />
            </div>
            <div class="sm:col-span-3">
              <label class="form-label" for="pass">Mật khẩu</label>
              <input id="pass" name="password" type="password" class="input" [(ngModel)]="form.password" autocomplete="new-password"
                     [placeholder]="cur.hasPassword ? 'Để trống = giữ mật khẩu đã lưu' : 'Mật khẩu ứng dụng'" [disabled]="!canEdit || form.clearPassword" />
              @if (cur.hasPassword && canEdit) {
                <label class="flex items-center gap-2 text-xs text-gray-600 mt-1.5 cursor-pointer select-none">
                  <input type="checkbox" class="rounded border-gray-300" name="clear" [(ngModel)]="form.clearPassword" /> Xoá mật khẩu đã lưu
                </label>
              }
            </div>
            <div class="sm:col-span-3">
              <label class="form-label" for="from">Địa chỉ gửi <span class="text-red-500">*</span></label>
              <input id="from" name="fromAddress" type="email" class="input" [(ngModel)]="form.fromAddress" placeholder="thuvien@truong.edu.vn" [disabled]="!canEdit" />
            </div>
            <div class="sm:col-span-3">
              <label class="form-label" for="fromName">Tên người gửi</label>
              <input id="fromName" name="fromName" class="input" [(ngModel)]="form.fromName" placeholder="Thư viện trường ..." [disabled]="!canEdit" />
            </div>
          </form>
          <div class="bg-gray-50 px-6 py-4 border-t border-gray-100 flex flex-wrap gap-2 justify-between rounded-b-xl">
            <div>
              @if (cur.configured && canDelete) {
                <button type="button" (click)="confirmReset.set(true)" class="btn-outline-danger">
                  <span class="material-icons text-[18px]">restart_alt</span> Dùng máy chủ nền tảng
                </button>
              }
            </div>
            @if (canEdit) {
              <button type="button" (click)="save()" [disabled]="busy()" class="btn-primary">
                <span class="material-icons text-[18px]">save</span> Lưu cấu hình
              </button>
            }
          </div>
        </div>

        @if (canEdit) {
          <div class="panel mt-5">
            <h5 class="font-semibold text-gray-800 mb-1">Gửi thư kiểm tra</h5>
            <p class="text-sm text-gray-500 mb-3">Gửi theo cấu hình <b>đã lưu</b>, dùng mẫu "Thư kiểm tra cấu hình email".</p>
            <div class="flex flex-col sm:flex-row gap-2">
              <input type="email" class="input sm:max-w-sm" name="testTo" [(ngModel)]="testTo" placeholder="Địa chỉ nhận thư" (keydown.enter)="sendTest()" />
              <button type="button" (click)="sendTest()" [disabled]="busy() || !testTo.trim()" class="btn-add !py-2">
                <span class="material-icons text-[18px]">send</span> Gửi thử
              </button>
            </div>
            @if (testResult(); as r) {
              <div class="mt-3 text-sm rounded-lg px-4 py-3 border"
                   [class]="r.status === 'Sent' ? 'bg-emerald-50 border-emerald-200 text-emerald-800' : 'bg-red-50 border-red-200 text-red-700'">
                @if (r.status === 'Sent') {
                  Đã gửi tới <b>{{ r.recipient }}</b> qua {{ r.viaPlatform ? 'máy chủ nền tảng' : 'máy chủ của đơn vị' }}.
                } @else {
                  Gửi không thành công: {{ r.error }}
                }
              </div>
            }
          </div>
        }
      </div>
    } @else {
      <div class="relative h-40"><app-loading /></div>
    }

    @if (confirmReset()) {
      <app-confirm-delete title="Bỏ cấu hình riêng" confirmText="Dùng máy chủ nền tảng" [busy]="busy()"
                          message="Xoá cấu hình SMTP của đơn vị (kể cả mật khẩu đã lưu) và gửi email bằng máy chủ chung của nền tảng?"
                          (confirmed)="reset()" (cancelled)="confirmReset.set(false)" />
    }
  `,
})
export class EmailSettingsPage implements OnInit {
  private readonly api = inject(Api);
  private readonly session = inject(Session);
  private readonly toastr = inject(ToastrService);

  protected readonly current = signal<EmailSettings | null>(null);
  protected readonly busy = signal(false);
  protected readonly testResult = signal<NotificationLog | null>(null);
  protected readonly confirmReset = signal(false);
  protected readonly canEdit = this.session.can('NOTIFICATION_CONFIG:edit');
  protected readonly canDelete = this.session.can('NOTIFICATION_CONFIG:delete');

  protected form = this.blank();
  protected testTo = '';

  ngOnInit(): void {
    void this.load();
  }

  /** Đổi kiểu mã hoá thì đổi cổng mặc định theo — trừ khi người dùng đã nhập cổng riêng. */
  protected onSecurity(security: SmtpSecurity): void {
    if (Object.values(PORTS).includes(this.form.port)) this.form.port = PORTS[security];
  }

  protected async save(): Promise<void> {
    if (!this.form.host.trim() || !this.form.fromAddress.trim() || !this.form.port) {
      this.toastr.warning('Vui lòng nhập máy chủ, cổng và địa chỉ gửi.');
      return;
    }
    this.busy.set(true);
    try {
      this.apply(await this.api.saveEmailSettings({ ...this.form, password: this.form.password || null }));
      this.toastr.success('Đã lưu cấu hình email');
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async sendTest(): Promise<void> {
    this.busy.set(true);
    this.testResult.set(null);
    try {
      this.testResult.set(await this.api.sendTestEmail(this.testTo.trim()));
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  protected async reset(): Promise<void> {
    this.busy.set(true);
    try {
      await this.api.deleteEmailSettings();
      this.toastr.success('Đã chuyển sang máy chủ nền tảng');
      this.confirmReset.set(false);
      await this.load();
    } catch (e) {
      this.toastr.error(errorMessage(e));
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    try {
      this.apply(await this.api.emailSettings());
    } catch (e) {
      this.toastr.error(errorMessage(e));
    }
  }

  private apply(s: EmailSettings): void {
    this.current.set(s);
    this.form = s.configured
      ? {
          host: s.host ?? '', port: s.port ?? 587, security: s.security ?? 'StartTls', userName: s.userName, password: null,
          clearPassword: false, fromAddress: s.fromAddress ?? '', fromName: s.fromName, status: s.status ?? STATUS_ACTIVE,
        }
      : this.blank();
  }

  private blank(): EmailSettingsInput {
    return { host: '', port: 587, security: 'StartTls', userName: null, password: null, clearPassword: false, fromAddress: '', fromName: null, status: STATUS_ACTIVE };
  }
}
