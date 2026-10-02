# OpenID Connect (OIDC) & Single Sign-On (SSO)

AuthBridge ஏன் OpenID Connect-ஐ பயன்படுத்துகிறது, இந்த codebase-ல் இந்த protocol end-to-end
எப்படி வேலை செய்கிறது, மற்றும் SSO-க்கு இது ஏன் சரியான building block என்பதை விளக்குகிறது.

## ஏன் OIDC தேவை?

Basic Authentication (email + password, `POST /api/auth/login`) என்பது *இந்த ஒரு application-க்கு
மட்டும்* user யார் என்பதை நிரூபிக்கிறது. SSO என்றால் "ஒரு trusted Identity Provider (IdP)-இடம்
ஒரே ஒரு முறை user யார் என்பதை நிரூபித்து, அந்த proof-ஐ ஒவ்வொரு application-உம் ஏற்றுக்கொள்ளும்
வகையில் வைத்திருப்பது" — ஒவ்வொரு app-க்கும் credentials மீண்டும் மீண்டும் type செய்ய தேவையில்லை,
மேலும் இந்த app ஒருபோதும் Microsoft Entra ID password-ஐ பார்க்கவோ சேமிக்கவோ தேவையில்லை.

OIDC-ஐ தேர்வு செய்ததற்கான காரணங்கள் (ஒரு custom token exchange-ஐ கண்டுபிடிப்பதற்கு பதில்):

- இது ஒரு **industry-standard, audited protocol** (OAuth2-ன் மேல் கட்டப்பட்டது) — browsers,
  authenticator apps, மற்றும் ஒவ்வொரு முக்கிய IdP-யும் (Entra ID, Google, Okta, Duende
  IdentityServer) ஏற்கனவே இதை பேசுகின்றன.
- OAuth2-க்கு மேல் ஒரு **standardized identity layer**-ஐ (`id_token`, `sub`, `email`, `name`
  போன்றவற்றுடன் கூடிய signed JWT) சேர்க்கிறது — OAuth2 மட்டும் authorization/access tokens-ஐ
  மட்டுமே handle செய்யும். AuthBridge-க்கு user *யார்* என்பது தெரிய வேண்டும், வெறும் access
  grant இருக்கிறதா என்பது மட்டும் அல்ல — இதுதான் சரியாக OIDC-யின் வேலை.
- **Authorization Code flow**-ஐ support செய்கிறது, இதில் credentials நேரடியாக browser → IdP →
  back-channel token exchange இடையே பரிமாறப்படும், எனவே இந்த app-இன் backend user-இன் Entra ID
  password-ஐ ஒருபோதும் தொடாது.
- Token **signatures verify செய்யக்கூடியவை** (IdP-யின் published JWKS/metadata மூலம்), எனவே
  பெறப்பட்ட `id_token`/access token-ஐ ஒவ்வொரு request-க்கும் IdP-இடம் live round-trip செய்யாமலேயே
  trust செய்யலாம்.

## இந்த codebase-ல் இரண்டு தனித்தனி OIDC relationships

AuthBridge உண்மையில் OIDC-யின் **இரண்டு roles-ஐயும்** ஒரே நேரத்தில், இரண்டு வெவ்வேறு
நோக்கங்களுக்காக வகிக்கிறது:

1. **AuthBridge ஒரு Identity Provider-ஆக** (Duende IdentityServer மூலம், `Program.cs`-ல்
   `AddIdentityServer()`): Basic Authentication பயன்படுத்தும் அதே `AspNetUsers` table-ஐ
   backend-ஆக கொண்டு, API/UI clients-க்கு தன் சொந்த OIDC tokens-ஐ issue செய்கிறது. இது `"oidc"`
   scheme.
2. **AuthBridge ஒரு Relying Party / client-ஆக** **Microsoft Entra ID**-க்கு: "Continue with SSO"
   button-க்காக authentication-ஐ Entra ID-க்கு delegate செய்து, முதல் login-லேயே ஒரு local
   user-ஐ auto-provision செய்கிறது. இது `"EntraId"` scheme.

