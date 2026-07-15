# Retrospective: Implementations 038 — Retire the `X-Customer-Id` dev-only fallback (hard cutover)

**Session date**: 2026-07-14
**Prompt**: [`docs/prompts/implementations/038-retire-x-customer-id-fallback.md`](../../prompts/implementations/038-retire-x-customer-id-fallback.md)
**Handoff consumed**: [`docs/handoffs/x-customer-id-hard-cutover.md`](../../handoffs/x-customer-id-hard-cutover.md)
**Commit subject**: `refactor: retire the X-Customer-Id dev-only fallback — sub is the sole customer trust boundary`

## Outcome summary

The hard cutover landed whole, in one PR, exactly as the handoff scoped it. The JWT `sub` claim is now the
**sole** customer trust boundary in Orders: `CustomerIdentity` collapsed from a four-case resolver to a
one-line `ClaimsPrincipal.CustomerId()` extension, the six customer-keyed endpoints are blanket-
`[Authorize]`'d (dropping both the `[FromHeader]` parameter and the `HttpContext` parameter — Wolverine
binds `ClaimsPrincipal` directly), and an unauthenticated request is `401` before any handler runs. The
Orders + CrossBc suites migrated to real dev-key-minted Bearer tokens via a new shared
`tests/CritterMart.TestSupport` project; the layered-cutover fallback test was inverted to assert rejection
(header-only → 401 **and** no cart created, verified through the read model with a valid token for the same
id). `demo-traffic.ps1` and the runbook's four manual command blocks now register a throwaway shopper and
log in per run. Full backend suite green: 173 tests (Orders 104, CrossBc 3, Catalog 9, Inventory 31,
Identity 26). Live-verified on the real Aspire stack: 3 script-driven Bearer orders, a hand-driven
register → login → add → change-quantity → checkout journey to `confirmed` (through the 3-min AuthDelay,
stock `1000→995/committed 5`, `customerName` enriched), and seven rejection probes all `401` with the
anonymous automation reads still serving. OpenSpec change `retire-x-customer-id-fallback` authored with the
CLI, validated, and **archived** (7 MODIFIED requirements synced into the three main specs).

Both session-start forks were decided with Erik before any code: **blanket `[Authorize]` → 401-everywhere**
and **script self-registers per run**. Erik then went offline mid-session; everything after ran on those two
decisions with no further gates needed.

## What worked

- **The handoff's verified line anchors were exact.** Case 3 was at `CustomerIdentity.cs:44-49`, the six
  endpoints were as listed, `MintToken` was at `TokenAuthTests.cs:38`. Zero re-derivation; the survey phase
  went to confirming, not discovering.
- **Deciding both forks up front via AskUserQuestion with previews** meant Erik's departure cost nothing —
  the rest of the session was execution against settled decisions.
- **The latent spec contradiction made the 401 decision easy to present**: `customer-registry` already
  required 401 for a missing token while `shopping-cart`/`order-lifecycle` asserted 400 — surfacing that in
  the option preview turned a style question into a consistency fix.
- **`ClaimsPrincipal` as a bound endpoint parameter** (per the `wolverine-http-fundamentals` skill) let the
  endpoints shed `HttpContext` entirely — the signatures got *simpler* than pre-auth, not just equivalent.
- **The migrated test suite doubled as the `[Authorize]`-on-Wolverine proof.** No spike needed: the new
  no-token/header-only 401 tests passing was the evidence the attribute is honored as endpoint metadata.

## What was harder than expected

- **The handoff's "dev-seeded creds" assumption was false**: `customer-demo` is registered via the
  passwordless `POST /customers` admin path and has no login, and `POST /register` mints its own UUID (no
  deterministic-id support). The fork was re-framed around that discovery — the chosen self-register-per-run
  option sidesteps it entirely, and the seeder needed only a comment correction.
- **My own bulk-migration script bit the inversion test**: the regex swapping
  `.WithRequestHeader("X-Customer-Id", …)` → Bearer also rewrote the *deliberate* header-only request inside
  the freshly-written inverted TokenAuthTests, silently flipping its meaning. Caught on review of the diff,
  restored by hand. Mechanical sweeps must exclude the files whose X-Customer-Id usage is intentional.
