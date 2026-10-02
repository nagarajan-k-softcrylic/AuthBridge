# பல-காரணி அங்கீகாரம் (MFA)

Basic Authentication (email/password) login flow-க்கு, ASP.NET Core Identity-யின் authenticator
token provider-ஐ பயன்படுத்தி உருவாக்கப்பட்ட TOTP (Time-based One-Time Password) அடிப்படையிலான
இரண்டாம்-காரணி (second factor) அங்கீகாரம். Google Authenticator, Microsoft Authenticator, Authy
போன்ற எந்த standard authenticator app-உடனும் இணக்கமானது (compatible).

## இது எப்படி வேலை செய்கிறது

1. **Setup** — Sign-in ஆன user ஒரு shared secret-ஐ கோருகிறார் (`GET /api/auth/mfa/setup`).
   இந்த secret `UserManager.ResetAuthenticatorKeyAsync` மூலம் (unconfirmed state-ல்)
   சேமிக்கப்படுகிறது. Response-ல் raw key மற்றும் `otpauth://` URI இருக்கும்; UI அந்த URI-ஐ
   (`api.qrserver.com` மூலம்) ஒரு QR code-ஆக render செய்கிறது scan செய்ய, மேலும் manual-entry
   fallback-ஆக raw key-யும் காட்டப்படும்.
2. **Enable** — User தன் app-ல் வந்த 6-digit code-ஐ enter செய்கிறார். `POST /api/auth/mfa/enable`
   அதை `UserManager.VerifyTwoFactorTokenAsync` மூலம் verify செய்து, சரியாக இருந்தால்
   `ApplicationUser.MfaEnabled = true` என set செய்கிறது.
3. **Login challenge** — MFA enable செய்யப்பட்ட user `POST /api/auth/login` மூலம் login
   செய்யும்போது, password சரிபார்க்கப்பட்ட பின்பும் tokens issue செய்யாமல்
   `{ succeeded: true, requiresMfa: true, userId, email }` என response திருப்பப்படும். இன்னும்
   JWT/refresh cookies set செய்யப்படாது.
4. **Verify** — Client ஒரு code கேட்டு `POST /api/auth/mfa/verify`-ஐ
   `{ userId, code, rememberDevice }`-உடன் call செய்கிறது. Success ஆனால் இது normal login போலவே
   நடக்கும்: `SignInManager` மூலம் sign in செய்து, JWT + refresh token cookies issue செய்யும்,
   மேலும் (`rememberDevice` checked ஆக இருந்தால்) ஒரு "trusted device" cookie-யும் issue
   செய்யும் (கீழே பார்க்கவும்).
5. **Disable** — `POST /api/auth/mfa/disable` MFA-ஐ off செய்து, authenticator key-ஐ reset செய்து,
   அந்த account-இன் trusted-device records அனைத்தையும் revoke செய்கிறது.

## "Remember this device" (7-நாள் MFA bypass)

ஒரே browser-ல் ஒவ்வொரு login-லும் code கேட்காமல் இருக்க, verify-code படியில் ஒரு checkbox மூலம்
user opt-in செய்யலாம்.

- `mfa/verify`-ல் `rememberDevice = true` என வரும்போது, server ஒரு random token உருவாக்கி, அதன்
  SHA-256 hash-ஐ மட்டும் `MfaTrustedDevices` table-ல் (`UserId`, `TokenHash`, `ExpiresAtUtc`)
  சேமிக்கும், மற்றும் raw token-ஐ ஒரு முறை மட்டும் response-ல் திருப்பும்.
- `AuthController` அந்த raw token-ஐ ஒரு httpOnly, Secure, `SameSite=Strict` cookie-ல்
  (`authbridge_trusted_device`, `/api/auth`-க்கு மட்டும் scoped, 7-நாள் expiry) சேமிக்கிறது —
  refresh tokens handle செய்யப்படும் விதத்தை ஒத்தது. Raw value JSON response body-ல் ஒருபோதும்
  வெளிப்படாது.
- அடுத்த `POST /api/auth/login`-ல், controller இந்த cookie-ஐ `AuthService.LoginAsync`-க்கு
  forward செய்கிறது. அது அந்த user-க்கான active (expire ஆகாத) record-உடன் match ஆனால், MFA
  challenge முழுவதுமாக skip செய்யப்பட்டு tokens உடனடியாக issue செய்யப்படும்.
