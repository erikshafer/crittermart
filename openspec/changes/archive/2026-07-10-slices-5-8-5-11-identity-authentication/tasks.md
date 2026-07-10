# Tasks: slices-5-8-5-11-identity-authentication (slices 5.8–5.11)

Consolidated into **one PR** (OpenSpec proposal/specs/design/tasks + narrative + prompt + code + tests +
retro), per Erik's slice-PR preference. Architecture fixed by ADR 023; forks resolved at session start
(entity shape = two id-linked tables/one context; cutover = layered; gate = browse-anon/checkout-gated).

## Packages

- [x] `Directory.Packages.props`: add `Microsoft.AspNetCore.Identity.EntityFrameworkCore`,
      `Microsoft.IdentityModel.JsonWebTokens` (Identity), `Microsoft.AspNetCore.Authentication.JwtBearer`
      (Orders) — .NET 10 line.
- [x] `CritterMart.Identity.csproj`: reference the two Identity packages.
- [x] `CritterMart.Orders.csproj`: reference JwtBearer.

## Identity — issuer (slices 5.8 / 5.9 / 5.11)

- [x] Rename `Customers/IdentityDbContext.cs` → `Customers/CustomerDbContext.cs`; derive
      `IdentityUserContext<IdentityUser>`; `base.OnModelCreating` first, then `HasDefaultSchema` + existing
      `Customer`/`EmailChange` mappings. Update all references (`Program.cs`, `RegisterCustomer`,
      `GetCustomer`, `EmailChange`, `RequestEmailChange`, `ConfirmEmailChange`, tests).
- [x] `Auth/JwtSettings.cs` — config-bound record (`Issuer`, `Audience`, `AccessTokenLifetime`) + demo default.
- [x] ~~`Auth/JwtSigningKeys.cs`~~ **→ `ServiceDefaults/DevJwtDefaults.cs`** — the committed **DEV-ONLY**
      keypair (both halves) lives in ServiceDefaults so issuer + resource server share one source; RSA is
      loaded inline via `RSA.ImportFromPem(config["Jwt:*"] ?? DevJwtDefaults.*)` where needed, not a per-service
      key class.
- [x] `Auth/JwtTokenIssuer.cs` — mint via `JsonWebTokenHandler` + `SecurityTokenDescriptor` (sub, iss, aud,
      exp), RSA-SHA256.
