# Prompt: Implementations 038 — Retire the `X-Customer-Id` dev-only fallback (hard cutover)

**Kind**: per-slice implementation (a subtraction — deleting the layered cutover's second identity path), one session → one PR per [[feedback-consolidate-slice-prs]]
**Files touched**: `src/CritterMart.Orders/Auth/CustomerIdentity.cs` (resolver collapse) + the six customer-keyed endpoint files + `Program.cs` comments; `src/CritterMart.Orders/Customers/LocalCustomerView.cs`, `src/CritterMart.Identity/{Customers/Customer.cs,Features/RegisterCustomer.cs}`, `src/CritterMart.Seeding/Program.cs` (stale-comment corrections); `tests/CritterMart.TestSupport/` (new shared JWT-mint seam) + `CritterMart.slnx` + both test csprojs; the Orders + CrossBc test suites (Bearer migration, 400→401 conversions, fallback-test inversion); `docs/demo-traffic.ps1` + `docs/demo-runbook.md` (login→Bearer); `openspec/changes/retire-x-customer-id-fallback/` (authored, validated, archived → main specs synced); `docs/narratives/010-customer-authentication.md` (v1.1); `docs/rules/structural-constraints.md` (v1.10); `CLAUDE.md` (non-negotiables line); this prompt + retro 038
**Mode**: solo implementation, driven by the durable handoff `docs/handoffs/x-customer-id-hard-cutover.md` (authored 2026-07-14, same day) — the handoff is the mission source; this prompt freezes the session-start intent after the two AskUserQuestion forks resolved
**Commit subject**: `refactor: retire the X-Customer-Id dev-only fallback — sub is the sole customer trust boundary`

## Framing

ADR 023 shipped real auth with a **layered** cutover: `sub` became the trust boundary but the round-one
`X-Customer-Id` header lingered as a dev-only fallback (case 3 of `CustomerIdentity.TryResolve`) so the
seeder, the pre-auth test suites, and `demo-traffic.ps1` stayed green inside that PR — explicitly
DEBT-tracked (retro implementations/037 Outstanding; CLAUDE.md non-negotiables). The frontend is already
clean (PR #130). This session **deletes the second path** and migrates every non-frontend caller. Scope is a
subtraction, not a feature; do not reopen the SPA auth flow.

Two forks resolved with Erik at session start (AskUserQuestion, per [[feedback-collaborate-on-decisions]]):
1. **Rejection shape** → blanket `[Authorize]` on the six customer-keyed endpoints; no-token → **401**
   (was 400). Chosen over keeping the hand-rolled helper: the 400's "malformed request — missing required
   header" rationale died with the header, and `customer-registry` already asserted 401 for a missing token
   (a latent contradiction with `shopping-cart`/`order-lifecycle`'s 400s that this resolves).
2. **Demo token source** → `demo-traffic.ps1` self-registers a fresh throwaway shopper per run
   (`POST /register` → `POST /login`). Chosen over seeder-provisioned creds: per-run cart isolation
   (Ctrl+C-safe), no seeder dependency, and the discovery that `customer-demo` never had login creds
   (passwordless `POST /customers` path) while `POST /register` can't take a deterministic id.

## Goal

- Delete `TryResolve` cases 3/4; collapse `CustomerIdentity` to a `ClaimsPrincipal.CustomerId()` extension;
  `[Authorize]` the six endpoints and drop the `[FromHeader]` parameter.
- Lift `MintToken` into a shared `tests/CritterMart.TestSupport` seam; migrate Orders + CrossBc suites to
  `Authorization: Bearer`; invert the fallback test to assert rejection; full backend suite green.
- Migrate `demo-traffic.ps1` + the runbook's manual blocks to register→login→Bearer; correct the seeder's
  stale comment.
- Author, validate, and archive the tool-backed openspec change `retire-x-customer-id-fallback`
  (MODIFIED requirements in `customer-registry`, `shopping-cart`, `order-lifecycle`).
- Live-verify on the real Aspire stack: real-token journey confirmed end to end; header-only call rejected;
  no functional `X-Customer-Id` read left in production code.

## Spec delta

OpenSpec change `retire-x-customer-id-fallback`: **7 MODIFIED requirements** — `customer-registry` "Verify a
token at a resource server" (fallback MAY-clause + scenario removed; header-only → 401 scenario added);
`shopping-cart` add/remove/change-quantity/view-my-cart (identity = `sub` via Bearer; "no identity → 400"
scenarios become "no token → 401"); `order-lifecycle` list-my-orders + read-time identity resolution
(same swap). Narrative 010 → v1.1 (Moment 3's "tracked for removal" parenthetical closed);
structural-constraints → v1.10; CLAUDE.md non-negotiables line updated.

## Orientation files

1. `docs/handoffs/x-customer-id-hard-cutover.md` — the mission, verified line anchors, carry-forwards.
2. `src/CritterMart.Orders/Auth/CustomerIdentity.cs` — the resolver whose case 3 dies.
3. `tests/CritterMart.Orders.Tests/TokenAuthTests.cs` — the `MintToken` to lift + the fallback test to invert.
4. `docs/decisions/023-real-authentication-for-identity.md` — the architecture; the fallback was its named debt.
5. `openspec/specs/{customer-registry,shopping-cart,order-lifecycle}/spec.md` — the SHALLs to tighten.

## Working pattern

Production cut → build → shared test seam + suite migration → full backend test run → demo tooling →
openspec change (CLI-scaffolded, validated) → live-verify on the booted Aspire stack (demo-traffic run,
rejection probes, hand-driven journey through the 3-min AuthDelay) → teardown → archive → narrative/rules/
CLAUDE.md sync → retro → one PR.

## Out of scope

- The SPA (already Bearer-only; its stale `X-Customer-Id` comments are a logged tidy candidate, not edits here).
- AuthZ/roles (ADR 023 Q16), refresh/revocation (Q15).
- Dependabot PRs #132–139 (two are Wolverine-pin-risky — untouched).
- The five AppHost demo knobs (post-talk).
- The Promotions event-modeling pass (queued next — `docs/handoffs/promotions-dcb-workshop.md`).
