# Prompt: Implementations 018 — W2 Cart-Review screen (first storefront screen slice)

**Kind**: frontend **screen slice** — the first real storefront screen (ADR 016, UI first-class). Binds the already-shipped `GET /carts/mine` (slice 3.5) into wireframe **W2**. **Read-only**: render the cart, no edits, no checkout.
**Source**: frozen from the session handoff `crittermart-handoff-w2-cart-review.md` (ephemeral, `%TEMP%`); this is its durable in-repo transcription.
**Files touched**: this prompt; `client/src/cart/**` (new feature folder — `cartSchema.ts`, `cartQueries.ts`, `CartPage.tsx`, `cartSchema.test.ts`, `cartQueries.test.ts`, `CartPage.test.tsx`); `client/src/router.tsx` (+ `/cart` route); `client/src/components/AppShell.tsx` (static `storefront` span → live cart badge); `docs/skills/frontend/SKILL.md` (Convention 2/4 examples converge from "added by the W2 screen slice" → real `client/src/cart/` references); `docs/narratives/005-customer-storefront.md` (v1.1 → **v1.2**); `docs/{prompts,retrospectives}/README.md` (counts); `docs/retrospectives/implementations/018-slice-w2-cart-review.md` (forthcoming).
**Mode**: solo; two forks presented collaboratively (AskUserQuestion + previews) and resolved with the user **before any code** — they appear below as locked decisions.
**Commit subject**: `feat: slice W2 cart-review screen — render GET /carts/mine (read-only)`

## Framing

