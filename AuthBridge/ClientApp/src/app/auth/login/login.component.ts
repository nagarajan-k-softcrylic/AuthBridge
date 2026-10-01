import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
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

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router,
    private route: ActivatedRoute
  ) {
    this.form = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required]]
    });

    this.authService.getAuthOptions().subscribe({
      next: (opts) => (this.ssoEnabled = opts.ssoEnabled),
      error: () => (this.ssoEnabled = false)
    });
  }

  /**
   * ASP.NET Core Identity/IdentityServer appends a `ReturnUrl` query param (e.g.
   * `/connect/authorize/callback?...`) when it redirects an unauthenticated user here mid-OIDC
   * handshake. That's a server-side path, not an Angular route, so it must be followed with a
   * full browser navigation (`window.location.href`) instead of `router.navigateByUrl`, which
   * would try (and fail) to resolve it against the SPA's client-side routes.
   */
  private navigateAfterLogin(): void {
    const returnUrl = this.route.snapshot.queryParamMap.get('ReturnUrl') ?? this.route.snapshot.queryParamMap.get('returnUrl');
    if (returnUrl) {
      window.location.href = returnUrl;
    } else {
      this.router.navigateByUrl('/home');
    }
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
          this.navigateAfterLogin();
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
