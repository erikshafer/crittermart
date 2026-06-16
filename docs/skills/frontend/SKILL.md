---
name: frontend
description: "CritterMart's storefront SPA conventions: the locked version pins, Zod at every one of the three service wire boundaries, optimistic-UI + rollback against the read model, the useCurrentCustomer identity seam carried as the X-Customer-Id header, and the no-BFF / CORS-in-dev-and-prod posture. Use when building or reviewing any client/ code that calls a Wolverine.Http service. Defers React/Vite/Tailwind/Zod/TanStack library mechanics to the installed per-library skills."
cluster: frontend
tags: [frontend, react, vite, tanstack-query, tanstack-router, zod, shadcn, cors, optimistic-ui, identity-seam]
---

# CritterMart Storefront Frontend Conventions

How CritterMart's round-two storefront SPA is built, and the project-specific stances that the per-library skills cannot carry. The ADRs are the authority for *why*; this skill is the *how* a session-runner needs in seconds. When this skill and the code disagree, the code wins and this skill gets a DEBT row.

> **Status: v4 — W1 browse + the first mutation landed.** The seed was authored alongside slice 3.5's backend read (`GET /carts/mine`) before any client code existed; the **frontend-bootstrap PR** stood up `client/` (the flat single-app Vite SPA), the Aspire `AddViteApp` wiring, and the CORS-origin injection (converging the seed's `[planned]` markers onto the real `client/src/config.ts`, `client/src/api/client.ts`, `client/src/identity/useCurrentCustomer.tsx`, `client/src/router.tsx`, `client/package.json` + committed lockfile). The **W2 cart-review screen** then landed as the first real screen, realizing this skill's Convention 2/4 examples in code: `client/src/cart/` — `cartSchema.ts` (`CartViewSchema`, the first per-read-model schema), `cartQueries.ts` (the first `queryOptions` factory + the `404`→`null` empty-cart mapping), `CartPage.tsx` (the W2 render), `CartBadge.tsx` (the live `select`-derived `Cart (N)`). The **W1 browse screen + add-to-cart** (slices 1.2 + 3.1) then realized Convention 2 against a **second service surface** (`client/src/catalog/` — `ProductCatalogViewSchema`, the second per-read-model schema, a `z.array`; `catalogQueries.ts`, public-keyed; `BrowsePage.tsx`) and brought **Convention 3 to life** with the storefront's first mutation (`client/src/cart/cartMutations.ts` — `useAddToCart`, the three-callback optimistic hook + the pure `addLineToCart` merge) and the command-side `postCommand` in `client/src/api/client.ts`. What is still ahead, not yet in code: the **W3/W4** screens and the `OrderStatusViewSchema`, and the remaining W2 **command** slices — change-qty (3.3), remove (3.2), place-order (4.1) — where `react-hook-form` gains its first consumer (add-to-cart is a button, not a form, so it stays unconsumed). When this skill and the code disagree, the code wins and this skill gets a DEBT row.

**Defer-to-upstream discipline.** The installed per-library skills own the *library* mechanics: **`tanstack-query-best-practices`** (query/mutation APIs, cache keys), **`zod`** (schema authoring), **`shadcn`** + **`tailwind`** (components, v4 styling), **`react-hook-form`** (forms), **`vercel-react-best-practices`** / **`vercel-composition-patterns`** (React structure), **`web-design-guidelines`** (a11y/UX review). This skill documents only what CritterMart layers on top — the pins, the three-surface Zod boundary, the optimistic-UI contract, the identity seam, and the no-BFF/CORS posture. Do not consult this skill for library APIs; do not consult the library skills for the CritterMart convention.

## When to apply this skill

- Writing or reviewing any `client/` code that calls one of the three Wolverine.Http services.
- Choosing how a component obtains the current customer's identity.
- Deciding whether an interaction is a modeled slice or pure frontend state.
- Wiring a new query or mutation against `CartView` / `ProductCatalogView` / `OrderStatusView`.
- Diagnosing a browser CORS error against a service, or a response that "looks right but the app rejects it."

## Convention 1 — pinned versions, never "latest" ([ADR 015](../../decisions/015-vite-react-frontend-stack.md) amendment)

The stack is pinned to the version line both sibling projects (CritterBids, MmoReconnect) run in anger. A lockfile is committed; `latest`/`*` ranges are not used.

