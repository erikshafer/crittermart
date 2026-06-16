# Prompt: Implementations 019 — W1 Browse + Add-to-cart (slices 1.2 + 3.1, bundled)

**Kind**: frontend **screen slice** — the W1 storefront landing (ADR 016, UI first-class). Bundles the **browse read** (slice 1.2, `GET /products`) and the **add-to-cart command** (slice 3.1, `POST /carts/{customerId}/items`) in one PR — the project's **first mutation** and **first optimistic-UI** (frontend SKILL Convention 3 goes live). The browse→add→cart loop becomes demonstrable: W2 (#58) finally has a way to be populated.
**Source**: frozen from the session handoff `crittermart-handoff-w1-browse-add.md` (ephemeral, `%TEMP%`); this is its durable in-repo transcription.
**Files touched**: this prompt; `client/src/api/client.ts` (+ `postCommand`, the command-side counterpart of `fetchParsed`); `client/src/catalog/**` (new noun folder — `catalogSchema.ts`, `catalogQueries.ts`, `BrowsePage.tsx`, `catalogSchema.test.ts`, `catalogQueries.test.ts`, `BrowsePage.test.tsx`); `client/src/cart/cartMutations.ts` (+ `cartMutations.test.tsx` — the first cart command); `client/src/router.tsx` (`/` → `BrowsePage`, retire the home route); **delete** `client/src/routes/HomePage.tsx` + `HomePage.test.tsx`; `docs/skills/frontend/SKILL.md` (Convention 3 example converges onto real `cartMutations.ts`; status v3 → v4); `docs/narratives/005-customer-storefront.md` (v1.2 → **v1.3**); `docs/{prompts,retrospectives}/README.md` (counts 18 → 19); `docs/retrospectives/implementations/019-slice-w1-browse-add.md` (forthcoming).
**Mode**: solo; two forks presented collaboratively (AskUserQuestion + previews) and resolved with the user **before any code** — they appear below as locked decisions.
**Commit subject**: `feat: slice W1 browse + add-to-cart — product listing + first optimistic mutation`

## Framing

