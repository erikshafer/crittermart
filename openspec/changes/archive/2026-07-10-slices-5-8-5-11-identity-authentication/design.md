## Context

ADR 023 fixes the architecture: ASP.NET Core Identity for the user store + credentials, a self-validated
asymmetrically-signed JWT (`sub` = customer id) minted by Identity and verified **offline** by the resource
servers against a config-distributed public key. This document records the *implementation* decisions that
architecture leaves open — the ones Workshop 002 § 8 (open Q13–Q17) and the handoff delegated to this layer.
API specifics were checked against current ASP.NET Core / Identity docs (ctx7 `/dotnet/aspnetcore.docs`,
.NET 10) rather than assumed. See the proposal for Why/What and the `customer-registry` spec delta for the
SHALLs.

## Goals / Non-Goals

**Goals:**
- Make Identity the sole auth issuer with the smallest honest surface: register-with-credentials, login→JWT,
  logout (client-side), on the existing EF-Core/`identity`-schema store.
- Verify tokens offline in Orders with zero HTTP into Identity — the load-bearing demonstration that ADR 001
  survives real auth.
- Ship in one consolidated PR with **green existing tests** (layered cutover), demoable end-to-end.

**Non-Goals:**
- No authorization (roles/policies) — open Q16, deferred until a second actor.
- No refresh tokens, no server-side revocation/denylist — open Q15; logout is client discard.
- No JWKS endpoint — ADR 023 chose config-distributed public key over a fetched JWKS (the last soft asterisk
  on no-sync-HTTP).
- No real secret store — dev keypair in config; open Q17.

## Decisions

### 1. Two id-linked tables, one `CustomerDbContext` deriving `IdentityUserContext<IdentityUser>`

`Customer` stays exactly as it is — the registry row and the `CustomerRegistered` Published-Language contract
shape, free of framework base classes. ASP.NET Core Identity's own user table (`AspNetUsers` and its
satellites) sits alongside it, sharing the **same string id** (`IdentityUser.Id` is a string by default;
`Customer.Id` is already a string). Register-with-credentials creates both with that shared id.

The existing `IdentityDbContext` is **renamed `CustomerDbContext`** and now derives from
`Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityUserContext<IdentityUser>`. This resolves the name
collision the handoff flagged (a local `IdentityDbContext` next to ASP.NET's `IdentityDbContext<TUser>`) by
sidestepping the shared name entirely, and keeps **one** DbContext — so the Identity user insert, the
`Customer` row insert, and the outbox enrollment all commit in **one** Wolverine-managed transaction (the
all-or-nothing registration the spec requires). `IdentityUserContext` (user tables only) is chosen over
`IdentityDbContext` (which adds `AspNetRoles`/`AspNetUserRoles`/`AspNetRoleClaims`) because authZ/roles are
out of scope — no unused role tables. `OnModelCreating` calls `base.OnModelCreating(modelBuilder)` **first**
(so the Identity entities are configured), then sets `HasDefaultSchema("identity")` and applies the existing
lowercase-column mappings for `Customer`/`EmailChange`. Every rename site (`Program.cs`, endpoints, saga,
tests) updates to `CustomerDbContext`.

**Weasel + Identity tables.** The Identity tables ride the existing `UseEntityFrameworkCoreWolverineManagedMigrations`
path. Weasel migrates tables/columns/PKs/FKs but **not secondary indexes** (the documented gotcha behind
`ux_customers_email`). ASP.NET Identity's unique indexes on `NormalizedUserName`/`NormalizedEmail` will
therefore be absent — **acceptable**: email uniqueness is already DB-enforced by `ux_customers_email` on the
`customers` table (plus the app guard), and `UserManager.FindByEmailAsync` matches on the `NormalizedEmail`
**column**, which Weasel does create (an unindexed scan, fine at demo scale). Verified live before the PR.

### 2. Registration: layer, don't replace (open Q14 → layer)

`RegisterWithCredentials` (`POST /register`) is a **new** endpoint; the spike's `RegisterCustomer`
(`POST /customers`, no password) is **kept** for admin/seeder-provisioned customers. The seeder keeps
registering `customer-demo` via `POST /customers` unchanged. Register-with-credentials reuses
`RegisterCustomer`'s duplicate-email guard idiom (normalized-email `ValidateAsync` → `409`) and the same
`CustomerRegistered` outbox cascade, adding the `UserManager.CreateAsync` user creation and password-policy
rejection ahead of the row write.

**Handler shape.** ASP.NET Identity's `UserManager`/`SignInManager` are async service calls, not pure
functions — so register/login are ordinary Wolverine.HTTP endpoint methods that take `UserManager<IdentityUser>`
/ `SignInManager<IdentityUser>` as injected parameters (the a-frame "service call in the handler" shape), not
railway-pure handlers. The duplicate-email `ValidateAsync` guard stays a pre-handle railway check; the
password-policy failure is surfaced from `CreateAsync`'s `IdentityResult` inside the handler (it needs the
manager), returned as `400 ProblemDetails`. Because the user-create and the row-write must be one transaction,
both happen in the endpoint body under `AutoApplyTransactions` — `UserManager.CreateAsync` writes through the
same `CustomerDbContext`, and the `Customer` row + `CustomerRegistered` cascade commit with it.

### 3. JWT issuance — `JsonWebTokenHandler`, RSA, clean `sub`

Identity mints with the modern `Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler` +
`SecurityTokenDescriptor`, signed with `SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256)`.
The legacy `JwtSecurityTokenHandler` is avoided because it silently remaps `sub` → `ClaimTypes.NameIdentifier`;
`JsonWebTokenHandler` preserves `sub` as `sub`. The descriptor carries `Subject` (a `ClaimsIdentity` with the
`sub` claim = customer id), `Issuer`, `Audience`, and `Expires = now + JwtSettings.AccessTokenLifetime`.
Issuance is encapsulated in a `JwtTokenIssuer` service (injected into `LogIn`), keeping token mechanics out of
the endpoint.

