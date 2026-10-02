import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService, AuthUser } from '../auth.service';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent {
  user: AuthUser | null = null;

  mfaSetupActive = false;
  mfaSharedKey: string | null = null;
  mfaAuthenticatorUri: string | null = null;
  mfaEnableForm: FormGroup;
  mfaMessage: string | null = null;
  mfaError: string | null = null;

  constructor(private authService: AuthService, private router: Router, private fb: FormBuilder) {
    // The auth guard already resolved /api/auth/me before this component loads, so the
    // user is cached in memory; fetchCurrentUser() returns it without another round trip.
    this.authService.fetchCurrentUser().subscribe((user) => (this.user = user));

    this.mfaEnableForm = this.fb.group({
      code: ['', [Validators.required]]
    });
  }

  logout(): void {
    this.authService.logout().subscribe(() => this.router.navigateByUrl('/login'));
  }

  startMfaSetup(): void {
    this.mfaMessage = null;
    this.mfaError = null;
    this.authService.getMfaSetup().subscribe({
      next: (setup) => {
        this.mfaSharedKey = setup.sharedKey;
        this.mfaAuthenticatorUri = setup.authenticatorUri;
        this.mfaSetupActive = true;
      },
      error: () => (this.mfaError = 'Unable to start MFA setup.')
    });
  }

  /** Renders the otpauth:// URI as a scannable QR code image via a public QR generation endpoint. */
  get mfaQrCodeUrl(): string | null {
    if (!this.mfaAuthenticatorUri) {
      return null;
    }

    return `https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=${encodeURIComponent(this.mfaAuthenticatorUri)}`;
  }

  confirmMfaEnable(): void {
    if (this.mfaEnableForm.invalid) {
      this.mfaEnableForm.markAllAsTouched();
      return;
    }

    this.mfaError = null;
    this.authService.enableMfa(this.mfaEnableForm.value.code).subscribe({
      next: (res) => {
        if (res.succeeded) {
          this.mfaSetupActive = false;
          this.mfaMessage = 'MFA has been enabled on your account.';
          if (this.user) {
            this.user = { ...this.user, mfaEnabled: true };
          }
        } else {
          this.mfaError = res.errors?.[0] ?? 'Invalid verification code.';
        }
      },
      error: (err) => (this.mfaError = err?.error?.errors?.[0] ?? 'Invalid verification code.')
    });
  }

  cancelMfaSetup(): void {
    this.mfaSetupActive = false;
    this.mfaSharedKey = null;
    this.mfaAuthenticatorUri = null;
  }

  disableMfa(): void {
    this.mfaMessage = null;
    this.mfaError = null;
    this.authService.disableMfa().subscribe({
      next: () => {
        this.mfaMessage = 'MFA has been disabled on your account.';
        if (this.user) {
          this.user = { ...this.user, mfaEnabled: false };
        }
      },
      error: () => (this.mfaError = 'Unable to disable MFA.')
    });
  }
}