இரண்டும் `Program.cs`-ல் தனித்தனி `AddOpenIdConnect(...)` handlers-ஆக, வெவ்வேறு `Authority`,
`ClientId`/`ClientSecret`, மற்றும் `CallbackPath`-உடன் register செய்யப்பட்டுள்ளன, எனவே அவை
ஒன்றுக்கொன்று மோதாது (collide ஆகாது).

## Flow 1 — Local OIDC (IdentityServer) SSO

```
Browser          AuthBridge (Angular SPA)      AuthBridge API + IdentityServer
  |  GET /api/auth/oidc-login                           |
  |---------------------------------------------------->|
  |            Challenge("oidc") -> 302 to /connect/authorize
  |<------------------------------------------------------|
  |  user-க்கு ஏற்கனவே "Cookies"/"Identity.Application" session இருக்கும் (எ.கா. Basic login-ல் இருந்து)
  |  IdentityServer ஒரு authorization code issue செய்து, திரும்ப redirect செய்யும்
  |----------------------------------------------------------------------------------->|
  |            OIDC middleware code-ஐ tokens-ஆக exchange செய்து, "Cookies" principal-ஐ ஏற்படுத்தும்
  |            /api/auth/oidc-callback-க்கு redirect செய்யும்
  |<------------------------------------------------------|
  |  OidcCallback() ClaimTypes.NameIdentifier/"sub"-ஐ படித்து,
  |  IssueTokensForOidcUserAsync(userId)-ஐ call செய்யும் -> Basic Auth login போலவே அதே
  |  JWT + refresh-token cookies-ஐ மின்ட் செய்து, /home-க்கு redirect செய்யும்
```

இது ஏன் இருக்கிறது: OIDC/SSO plumbing (handler config, cookie schemes, callback bridging)
external IdPs சேர்ப்பதற்கு முன், AuthBridge-இன் சொந்த IdentityServer-க்கு எதிராக end-to-end
வேலை செய்கிறது என்பதை நிரூபிக்கிறது, மேலும் இந்த app-இன் custom JWT format-ஐ புரிந்துகொள்ளாத,
OIDC-ஐ மட்டும் புரிந்துகொள்ளும் எந்த future client-க்கும், அதே user store-க்கு எதிராக
authenticate செய்ய ஒரு path தருகிறது.

## Flow 2 — Microsoft Entra ID SSO (external IdP)

```
Browser                 AuthBridge API                         Microsoft Entra ID
  |  GET /api/auth/sso-login?email=user@contoso.com                  |
  |-------------------------------------------------->|
  |     Challenge("EntraId") -> 302 to Entra's /authorize, login_hint=email
  |<----------------------------------------------------|
  |------------------------------------------------------------------------------>|
  |            user Entra-உடன் authenticate செய்கிறார் (password, MFA, Conditional Access policies, etc.)
  |<--------------------------------------------------------------------------------|
  |    /signin-oidc-entra (CallbackPath)-க்கு ஒரு authorization code-உடன் redirect
  |-------------------------------------------------->|
  |     OIDC middleware code-ஐ tokens-ஆக exchange செய்து, id_token signature-ஐ Entra-வின்
  |     JWKS-க்கு எதிராக validate செய்து, ஒரு "Cookies" principal-ஐ ஏற்படுத்தும்
  |     (SignInScheme = "Cookies"), /api/auth/entra-callback-க்கு redirect செய்யும்
  |<----------------------------------------------------|
  |  EntraCallback() Entra id_token-இல் இருந்து email/given_name/surname claims-ஐ படித்து,
  |  IssueTokensForExternalUserAsync(email, firstName, lastName)-ஐ call செய்யும், அது ஒரு local
  |  ApplicationUser row-ஐ கண்டுபிடிக்கும்/உருவாக்கும், பிறகு AuthBridge-இன் சொந்த JWT +
  |  refresh-token cookies-ஐ மின்ட் செய்து, /home-க்கு redirect செய்யும்
```

முக்கிய config (`Program.cs`, `"EntraId"` handler):

- `Authority = https://login.microsoftonline.com/{TenantId}/v2.0` — Entra-வின் metadata
  (`/.well-known/openid-configuration`) மற்றும் public signing keys-ஐ எங்கே fetch செய்வது என
  OIDC middleware-க்கு சொல்கிறது.