| Package | Pin | Package | Pin |
| --- | --- | --- | --- |
| `react` / `react-dom` | `^19.2` | `@tanstack/react-query` | `^5.101` |
| `vite` | `^8` | `@tanstack/react-router` | (code-based; Convention 6) |
| `typescript` | `^6` (strict) | `vitest` | `^4.1` |
| `tailwindcss` | `^4.3` via `@tailwindcss/vite` | `zod` | (latest in the `^3`/`^4` line both siblings run) |
| Node | `≥22` | shadcn/ui companions | `class-variance-authority`, `clsx`, `tailwind-merge` |
| forms | `react-hook-form` + `@hookform/resolvers` | | |

The pins are realized in `client/package.json` with the committed `client/package-lock.json` (and `@types/node` for the Vite config). When a new dependency is added, pin it to a caret-minor range and commit the lockfile in the same PR. Do not bump the majors above without an ADR amendment — the pins are a cross-repo agreement, not a default. (`react-hook-form` + `@hookform/resolvers` are pinned now; they gain their first consumer with the first form-bearing command slice.)

## Convention 2 — Zod at every one of the three wire boundaries ([ADR 015](../../decisions/015-vite-react-frontend-stack.md) R3)

Every HTTP response body is parsed through a Zod schema **before the app trusts it**. This is more load-bearing for CritterMart than for the single-host siblings: the SPA calls **three independently-deployed services** ([ADR 006](../../decisions/006-wolverine-http-per-service-no-bff.md), no BFF), so there are **three contract surfaces** that can drift independently. The Zod parse at the boundary is the only place that drift is caught.

- One schema per read model the SPA binds — `CartViewSchema` (realized: `client/src/cart/cartSchema.ts`), `ProductCatalogViewSchema`, `OrderStatusViewSchema` — `parse()`d (not `safeParse()`-and-ignore) in the query function so a contract drift surfaces as a loud, located failure, not a silent `undefined` three components deep. Model the **full** payload (even fields the screen doesn't read) so the boundary parse validates the whole response; use the default `.strip()` (not `.strict()`) so a benign **additive** backend field is dropped rather than rejected — the wire is one the SPA doesn't own both ends of.
- The same schema feeds shadcn/ui's `Form` + `react-hook-form` (`@hookform/resolvers/zod`), so the wire shape is written once and used at both ends — outbound command validation and inbound response parsing.
- The schema is the SPA's copy of the service contract; it is *not* generated from the backend. When a service's response shape changes, the schema is updated in the same PR that consumes the change.

## Convention 3 — optimistic UI + rollback; the read model is the source of truth ([ADR 015](../../decisions/015-vite-react-frontend-stack.md) R4)

Cart mutations feel instant via TanStack Query's three-callback pattern — and the **re-queried read model, never the optimistic guess, is authoritative**. Realized first in `client/src/cart/cartMutations.ts` (`useAddToCart`, slice 3.1):

- `onMutate` — cancel in-flight cart queries, snapshot the current cache (returned as context for rollback), apply the optimistic change against `cartKeys.mine(customerId)`. Add-to-cart adds/merges the line, so the `CartBadge` (a `select` off the same key) bumps the instant the line lands.
- `onError` — restore the snapshot from context (roll back the guess).
- `onSettled` — invalidate the cart key so the guess converges on the re-fetched `CartView`.

The optimistic *guess* must follow the server's own rule so it reconciles cleanly instead of flickering — so the merge is a **pure, unit-tested function** (`addLineToCart`: a SKU is exactly one line, re-adds sum quantity and never re-price, no open cart yet seeds a fresh one), mirroring the Cart aggregate. This covers the four cart mutations — **add-to-cart** (3.1, realized), **change-quantity** (3.3), **remove-from-cart** (3.2), and **place-order** (4.1, where optimism deliberately *stops* — placement kicks off a cross-BC process whose outcome the SPA can't know, so W3 shows an honest pending status). Because there is **no real-time push** round one (ADR 015 R5), order *status* converges the same way — by refetch (a polling or on-focus `invalidate`), not a socket. Never display the optimistic payload as settled truth; it is a placeholder until `onSettled` reconciles.

## Convention 4 — the `useCurrentCustomer` seam → `X-Customer-Id` header ([ADR 009](../../decisions/009-polecat-deferred-for-round-one.md))

Identity is the round-one stub: a hardcoded customer id behind a single React seam — `useCurrentCustomer()` (a context provider / hook) — that today returns the stubbed id. **Components never read the stubbed value directly**; they call the hook. There is no login screen.

How the id reaches the services (established by slice 3.5): the seam's value is set **once** on the shared HTTP client as the **`X-Customer-Id` header** — not appended per-call as a query param, and not placed in routes. Customer-keyed reads/commands then carry identity ambiently:

```ts
// client/src/cart/cartQueries.ts — fetchMyCart, the cart query function (X-Customer-Id rides ambiently):
const ctx = useApiContext();                       // { customerId } sourced from useCurrentCustomer()
return await fetchParsed(`${serviceUrls.ordersUrl}/carts/mine`, CartViewSchema, ctx);
// fetchParsed sets the X-Customer-Id header, then Zod-parses the body at the boundary.
// 200 → CartView · 404 → throws NotFoundError ("no open cart", a domain state) · 400 → seam misfired.
// The cart query wraps this and maps the NotFoundError → null (empty cart) so the 404 domain-empty
// state is DATA, not an error (see below + client/src/cart/cartQueries.ts:fetchMyCart).
```

Why ambient-header over a `?customerId=` query param: the route already says `/carts/mine`, so identity should not be restated in the URL, and the header is the closest round-one stand-in for the authenticated claim **Polecat** will eventually provide. The promotion is then a **localized swap** — the header becomes a `Bearer` token / claim, call sites unchanged — not a sweep that removes a param from every caller. (The server side resolves the open cart from this identity; see `src/CritterMart.Orders/Features/ViewMyCart.cs`.)

**`404` from `/carts/mine` is a domain state, not an error**: it means "this customer has no open cart" — render an empty cart, ready for the next add. A `400` means the request carried no identity (the seam misfired) — that is a bug, not an empty cart.

**Known divergence — the add-to-cart *command* is route-keyed, the cart *read* is header-keyed.** The cart read is `/carts/mine` (header), but the round-one `AddToCart` endpoint is `POST /carts/{customerId}/items` — so `useAddToCart` (slice 3.1, W1) interpolates the seam's `customerId` into the path while the `X-Customer-Id` header rides along harmlessly (the shared client always sets it; the route wins server-side). This is the one place a customer id legitimately appears in a URL — **not** the `?customerId=` anti-pattern below, but a pre-existing route key the SPA conforms to. It is **logged for a future harmonization tidy** (a header-keyed `POST /carts/mine/items`, a backend + OpenSpec change); until then, do not "fix" the W1 call site by stripping the path segment. (Prompt 019, locked decision 1.)

## Convention 5 — no BFF; CORS in dev *and* prod; no Vite proxy ([ADR 018](../../decisions/018-frontend-three-services-cors-posture.md))

The SPA calls the three services **directly, cross-origin, in every environment**. There is **no BFF** ([ADR 006](../../decisions/006-wolverine-http-per-service-no-bff.md)) and **no Vite dev-server proxy** ([ADR 018](../../decisions/018-frontend-three-services-cors-posture.md)).

- Each service's base URL is read from **Aspire-injected configuration** — `AddViteApp` injects `VITE_CATALOG_URL` / `VITE_INVENTORY_URL` / `VITE_ORDERS_URL` into the SPA resource ([ADR 004](../../decisions/004-dotnet-aspire-orchestrator.md); `src/CritterMart.AppHost/Program.cs`), which `client/src/config.ts` reads off `import.meta.env` and **Zod-validates at startup** (Convention 2 at the config boundary). Standalone `npm run dev` falls back to the services' launchSettings ports (5101/5102/5103). Never hard-coded, never assumed same-origin.
- Because there is no proxy, dev issues **genuine cross-origin requests** exactly like prod — so the cross-network OpenTelemetry boundary (a hard success criterion) is exercised in dev, and a misconfigured CORS allowlist surfaces as a browser CORS error at the cheapest moment.
- The CORS wiring is **symmetric with the URL injection**: the AppHost injects the storefront's origin into each service's `Cors:AllowedOrigins` (as `Cors__AllowedOrigins__0`), so it is the single source of truth for both directions. The dev port is pinned to `5173`, the value `ServiceDefaults.AddFrontendCors` already falls back to. A new SPA origin is added there, not via a proxy (that was the rejected CritterBids shape). The allowlist is asserted per service — `tests/CritterMart.{Catalog,Inventory,Orders}.Tests/CorsPolicyTests.cs`.

## Convention 6 — TanStack Router, code-based; presentation state stays off the event stream

