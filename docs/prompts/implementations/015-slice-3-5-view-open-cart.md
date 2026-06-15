# Prompt: Implementations 015 — Slice 3.5 View My Open Cart (`GET /carts/mine`)

**Kind**: per-slice implementation, consolidated one PR (OpenSpec change + implementation + the deferred frontend skill + narrative bump + prompt/retro), per the consolidate-slice-prs convention. Covers **one workshop slice** (3.5 view my open cart — the round-two *view/query* slice) — the **first frontend implementation slice** of round two (ADR 016 puts the frontend inside the full SDD pipeline). A pure read: **no new event, projection, or index.**
**Source**: frozen from the session handoff `crittermart-handoff-2026-06-15-first-frontend-slice.md` (ephemeral, `%TEMP%`); this is its durable in-repo transcription.
**Files touched**: this prompt; `openspec/changes/slice-3-5-view-open-cart/{proposal.md, design.md, tasks.md, specs/shopping-cart/spec.md}` (new — one capability delta, 1 ADDED requirement); `src/CritterMart.Orders/Features/ViewMyCart.cs` (new — the `GET /carts/mine` endpoint); `tests/CritterMart.Orders.Tests/ViewMyCartTests.cs` (new — Alba integration); `docs/skills/frontend/SKILL.md` (new — the deferred frontend skill v1 seed) + `docs/skills/README.md` (index row); `docs/narratives/005-customer-storefront.md` (→ v1.1 — slice 3.5 server half built); `docs/retrospectives/implementations/015-slice-3-5-view-open-cart.md` (forthcoming).
**Mode**: solo, consolidated one-PR slice; the one genuine fork (customer-identity transport) was presented collaboratively (AskUserQuestion + previews) and resolved with the user **before the endpoint was written** — it appears below as a locked decision.
**Commit subject**: `feat: slice 3.5 view my open cart (GET /carts/mine)`

## Framing