On the **resource server**, `AddJwtBearer` sets `TokenValidationParameters { ValidateIssuer, ValidateAudience,
ValidateLifetime, IssuerSigningKey = <public RsaSecurityKey> }` and **`options.MapInboundClaims = false`** so
the inbound `sub` is readable as `sub` (not remapped). No `Authority`/`MetadataAddress` is set — that is what
keeps validation offline (no JWKS fetch).

### 4. Signing keys — asymmetric RSA, config-distributed, dev-default committed

One RSA keypair. Identity reads the **private** key PEM from `Jwt:PrivateKey`; Orders reads the **public** key
PEM from `Jwt:PublicKey`. Both fall back to a **committed dev-only** keypair (a `JwtSigningKeys` helper with
`static readonly` dev PEM constants, loudly marked DEV ONLY / regenerate-for-prod — the pattern mirrors
`EmailChangeDeadline.Default` for "demo-workable default, config-overridable"). Consequences:
- The Aspire demo and both services' Alba test hosts work with **zero** key wiring (they fall back to the same
  dev pair). The AppHost additionally passes `Jwt:PublicKey`/`Jwt:Issuer`/`Jwt:Audience` to Orders as config,
  making the "public key distributed as configuration" story **literal and visible** in `AppHost/Program.cs`
  (a teaching beat), even though the fallback would also suffice.
- Committing a dev private key is acceptable because it signs nothing of value in a local demo and is not a
  real secret; prod supplies real key material via `Jwt:PrivateKey`/`Jwt:PublicKey` (user-secrets / secret
  store). Rotation = redeploy config (ADR 023's accepted round-one tradeoff). This is the honest edge of
  open Q17, recorded not hidden.

`JwtSettings` (issuer, audience, access-token lifetime) is a config-bound singleton; `AccessTokenLifetime`
defaults short (demo-paced, e.g. 1h) and is overridable — a smaller sibling of the `EmailChangeDeadline` /
`PaymentDeadline` demo-knob pattern, though not one of the four demo knobs slated for post-talk deletion.

### 5. The X-Customer-Id cutover — layered (Bearer wins, header is a dev fallback)

Orders' customer-keyed endpoints today read `[FromHeader(Name = "X-Customer-Id")] string? customerId` and
`400` on blank. The cutover introduces a small resolution helper that prefers the authenticated `sub` claim
and falls back to the header: given the endpoint's `HttpContext`/`ClaimsPrincipal` and the header value, it
returns `User.FindFirst("sub")?.Value ?? headerValue`, and the endpoint `400/401`s when neither is present.
`AddAuthentication`/`AddJwtBearer` + `UseAuthentication`/`UseAuthorization` are added to Orders; endpoints are
**not** blanket-`[Authorize]`d (that would break the fallback-header path the existing tests rely on) — instead
the resolution helper enforces "must have an identity," so a request with neither a valid token nor a header is
rejected. The header fallback is a transitional shim tracked for removal in `docs/skills/DEBT.md`; the
**frontend stops sending it** and uses Bearer exclusively.

Catalog and Inventory get **no** auth changes — Catalog's product reads carry no customer identity, and
Inventory has no browser-facing customer-keyed endpoint (its customer id rides RabbitMQ message payloads from
Orders, off the trust-boundary path entirely, exactly as ADR 023 notes).

### 6. Frontend — Bearer, session token, gated checkout

A `LoginPage`/`RegisterPage` post to Identity; the returned JWT is held in an in-memory + `sessionStorage`
auth store; `useCurrentCustomer` decodes `sub` from the held token (returns unauthenticated when absent).
`client.ts`'s `fetchParsed`/`postCommand`/`deleteCommand` send `Authorization: Bearer <token>` instead of
`X-Customer-Id`. Browse (Catalog) works logged-out; cart/checkout/orders routes redirect to login when
unauthenticated. Logout clears the store. Vitest coverage updates for the header→Bearer swap and the seam.

## Risks / Trade-offs

- **Weasel skips Identity's unique indexes** (Decision 1) — mitigated: email uniqueness already enforced on
  `customers`; normalized-email lookup uses the column, not the index. Live-verify register+login before PR.
- **Committed dev private key** (Decision 4) — a deliberate, loudly-marked demo affordance, not a secret;
  prod overrides via config. Recorded in the ADR's open Q17 lineage.
- **Layered fallback header** (Decision 5) — the trust boundary is `sub`; the header remains a bypass until
  removed. Tracked in DEBT; the frontend no longer emits it, shrinking the real-world exposure to
  hand-crafted requests in dev.
- **`UserManager.CreateAsync` + row write in one transaction** — both go through `CustomerDbContext` under
  `AutoApplyTransactions`; if `CreateAsync` fails policy/duplicate, the endpoint returns before adding the row,
  so no partial state. Verified by the duplicate/weak-password tests.

## Open questions resolved here

- **Q13 entity shape + name collision** → two id-linked tables, one `CustomerDbContext : IdentityUserContext<IdentityUser>`.
- **Q14 registration replace-or-layer** → layer (`/register` new, `/customers` kept).
- **Q15 token lifetime / logout** → short configurable access-token lifetime; client-side-discard logout; no
  refresh, no revocation (deferred).
- **Q17 key management** → asymmetric RSA, config PEM with committed dev default; rotation by redeploy.
- **Q16 authorization** → explicitly out of scope (no roles/policies this increment).
