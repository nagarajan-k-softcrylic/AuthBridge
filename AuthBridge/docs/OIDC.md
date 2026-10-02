# OpenID Connect (OIDC) & Single Sign-On (SSO)

Explains why AuthBridge uses OpenID Connect, how the protocol works end-to-end in this codebase,
and why it's the right building block for SSO.

## Why OIDC at all?

Basic Authentication (email + password, `POST /api/auth/login`) only proves *who the user is to
this one application*. SSO means "prove who the user is once, to a trusted Identity Provider (IdP),
and let every application accept that proof" — the user doesn't re-enter credentials per app, and
this app never has to see/store a Microsoft Entra ID password at all.

OIDC was chosen (over inventing a custom token exchange) because:

- It's an **industry-standard, audited protocol** (built on top of OAuth2) — browsers, authenticator
  apps, and every major IdP (Entra ID, Google, Okta, Duende IdentityServer) already speak it.
- It adds a **standardized identity layer** (the `id_token`, a signed JWT with `sub`, `email`,
  `name`, etc.) on top of OAuth2, which only handles authorization/access tokens. AuthBridge needs
  to know *who* the user is, not just that they have an access grant — that's exactly OIDC's job.
- It supports the **Authorization Code flow**, where credentials are exchanged directly between the
  browser → IdP → back-channel token exchange, so this app's backend never touches the user's Entra
  ID password.
- Token **signatures are verifiable** (via the IdP's published JWKS/metadata), so a received
  `id_token`/access token can be trusted without a live round-trip to the IdP on every request.

## Two separate OIDC relationships in this codebase

AuthBridge actually plays **both roles** of OIDC at once, for two different purposes:

1. **AuthBridge as Identity Provider** (via Duende IdentityServer, `Program.cs`
   `AddIdentityServer()`): issues its own OIDC tokens to API/UI clients, backed by the same
   `AspNetUsers` table as Basic Authentication. This is the `"oidc"` scheme.
2. **AuthBridge as Relying Party / client** to **Microsoft Entra ID**: delegates authentication to
   Entra ID for the "Continue with SSO" button, then auto-provisions a local user on first login.
   This is the `"EntraId"` scheme.

Both are registered as separate `AddOpenIdConnect(...)` handlers in `Program.cs` with different
`Authority`, `ClientId`/`ClientSecret`, and `CallbackPath`, so they don't collide.

## Flow 1 — Local OIDC (IdentityServer) SSO

```
Browser          AuthBridge (Angular SPA)      AuthBridge API + IdentityServer
  |  GET /api/auth/oidc-login                           |
  |---------------------------------------------------->|
  |            Challenge("oidc") -> 302 to /connect/authorize
  |<------------------------------------------------------|
  |  user already has "Cookies"/"Identity.Application" session (e.g. from Basic login)
  |  IdentityServer issues an authorization code, redirects back
  |----------------------------------------------------------------------------------->|
  |            OIDC middleware exchanges code for tokens, establishes "Cookies" principal
  |            redirects to /api/auth/oidc-callback
  |<------------------------------------------------------|
  |  OidcCallback() reads ClaimTypes.NameIdentifier/"sub", calls
  |  IssueTokensForOidcUserAsync(userId) -> mints the SAME JWT + refresh-token cookies
  |  as Basic Auth login, redirects to /home
```

Why this exists: it proves the OIDC/SSO plumbing (handler config, cookie schemes, callback
bridging) works end-to-end against AuthBridge's own IdentityServer before adding external IdPs,
and gives a path for any future client that only understands OIDC (not this app's custom JWT
format) to still authenticate against the same user store.

## Flow 2 — Microsoft Entra ID SSO (external IdP)

```
Browser                 AuthBridge API                         Microsoft Entra ID
  |  GET /api/auth/sso-login?email=user@contoso.com                  |
  |-------------------------------------------------->|
  |     Challenge("EntraId") -> 302 to Entra's /authorize, login_hint=email
  |<----------------------------------------------------|
  |------------------------------------------------------------------------------>|
  |            user authenticates with Entra (password, MFA, Conditional Access policies, etc.)
  |<--------------------------------------------------------------------------------|
  |    redirect to /signin-oidc-entra (CallbackPath) with an authorization code
  |-------------------------------------------------->|
  |     OIDC middleware exchanges code for tokens, validates id_token signature against
  |     Entra's JWKS, establishes a "Cookies" principal (SignInScheme = "Cookies"),
  |     redirects to /api/auth/entra-callback
  |<----------------------------------------------------|
  |  EntraCallback() reads email/given_name/surname claims from the Entra id_token,
  |  calls IssueTokensForExternalUserAsync(email, firstName, lastName) which finds-or-creates
  |  a local ApplicationUser row, then mints AuthBridge's own JWT + refresh-token cookies,
  |  redirects to /home
```

Key config (`Program.cs`, `"EntraId"` handler):

- `Authority = https://login.microsoftonline.com/{TenantId}/v2.0` — tells the OIDC middleware
  where to fetch Entra's metadata (`/.well-known/openid-configuration`) and public signing keys.
- `SignInScheme = "Cookies"` — **required**; without it, ASP.NET Core Identity's own
  `"Identity.Application"` cookie scheme becomes the ambiguous default, and `EntraCallback()`'s
  `[Authorize]` fails to see the Entra-authenticated principal, falling through to a challenge on
  the local `"oidc"` client instead (the symptom that caused the earlier redirect-loop bug).
- `Scope`: `openid email profile` — the minimum needed to get `sub`, `email`, `given_name`,
  `family_name` claims back in the `id_token`.
- `login_hint` (via `OnRedirectToIdentityProvider`) — pre-fills the email the user typed on the
  SSO tab so Entra's login page doesn't show a generic "pick an account" screen.

## Why this is *the* right mechanism for SSO specifically

- **No password sharing**: AuthBridge never collects or stores the user's Entra ID (corporate)
  password — only Entra ID ever sees it. This is the core trust boundary SSO is supposed to create.
- **Centralized policy enforcement**: Conditional Access, MFA requirements, device compliance, and
  account disablement are all enforced by Entra ID itself, automatically applying to AuthBridge
  without any extra code here.
- **Auto-provisioning**: `IssueTokensForExternalUserAsync` finds-or-creates the local user by
  email, so a brand-new employee who exists in Entra ID can sign into AuthBridge on first try with
  zero manual account setup.
- **One session model downstream**: regardless of whether the user came through Basic Auth, local
  OIDC, or Entra ID, every flow ends at `SetAuthCookies(result)` — the exact same JWT + refresh
  token cookie pair. The rest of the app (interceptors, `[Authorize(AuthenticationSchemes =
  "Bearer")]` endpoints, refresh/logout) doesn't need to know or care which login path was used.
- **Standardized token validation**: because `id_token`s are signed JWTs validated against the
  IdP's published keys, there's no custom "call back to Microsoft to check this token" round trip
  needed on every request — only once, during the callback.

## Summary of schemes in `Program.cs`

| Scheme | Role | Purpose |
|---|---|---|
| `Cookies` | Default scheme | Holds the authenticated principal after any OIDC handshake (local or Entra) completes |
| `oidc` | Default challenge scheme | Local IdentityServer client — `[Authorize]` with no explicit scheme redirects here |
| `EntraId` | Named OIDC handler | External IdP for "Continue with SSO"; explicit `SignInScheme = "Cookies"` |
| `Bearer` | Named JWT handler | Validates AuthBridge's own JWT (from the `authbridge_token` cookie) for API endpoints like `GET /api/auth/me` — returns 401 instead of redirecting |