- **Routing is TanStack Router, wired code-based** (no route-tree codegen) — chosen for shared lineage with the already-accepted TanStack Query and type-safe routes + search-params-as-state, at the storefront's small route count ([ADR 015](../../decisions/015-vite-react-frontend-stack.md) amendment ¶2). The route tree is composed in `client/src/router.tsx` (`createRootRoute` + `createRoute` + `createRouter`, with the `Register` module augmentation for typed `Link`s); screen slices add their routes there.
- **Presentation-state guardrail** ([ADR 016](../../decisions/016-frontend-full-pipeline-ui-first-class.md)): an interaction that **reads** a domain fact is a view/query slice (modeled), one that **produces** a domain fact is a command slice (modeled); **pure presentation state** — modal open, pagination cursor, theme toggle, the cart-badge tween — is **not an event** and lives only in frontend code (and, where it matters to the journey, in [Narrative 005](../../narratives/005-customer-storefront.md)). Never mint a domain event for presentation state.

## Pipeline Integration

The frontend runs the **full SDD pipeline** like every backend slice ([ADR 016](../../decisions/016-frontend-full-pipeline-ui-first-class.md)): workshop slice (with a `Wireframe` dimension, § 5.1 W1–W4) → narrative (the *screen lens*, [Narrative 005](../../narratives/005-customer-storefront.md)) → OpenSpec proposal → prompt → implementation → retro. The screens bind to the three read models the audit confirmed: `ProductCatalogView` (W1), `CartView` (W2, via slice 3.5's `GET /carts/mine`), `OrderStatusView` (W4). Build order follows the [pre-frontend endpoint audit](../../research/pre-frontend-endpoint-audit.md): Gap #1 (slice 3.5) first.

## Quick Reference: common mistakes to catch

- **Trusting a response without a Zod `parse` at the boundary.** Three services drift independently; the boundary parse is the only catch. (Convention 2)
- **Displaying the optimistic payload as settled truth** instead of reconciling against the refetched read model on `onSettled`. (Convention 3)
- **Reading the stubbed customer id directly** instead of through `useCurrentCustomer()`, or appending `?customerId=` per call instead of the ambient `X-Customer-Id` header. Both make the Polecat promotion a call-site sweep. (Convention 4)
- **Treating `404` from `/carts/mine` as an error** rather than "no open cart → render empty." (Convention 4)
- **Hard-coding a service base URL or assuming same-origin / a dev proxy.** URLs are Aspire-injected; every call is real cross-origin. (Convention 5)
- **Minting a domain event for presentation state** (modal, pagination, theme). (Convention 6)
- **Using `latest`/`*` for a dependency** instead of the pinned caret-minor range + committed lockfile. (Convention 1)

## See also

- Per-library skills (mechanics): `tanstack-query-best-practices`, `zod`, `shadcn`, `tailwind`, `react-hook-form`, `vercel-react-best-practices`, `vercel-composition-patterns`, `web-design-guidelines`.
- [ADR 015](../../decisions/015-vite-react-frontend-stack.md) — the stack + version pins + R3/R4/R5 stances.
- [ADR 016](../../decisions/016-frontend-full-pipeline-ui-first-class.md) — UI first-class; the presentation-state guardrail.
- [ADR 018](../../decisions/018-frontend-three-services-cors-posture.md) — no BFF, CORS in dev and prod, no proxy.
- [ADR 009](../../decisions/009-polecat-deferred-for-round-one.md) — stubbed identity; the `useCurrentCustomer` seam.
- [Narrative 005](../../narratives/005-customer-storefront.md) — the screen-lens journey through W1–W4.
- [pre-frontend endpoint audit](../../research/pre-frontend-endpoint-audit.md) — the read-model gaps and build order.
- `src/CritterMart.Orders/Features/ViewMyCart.cs` — the slice 3.5 server side this skill's Convention 4 example consumes.
- `client/` — the storefront SPA. Entry points: `src/config.ts` (Aspire-injected service URLs, Zod-validated), `src/api/client.ts` (shared fetch — `fetchParsed` read + `postCommand` command, both header seam + boundary parse), `src/identity/useCurrentCustomer.tsx` (the ADR 009 seam), `src/router.tsx` (code-based router; `/` → W1 browse, `/cart` → W2), `src/catalog/` (the W1 browse feature folder — `catalogSchema.ts` / `catalogQueries.ts` / `BrowsePage.tsx`), `src/cart/` (the W2 + cart-command folder — `cartSchema.ts` / `cartQueries.ts` / `CartPage.tsx` / `CartBadge.tsx` / `cartMutations.ts`, the optimistic-UI precedent the W2-edit slices reuse), `client/README.md` (run + layout). The Aspire wiring is in `src/CritterMart.AppHost/Program.cs` (`AddViteApp` + CORS injection).
