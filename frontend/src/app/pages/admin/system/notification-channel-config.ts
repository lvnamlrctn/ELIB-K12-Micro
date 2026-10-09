import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl } from '@angular/forms';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { MatIconModule } from '@angular/material/icon';
import { ToastrService } from '../../../services/shared/toastr.service';
import { NotificationChannelConfigService } from '../../../services/system/notification-channel-config.service';

// Cấu hình SMS/Zalo ZNS — 1 dòng/tenant (per-tenant, xem NotificationChannelConfig entity). Không phải
// CRUD danh sách thông thường: tải dòng của tenant hiện tại (nếu có) → Update; nếu chưa có → Add tạo mới.
@Component({
  selector: 'app-notification-channel-config',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslateModule, MatIconModule],
  templateUrl: './notification-channel-config.html'
})
export class NotificationChannelConfigPage implements OnInit {
  private service = inject(NotificationChannelConfigService);
  private toastr = inject(ToastrService);
  public translate = inject(TranslateService);

  isLoading = signal(false);
  isSaving = signal(false);
  publicId = signal<string | null>(null);
  hasSmsToken = signal(false);
  hasZaloToken = signal(false);

  form = new FormGroup({
    smsAccessToken: new FormControl<string>('', { nonNullable: true }),
    smsSender: new FormControl<string>('', { nonNullable: true }),
    smsApiUrl: new FormControl<string>('', { nonNullable: true }),
    zaloAccessToken: new FormControl<string>('', { nonNullable: true }),
    zaloApiUrl: new FormControl<string>('', { nonNullable: true }),
  });

  ngOnInit() {
    this.load();
  }

  load() {
    this.isLoading.set(true);
    this.service.getMine().subscribe({
      next: (config) => {
        if (config) {
          this.publicId.set(config.publicId ?? null);
          this.hasSmsToken.set(!!config.smsAccessToken);
          this.hasZaloToken.set(!!config.zaloAccessToken);
          this.form.patchValue({
            smsSender: config.smsSender ?? '',
            smsApiUrl: config.smsApiUrl ?? '',
            zaloApiUrl: config.zaloApiUrl ?? '',
          });
        }
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  save() {
    this.isSaving.set(true);
    const vals = this.form.getRawValue();
    const payload = {
      smsAccessToken: vals.smsAccessToken || undefined, // rỗng = giữ nguyên giá trị cũ (server side)
      smsSender: vals.smsSender || null,
      smsApiUrl: vals.smsApiUrl || null,
      zaloAccessToken: vals.zaloAccessToken || undefined,
      zaloApiUrl: vals.zaloApiUrl || null,
    };
    const id = this.publicId();
    const req$ = id ? this.service.update(id, payload) : this.service.create(payload);
    req$.subscribe({
      next: () => {
        this.toastr.success(this.translate.instant('COMMON.SAVE_SUCCESS'));
        this.form.patchValue({ smsAccessToken: '', zaloAccessToken: '' });
        this.isSaving.set(false);
        this.load();
      },
      error: () => {
        this.toastr.error(this.translate.instant('COMMON.SAVE_ERROR'));
        this.isSaving.set(false);
      }
    });
  }
}