Backend round one is complete; the frontend-mode entry (PR #49) modeled the storefront. The pre-frontend endpoint audit found one **blocking** read-model gap: every cart *command* is customer-keyed (the server resolves the open cart) but the only cart *read* is `GET /carts/{cartId}` — so a cold-loaded SPA, holding only the stubbed customer id, has no `cartId` and cannot render the cart-review screen (wireframe W2). Slice 3.5 is the deliberate first frontend slice because it is the cleanest: it exposes the **same open-cart resolution every command already runs** (`AddToCart.cs:31`) as a read, over the **partial-unique open-cart index that already exists** (`Program.cs:74`). No new event.

## Goal

A new read endpoint `GET /carts/mine` (Orders) resolves the customer's single open `CartView` by identity and returns it (`200`), or `404` when there is no open cart (cold start, or the last cart was checked out / abandoned), or `400` when no identity is supplied. Customer identity arrives in the `X-Customer-Id` header (the resolved fork), bound via `[FromHeader]`, behind the ADR 009 `useCurrentCustomer` seam. An Alba integration suite proves the happy path (resolved by identity, not "any open cart"), the no-open-cart `404`, the checked-out-cart `404`, and the missing-identity `400`. The deferred `docs/skills/frontend/SKILL.md` is authored. `openspec validate --strict` passes; full solution green.

## Spec delta

A new OpenSpec change `slice-3-5-view-open-cart` with **one** capability delta — **`shopping-cart`** (1 ADDED requirement): *Read the Customer's open cart* — the customer-keyed read counterpart to the customer-keyed write side. Three scenarios: open cart returned (`200`), no open cart (`404`), missing identity (`400`). No existing requirement text changes. Narrative 005 → v1.1 (slice 3.5 server half built; W2 screen still pending the client bootstrap). The workshop § 6 slice 3.5 GWTs (happy + no-open-cart) are satisfied; the identity-transport question the workshop left open is resolved to the header, and a third guard (missing identity → `400`) is added — both recorded as design.md faithfulness notes for the post-merge workshop amendment.

## Locked decisions (fork resolved with the user at session start, 2026-06-15)

1. **Customer identity via the `X-Customer-Id` header** (not a `?customerId=` query param, not a route segment). Presented as a fork with previews; the header won because it matches the literal `/carts/mine` route semantics (identity ambient, not restated in the URL), keeps the id out of URLs/logs, and is the closest stand-in for the eventual Polecat claim — the promotion swaps header → claim with call sites unchanged, where a query param would require removing `?customerId=` from every caller. A route param `/carts/{customerId}` was rejected for contradicting the workshop-locked "mine" route. (design.md Decision 1.)
2. **A query, not `[ReadAggregate]`** *(session-runner decision)*: `[ReadAggregate]` loads by stream id (`cartId`) — the very thing a cold load lacks. The endpoint queries `CartView` by `customerId` over the open-cart index (the same query `AddToCart` runs). (design.md Decision 2.)
3. **Missing identity is `400`, not `404`** *(session-runner decision)*: keeps the workshop's no-open-cart edge (`404`) semantically crisp vs. a malformed request (no identity). Header binding defaults an absent header to null, so the endpoint guards `IsNullOrWhiteSpace`. (design.md Decision 5.)
4. **`client/` scaffolding deferred to a dedicated frontend-bootstrap PR** *(scope decision)*: this PR is the endpoint + tests + OpenSpec + the frontend skill seed. Standing up the Vite app pulls in the owed Aspire `AddViteApp`-integration decision (cross-repo-comparison open #2), CORS-origin injection, and the dependabot npm block — too much for a read-slice PR, and the Aspire decision deserves its own session.

## Orientation

1. **`docs/workshops/001-crittermart-event-model.md`** § 6 slice 3.5 (the GWTs: happy + no-open-cart edge), § 5 row 3.5, § 5.1 W2 (the cart-review wireframe this read unblocks).
2. **`docs/research/pre-frontend-endpoint-audit.md`** — Gap #1 (this slice), the index-already-exists finding.
3. **`openspec/specs/shopping-cart/spec.md`** — the durable spec (7 requirements) this change extends; the read requirement is purely additive.
4. **The code to mirror**: `src/CritterMart.Orders/Features/AddToCart.cs` — `CartEndpoint.Get` (`GET /carts/{cartId}`, the by-id read) and `GetAwaitingActivity` (`GET /carts/awaiting-activity`, the literal-route query + route-precedence note); the open-cart resolution at `AddToCart.cs:31`. `Cart/CartView.cs` + `Program.cs:74` (the index).
5. **Tests to mirror**: `tests/CritterMart.Orders.Tests/{AddToCartTests.cs (the_cart_is_readable_over_http), PlaceOrderTests.cs (AddAsync/PlaceOrderAsync helpers, checkout setup), OrdersAppFixture.cs (read-only)}`.
6. **Frontend ADRs (for the skill)**: 015 (+amendment — version pins, R3 Zod, R4 optimistic-UI, R5 no-push), 016 (UI first-class + presentation-state guardrail), 018 (no-BFF / CORS dev=prod / no proxy), 009 (the seam). `docs/narratives/005-customer-storefront.md` (Moment 3 = this read).
7. **Skills**: `wolverine-http-marten-integration` (`[ReadAggregate]` vs query, identity resolution), `wolverine-testing-alba` / `marten-integration-testing` (the suite), `openspec-propose` (the CLI), `find-docs` (ctx7 — verify `[FromHeader]`/query binding + the Alba header API before wiring). For the skill: defer library mechanics to the installed per-library skills (`tanstack-query-best-practices`, `zod`, `shadcn`, `tailwind`, `react-hook-form`); write only CritterMart conventions.

## Working pattern

Author on branch `feat/slice-3-5-view-open-cart`: (1) this frozen prompt; (2) OpenSpec change via the CLI (`openspec new change` → author proposal/design/tasks/spec-delta) + `validate --strict`; (3) implementation (ctx7 verify-before-wiring → endpoint → tests green); (4) `docs/skills/frontend/SKILL.md` + README index row; (5) narrative 005 → v1.1; (6) retro. One consolidated PR; the user merges. `openspec archive` + the workshop § 6 slice 3.5 amendment are the post-merge `tidy: docs` session's job.

## Out of scope

- **No `client/` scaffolding** — no Vite app, no `AddViteApp` wiring, no W2 screen, no TanStack Router setup (locked decision 4; the frontend-bootstrap PR).
- **No CORS-origin injection / no dependabot npm block** — they belong with the Vite app that introduces the SPA origin (carry-forward to the bootstrap PR).
- **No new event, command, projection, or index** — this is a query slice (the index already exists).
- **No Gap #2 (product detail) or Gap #3 (order history)** — separate, non-blocking view slices.
- **No edit to `AddToCart.cs` or `Program.cs`** — the new endpoint lands in its own feature file (no opportunistic edits).
- **No workshop amendment and no `openspec archive`** — post-merge `tidy: docs` concerns; the resolved transport + the added `400` guard are *named* in this PR's design.md and *landed* in the tidy.
