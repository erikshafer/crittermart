# Design — retire the X-Customer-Id fallback

## Context

ADR 023 shipped real authentication with a **layered** cutover: `CustomerIdentity.TryResolve` in Orders
resolved the customer from four cases in priority order — (1) a valid token's `sub`, (2) Bearer-but-invalid
→ local 401, (3) the dev-only `X-Customer-Id` header, (4) nothing → the endpoint's 400. Case 3 existed only
so the seeder, the pre-auth test suites, and `demo-traffic.ps1` kept working inside the auth PR; it was
DEBT-tracked for removal. The frontend (PR #130) already sends `Authorization: Bearer` exclusively. This
change deletes cases 3 and 4 and everything that fed them.

## Goals / Non-Goals

**Goals:**

- The JWT `sub` claim is the **sole** customer trust boundary in Orders; no request header names a customer.
- Every non-frontend caller (tests, demo script, runbook blocks) authenticates the way a real client does.
- The shipped specs stop describing the fallback as a requirement.

**Non-Goals:**

- No authorization (roles/policies) — still deferred (ADR 023 Q16).
- No refresh tokens or revocation (ADR 023 Q15).
- No SPA changes — the frontend auth flow is already clean and is not reopened.
- No change to the anonymous Orders reads (`/carts/{cartId}`, `/orders/{orderId}`, `/orders/awaiting-payment`,
  `/carts/awaiting-activity`) — they carry no customer identity to trust.

## Decisions

**1. Blanket `[Authorize]` on the six customer-keyed endpoints; no-token → 401 (was 400).**
Alternatives: (a) keep the hand-rolled helper and preserve the 400-for-no-identity distinction; (b) keep the
helper but return 401. The 400 came from the pre-auth era, when the header was a required request *input* —
its absence made the request malformed. With the header retired, "no identity" is precisely
"unauthenticated", which HTTP spells 401 — and the `customer-registry` spec already required 401 for a
missing token, contradicting the 400s in `shopping-cart`/`order-lifecycle`. Blanket `[Authorize]` resolves
the contradiction in 401's favor, moves the guard into middleware (uniform, and the natural seat for future
authZ policies), and lets `CustomerIdentity` collapse from a four-case resolver to a one-line
`ClaimsPrincipal` extension. Wolverine.Http honors the attribute as endpoint metadata; the migrated
`TokenAuthTests` (no-token → 401, header-only → 401) prove it end to end.

**2. `CustomerIdentity` becomes a `ClaimsPrincipal.CustomerId()` extension that throws on a missing `sub`.**
Behind `[Authorize]` the principal is guaranteed authenticated, and every token Identity mints carries
`sub`; a missing claim means a misconfigured issuer, not a client error — an `InvalidOperationException`
(500), not a 4xx. Endpoints bind `ClaimsPrincipal` directly (Wolverine binds it from `HttpContext`), so the
`HttpContext` parameter disappears from the signatures.

**3. Token minting lives in a new shared `tests/CritterMart.TestSupport` project.**
Alternatives: duplicate `MintToken` per test project; share via linked `<Compile>` items. Both Orders.Tests
and CrossBc.Tests drive customer-keyed endpoints, and a real project is the discoverable, idiomatic seam.
It references `Microsoft.AspNetCore.Authentication.JwtBearer` (already centrally pinned) rather than a
standalone `Microsoft.IdentityModel.JsonWebTokens` pin, keeping one IdentityModel version line per the
Directory.Packages.props pin note.

**4. `demo-traffic.ps1` registers a fresh throwaway shopper per run (`POST /register` → `POST /login`).**
Alternatives: seeder-provisioned well-known credentials; both. Chosen (with Erik) because a brand-new
customer per run can never inherit a half-finished cart an interrupted (Ctrl+C) run left open, the script
stays standalone (no seeder dependency), and registration exercises the same journey a real shopper takes —
including the `CustomerRegistered` → `LocalCustomerView` enrichment. The seeded `customer-demo` stays on the
passwordless admin path (`POST /customers`) as demo data; it has no login and needs none.

## Risks / Trade-offs

- [One shopper per demo-traffic run changes the traffic shape — orders no longer spread across customers]
  → Acceptable for a monitoring-flourish script; sequential add→place closes each cart before the next
  iteration, and the per-run registration keeps runs isolated from each other.
- [The 400→401 flip is breaking for any caller that asserted 400 on missing identity] → All known callers
  (SPA, tests, script, runbook) are migrated in the same change; the spec delta records the new contract.
- [`demo-traffic.ps1` now requires Identity to be up] → It already required Orders; the register/login
  preamble fails fast with a clear error if Identity isn't reachable.

## Migration Plan

Single PR, no data migration (identity was never persisted differently). Rollback = revert the PR; the
fallback code path is self-contained and restores cleanly.

## Open Questions

None — both forks (rejection shape, demo token source) were decided with Erik at session start.
