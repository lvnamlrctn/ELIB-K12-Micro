import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { UserService } from '../../services/system/user.service';

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './change-password.html'
})
export class ChangePassword {
  private fb = inject(FormBuilder);
  private userService = inject(UserService);

  pwdForm = this.fb.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(6)]],
    confirmPassword: ['', Validators.required]
  });

  success = false;
  error = '';
  loading = signal(false);

  onSubmit() {
    if (!this.pwdForm.valid || this.loading()) return;

    const { currentPassword, newPassword, confirmPassword } = this.pwdForm.value;
    if (newPassword !== confirmPassword) {
      this.error = 'Mật khẩu xác nhận không khớp!';
      this.success = false;
      return;
    }

    this.error = '';
    this.success = false;
    this.loading.set(true);

    this.userService.changePassword(currentPassword!, newPassword!).subscribe({
      next: res => {
        this.loading.set(false);
        if (res?.success === false) {
          this.error = res?.message || 'Đổi mật khẩu thất bại.';
          return;
        }
        this.success = true;
        this.pwdForm.reset();
        setTimeout(() => this.success = false, 3000);
      },
      error: err => {
        this.loading.set(false);
        this.error = err?.error?.message || 'Đổi mật khẩu thất bại. Vui lòng thử lại.';
      }
    });
  }
}