- Trusted-device cookie **logout மற்றும் refresh-token failures-ஐயும் தாண்டி survive ஆகும்** —
  ஒரு explicit **Disable MFA** மட்டுமே இதை clear செய்யும் (MFA on-ஆக இருக்கும்போது மட்டுமே
  device trust செய்வது அர்த்தமுள்ளது).
- Expiry, cookie-யின் சொந்த lifetime மற்றும் server-side-ல்
  `MfaTrustedDevice.IsActive` (`DateTime.UtcNow < ExpiresAtUtc`) மூலமும் enforce செய்யப்படுகிறது,
  எனவே 7 நாட்களுக்கு மேல் stale/leaked cookie replay செய்யப்பட்டாலும் reject ஆகும்.

## Data model

```
ApplicationUser
├── AuthenticatorKey   (Identity-யின் AspNetUserTokens-இல் இருந்து, UserManager மூலம்)
└── MfaEnabled: bool

MfaTrustedDevice
├── Id: int
├── UserId: string      (FK -> AspNetUsers, cascade delete)
├── TokenHash: string    (SHA-256 hex, unique index)
├── ExpiresAtUtc: DateTime
└── CreatedAtUtc: DateTime
```

Migration: `Migrations/20261002062108_AddMfaTrustedDevices.cs`.

## API Reference

| Method | Route | Auth | நோக்கம் |
|---|---|---|---|
| GET  | `/api/auth/mfa/setup`  | Bearer | QR/manual setup-க்கு `{ sharedKey, authenticatorUri }` திருப்பும் |
| POST | `/api/auth/mfa/enable` | Bearer | Body `{ code }` — setup-ஐ confirm செய்து MFA-ஐ on செய்யும் |
| POST | `/api/auth/mfa/disable`| Bearer | MFA-ஐ off செய்து trusted devices-ஐ revoke செய்யும் |
| POST | `/api/auth/login`      | Anon   | Body `{ email, password }` — `requiresMfa: true` திருப்பலாம் |
| POST | `/api/auth/mfa/verify` | Anon   | Body `{ userId, code, rememberDevice }` — login-ஐ complete செய்யும் |
| GET  | `/api/auth/me`         | Bearer | `mfaEnabled` உள்ளடக்கியது, எந்த button காட்டவேண்டும் என UI அறிய |

MFA-க்கு தொடர்புடைய `AuthResponseDto` fields:

- `requiresMfa: bool` — இரண்டாம் காரணி தேவைப்படும்போது `login` இதை set செய்யும்.
- `mfaEnabled: bool` — account-இன் தற்போதைய MFA status (`/me`, `/mfa/enable`, `/mfa/disable`-ல்
  பயன்படுத்தப்படுகிறது).
- `trustedDeviceToken: string?` — internal use மட்டும்; controller இதை cookie-க்குள் மாற்றி,
  response serialize ஆவதற்கு முன் null செய்துவிடும், எனவே இது client-க்கு JSON-ஆக ஒருபோதும்
  போகாது.

## Frontend (Angular)

- `AuthService` — `getMfaSetup()`, `enableMfa(code)`, `disableMfa()`,
  `verifyMfa({ userId, code, rememberDevice })`.
- `LoginComponent` — password login `requiresMfa: true` திருப்பிய பின், code-entry form-க்கு
  மாறும், அதில் "Remember this device for 7 days" checkbox இருக்கும்.
- `HomeComponent` — setup-இன் போது QR code-ஐ render செய்கிறது (`mfaQrCodeUrl` getter
  `otpauth://` URI-ஐ `api.qrserver.com` மூலம் wrap செய்கிறது), மேலும் `user.mfaEnabled`-ஐ
  பொறுத்து **ஒன்று மட்டும்** — "Enable MFA" அல்லது "Disable MFA" button-ஐ காட்டும் (இரண்டும்
  ஒரே நேரத்தில் ஒருபோதும் காட்டப்படாது).

## Security குறிப்புகள்

- Refresh tokens மற்றும் trusted-device tokens-இன் SHA-256 hashes மட்டுமே persist
  செய்யப்படுகின்றன — raw values httpOnly cookie-லும், ஒரு முறை மட்டும் வரும் API response-லும்
  தான் இருக்கும்.
