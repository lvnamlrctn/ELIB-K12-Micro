import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { Auth } from '../services/auth';
import { ToastrService } from '../services/shared/toastr.service';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { OtpInput } from '../shared/otp-input/otp-input';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, TranslateModule, OtpInput],
  templateUrl: './login.html'
})
export class Login {
  private fb = inject(FormBuilder);
  private auth = inject(Auth);
  private router = inject(Router);
  private toastr = inject(ToastrService);
  private translate = inject(TranslateService);
  private sanitizer = inject(DomSanitizer);

  loginForm = this.fb.group({
    username: ['', Validators.required],
    password: ['', Validators.required]
  });

  errorMessage = '';
  isLoading = signal(false);

  // Lớp bảo mật đăng nhập thứ 1 — CAPTCHA (tùy chọn, bật/tắt theo tenant). captchaEnabled=false khi
  // backend báo tenant chưa bật cờ — form login hoạt động y hệt trước đây (không có ô CAPTCHA).
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
    // Tải sẵn thử thách CAPTCHA khi vào trang — endpoint tự báo enabled=false nếu tenant chưa bật,
    // lúc đó ô CAPTCHA đơn giản là không hiển thị (xem login.html).
    this.loadCaptcha();
  }

  loadCaptcha(): void {
    this.captchaAnswer.set('');
    this.auth.loadCaptcha().subscribe(res => {
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
      this.errorMessage = 'Vui lòng nhập mã xác thực.';
      return;
    }

    this.errorMessage = '';
    this.isLoading.set(true);
    const { username, password } = this.loginForm.value;
    const captcha = this.captchaEnabled()
      ? { captchaId: this.captchaId(), captchaAnswer: this.captchaAnswer() }
      : undefined;

    this.auth.login(username!, password!, captcha).subscribe(res => {
      this.isLoading.set(false);
      if (res.otpRequired) {
        // Mật khẩu đã đúng nhưng tenant bật OTP — chuyển sang bước nhập mã, CHƯA đăng nhập xong.
        this.otpStep.set(true);
        this.otpSessionToken.set(res.otpSessionToken ?? '');
        this.otpCode.set('');
        this.errorMessage = '';
        return;
      }
      if (res.success) {
        this.router.navigate(['/admin/dashboard']);
      } else {
        const msg = res.message || this.translate.instant('LOGIN.ERROR_MSG');
        this.errorMessage = msg;
        this.toastr.error(msg);
        // CAPTCHA dùng 1 lần — bị backend xoá dù đúng/sai, nên đăng nhập thất bại luôn cần tải mã mới.
        if (this.captchaEnabled()) this.loadCaptcha();
      }
    });
  }

  verifyOtp(): void {
    const code = this.otpCode().trim();
    if (!code) {
      this.errorMessage = 'Vui lòng nhập mã OTP.';
      return;
    }

    this.errorMessage = '';
    this.isLoading.set(true);
    this.auth.verifyOtp(this.otpSessionToken(), code).subscribe(res => {
      this.isLoading.set(false);
      if (res.success) {
        this.otpStep.set(false);
        this.router.navigate(['/admin/dashboard']);
      } else {
        const msg = res.message || 'Mã xác thực không đúng.';
        this.errorMessage = msg;
        this.toastr.error(msg);
      }
    });
  }

  // Quay lại bước nhập tài khoản/mật khẩu — chỉ reset state phía client, mã OTP cũ (nếu còn) tự hết hạn
  // sau 5 phút phía server, không có endpoint huỷ riêng.
  cancelOtpStep(): void {
    this.otpStep.set(false);
    this.otpSessionToken.set('');
    this.otpCode.set('');
    this.errorMessage = '';
    if (this.captchaEnabled()) this.loadCaptcha();
  }
}