- **`openspec archive` blocked interactively on its own close-out checkbox** — tasks.md's "archive this
  change" task can't be checked before archiving without lying, and the archive prompts on any unchecked
  task (and hung the non-interactive shell). Resolved by checking the box (the archive *is* that step) and
  re-running with `--yes`.
- **structural-constraints.md was one increment behind before this session**: v1.8 wrote the auth
  constraints as "not yet built / until those slices ship", and the auth implementation session
  (implementations/037) never bumped it. This session's v1.10 syncs both that missed update and the cutover.
  The file's own header rule ("paired update in the same PR") failed silently once already — worth watching.

## Methodology refinements

- **A "durable handoff + frozen prompt" pair works.** The handoff carried the mission across sessions; this
  prompt froze the post-fork intent. The prompt cites the handoff rather than duplicating it — that split
  (handoff = mission + verified state; prompt = resolved intent + deliverable plan) felt right and is worth
  repeating.
- **Regex migrations need an exclusion list authored *before* the sweep** — any file whose old-pattern usage
  is deliberately preserved (inverted tests, historical docs) gets hand-migrated first or excluded.
- **Runbook "Last verified" entries are append-only history**: the 2026-06-17 entry still names the
  X-Customer-Id seam it verified then — correct as a record. New state gets a new dated entry (added:
  Bearer-only pass, 2026-07-14); old entries are never rewritten.

## Outstanding items / next-session inputs

- **Carry-forwards, triaged** (all deferred with reason, none picked up — this was a subtraction session):
  - Two remote branches await Erik's delete/keep call: `origin/feat/cart-identity-harmonization`,
    `origin/research/cw-telemetry-spike`.
  - `UseDurableLocalQueues()` saga-timeout decision + the Marten-sibling `ReplenishTimeout` verification gap
    — unchanged, in research docs.
  - Five AppHost demo knobs — post-talk removal only.
  - Wolverine stays ≤ 6.16.0 (CritterWatch coupling); dependabot #132–139 untouched (#135/#137/#139 pin-risky).
  - Refresh tokens/revocation (ADR 023 Q15) and authZ (Q16) — still deferred, now the only remaining auth debt.
  - Pre-existing frontend test collision: run client units with `--exclude "**/e2e/**"`.
- **New tidy candidate (logged, not done)**: ~10 `client/src` files carry stale comments describing the
  header-keyed identity transport (`cartMutations.ts`, `cartQueries.ts`, `orderQueries.ts`,
  `placeOrderMutation.ts`, page components) plus two client test names. Production behavior is correct
  (Bearer-only); the comments lie about *why*. A `tidy: frontend comments` pass, or fold into the next
  frontend-touching session.
- **CritterWatch trial expired 2026-07-10** — live-console verification stays blocked until renewal (not
  needed this session).
- **Next direction (queued)**: the Promotions event-modeling pass —
  [`docs/handoffs/promotions-dcb-workshop.md`](../../handoffs/promotions-dcb-workshop.md), still valid and
  un-consumed.
- **Close-out ritual pending the merge**: `/post-merge` → `/handoff` → `/blurb` once Erik merges the PR.

## Spec-delta — landed?

**Yes.** OpenSpec change `retire-x-customer-id-fallback` authored (proposal, design, specs, tasks — CLI-
scaffolded and `openspec validate` green), archived as `2026-07-15-retire-x-customer-id-fallback`, syncing
**7 MODIFIED requirements**: `customer-registry` ×1 (Verify a token at a resource server — fallback clause
and scenario removed, header-only-401 scenario added), `shopping-cart` ×4 (add / remove / change-quantity /
view-my-cart — `sub`-via-Bearer identity, 400→401), `order-lifecycle` ×2 (list-my-orders, read-time identity
resolution). Narrative 010 bumped to v1.1 with the Moment 3 amendment recorded in its Document History;
structural-constraints bumped to v1.10; CLAUDE.md's non-negotiables line updated. The prompt named this
delta; this retro confirms it landed.