W2 (#58) shipped the cart-review *read* but nothing yet fills the cart. W1 is the journey entry and the only UI path that populates a cart: render the storefront landing (wireframe **W1**) from `GET /products`, and make `[ Add to cart ]` work. It bundles two workshop slices because they share one screen — 1.2 is the listing, 3.1 is the button on it — and together they close the **browse → add → (badge bumps) → W2 cart** loop.

This is the slice where two project firsts land: the **second per-read-model Zod schema** (`ProductCatalogViewSchema`, proving the multi-service SPA against a *second* service surface, Catalog) and the **first mutation / first optimistic-UI** (`useAddToCart`, the template every later cart mutation — 3.2 remove, 3.3 change-qty, 4.1 place-order — copies). It also discharges retro 016/018's still-owed **browser-level CORS + OpenTelemetry-trace** verification across **both** services (SPA → Catalog *and* SPA → Orders).

## Goal

Landing on `/` renders the product listing — wireframe **W1**: a grid of cards (name · sku · price · description · `[ Add to cart ]`) from `GET /products`, Zod-parsed at the boundary, with honest **loading / empty / error** states. Tapping `[ Add to cart ]` snapshots the product's name + price into `AddToCart { sku, quantity: 1, productSnapshot: { name, price } }`, sends it to Orders, and the header cart badge bumps **optimistically** — `onMutate` adds/merges the line into the cached `CartView`, `onError` rolls back, `onSettled` invalidates `cartKeys.mine` so the guess reconciles against the refetched read model. `npm run build` + `npm run test` green. The Aspire stack boots, the SPA loads at `:5173`, the cross-origin `GET /products` (CORS, Catalog) and `POST /carts/{id}/items` (CORS, Orders) succeed, and the OTel trace spans **SPA → Catalog** and **SPA → Orders** in the dashboard.

## Spec delta

**No OpenSpec / workshop change** — this slice *binds* two already-shipped contracts (`GET /products` slice 1.2; `POST /carts/{customerId}/items` slice 3.1); it adds no new behavioral SHALL, no new event, no new read model (ADR 016 + the #58 precedent: a screen that binds existing contracts is not a new modeled contract). The canonical-spec movement is **Narrative 005 → v1.3**: Moment 1 (land/browse) and Moment 2 (add to cart, optimistic badge) go from *screen-pending* → *built*; `## Forthcoming` and `## Document History` record it. The frontend skill's Convention 3 example converges from abstract ("e.g. bump the badge, add the line") onto the real `client/src/cart/cartMutations.ts`, and the status note advances v3 → v4.

## Locked decisions (forks resolved with the user at session start, 2026-06-16)

1. **Route-keyed URL for the add-to-cart command — W1 stays screen-only.** The command call site reads `customerId` from the `useCurrentCustomer` seam and interpolates it into the path (`POST /carts/${customerId}/items`); the `X-Customer-Id` header rides along (set by the shared client) but the route is authoritative server-side. **No backend change, no OpenSpec change.** This accepts a divergence — the cart **read** is header-keyed (`/carts/mine`), the **command** is route-keyed (`/carts/{id}/items`) — which is **logged as a future harmonization tidy** (a header-keyed `POST /carts/mine/items` would be the consistent shape, but that is a backend + OpenSpec change, not W1's to make; the route-keying is pre-existing round-one backend). (Rejected: option (b), adding `POST /carts/mine/items` now — breaks the screen-only/one-PR grain.)
2. **`/` = `BrowsePage`; retire the bootstrap `HomePage`.** Narrative Moment 1 says W1 *is* the storefront landing, so the listing takes `/`. The bootstrap wiring-check placeholder (`routes/HomePage.tsx` + its test) is deleted — its job (prove the scaffold renders, identity resolves, service URLs arrive) is done. (Rejected: a separate `/products` route keeping `/` as `HomePage` — a detour that contradicts the narrative.)

## Orientation

1. **The session handoff** + **CLAUDE.md** — screen-slice scope, one-prompt-one-PR, no-opportunistic-edits, spec-delta closure, `{type}/{slug}` branch.
2. **`docs/skills/frontend/SKILL.md` (v3)** — the authority for *how*: Convention 2 (Zod-at-boundary, `parse` not `safeParse`; second schema is `z.array`), **Convention 3 (optimistic-UI + rollback — goes live this slice)**, Convention 4 (`useCurrentCustomer` → `X-Customer-Id`), Convention 5 (no-BFF/CORS, every call real cross-origin), Convention 6 (code-based router; presentation-state guardrail).
3. **`docs/narratives/005-customer-storefront.md` (v1.2)** — Moment 1 (W1 land/browse) + Moment 2 (W1 add → badge); the spec this session bumps to v1.3.
4. **`docs/workshops/001-crittermart-event-model.md`** — § 5.1 W1 wireframe (lines ~208–229); § 6 GWT for slice 1.2 (~320) and 3.1 (~405).
5. **The W2 precedent — mirror it**: `client/src/cart/` — `cartSchema.ts`, `cartQueries.ts` (the `queryOptions` + key-factory + `select` idiom), `CartPage.tsx`, `CartBadge.tsx`, `*.test.ts(x)`.
6. **The contracts — already shipped (no backend change)**: `src/CritterMart.Catalog/Features/BrowseProducts.cs` + `Products/Product.cs` (`ProductCatalogView(Sku, Name, Description, Price)`, `GET /products` → `List<…>`, empty → `[]` 200); `src/CritterMart.Orders/Features/AddToCart.cs` + `Shopping/ProductSnapshot.cs` (`AddToCart(Sku, Quantity, ProductSnapshot)`, `ProductSnapshot(Name, Price)`, → 201 `AddToCartResponse(CartId)`). **Send `productSnapshot` exactly** — retro 018 found sending `snapshot` binds null and 500s the projection.
7. **Skills**: `tanstack-query-best-practices` (the three-callback optimistic mutation — `mut-optimistic-updates` incl. rollback context, `mut-invalidate-queries`; `queryOptions` for the read), `zod` (the `z.array` schema), `shadcn`/`tailwind` (cards/grid/button on Tailwind v4, neutral-token idiom), `vitest` (tests), `web-design-guidelines` (a11y/UX on the grid + button states), `verify`/`run` (boot Aspire for the two-service CORS + OTel check).

## Working pattern

Add `postCommand` to the shared client → author `catalogSchema` (the `z.array`) + `catalogQueries` (`productsQueryOptions`, no customerId in the key — the catalog is public) → `cartMutations` (the pure `addLineToCart` merge + `useAddToCart` three-callback optimistic hook against `cartKeys.mine`) → `BrowsePage` (grid of `ProductCard`s, each with per-card add feedback; loading/empty/error) → router (`/` → `BrowsePage`, delete `HomePage`) → vitest (schema parse + drift; browse render populated + empty; mutation optimistic-bump + rollback + invalidate) → `npm run build`/`test` green → boot Aspire, verify two-service browser CORS + OTel trace (and confirm wire casing) → converge the skill (Convention 3 example) + bump Narrative 005 → v1.3 → README counts → retro. One PR on `feat/w1-browse-add`; the user merges.

## Deliverable plan

- `client/src/api/client.ts` — `postCommand(url, body, ctx, schema)`: sets `X-Customer-Id` + `Content-Type`, POSTs JSON, parses the response body at the boundary (Convention 2). Pure (no React), like `fetchParsed`.
- `client/src/catalog/catalogSchema.ts` — `ProductCatalogViewSchema` (camelCase `sku`/`name`/`description`/`price`; `price` non-negative number) + `ProductCatalogListSchema` (`z.array`); `z.infer` types.
- `client/src/catalog/catalogQueries.ts` — `catalogKeys` (public — no customerId) + `productsQueryOptions(ctx)` calling `fetchParsed(catalogUrl/products, …)`.
- `client/src/cart/cartMutations.ts` — `addLineToCart` (pure optimistic merge: null → new cart; existing sku → sum qty; new sku → append) + `AddToCartResponseSchema` + `useAddToCart()` (the three-callback optimistic hook; route-keyed URL per locked decision 1).
- `client/src/catalog/BrowsePage.tsx` — the W1 grid; `ProductCard` sub-component owns its own `useAddToCart()` for per-card `isPending` feedback; loading/empty/error.
- `client/src/router.tsx` — `/` → `BrowsePage`; remove the home route + `HomePage` import.
- Tests: `catalogSchema.test.ts`, `catalogQueries.test.ts`, `cartMutations.test.tsx`, `BrowsePage.test.tsx`.
- **Delete**: `client/src/routes/HomePage.tsx`, `client/src/routes/HomePage.test.tsx`.
- `docs/skills/frontend/SKILL.md` (Convention 3 convergence; status v4); `docs/narratives/005-customer-storefront.md` v1.3; README counts; retro 019.

## Out of scope

- **No W2 edit/checkout** (3.2 remove / 3.3 change-qty / 4.1 place-order) — their own command slices.
- **No `GET /products/{sku}` / product-detail fetch** — detail folds from the list payload (Gap #2, deferred).
- **No `react-hook-form` consumer** — add-to-cart is a button, not a form; the first form is a later slice.
- **No backend change, no OpenSpec change** — both contracts are consumed as-is (locked decision 1); Narrative 005 v1.3 is the only spec movement.
- **No harmonization of the cart identity transport** — the read=header / command=route divergence is logged for a future tidy, not fixed here (locked decision 1).
- **No W3 / W4 screens**, no `OrderStatusViewSchema`.
- **No shadcn component install churn** unless genuinely needed; reuse the neutral-token Tailwind idiom from `CartPage`/`AppShell`.