Slice 3.5 shipped the cold-load read (`GET /carts/mine`); the frontend-bootstrap PR (#52) stood up `client/`. What has never existed is a real **screen**. This is the first one: the **W2 cart-review** view, the keystone the narrative names — the screen that renders the customer's open cart on a cold load by binding the customer-keyed read. It introduces the precedents every later screen reuses: the **first per-read-model Zod schema** (`CartViewSchema`), the **first TanStack Query `queryOptions` factory**, the **404→empty-as-data** mapping, and the **live cart badge**. It also discharges retro 016's deferred **browser-level CORS + OpenTelemetry-trace verification** — the first time a running SPA issues a real cross-origin fetch into a service.

## Goal

Navigating to `/cart` renders the customer's open cart — wireframe **W2** — from `GET /carts/mine`: line rows (name · sku · price · qty), a client-derived **Total** (`Σ price×qty`), and honest **loading / empty / error** states. The header cart badge shows the live **distinct-line count**. Every wire response is Zod-parsed at the boundary (Convention 2); `404` resolves to an **empty cart** (Convention 4), not an error. `npm run build` + `npm run test` green. The Aspire stack boots, the SPA loads at `:5173`, the cross-origin `GET /carts/mine` succeeds (CORS), and the OTel trace spans **SPA → Orders** in the dashboard.

## Spec delta

**No OpenSpec / workshop change** — this slice purely *binds* the existing `GET /carts/mine` contract (slice 3.5, archived); it adds no new behavioral SHALL, no new event, no new read model (ADR 016 + retro 015's call: a screen that binds an existing read is not a new modeled contract). The canonical-spec movement is **Narrative 005 → v1.2**: Moment 3's keystone read goes from *server-half built, screen pending* → *screen built*; the `## Forthcoming` and `## Document History` record the W2 render landing. The frontend skill's Convention 2/4 examples converge from their "added by the W2 screen slice" placeholders onto the real `client/src/cart/` files. If the session discovers it needs a new read contract, **that** is the trigger to add an OpenSpec change — but a read-only bind shouldn't.

## Locked decisions (forks resolved with the user at session start, 2026-06-16)

1. **Read-only W2 first.** Render the cart by binding `GET /carts/mine`. The W2 **edit** commands (3.2 remove `[x]`, 3.3 change-qty `[-]/[+]`) and **checkout** (4.1 `[ Place Order ]` → W3) are their own modeled command slices and follow as the next W2 PRs — keeping one-slice grain and leaving `react-hook-form` unconsumed until a command slice. The screen renders qty as read-only text; the edit/checkout controls are deliberately absent, not stubbed.
2. **Feature folder `client/src/cart/`.** Colocate the cart's schema + query factory + page + tests in one folder, mirroring the backend's vertical-slice architecture (`Features/ViewMyCart.cs`) — the whole slice reviewable in one place, and W1 (`catalog/`) / W4 (`orders/`) get sibling folders. (Rejected: kind-folders `api/schemas/` + `queries/`, which scatter one slice across three folders and diverge from the backend VSA colocation.) The bootstrap's `routes/HomePage.tsx` placeholder stays put.

## Orientation

1. **The session handoff** + **CLAUDE.md** — the screen-slice scope, one-prompt-one-PR, no-opportunistic-edits, spec-delta-closure, `{type}/{slug}` branch.
2. **`docs/skills/frontend/SKILL.md`** — the authority for *how*: Convention 2 (Zod-at-boundary, `parse` not `safeParse`), Convention 3 (optimistic-UI — groundwork only this slice), Convention 4 (`useCurrentCustomer` → `X-Customer-Id`; `404` = no open cart), Convention 5 (no-BFF/CORS), Convention 6 (code-based router; presentation-state guardrail).
3. **`docs/narratives/005-customer-storefront.md` (v1.1)** — Moment 3 is W2; the spec this session bumps to v1.2.
4. **`docs/workshops/001-crittermart-event-model.md` § 5.1** — the W2 wireframe (lines ~231–251) + the slice/wireframe table.
5. **The contract — already shipped**: `src/CritterMart.Orders/Features/ViewMyCart.cs` (don't change) + `src/CritterMart.Orders/Cart/CartView.cs` (the shape). `200` → `CartView`; `404` → no open cart; `400` → missing header (a bug).
6. **The client surface that exists**: `client/src/api/client.ts` (`fetchParsed`, `useApiContext`, `NotFoundError`), `client/src/config.ts` (`ordersUrl`), `client/src/identity/useCurrentCustomer.tsx` (the seam), `client/src/router.tsx` (the `/cart` reservation), `client/src/components/AppShell.tsx` (the badge slot). Mirror the existing test idiom in `client/src/api/client.test.ts` + `client/src/routes/HomePage.test.tsx`.
7. **Skills**: `tanstack-query-best-practices` (`queryOptions` + key factory + `select` for the badge), `zod` (the first schema), `shadcn`/`tailwind` (the rows/badge/empty-state on Tailwind v4), `vitest` (tests), `web-design-guidelines` (a11y/UX pass), `verify`/`run` (boot Aspire for the CORS + OTel check). Defer **library** mechanics to those skills; defer **CritterMart conventions** to the frontend SKILL.

## Working pattern

Author the schema (verify camelCase wire casing empirically against the live response before freezing) → the query factory (`queryOptions` + `404`→`null`) → `CartPage` (rows · derived total · loading/empty/error) → the `/cart` route + the live badge → vitest (schema parse + drift; `404`→empty; populated + empty render) → `npm run build`/`test` green → boot Aspire, verify browser CORS + OTel trace (and confirm the wire casing) → converge the skill + bump Narrative 005 → v1.2 → README counts → retro. One PR on `feat/w2-cart-review`; the user merges.

## Deliverable plan

- `client/src/cart/cartSchema.ts` — `CartViewSchema` (+ `CartLineSchema`), `parse()`d at the boundary; `z.infer` types exported. Models the full camelCase payload; default `.strip()` (forward-compatible to additive backend fields, loud on missing/mistyped fields the SPA reads).
- `client/src/cart/cartQueries.ts` — `cartKeys` (key factory), `cartQueryOptions(ctx)` (calls `fetchParsed(ordersUrl/carts/mine, …)`, maps `NotFoundError`→`null`), `selectCartLineCount` (stable selector for the badge).
- `client/src/cart/CartPage.tsx` — the W2 screen; total summed in **integer cents** (no binary-float money drift), `Intl.NumberFormat` for display.
- `client/src/router.tsx` — the `/cart` route.
- `client/src/components/AppShell.tsx` — the live `Cart (N)` badge (a `Link` to `/cart`), count via `select`.
- Tests: `cartSchema.test.ts`, `cartQueries.test.ts`, `CartPage.test.tsx`.
- `docs/skills/frontend/SKILL.md` convergence; `docs/narratives/005-customer-storefront.md` v1.2; README counts; retro 018.

## Out of scope

- **No edit mutations** (3.2 remove / 3.3 change-qty) and **no checkout** (4.1 place-order / W3) — the next W2 PRs. No `[-]/[+]/[x]/[ Place Order ]` controls this slice (read-only).
- **No `react-hook-form` consumer** — it gains its first use with a form-bearing command slice.
- **No W1 / W3 / W4 screens**, no `ProductCatalogViewSchema` / `OrderStatusViewSchema` — their own screen slices.
- **No OpenSpec change, no workshop amendment** — a read-only bind of an existing contract (Narrative 005 v1.2 is the only spec movement).
- **No optimistic-UI wiring** — Convention 3 is groundwork-only here (no mutation to make optimistic); it goes live with the first cart-edit command slice.
- **No backend change** — `Features/ViewMyCart.cs` / `CartView.cs` are consumed, not edited.
- **No shadcn component install churn** unless a component is genuinely needed; reuse the existing neutral-token Tailwind idiom from `AppShell`/`HomePage`.
