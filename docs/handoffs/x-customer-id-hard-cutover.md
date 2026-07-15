# CritterMart — Handoff: retire the `X-Customer-Id` dev-only fallback (hard cutover)

> **Durable handoff** (version-controlled at `docs/handoffs/`), authored 2026-07-14.
> Sibling to [`promotions-dcb-workshop.md`](promotions-dcb-workshop.md): that handoff names the *design-phase*
> next direction (Promotions event-modeling pass). Erik chose to **retire shipped-auth debt first** — clean the
> layered cutover so `sub` is the sole trust boundary — before opening new Promotions territory. The Promotions
> pass remains the queued direction after this lands; `promotions-dcb-workshop.md` stays valid and un-consumed.
> This session is **implementation**, not design.

## Mission for the next session

Remove the **dev-only `X-Customer-Id` fallback** from Orders so the JWT `sub` claim is the **sole** customer
trust boundary. This is the hard cutover deferred as debt when real auth shipped (ADR 023, slice 5.10; retro
`implementations/037` Outstanding). One session → one PR (per Erik's slice-consolidation preference).

**Scope is a subtraction, not a feature.** The identity contract already exists (Bearer JWT, verified offline).
This session *deletes* the second path and migrates every non-frontend caller that still relied on it.

## The precise cutover (verified this session — files + line anchors)

**1. Production — the actual removal.** `src/CritterMart.Orders/Auth/CustomerIdentity.cs`
- Delete **case 3** (`CustomerIdentity.cs:44-49`, the `headerCustomerId` fallback). Keep cases 1 (valid `sub`
  wins), 2 (Bearer-but-invalid → local 401), and 4 (no identity → the endpoint's existing 400).
- Once case 3 is gone, the `headerCustomerId` parameter is dead. Remove it from `TryResolve` and from the **six
  Orders feature endpoints** that thread a `[FromHeader("X-Customer-Id")]` param through to it:
  `Features/AddToCart.cs`, `ViewMyCart.cs`, `RemoveCartItem.cs`, `ChangeCartItemQuantity.cs`, `PlaceOrder.cs`,
  `ListMyOrders.cs`. Check `Program.cs` for any header-related config to drop.
- **Decision the session must make:** with only the token path left, should the endpoints now be blanket
  `[Authorize]`'d? The current comment (`CustomerIdentity.cs:17-19`) explains they are *deliberately not*
  `[Authorize]`'d so the header-fallback path works — that rationale dies with case 3. Blanket `[Authorize]` is
  the cleaner post-cutover shape (401 for no/invalid token, uniform), but confirm it preserves the "no identity
  → 400 vs bad token → 401" distinction the specs assert, or decide the spec should change to 401-everywhere.
  Present this to Erik.

**2. Test migration — the bulk of the diff.** No shared header seam exists; each Alba `Scenario` sets the header
inline via `.WithRequestHeader("X-Customer-Id", id)`.
- **Reuse target:** `tests/CritterMart.Orders.Tests/TokenAuthTests.cs:38` already has a `MintToken(...)` helper
  that mints a JWT exactly as Identity does (dev private key, dev issuer/audience). **Lift it into a shared test
  helper/fixture**, then across the Orders + CrossBc suites swap
  `.WithRequestHeader("X-Customer-Id", id)` → `.WithRequestHeader("Authorization", $"Bearer {MintToken(id)}")`.
- Affected suites (from grep): `AddToCartTests`, `ViewMyCartTests`, `RemoveCartItemTests`,
  `ChangeCartItemQuantityTests`, `ListMyOrdersTests`, `PlaceOrderTests`, `PaymentTimeoutTests`,
  `CartAbandonmentTests`, `CustomerRegisteredHandlerTests`, and the three `CritterMart.CrossBc.Tests` smoke tests.
- `TokenAuthTests` itself keeps its "no-Bearer → header fallback" case (`:122+`) **only to delete it** — after
  cutover, no-Bearer must resolve to 401/400, not a customer. Convert that test to assert the *rejection*.

**3. `docs/demo-traffic.ps1`** — sends `X-Customer-Id` to `/carts/mine/items` and `/orders`
(lines ~136/138/198/202, plus the stale PR#87 comment at ~200). Migrate to: `POST /login` (dev-seeded creds) →
capture the JWT → send `Authorization: Bearer`. Verify against the runbook, since this script drives the demo.

**4. `src/CritterMart.Seeding/Program.cs:97`** — registers the demo customer with a deterministic id that
"matches the SPA's X-Customer-Id stub." The seeder itself doesn't call Orders with the header; the deterministic
id stays useful, but **update the stale comment** and confirm the seeded customer has working login creds so
demo-traffic can mint a token for it.

**5. Spec delta (do this the tool-backed way — `openspec` CLI, [[feedback-prefer-tool-backed-over-freeform]]).**
The dev-only fallback is described as a requirement in the shipped specs. Author a small **openspec change** with
a **MODIFIED** requirement in `customer-registry` (and check `shopping-cart` / `order-lifecycle`) tightening
"identity from `sub` OR `X-Customer-Id`" down to **`sub` only**. Grep the specs for `X-Customer-Id` first to find
every SHALL that mentions it. This is the session's named spec delta; the retro confirms it landed and archives
the change.

## What's true right now (2026-07-14, verified this session — don't re-derive)

- `main` @ `9a99dff` (`docs: add Promotions/DCB workshop handoff`), clean tree, on `main`. One commit past
  `e43bb59` = squash-merge of **PR #131** (ADR 024, design-only). No code in flight.
- **The frontend is already clean.** The auth PR (#130) stopped the SPA sending `X-Customer-Id`; it authenticates
  with `Authorization: Bearer`. So this cutover touches **no frontend production code** — the `client/` grep hits
  are tests/comments. Do not reopen the SPA auth flow.
- **The layered resolver is the whole surface.** Everything routes through `CustomerIdentity.TryResolve`; there is
  no second place the header is read in Orders production code.
- **CritterWatch trial expired 2026-07-10**, renewal unresolved → live-**console** verification is BLOCKED. This
  cutover does **not** need the console: live-verify the flow through the stack + `demo-traffic.ps1`, not CW.
- **8 dependabot PRs open** (#132–139), untouched. #135 (critter-stack group, 7 updates) and #137/#139
  (CritterWatch beta.2) are **Wolverine-pin-risky** — out of scope here; do not merge them as a side quest.

## Carry-forwards (triage: pick up, defer-with-reason, or log)

- **Two remote branches** await Erik's call: `origin/feat/cart-identity-harmonization`,
  `origin/research/cw-telemetry-spike`. Delete or keep as remote backups.
- **`UseDurableLocalQueues()` saga-timeout decision** + the **Marten-sibling `ReplenishTimeout` verification gap**
  — open observations in research docs, unchanged.
- **Five AppHost demo knobs** (`Payment__DeclineOverAmount`, `Payment__AuthDelay`, `Orders__PaymentTimeout`,
  `Inventory__ReplenishTimeout`, `Identity__EmailChangeTimeout`) — **post-talk only**, do not delete yet.
- **Do NOT bump Wolverine past 6.16.0** ([[critterwatch-wolverine-version-coupling]]) or transitive JasperFx deps
  (suppressed MessagePack CVE — [[feedback-no-transitive-dep-bumps]]).
- **Refresh tokens + revocation** (ADR 023 Q15) and **authorization/roles** (ADR 023 Q16) remain deferred.
- **Pre-existing frontend test collision** (not ours): `client/e2e/seeder.spec.ts` fails under `vitest run`; run
  frontend units with `--exclude "**/e2e/**"`.
- **The Promotions event-modeling pass** ([`promotions-dcb-workshop.md`](promotions-dcb-workshop.md)) is the queued
  *next* direction after this cutover — don't lose it.

## Orientation files (read first, in order)

1. This handoff.
2. `src/CritterMart.Orders/Auth/CustomerIdentity.cs` — the resolver whose case 3 you delete; its header comment is
   the full rationale for the layered design you're retiring.
3. `tests/CritterMart.Orders.Tests/TokenAuthTests.cs` — the `MintToken` helper to lift + the fallback test to invert.
4. [`docs/decisions/023-real-authentication-for-identity.md`](../decisions/023-real-authentication-for-identity.md)
   — the auth architecture; the fallback was ADR 023's explicit transitional debt.
5. [`docs/retrospectives/implementations/037-slices-5-8-5-11-identity-authentication.md`](../retrospectives/implementations/037-slices-5-8-5-11-identity-authentication.md)
   — Outstanding section names this exact cutover.
6. `openspec/specs/customer-registry/spec.md` — where the fallback requirement to MODIFY lives.
7. `CLAUDE.md` — the per-slice loop + `tidy:`-vs-feature commit convention (this is a `feat`/`refactor`, not `tidy`).

## Working style (Erik's standing preferences — carried from memory)

Present options + a recommendation at genuine forks, via `AskUserQuestion` with previews
([[feedback-collaborate-on-decisions]], [[feedback-options-with-previews]]); prefer tool-backed artifacts over
freeform (`openspec` CLI — [[feedback-prefer-tool-backed-over-freeform]]); ask where something should live before
writing it if Erik says "persist"/"make durable" ([[feedback-ask-where-to-persist]]); after this non-trivial
**code** change, **live-verify against the real stack and drive the demo flow yourself**
([[feedback-live-verify-after-changes]], [[feedback-drive-demo-flows]]) — boot per the demo-runbook, drive
register→login→add-to-cart→checkout with a real token, and confirm a header-only call is now **rejected**; flag
any deferred/non-terminal state explicitly ([[feedback-flag-deferred-state-on-completion]]).

## Definition of done

- [ ] Case 3 removed from `CustomerIdentity.cs`; `headerCustomerId` dropped from `TryResolve` + the 6 endpoints
- [ ] `[Authorize]`-vs-helper decision made with Erik and applied consistently with the spec
- [ ] `MintToken` lifted to a shared test seam; Orders + CrossBc suites migrated to `Authorization: Bearer`
- [ ] `TokenAuthTests` fallback case inverted to assert rejection; full backend suite green
- [ ] `demo-traffic.ps1` migrated to login→Bearer; seeder comment corrected + login creds confirmed
- [ ] openspec change authored (MODIFIED requirement: identity from `sub` only), validated, and archived
- [ ] Live-verified: real-token journey works; header-only call rejected; no `X-Customer-Id` left in prod code
- [ ] Carry-forwards triaged; `/post-merge` → `/handoff` → `/blurb` close-out once the PR lands

## Suggested skills

- `wolverine-http-fundamentals` / `wolverine-http-marten-integration` — endpoint identity resolution, `[Authorize]`,
  claim-sourced customer id.
- `wolverine-testing-alba` — auth stubs and Bearer-header scenarios for the test migration.
- `opsx:propose` / `openspec-propose` then `openspec-archive-change` — the spec delta, tool-backed.
- `post-merge` → `handoff` → `blurb` — the close-out ritual once the PR lands.
