---
retrospective: 018
kind: implementations
prompt: docs/prompts/implementations/018-slice-w2-cart-review.md
deliverable: client/src/cart/ (cartSchema.ts, cartQueries.ts, CartPage.tsx, CartBadge.tsx + 3 test files), client/src/router.tsx, client/src/components/AppShell.tsx, docs/narratives/005-customer-storefront.md (v1.2), docs/skills/frontend/SKILL.md (v3)
date: 2026-06-16
mode: solo
session-runner: Claude (Opus 4.8)
---

# Retrospective — Implementations 018: W2 Cart-Review screen (first storefront screen)

## Outcome summary

Shipped the **first real storefront screen** — wireframe **W2**, the cart-review read. `client/src/cart/` binds slice 3.5's `GET /carts/mine` and renders the customer's open cart on a cold load, establishing the precedents every later screen (W1/W4) reuses:

- **`CartViewSchema`** (`cartSchema.ts`) — the first per-read-model Zod schema, modeling the full camelCase `CartView` payload with default `.strip()` (forward-compatible to additive backend fields; loud on missing/mistyped read fields).
- **`cartQueryOptions` + `fetchMyCart`** (`cartQueries.ts`) — the first TanStack Query `queryOptions` factory, with the **`404`→`null`** mapping that makes "no open cart" empty *data*, not an error (Convention 4). A `cartKeys` key-factory carries the customer id (the resolution dependency).
- **`CartPage`** (`CartPage.tsx`) — line rows (name · sku · price · qty · subtotal), a client-derived **Total summed in integer cents** (no binary-float money drift), and honest **loading / empty / error** states in a semantic, accessible `<table>`.
- **`CartBadge`** (`CartBadge.tsx`) — the live header `Cart (N)`, a `select`-derived distinct-line count sharing the cart query key (one `GET /carts/mine` feeds both badge and page).

**Read-only by design** (locked decision 1): the W2 `[-]/[+]/[x]` edits (3.2/3.3) and `[ Place Order ]` checkout (4.1 → W3) stay screen-pending as their own command slices; `react-hook-form` + Convention 3 optimistic-UI remain unconsumed until then. The `/cart` route was added and the static `storefront` span swapped for the live badge. **Two forks** (read-only-first scope; `src/cart/` feature-folder) were presented with previews and resolved with the owner before any code.

**Tests**: client — **18 vitest tests, 0 failures** (12 new: 4 schema parse/drift, 6 query incl. the `404`→`null` path + the badge selector + key factory, 2 `CartPage` populated/empty render). `npm run build` (tsc --noEmit + vite build, 234 modules) green. No backend code touched.