- MFA தொடர்பான cookies அனைத்தும் `HttpOnly`, `Secure`, `SameSite=Strict` ஆக இருப்பதால், plain
  HTTP-ல் (`https` launch profile இல்லாமல் `dotnet run`) இது வேலை செய்யாது — `--launch-profile
  https`-ஐ பயன்படுத்தவும்.
- MFA-ஐ disable செய்தவுடன் remembered devices அனைத்தும் உடனடியாக invalid ஆகிவிடும், bypass
  window மூடப்படும்.

## முக்கியமாக நினைவில் கொள்ள வேண்டியவை

- **`MfaTrustedDevices` table-ல் ஒரு row, user verify-code படியில் "Remember this device for 7
  days" checkbox-ஐ check செய்து, சரியான TOTP code-ஐ submit செய்தால் மட்டுமே உருவாகும்.**
  Checkbox check செய்யாமல் வெறும் MFA verify success ஆனால் row insert ஆகாது — எனவே testing-ல்
  checkbox check செய்யவில்லை என்றால் table empty-ஆக இருப்பது bug இல்லை, expected behavior தான்.
- Trusted-device cookie `Path=/api/auth`-க்கு மட்டும் scoped ஆக இருப்பதால், அது
  `/api/auth/...` requests-க்கு (எ.கா. `login`) மட்டுமே திரும்ப அனுப்பப்படும். வேறு path-களில்
  browser-இன் cookie list-ல் இது தெரியாது, மேலும் cookie set செய்த host/port வேறு ஒன்றில்
  app-ஐ access செய்தால் இது அனுப்பப்படாது.
- Cookie `Secure=true` ஆக இருப்பதால், plain HTTP-ல் browser இதை **அமைதியாக drop செய்துவிடும்**.
  MFA ("remember device" உட்பட) எப்போதும் `https` launch profile-க்கு எதிராகவே (`--launch-profile
  https`) test செய்யவும், `http://localhost:5001` அல்ல.
- `rememberDevice` என்பது ஒவ்வொரு login-க்கும் ஒரு முறை எடுக்கப்படும் choice — எதிர்கால
  logins-க்கு இது "sticky" அல்ல. User logout செய்தாலோ, cookies clear செய்தாலோ, வேறு
  browser/device பயன்படுத்தினாலோ, அடுத்த MFA challenge-ல் மீண்டும் checkbox-ஐ check செய்ய
  வேண்டும் இன்னொரு 7-நாள் trusted window பெற.
- ஒவ்வொரு remember செய்யப்பட்ட device/browser-க்கும் ஒரு `MfaTrustedDevices` row உருவாகும்,
  user-க்கு ஒன்று அல்ல. Chrome மற்றும் Firefox இரண்டிலும் device remember செய்த ஒரு user-க்கு
  இரண்டு தனித்தனி rows இருக்கும்.
- MFA-ஐ off செய்து மீண்டும் on செய்வது, அந்த user-இன் trusted devices அனைத்தையும் அழித்துவிடும்
  (`DisableMfaAsync` அந்த `UserId`-க்கான ஒவ்வொரு row-ஐயும் delete செய்யும்), எனவே MFA-ஐ
  மீண்டும் enable செய்யும்போது trust list எப்போதும் காலியாகவே தொடங்கும் — முன்பு trust செய்யப்பட்ட
  browsers மீண்டும் challenge செய்யப்படும்.
- Rows expire ஆனவுடன் தானாக delete செய்யப்படாது — `ExpiresAtUtc`, verification நேரத்தில் மட்டுமே
  check செய்யப்படுகிறது (`IsTrustedDeviceAsync` / `MfaTrustedDevice.IsActive`). Expired rows,
  disable/re-enable clear செய்யும் வரை அல்லது ஒரு cleanup job சேர்க்கும் வரை table-ல்
  இருக்கும்; table-ஐ நேரடியாக query செய்தால் "present" போல் தெரியும் ஆனால் bypass தராத
  stale rows-ஐ காணலாம்.
- `AuthenticatorKey` (TOTP secret), ASP.NET Core Identity-யின் சொந்த `AspNetUserTokens`
  table-ல்தான் இருக்கும், `MfaTrustedDevices` அல்லது `AspNetUsers`-ல் இல்லை — `ApplicationUser`-ல்
  `MfaEnabled` boolean flag மட்டுமே இருக்கிறது.
