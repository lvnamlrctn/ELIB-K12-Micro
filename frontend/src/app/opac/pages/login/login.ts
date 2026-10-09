import { TranslateModule } from '@ngx-translate/core';
import { Component, computed, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { AuthApiService } from '../../services/auth-api.service';
import { User } from '../../services/models';
import { AuthService } from '../../services/auth.service';
import { TenantService } from '../../services/tenant.service';
import { OtpInput } from '../../../shared/otp-input/otp-input';

@Component({
  selector: 'app-login',
  imports: [TranslateModule, ReactiveFormsModule, OtpInput],
  templateUrl: './login.html'
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  private authApi = inject(AuthApiService);
  private authService = inject(AuthService);
  private router = inject(Router);
  private sanitizer = inject(DomSanitizer);
  public tenant = inject(TenantService);

  loginForm: FormGroup;
  isLoading = signal(false);
  errorMessage = signal<string | null>(null);

  // Lớp bảo mật đăng nhập thứ 1 — CAPTCHA (tùy chọn, bật/tắt theo tenant). captchaEnabled=false
  // khi backend báo tenant chưa bật cờ — khi đó form login hoạt động y hệt trước đây (không có ô CAPTCHA).
  captchaEnabled = signal(false);
  captchaId = signal('');
  captchaSvg = signal('');
  captchaSvgSafe = computed<SafeHtml>(() => this.sanitizer.bypassSecurityTrustHtml(this.captchaSvg()));
  captchaAnswer = signal('');

  // Lớp bảo mật đăng nhập thứ 2 — OTP qua email (tùy chọn). otpStep=true khi Login trả về
  // otpRequired=true — chuyển form sang bước nhập mã OTP thay vì hoàn tất đăng nhập ngay.
  otpStep = signal(false);
  otpSessionToken = signal('');
  otpCode = signal('');

  constructor() {
    this.loginForm = this.fb.group({
      username: ['', [Validators.required]],
      password: ['', [Validators.required]],
      rememberMe: [false]
    });
    // Tải sẵn thử thách CAPTCHA khi vào trang — endpoint tự báo enabled=false nếu tenant chưa bật,
    // lúc đó ô CAPTCHA đơn giản là không hiển thị (xem login.html).
    this.loadCaptcha();
  }

  loadCaptcha(): void {
    this.captchaAnswer.set('');
    this.authApi.getCaptcha().subscribe(res => {
      this.captchaEnabled.set(res.enabled);
      this.captchaId.set(res.enabled ? (res.captchaId ?? '') : '');
      this.captchaSvg.set(res.enabled ? (res.svg ?? '') : '');
    });
  }

  onSubmit() {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }
    if (this.captchaEnabled() && !this.captchaAnswer().trim()) {
      this.errorMessage.set('Vui lòng nhập mã xác thực.');
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    const { username, password } = this.loginForm.value;
    const captcha = this.captchaEnabled()
      ? { captchaId: this.captchaId(), captchaAnswer: this.captchaAnswer() }
      : undefined;

    this.authApi.login(username, password, captcha).subscribe({
      next: (result) => {
        this.isLoading.set(false);
        if (result.otpRequired) {
          // Mật khẩu đã đúng nhưng tenant bật OTP — chuyển sang bước nhập mã, CHƯA đăng nhập xong.
          this.otpStep.set(true);
          this.otpSessionToken.set(result.otpSessionToken ?? '');
          this.otpCode.set('');
          this.errorMessage.set(null);
          return;
        }
        if (result.user) {
          this.completeLogin(result.user);
        } else {
          this.errorMessage.set(result.message || 'Tên đăng nhập hoặc mật khẩu không chính xác.');
          // CAPTCHA dùng 1 lần — bị backend xoá dù đúng/sai, nên đăng nhập thất bại luôn cần tải mã mới.
          if (this.captchaEnabled()) this.loadCaptcha();
        }
      },
      error: () => {
        this.isLoading.set(false);
        this.errorMessage.set('Đã có lỗi xảy ra. Vui lòng thử lại sau.');
        if (this.captchaEnabled()) this.loadCaptcha();
      }
    });
  }

  verifyOtp(): void {
    const code = this.otpCode().trim();
    if (!code) {
      this.errorMessage.set('Vui lòng nhập mã OTP.');
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.authApi.verifyOtp(this.otpSessionToken(), code).subscribe({
      next: (result) => {
        this.isLoading.set(false);
        if (result.user) {
          this.completeLogin(result.user);
        } else {
          this.errorMessage.set(result.message || 'Mã xác thực không đúng.');
        }
      },
      error: () => {
        this.isLoading.set(false);
        this.errorMessage.set('Đã có lỗi xảy ra. Vui lòng thử lại sau.');
      }
    });
  }

  // Quay lại bước nhập tài khoản/mật khẩu — chỉ reset state phía client, mã OTP cũ (nếu còn) tự hết hạn
  // sau 5 phút phía server, không có endpoint huỷ riêng.
  cancelOtpStep(): void {
    this.otpStep.set(false);
    this.otpSessionToken.set('');
    this.otpCode.set('');
    this.errorMessage.set(null);
    if (this.captchaEnabled()) this.loadCaptcha();
  }

  private completeLogin(user: User): void {
    this.authService.setUser(user);
    this.otpStep.set(false);
    this.otpSessionToken.set('');
    this.otpCode.set('');
    this.router.navigate(['/']);
  }
}
