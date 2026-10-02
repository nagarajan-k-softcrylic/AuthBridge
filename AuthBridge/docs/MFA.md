# Multi-Factor Authentication (MFA)

TOTP-based (Time-based One-Time Password) second factor for the Basic Authentication
(email/password) login flow, built on ASP.NET Core Identity's authenticator token provider.
Compatible with any standard authenticator app (Google Authenticator, Microsoft Authenticator,
Authy, etc.).

## How it works

1. **Setup** — The signed-in user requests a shared secret (`GET /api/auth/mfa/setup`). The
   secret is stored (unconfirmed) via `UserManager.ResetAuthenticatorKeyAsync`. The response
   includes the raw key and an `otpauth://` URI; the UI renders the URI as a QR code (via
   `api.qrserver.com`) for scanning, plus the raw key as a manual-entry fallback.
2. **Enable** — The user enters the 6-digit code their app generated. `POST /api/auth/mfa/enable`
   verifies it with `UserManager.VerifyTwoFactorTokenAsync` and, if valid, sets
   `ApplicationUser.MfaEnabled = true`.
3. **Login challenge** — When an MFA-enabled user logs in with `POST /api/auth/login`, the
   password is checked as normal, but instead of issuing tokens the response returns
   `{ succeeded: true, requiresMfa: true, userId, email }`. No JWT/refresh cookies are set yet.
4. **Verify** — The client prompts for a code and calls `POST /api/auth/mfa/verify` with
   `{ userId, code, rememberDevice }`. On success this behaves like a normal login: it signs in
   via `SignInManager`, issues the JWT + refresh token cookies, and (if `rememberDevice` was
   checked) also issues a "trusted device" cookie (see below).
5. **Disable** — `POST /api/auth/mfa/disable` turns MFA off, resets the authenticator key, and
   revokes all trusted-device records for the account.

## "Remember this device" (7-day MFA bypass)

To avoid prompting for a code on every login from the same browser, the user can opt in via a
checkbox on the MFA verification step.

- On `mfa/verify` with `rememberDevice = true`, the server generates a random token, stores only
  its SHA-256 hash in the `MfaTrustedDevices` table (`UserId`, `TokenHash`, `ExpiresAtUtc`), and
  returns the raw token once.
- `AuthController` stores the raw token in an httpOnly, Secure, `SameSite=Strict` cookie
  (`authbridge_trusted_device`, scoped to `/api/auth`, 7-day expiry) — mirroring how refresh
  tokens are handled. The raw value is never exposed in the JSON response body.
- On the next `POST /api/auth/login`, the controller forwards this cookie to
  `AuthService.LoginAsync`. If it matches an active (unexpired) record for that user, the MFA
  challenge is skipped entirely and tokens are issued immediately.
- The trusted-device cookie **survives logout and refresh-token failures** — only an explicit
  **Disable MFA** clears it (since trusting a device only makes sense while MFA is on).
- Expiry is enforced both by the cookie's own lifetime and server-side via
  `MfaTrustedDevice.IsActive` (`DateTime.UtcNow < ExpiresAtUtc`), so a stale/looted cookie past
  7 days is rejected even if somehow replayed.

## Data model

```
ApplicationUser
├── AuthenticatorKey   (from Identity's AspNetUserTokens, via UserManager)
└── MfaEnabled: bool

MfaTrustedDevice
├── Id: int
├── UserId: string      (FK -> AspNetUsers, cascade delete)
├── TokenHash: string    (SHA-256 hex, unique index)
├── ExpiresAtUtc: DateTime
└── CreatedAtUtc: DateTime
```

Migration: `Migrations/20261002062108_AddMfaTrustedDevices.cs`.

## API reference