- [x] `Program.cs` — `AddIdentityCore<IdentityUser>(pwd policy).AddSignInManager().AddEntityFrameworkStores<CustomerDbContext>()`;
      `AddAuthentication()` (for SignInManager's scheme-provider dependency); bind `JwtSettings`; register
      `JwtTokenIssuer`. (No cookie auth; no `[Authorize]` on the issuer — it mints, doesn't gate.)
- [x] `Features/RegisterWithCredentials.cs` — `POST /register`: `ValidateAsync` duplicate-email guard (409);
      **Identity building blocks** (`PasswordValidators` → 400, `PasswordHasher`, `KeyNormalizer`) + `db.Users.Add`
      + `Customer` row + `CustomerRegistered` cascade, ONE Wolverine transaction (NOT `UserManager.CreateAsync`,
      which self-commits mid-handler — see retro); `201 Created`.
- [x] `Features/LogIn.cs` — `POST /login`: `SignInManager.CheckPasswordSignInAsync`; success → `JwtTokenIssuer`
      → `{ token, … }`; failure → `401`, no enumeration.
- [x] `Features/LogOut.cs` — `POST /logout`: informational `200` (stateless; client discards). Doc-comment the
      client-side-discard semantics + deferred revocation.

## Orders — resource server (slice 5.10)

- [x] `Program.cs` — `AddAuthentication(JwtBearerDefaults).AddJwtBearer` with public `RsaSecurityKey` from
      config (dev fallback), `ValidateIssuer/Audience/Lifetime`, `MapInboundClaims = false`;
      `AddAuthorization`; `UseAuthentication()` + `UseAuthorization()` before `MapWolverineEndpoints`.
- [x] `Auth/CustomerIdentity.cs` (or a small helper) — resolve customer id: `sub` claim ?? `X-Customer-Id`
      fallback; endpoints `401`/`400` when neither present.
- [x] Update customer-keyed endpoints (`AddToCart`, `ViewMyCart`, `PlaceOrder`, `ListMyOrders`, cart edits) to
      source the customer id via the helper (accept `ClaimsPrincipal`/`HttpContext` + keep the header param as
      fallback).
- [x] Orders public-key load — inline `RSA.ImportFromPem(config["Jwt:PublicKey"] ?? DevJwtDefaults.DevPublicKeyPem)`
      in `Program.cs`; **no** private key on the resource server (verify-only).

## AppHost + config

- [x] `AppHost/Program.cs` — add `VITE_IDENTITY_URL` + `.WaitFor(identity)` for the SPA, and Identity's SPA-origin
      CORS entry. **Divergence:** the AppHost does NOT pass `Jwt:PublicKey` — it can't reference `DevJwtDefaults`
      (Aspire gives only `Projects.*` metadata refs, not compile refs), and a multiline-PEM env var through the DCP
      launcher is fragile. The config-distribution mechanism lives in Orders' `Program.cs` (`Jwt:PublicKey` ?? dev
      default) with a pointer comment in the AppHost. See retro.
- [x] Config — issuer/audience/lifetime + keys all resolve from the shared `DevJwtDefaults` unless overridden by
      `Jwt:*` config; no per-service `appsettings` `Jwt` section was needed (dev default suffices for demo + tests).

## Frontend (slices 5.9 / 5.11 + cutover)

- [x] `identity/authStore.ts` (or context) — hold JWT (memory + `sessionStorage`); `login`/`register`/`logout`.
- [x] `identity/useCurrentCustomer.tsx` — source `sub` from the held token; unauthenticated when absent.
- [x] `api/client.ts` — send `Authorization: Bearer <token>` (drop `X-Customer-Id`); update `RequestContext`.
- [x] `identity/LoginPage.tsx` / `RegisterPage.tsx` + routes; gate cart/checkout/orders behind auth (redirect to
      login); Catalog browse stays anonymous; logout control.
- [x] Vitest: update `client.test.ts` (Bearer), seam tests; add login/register/logout coverage.

## Tests (backend)

- [x] Identity: `RegisterWithCredentialsTests` (happy → user+row+outbox `201`; duplicate `409`; weak-password
      `400`, no user/row/event); `LogInTests` (happy → JWT with `sub`=id, valid signature/claims; bad creds
      `401` no-enumeration). Extend `IdentityAppFixture` if it needs `Jwt:*` env (dev default should suffice).
- [x] Orders: `TokenAuthTests` — valid Bearer authenticates (`sub` sourced), invalid/expired `401`,
      `X-Customer-Id` fallback still resolves. Test host mints tokens with the matching dev/test key.
- [x] `dotnet build` (whole repo) zero errors; `dotnet test` (whole repo) green incl. all pre-existing
      Catalog/Inventory/Orders/CrossBc/Identity tests (layered cutover = no regressions).

## Live-verify (real Aspire stack, demo-runbook)

- [x] Boot the stack; `POST /register` (201) → `POST /login` (200, RS256 JWT, `sub`=id) → authenticated Orders
      AddToCart+read (200, `customerId`==`sub`) → no-identity (400) → garbage token (401, local). Plus storefront
      up (200) + Identity CORS preflight echoes the SPA origin. Confirmed offline validation (no HTTP into Identity).
- [ ] ~~Drive it from the SPA in a browser~~ — **NOT done** (Claude-in-Chrome extension not connected this
      session). Mitigated by the HTTP drive above + 112 frontend unit tests. Flagged in the retro's Outstanding.

## Artifacts (same PR)

- [x] `docs/narratives/NNN-customer-authentication.md` (v1.0) + narratives README count.
- [x] `docs/prompts/implementations/NNN-slices-5-8-5-11-identity-authentication.md` + prompts README count.
- [x] `docs/retrospectives/implementations/NNN-…md` — spec-delta closure (4 ADDED `customer-registry`
      requirements landed); Weasel-index + fallback-header findings.
- [x] CLAUDE.md — update the "frontend sends a hardcoded `X-Customer-Id`" non-negotiable line (now Bearer/`sub`).
- [x] ~~`docs/skills/DEBT.md` row~~ **→ retro Outstanding section.** The fallback-header removal is *implementation*
      debt; `DEBT.md` is scoped to *skill-file* gaps, so the hard-cutover follow-up is recorded in the retro's
      Outstanding/next-session-inputs instead (honest divergence — see retro).
- [x] Post-merge: `openspec archive slices-5-8-5-11-identity-authentication -y`.