**Spec movement**: Narrative 005 → **v1.2** (Moment 3's read screen built; edits/checkout pending); frontend skill → **v3** (Convention 2/4 examples converged onto the real `client/src/cart/` files). No OpenSpec/workshop change — a screen-only bind of the existing read contract.

## What worked

- **The live Aspire probe discharged retro 016's deferred verification — headlessly, further than expected.** Booting the full stack and hitting Orders directly on `:5103` confirmed at the wire: (a) **camelCase casing** with `price` as a JSON number and `lastActivityAt` an ISO string — exactly what `CartViewSchema` assumes, so the schema freeze is empirically grounded, not inferred; (b) the **CORS allowlist** admits `http://localhost:5173` (ACAO echoed) and omits the header for an unlisted origin. Retro 016 predicted only the *browser render + OTel dashboard trace* would remain un-headless-checkable; that prediction held — but the casing and CORS-at-the-wire were both confirmable via curl, lifting the ceiling another notch.
- **Two-fork AskUserQuestion up front.** Scope (read-only vs bundling edits/checkout) and module layout (feature-folder vs kind-folders) were resolved with previews before any code, so the session ran without a mid-stream interrupt and the PR stayed one-slice-grained.
- **Integer-cents total.** Summing `Σ round(price×100)×qty` and formatting via `Intl.NumberFormat` keeps the displayed Total exact ($103.98) regardless of binary-float dollar drift — a small, defensible teaching detail confirmed against the live cart.
- **`select`-derived badge sharing the query key.** The badge subscribes to a projection of the same cart query, so it re-renders only on a count change and rides the single in-flight `GET /carts/mine` — the idiomatic TanStack sharing model, and a clean first use of `select`.

## What was harder / notable

- **The seeding 500 was a real teaching artifact.** My first seed payload sent `snapshot` instead of `productSnapshot` (the `AddToCart` record's parameter name). Wolverine bound the unmatched field to `null`, and the **inline `CartViewProjection`** — not the handler — threw an NRE folding `CartItemAdded` with a null `ProductSnapshot`. The transaction rolled back cleanly (the read stayed a correct 404). It surfaced a latent backend robustness gap: a malformed command 500s in projection rather than 400-validating at the boundary. **Out of scope** here — only reachable by a hand-malformed request the SPA never sends — but worth a future hardening note (see outstanding).
- **`CartPage`'s empty state needs router context to test.** The empty branch renders a router `<Link>`; a bare `render(<CartPage/>)` would throw. The test helper wraps it in a throwaway `createMemoryHistory` router — the correct pattern, which W1/W4 tests will copy. Noted so the next screen test doesn't re-derive it.
- **TS6 strictness, again.** `noUnusedLocals` flagged an unused `createRoute` import in the test (the router uses only the root route) — a one-line fix, but the same class of friction retro 016 flagged. The frontend build genuinely type-checks test files (they're under `src/`), so test imports must stay tight.
- **CORS negative case reads as `200` + absent header, not a rejection.** Worth restating in the skill's mental model: CORS is browser-enforced; the server answers either way and simply omits ACAO for an unlisted origin. The PowerShell client (not a browser) sees the body — the *absent header* is the allowlist working.

## Methodology refinements

- **Live wire-casing confirmation is the schema-freeze gate — don't write "confirmed" until observed.** Mid-session I had written "confirmed against the live response" in the schema/narrative *before* booting. That was premature. The fix-pattern: a per-read-model schema's casing claim is provisional ("expected — Wolverine.Http web defaults, no override") until a live probe (or the manual browser pass) observes it; only then does the wording harden to "confirmed." This session ran the probe, so the wording is now true — but the discipline is to not report verification ahead of doing it.
- **Headless-verification ceiling, lifted again.** Retro 011 lifted CORS to in-process Alba; retro 016 lifted it to the production config key but left the *browser* round trip deferred. This session adds: with the stack booted, the **wire casing and the live CORS allowlist are curl-confirmable** without a browser. What still genuinely needs eyes: the SPA's in-browser cross-origin fetch rendering, and the OTel trace spanning SPA→Orders in the Aspire dashboard. That residue is the irreducible visual criterion.
- **Feature-folder precedent for screens.** `src/cart/` colocates schema + queries + page + badge + tests, mirroring the backend's `Features/` VSA. W1 (`catalog/`) and W4 (`orders/`) get sibling folders; the bootstrap's `routes/HomePage.tsx` placeholder is the only thing outside this shape.

## Outstanding / next-session inputs

- **Browser render + OTel trace = the owner's visual pass (stack left running).** The Aspire stack is **still up** from this session's verification, with a two-line cart seeded for `customer-demo`. To finish the deferred criterion: load `http://localhost:5173/cart` (see the populated W2 render) and watch the `GET /carts/mine` trace span **SPA→Orders** in the dashboard (`https://localhost:17090`, login token in the AppHost log). Tear down with the runbook in the session summary when done. *Not headless-checkable — by design.*
- **W2 command slices are next**: 3.2 remove / 3.3 change-qty (the `[-]/[+]/[x]` controls + Convention 3 optimistic-UI), then 4.1 place-order → W3. `react-hook-form` gains its first consumer there.
- **Latent backend gap (surfaced, not fixed)**: `AddToCart` with a null `ProductSnapshot` 500s in the inline projection (NRE) rather than 400-validating at the boundary. Out of scope (the SPA always sends the snapshot); a candidate for a future Orders hardening slice if malformed-input robustness becomes a goal.
- **Design-return cadence**: this PR is self-interleaving (it bumps Narrative 005 + the skill), so it doubles as the design-return after the 016/017 implementation run. The next pure-implementation slice (3.2/3.3) is clear to proceed.
- **Focus-ring enhancement (deferred)**: the screen relies on the UA default focus outline (visible, compliant, consistent with the bootstrap). Adopting shadcn's styled `focus-visible:ring` app-wide is a future `tidy:`, not an opportunistic per-slice edit.

## Spec-delta — landed?

**Named delta landed.** The prompt named **Narrative 005 → v1.2** as the sole spec movement (no OpenSpec/workshop change, because W2 binds an existing read contract). That landed: Narrative 005 is v1.2, its `## Forthcoming` now reads "read screen built, edits/checkout pending," and a v1.2 `## Document History` row records the W2 render. The forward-confirmed "no behavioral spec delta" holds — no new SHALL, event, or read model was added. The frontend skill's v2→v3 convergence (Convention 2/4 examples onto real `client/src/cart/` files) landed alongside, as named.
