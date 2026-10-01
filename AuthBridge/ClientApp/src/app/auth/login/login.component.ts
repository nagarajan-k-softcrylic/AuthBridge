import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  form: FormGroup;
  submitting = false;
  errorMessage: string | null = null;
  authMode: 'basic' | 'sso' = 'basic';
  ssoEnabled = false;

  constructor(private fb: FormBuilder, private authService: AuthService, private router: Router) {
    this.form = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required]]
    });

    this.authService.getAuthOptions().subscribe({
      next: (opts) => (this.ssoEnabled = opts.ssoEnabled),
      error: () => (this.ssoEnabled = false)
    });
  }

  switchMode(mode: 'basic' | 'sso'): void {
    this.authMode = mode;
    this.errorMessage = null;
  }

  onSsoLogin(): void {
    window.location.href = this.authService.ssoLoginUrl();
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.errorMessage = null;
    this.submitting = true;

    this.authService.login(this.form.value).subscribe({
      next: (res) => {
        this.submitting = false;
        if (res.succeeded) {
          this.router.navigateByUrl('/home');
        } else {
          this.errorMessage = res.errors?.[0] ?? 'Login failed.';
        }
      },
      error: (err) => {
        this.submitting = false;
        this.errorMessage = err?.error?.errors?.[0] ?? 'Invalid email or password.';
      }
    });
  }
}
