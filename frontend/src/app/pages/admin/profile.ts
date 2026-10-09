import { Component, inject, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Auth } from '../../services/auth';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './profile.html'
})
export class Profile implements OnInit {
  private auth = inject(Auth);
  private fb = inject(FormBuilder);

  user = this.auth.getUser();

  profileForm = this.fb.group({
    name: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    role: [{ value: '', disabled: true }]
  });

  saved = false;

  ngOnInit() {
    if (this.user) {
      this.profileForm.patchValue({
        name: this.user.name,
        email: this.user.username + '@eliblrc.edu.vn', // Mock fallback email
        role: this.user.role
      });
    }
  }

  onSubmit() {
    if (this.profileForm.valid) {
      this.saved = true;
      setTimeout(() => this.saved = false, 3000);
    }
  }
}