| Method | Route | Auth | Purpose |
|---|---|---|---|
| GET  | `/api/auth/mfa/setup`  | Bearer | Returns `{ sharedKey, authenticatorUri }` for QR/manual setup |
| POST | `/api/auth/mfa/enable` | Bearer | Body `{ code }` — confirms setup, turns MFA on |
| POST | `/api/auth/mfa/disable`| Bearer | Turns MFA off, revokes trusted devices |
| POST | `/api/auth/login`      | Anon   | Body `{ email, password }` — may return `requiresMfa: true` |
| POST | `/api/auth/mfa/verify` | Anon   | Body `{ userId, code, rememberDevice }` — completes login |
| GET  | `/api/auth/me`         | Bearer | Includes `mfaEnabled` so the UI knows which button to show |

`AuthResponseDto` fields relevant to MFA:

- `requiresMfa: bool` — set by `login` when a second factor is needed.
- `mfaEnabled: bool` — current MFA status for the account (used by `/me`, `/mfa/enable`, `/mfa/disable`).
- `trustedDeviceToken: string?` — internal only; the controller moves it into a cookie and nulls
  it before the response is serialized, so it never reaches the client as JSON.

## Frontend (Angular)

- `AuthService` — `getMfaSetup()`, `enableMfa(code)`, `disableMfa()`, `verifyMfa({ userId, code, rememberDevice })`.
- `LoginComponent` — after a password login returns `requiresMfa: true`, swaps to a code-entry
  form with a "Remember this device for 7 days" checkbox.
- `HomeComponent` — renders the QR code (`mfaQrCodeUrl` getter wraps the `otpauth://` URI via
  `api.qrserver.com`) during setup, and shows **either** an "Enable MFA" or "Disable MFA" button
  based on `user.mfaEnabled` (never both at once).

## Security notes

- Only SHA-256 hashes of refresh tokens and trusted-device tokens are persisted — raw values
  exist only in the httpOnly cookie and the one-time API response.
- All MFA-related cookies are `HttpOnly`, `Secure`, `SameSite=Strict`, so plain HTTP (`dotnet run`
  without the `https` launch profile) will not work — use `--launch-profile https`.
- Disabling MFA immediately invalidates all remembered devices, closing the bypass window.

## Important things to keep in mind

- **A `MfaTrustedDevices` row is only created when the user explicitly checks "Remember this
  device for 7 days" on the verify-code step and submits a *correct* TOTP code.** A plain
  successful MFA verification without the checkbox ticked never inserts a row — so an empty
  table is expected/correct if that box wasn't checked during testing, not a bug.
- The trusted-device cookie is scoped to `Path=/api/auth`, so it is only ever sent back on
  `/api/auth/...` requests (e.g. `login`). It will not appear in the browser's cookie list for
  other paths, and it will not be sent if the app is accessed over a different host/port than the
  one that set it.
- Because the cookie is `Secure=true`, it is **silently dropped by the browser over plain HTTP**.
  Always test MFA (and "remember device") against the `https` launch profile
  (`--launch-profile https`), not `http://localhost:5001`.
- `rememberDevice` is a one-time choice made per login — it is not "sticky" across future logins.
  If the user logs out, clears cookies, or uses a different browser/device, they must check the
  box again on the next MFA challenge to get another 7-day trusted window.
- One `MfaTrustedDevices` row is created *per device/browser remembered*, not one per user. A user
  who remembers a device from both Chrome and Firefox will have two separate rows.
- Toggling MFA off and back on wipes all trusted devices for that user (`DisableMfaAsync` deletes
  every row for `UserId`), so re-enabling MFA always starts with a clean trust list — previously
  trusted browsers will be challenged again.
- Rows are **not** automatically deleted when they expire — `ExpiresAtUtc` is only checked at
  verification time (`IsTrustedDeviceAsync` / `MfaTrustedDevice.IsActive`). Expired rows remain in
  the table until disable/re-enable clears them or a cleanup job is added; querying the table
  directly can therefore show stale, expired entries that still look "present" but no longer grant
  a bypass.
- `AuthenticatorKey` (the TOTP secret) lives in ASP.NET Core Identity's own `AspNetUserTokens`
  table, not in `MfaTrustedDevices` or `AspNetUsers` — only the boolean `MfaEnabled` flag is on
  `ApplicationUser`.