- `SignInScheme = "Cookies"` — **அவசியம்**; இது இல்லாமல், ASP.NET Core Identity-யின் சொந்த
  `"Identity.Application"` cookie scheme ambiguous default ஆகிவிடும், மேலும்
  `EntraCallback()`-இன் `[Authorize]`, Entra-authenticated principal-ஐ பார்க்க முடியாமல்,
  local `"oidc"` client-க்கு ஒரு challenge-ஆக fall through ஆகிவிடும் (இது முன்பு redirect-loop
  bug-ஐ ஏற்படுத்திய symptom).
- `Scope`: `openid email profile` — `id_token`-ல் `sub`, `email`, `given_name`, `family_name`
  claims திரும்ப பெற தேவையான minimum.
- `login_hint` (`OnRedirectToIdentityProvider` மூலம்) — SSO tab-ல் user type செய்த email-ஐ
  pre-fill செய்கிறது, இதனால் Entra-வின் login page ஒரு generic "pick an account" screen-ஐ
  காட்டாது.

## இது ஏன் SSO-க்கு *சரியான* mechanism

- **Password பகிரப்படாது**: AuthBridge user-இன் Entra ID (corporate) password-ஐ ஒருபோதும்
  சேகரிக்காது அல்லது சேமிக்காது — Entra ID மட்டுமே அதை பார்க்கும். SSO உருவாக்க வேண்டிய core
  trust boundary இதுதான்.
- **ஒருங்கிணைந்த (centralized) policy enforcement**: Conditional Access, MFA requirements,
  device compliance, மற்றும் account disablement அனைத்தும் Entra ID-யாலேயே enforce
  செய்யப்படுகின்றன, இங்கு கூடுதல் code எதுவும் இல்லாமலேயே AuthBridge-க்கு தானாக apply ஆகிறது.
- **Auto-provisioning**: `IssueTokensForExternalUserAsync` email மூலம் user-ஐ
  கண்டுபிடிக்கும்/உருவாக்கும், எனவே Entra ID-ல் இருக்கும் ஒரு புதிய employee, முதல்
  முயற்சியிலேயே எந்த manual account setup-உம் இல்லாமல் AuthBridge-ல் sign in செய்யலாம்.
- **ஒரே session model, downstream**: Basic Auth, local OIDC, அல்லது Entra ID — எந்த வழியாக
  user வந்தாலும், ஒவ்வொரு flow-உம் `SetAuthCookies(result)`-ல் முடிவடைகிறது — அதே JWT +
  refresh token cookie pair. மீதமுள்ள app (interceptors, `[Authorize(AuthenticationSchemes =
  "Bearer")]` endpoints, refresh/logout) எந்த login path பயன்படுத்தப்பட்டது என்பதை தெரிந்துகொள்ள
  தேவையில்லை.
- **Standardized token validation**: `id_token`-கள் IdP-யின் published keys-க்கு எதிராக
  validate செய்யப்படும் signed JWTs ஆனதால், ஒவ்வொரு request-க்கும் "இந்த token-ஐ check செய்ய
  Microsoft-க்கு திரும்ப call செய்" என்ற custom round trip தேவையில்லை — callback-இன் போது
  ஒரே ஒரு முறை மட்டும்.

## `Program.cs`-ல் உள்ள schemes-இன் சுருக்கம்

| Scheme | Role | நோக்கம் |
|---|---|---|
| `Cookies` | Default scheme | எந்த OIDC handshake (local அல்லது Entra) முடிந்த பின்பும், authenticated principal-ஐ வைத்திருக்கும் |
| `oidc` | Default challenge scheme | Local IdentityServer client — explicit scheme இல்லாத `[Authorize]`, இங்கு redirect செய்யும் |
| `EntraId` | Named OIDC handler | "Continue with SSO"-க்கான external IdP; explicit `SignInScheme = "Cookies"` |
| `Bearer` | Named JWT handler | `GET /api/auth/me` போன்ற API endpoints-க்கு, (`authbridge_token` cookie-இல் இருந்து) AuthBridge-இன் சொந்த JWT-ஐ validate செய்யும் — redirect செய்யாமல் 401 திருப்பும் |
