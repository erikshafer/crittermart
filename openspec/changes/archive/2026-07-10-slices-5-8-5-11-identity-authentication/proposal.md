## Why

Identity is a customer registry with no authentication: identity arrives ambiently as a hardcoded
`X-Customer-Id` header sourced behind the frontend's `useCurrentCustomer` seam (ADR 009). Anyone who can set
that header is any customer. [ADR 023](../../../docs/decisions/023-real-authentication-for-identity.md) closes
that stub: Identity becomes the system's sole **auth issuer**, built on **ASP.NET Core Identity**
(`UserManager`/`SignInManager`, EF-Core-backed on the existing `identity` schema) — and on a successful
password check it mints a standard, asymmetrically-signed **JWT** carrying the customer id in the `sub`
claim. The SPA presents that JWT as `Authorization: Bearer …` to all four services; Catalog/Inventory/Orders
verify it **fully offline** against Identity's config-distributed public key. This honors the
[no-synchronous-service-to-service-HTTP non-negotiable](../../../docs/decisions/001-separate-services-topology.md)
(ADR 001) — there is **no HTTP into Identity**, per-request or at startup — and retires `X-Customer-Id` as
the trust boundary, the `sub` claim replacing it.

Two framings survive unchanged and are load-bearing: **(1)** auth is still non-event-sourced — ASP.NET Core
Identity is a relational user store, so this **extends** the boring-CRUD foil (ADR 009 amendment /
[ADR 022](../../../docs/decisions/022-convention-sagas-additive-to-pmvh.md)), it does not reverse it; there
are **zero stream events and no saga** in this increment. **(2)** the no-sync-HTTP rule survives real auth —
offline JWT validation against a config public key adds no cross-BC runtime edge. Modeled in Workshop 002
v1.3 (slices 5.8–5.11); the architecture is fixed by ADR 023 and is not re-litigated here.

## What Changes

Identity gains ASP.NET Core Identity (user store + password handling) and JWT issuance; the three resource
servers gain offline JWT verification. The four slices:

- **Identity (5.8 — register with credentials).** `RegisterWithCredentials { email, displayName, password }`
  at `POST /register` creates an ASP.NET Core Identity user (`UserManager.CreateAsync`, password hashed) **and**
  the registry `Customer` row with the **same** string id, and publishes `CustomerRegistered` via the EF-Core
  outbox — registration is all-or-nothing. Rejects a duplicate email (`409 CustomerAlreadyRegistered`,
  mirroring slice 5.1) and a password that fails the configured policy (`400`, Identity's validation errors).
  The spike's open `POST /customers` (`RegisterCustomer`, no password) is **kept** for admin/seeder-provisioned
  customers (they coexist — resolves Workshop 002 § 8 open Q14 in favor of layering, not replacement).
- **Identity (5.9 — log in, issue a JWT).** `LogIn { email, password }` at `POST /login` runs
  `SignInManager.CheckPasswordSignInAsync`; on success it mints a JWT (`sub` = customer id, plus issuer,
  audience, and a configured lifetime), signed with Identity's asymmetric **private** key, and returns it. Bad
  credentials — wrong password **or** unknown email, indistinguishably (no user enumeration) — return `401`.
- **Resource servers (5.10 — verify a token).** *Code lands in Orders* (the customer-keyed BC; Catalog's
  product reads carry no identity, Inventory has no browser-facing customer endpoints). Orders configures
  `AddJwtBearer` with Identity's **public key from configuration** and validates every token **offline** —
  signature, issuer, audience, lifetime — reading the customer id from `sub`. A valid token authenticates the
  request as that customer; a missing/tampered/expired token is rejected **locally** with `401`, no HTTP into
  Identity.
- **Identity/frontend (5.11 — log out).** The JWT is stateless, so logout is **client-side token discard**:
  the frontend drops the token and `useCurrentCustomer` returns to unauthenticated. No server-side revocation
  (deferred — Workshop 002 § 8 open Q15).

### The X-Customer-Id cutover (layered — decided this session)

Slice 5.10 retires `X-Customer-Id` **as the trust boundary**: when a valid Bearer token is present, the `sub`
claim is the authenticated customer id and wins. To keep the seeder, the existing Orders/cross-BC integration
tests, and `demo-traffic.ps1` green in one PR, the `X-Customer-Id` header is **retained as a dev-only compat
fallback** on Orders' customer-keyed endpoints — used only when no Bearer token is presented. This is a
transitional shim, not the trust boundary; its removal is tracked in `docs/skills/DEBT.md`. The **frontend
cuts fully to Bearer** (it no longer sends `X-Customer-Id`).

### Design decisions carried into this change (full detail in `design.md`)

- **Two id-linked tables, one DbContext.** `Customer` stays the clean registry row (and the `CustomerRegistered`
  Published-Language contract shape); ASP.NET Core Identity's own user table sits alongside, sharing the same
  string id. The existing `IdentityDbContext` is **renamed `CustomerDbContext`** and **derives from**
  `IdentityUserContext<IdentityUser>` — one context, one transaction, one outbox — resolving both the entity-shape
  question (Workshop 002 § 8 open Q13) and the `IdentityDbContext`-vs-`Microsoft.AspNetCore.Identity…IdentityDbContext`
  name collision the handoff flagged.
- **Asymmetric RSA keypair, config-distributed public key.** Identity holds the private key (mint); Orders holds
  the public key as config (verify offline). A committed **dev-only** keypair is the fallback default so the
  Aspire demo and Alba tests work with zero extra wiring; `Jwt:PrivateKey` / `Jwt:PublicKey` config overrides for
  prod. Rotation = redeploy config (ADR 023's accepted round-one tradeoff; open Q17).
- **Browse anonymous, checkout gated.** Catalog stays anonymous; Orders' customer-keyed endpoints require an
  authenticated customer (via Bearer `sub` or the fallback header). The frontend gates cart/checkout behind login.

## Capabilities

### New Capabilities

(None — Identity's single capability `customer-registry` gains authentication behavior; auth is not a new
capability, per one-capability-per-aggregate, CLAUDE.md § 4a.)

### Modified Capabilities

- `customer-registry`: Identity becomes the system's auth issuer. Registration gains a credentialed variant
  that creates an ASP.NET Core Identity user beside the `Customer` row; a new login flow mints a self-validated
  JWT; resource servers verify that token offline and source the authenticated customer id from `sub`; logout
  is client-side token discard. Registration/resolution (5.1/5.2) and the email-change saga (5.5–5.7) are
  unchanged. (Four ADDED requirements: register with credentials, log in and issue a JWT, verify a token at a
  resource server, log out.)

## Impact

- **Identity (issuer).** New `Features/RegisterWithCredentials.cs` (`POST /register`), `Features/LogIn.cs`
  (`POST /login`), `Features/LogOut.cs` (`POST /logout`, informational). New `Auth/` support: `IdentityUser`
  registration via `AddIdentityCore<IdentityUser>().AddSignInManager().AddEntityFrameworkStores<CustomerDbContext>()`;
  a `JwtTokenIssuer` (mints with `JsonWebTokenHandler` + `RsaSecurityKey`); `JwtSigningKeys`/`JwtSettings`
  config helpers (dev-default keypair, mirrors `EmailChangeDeadline.Default`). `IdentityDbContext` →
  `CustomerDbContext` (renamed; derives `IdentityUserContext<IdentityUser>`; `base.OnModelCreating` called).
  `Program.cs` wires Identity + issuer.
- **Orders (resource server, slice 5.10).** `AddAuthentication().AddJwtBearer` (public key from config,
  `MapInboundClaims = false`), `AddAuthorization`, `UseAuthentication()/UseAuthorization()`. Each customer-keyed
  endpoint sources the customer id from `sub` when a token is present, else the `X-Customer-Id` fallback (a small
  shared helper). `AppHost` passes the dev public key + issuer/audience to Orders as config.
- **Frontend.** Login/register/logout UI; token held in a session store; `useCurrentCustomer` sourced from the
  decoded token's `sub`; `client.ts` sends `Authorization: Bearer` (drops `X-Customer-Id`); cart/checkout gated
  behind an authenticated session, Catalog browsing anonymous.
- **Packages.** `Microsoft.AspNetCore.Identity.EntityFrameworkCore` + `Microsoft.IdentityModel.JsonWebTokens`
  (Identity); `Microsoft.AspNetCore.Authentication.JwtBearer` (Orders) — pinned in `Directory.Packages.props`
  on the .NET 10 line.
- **Persistence.** ASP.NET Core Identity's user tables ride the existing `UseEntityFrameworkCoreWolverineManagedMigrations`
  path (Weasel migrates tables/columns/PKs/FKs; its known skip of secondary indexes is acceptable — email
  uniqueness is already enforced by `ux_customers_email` on the `customers` table + the app guard). No new
  event, no stream, no projection.
- **Tests.** Identity: register-with-credentials (happy, duplicate-email, weak-password), login (happy JWT
  shape, bad-credentials 401 no-enumeration). Orders: valid token authenticates (`sub` sourced), invalid/expired
  token 401, and the `X-Customer-Id` fallback still works. Existing Orders/cross-BC/Identity tests unchanged
  (layered cutover).
- **Docs (same PR).** `design.md` + `tasks.md` here; the customer-auth narrative; the implementation prompt +
  retro; CLAUDE.md's "frontend sends a hardcoded `X-Customer-Id`" non-negotiable line updated; a `DEBT.md` row
  for the fallback-header removal.
- **Out of scope.** Authorization (roles/policies — open Q16); refresh tokens and server-side revocation (open
  Q15); a JWKS endpoint (ADR 023 chose config over JWKS); Catalog/Inventory browser-facing auth (no customer-keyed
  endpoints there); real secret-store key management (open Q17). No payment, no new BC.
