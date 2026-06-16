---
narrative: 005
title: The Customer Shops the Storefront
actor: Customer
status: draft
version: v1.2
lens: frontend / screen (the UI lens; companion to the behavior narratives 002 + 004)
slices: [1.2, 3.1, 3.2, 3.3, 3.5, 4.1]
references:
  - docs/workshops/001-crittermart-event-model.md (§ 5.1 wireframe dimension W1–W4; slice 3.5; § 6 GWT)
  - docs/decisions/016-frontend-full-pipeline-ui-first-class.md (UI first-class; this narrative is the journey through the wireframes)
  - docs/decisions/015-vite-react-frontend-stack.md (the SPA stack; Zod-at-boundary R3; optimistic-UI + rollback R4; no SEO/push R5)
  - docs/decisions/018-frontend-three-services-cors-posture.md (SPA → three services directly; CORS in dev and prod, no proxy)
  - docs/decisions/006-wolverine-http-per-service-no-bff.md (no BFF — the SPA is the cross-service orchestrator)
  - docs/decisions/009-polecat-deferred-for-round-one.md (stubbed customer id; the useCurrentCustomer seam)
  - docs/research/pre-frontend-endpoint-audit.md (Gaps #1–#3; slice 3.5 closes the blocker)
  - docs/narratives/002-customer-browse-catalog.md (the behavior of browsing — this is its screen lens)
  - docs/narratives/004-customer-purchase.md (the behavior of buying — this is its screen lens)
---

# Narrative 005 — The Customer Shops the Storefront

This is the same Customer as [Narrative 002](002-customer-browse-catalog.md) (browsing) and [Narrative 004](004-customer-purchase.md) (buying) — but told through a different lens. Those two narratives are the **behavior** of the journey: the event streams, the read models, the cross-bounded-context hops, the cancellations. This one is the **screen**: what the Customer actually sees and touches as they move from the storefront's product listing, into a cart, through checkout, to a tracked order. It is the realization of [ADR 016](../decisions/016-frontend-full-pipeline-ui-first-class.md) — the UI made first-class — written as the journey *through* the wireframes the workshop now carries in [§ 5.1](../workshops/001-crittermart-event-model.md) (**W1** Browse, **W2** Cart Review, **W3** Order Confirmation, **W4** Order Status).

The storefront is a **single client-side React SPA** that calls the three Wolverine.Http services **directly** — there is no BFF ([ADR 006](../decisions/006-wolverine-http-per-service-no-bff.md)); the SPA *is* the cross-service orchestrator, and that real cross-network boundary is the OpenTelemetry-trace beat the talk depends on ([ADR 015](../decisions/015-vite-react-frontend-stack.md), [ADR 018](../decisions/018-frontend-three-services-cors-posture.md)). The stack is locked and is not re-decided here; this narrative threads its load-bearing stances as established context: every wire response is **Zod-parsed at the boundary** before the app trusts it (three independently-deployed services means three contract surfaces; ADR 015 R3); cart mutations feel instant through **optimistic UI with rollback** (`onMutate` applies the guess, `onError` rolls back, `onSettled` refetches the authoritative read model; ADR 015 R4); and there is **no real-time push** round one (status converges by refetch, not a socket; ADR 015 R5). The Customer's identity is the round-one stub — a hardcoded customer id behind the `useCurrentCustomer` seam ([ADR 009](../decisions/009-polecat-deferred-for-round-one.md)) — so there is no login screen.

## Journey scope

The screen journey threads the wireframe-bearing slices into one browse → cart → checkout → track arc:

- **Slice 1.2 — Browse and view products.** Moment 1: landing on the storefront (**W1**).
- **Slice 3.1 — Add item to cart.** Moment 2: adding from the listing, optimistically (**W1** → cart badge).
- **Slice 3.5 — View my open cart** *(net-new view slice, modeled in this session's workshop amendment).* Moment 3: returning to the cart on a cold load (**W2**). This is the keystone — the read that makes the cart-review screen renderable.
- **Slices 3.2 / 3.3 — Remove item / change quantity.** Also Moment 3: editing the cart in place (**W2**), optimistically.
- **Slice 4.1 — Place order from cart.** Moment 4: checkout (**W2** → **W3**).
- **The order lifecycle, read as status** (slices 4.2–4.7, surfaced through `OrderStatusView`). Moment 5: tracking the order (**W4**).

**What is *not* in this narrative.** The system internals — the Klefter local commits, the broker hops, the Bruun timers, the cancellations — are Narrative 004's material, not repeated here; this narrative points at them as the *status* the Customer sees settle. And per ADR 016's guardrail, **pure presentation state** — a modal opening, pagination, a theme toggle, the cart-badge tween — is deliberately *not* modeled as events; it lives in frontend code and, where it matters to the journey, in this prose. The line this narrative walks is: a screen *reads* a domain fact (a view slice) or *produces* one (a command slice); anything that does neither is not on a stream.

## Moment 1 — Landing on the storefront

**Context.** The Customer opens CritterMart for the first time. The SPA is served locally through the Aspire AppHost (a managed resource in the dashboard alongside the three services, RabbitMQ, and PostgreSQL; ADR 015). The Seller has been at work — the catalog holds the *Cosmic Critter Plush* (`crit-001`, `$24.99`) and the *Nebula Newt* (`crit-002`, `$18.00`), each published through slice 1.1.

**Interaction.** The Customer arrives at the product listing — wireframe **W1**. They do nothing yet but look.

**System response.** The SPA reads the catalog **directly from the Catalog service** — `GET /products` over HTTP, no aggregating gateway in front of it — and **Zod-parses the response** before rendering anything from it (ADR 015 R3: the first of three contract surfaces the SPA validates). The listing shows both products, each with name, current price, and description — the full `ProductCatalogView`. Product *detail* is rendered from that same list payload; there is no separate `GET /products/{sku}` (Gap #2 from the endpoint audit — low, deferred), so a click for "more" expands what the SPA already holds rather than fetching a single product. The stubbed customer id rides along with the request but gates nothing: the catalog is public, and the Customer sees it without signing in. Whether anything is actually *in stock* is not shown here — stock lives in Inventory, which the storefront does not consult to render a listing (Narrative 002); a shortfall only ever surfaces later, as a cancelled order.

## Moment 2 — Adding to the cart, instantly

**Context.** The Customer has settled on the *Cosmic Critter Plush*. They have no cart yet — this is their first step toward a purchase, taken from the listing (**W1**).

**Interaction.** They tap **`[ Add to cart ]`** on the plush. The cart badge in the header (`Cart (0)`) ticks to `Cart (1)` the instant they tap — before the server has answered.

**System response.** When it rendered the listing the SPA already held the product's fields, so it **snapshots** name and price into the command — `AddToCart { customerId, sku: "crit-001", quantity: 1, productSnapshot: { name, price: 24.99 } }` — and sends it to Orders (this is the presentation-layer composition the context map names: product data reaches the Cart only through the frontend's snapshot, never a Catalog↔Orders integration). The instant badge bump is **optimistic UI** (ADR 015 R4): `onMutate` applies the guess locally; if the command fails, `onError` rolls the badge back; either way `onSettled` refetches the authoritative `CartView`, so the displayed cart converges on what the server actually recorded — the read model, never the optimistic guess, is the truth. Behind that snappy badge, the behavior is Narrative 004's Moment 1: Orders creates the Cart stream (`CartCreated` + `CartItemAdded`). If the Customer adds the *Nebula Newt* too, the badge reaches `Cart (2)` and the same optimistic-then-reconcile cycle runs again.

## Moment 3 — Returning to the cart, and editing it

**Context.** The Customer comes back to look at what they've gathered — perhaps in the same session, perhaps on a **cold load**: a fresh browser session the next day, holding nothing but the stubbed customer id. They open the cart — wireframe **W2**.

**Interaction.** They navigate to the cart-review screen.

**System response — and the gap that makes it possible.** Rendering this screen needs the cart's contents, and here the storefront hits the mismatch the [pre-frontend endpoint audit](../research/pre-frontend-endpoint-audit.md) flagged as **Gap #1, the one blocking gap**. Every cart *command* is **customer-keyed** — the Customer never sends a cart id; "which cart is theirs" is the server's business (Narrative 004, Moment 1A). But the only round-one cart *read* is `GET /carts/{cartId}`, and on a cold load the SPA has **no cart id** — it learned one only from an earlier `AddToCart` response that this fresh session never saw. Without a customer-keyed read, the cart-review screen simply cannot render.

**Slice 3.5 — "View my open cart"** closes that. The SPA asks for *its* cart by identity — `GET /carts/mine`, the customer carried behind the `useCurrentCustomer` seam (ADR 009) — and Orders resolves the Customer's single open cart through the partial-unique open-cart index that already exists (`Orders/Program.cs:74`), returning that one `CartView` (Zod-parsed, like every wire response). It adds **no new event** and exposes **no new projection** — it is the read counterpart to the customer-keyed write side every cart command already uses, the missing mirror. If there is **no open cart** — the Customer never started one, or their last cart was checked out or abandoned — the read resolves to "no open cart" (not an error), and the screen renders empty, ready for the next add.

With the cart on screen, the two edits are in reach. **`[-]` / `[+]`** issue `ChangeCartItemQuantity` (slice 3.3); **`[x]`** issues `RemoveCartItem` (slice 3.2) — each optimistic in exactly the way Moment 2's add was: the line updates immediately, then reconciles against the refetched `CartView`. Changing a quantity never re-prices — the snapshot price from Moment 2 stays authoritative (Narrative 004, Moment 1A). A SKU appears as exactly one line (adding it again merges, quantities summed), which is what makes "set `crit-001` to 2" and "remove `crit-001`" unambiguous on screen. And if the Customer empties every line, the cart does not vanish — it stays their open cart, just empty; the only thing an empty cart cannot do is check out (`PlaceOrder` is refused with `CartEmpty`).

## Moment 4 — Placing the order

**Context.** The Customer is on the cart-review screen (**W2**) with two lines — the plush (`crit-001`, quantity 2 at `$24.99`) and the newt (`crit-002`, quantity 3 at `$18.00`) — and decides to buy.

**Interaction.** They tap **`[ Place Order ]`**.

**System response.** The SPA sends `PlaceOrder { customerId }` — and *nothing else*: the order's contents are not re-sent from the browser. What the Customer is buying is whatever their open cart already holds, server-side (the same open-cart resolution slice 3.5 reads, now driving a write). Then the screen transitions to the confirmation — wireframe **W3** — and here the optimism that carried Moments 2 and 3 deliberately **stops**. A cart edit could be faked locally because its outcome was knowable; placing an order cannot, because it sets off the cross-bounded-context process — reserve stock in Inventory over the broker, then authorize (stubbed) payment — whose outcome the SPA does not yet know (Narrative 004, Moments 3–4). So **W3** shows an honest `awaiting confirmation` status, the `orderId`, and the total (`$103.98`) — not a faked "confirmed." The cart badge resets to `Cart (0)`: the checked-out cart is no longer the Customer's open cart, and their next add starts a fresh one.

This `POST /orders` is also the **front door of the trace beat**: it is the real HTTP hop from the SPA into Orders that the OpenTelemetry trace follows before it fans out across RabbitMQ to Inventory and back (ADR 015 § Consequences; the cross-network span chain is a hard success criterion, not a nice-to-have). The Customer sees a status; the talk's audience sees the span light up in the Aspire dashboard.

## Moment 5 — Tracking the order

**Context.** The order is placed and sits at `awaiting_confirmation` (**W3**). From here the Customer takes no further action — they watch the status settle on the order-status screen, wireframe **W4**.

**Interaction.** They tap **`[ Track this order ]`** (or return to it later). The screen reads the order and shows where it is.

**System response.** The SPA reads `OrderStatusView` — `GET /orders/{orderId}`, which exists today (the audit's ✅ row; single-order tracking needs no new slice) — Zod-parsed like every response. The status walks the path the backend drives behind the scenes: `awaiting_confirmation → stock_reserved → confirmed`, or it settles on `cancelled` for one of three reasons — `stock_unavailable`, `payment_declined`, or `payment_timeout` (the three failure routes of Narrative 004, Moments 3 / 5 / 6). Because there is **no live push** round one (no SignalR — ADR 015, explicitly unlike the CritterBids sibling), the screen does not update itself the instant the server changes; it converges by **TanStack Query refetch** — a poll or a manual refresh — so the optimistic-UI rule from the cart ("re-query, don't render a guess") applies here too, just to a read instead of a mutation. "Confirmed" is as far as the order travels: CritterMart models no picking, packing, or shipping, so the terminal of a successful purchase is a *confirmed order*, not a delivered parcel (Narrative 004). A Customer who wants to see *all* their orders at once finds no such screen yet — the "My Orders" list (`GET /orders?customerId=`) is **Gap #3**, named and deferred; round two tracks one order at a time.

## What the Customer does *not* yet see (on screen)

These absences are deliberate, and naming them keeps the screen journey honest:

- **No login screen.** Identity is the round-one stub behind the `useCurrentCustomer` seam (ADR 009); the Customer is "signed in" as a hardcoded id from the first render. A real Identity service is a long-road item.
- **No live stock on the listing (W1).** The storefront does not consult Inventory to render the catalog; a shortfall surfaces only later, as a cancelled order (Narrative 002, Narrative 004).
- **No deep-linkable product page (W1).** Detail renders from the list payload — there is no `GET /products/{sku}` (Gap #2) — and the SPA has no SEO/prerender story (ADR 015 R5, explicitly *unlike* MmoReconnect): an authenticated demo storefront has neither crawler-visibility nor launch-wave-resilience as a driver.
- **No real-time status (W4).** No socket; status converges by refetch. The "instant" feel lives only where it can honestly be faked — cart mutations (W2) — never on the order's cross-BC outcome.
- **No "My Orders" history (W4).** Gap #3, deferred; single-order tracking is the round-two surface.
- **No modeled presentation state anywhere.** Modals, pagination, the cart-badge animation, route transitions — none of it reaches an event stream (ADR 016 guardrail). It is real, and it lives in the frontend code, but it is not domain behavior.

## Forthcoming

This narrative is **partly built** — its keystone screen now renders. The backend reads are all shipped (slice **3.5 `GET /carts/mine`** closed the blocking Gap #1), the **frontend-bootstrap PR** stood up the `client/` Vite SPA + Aspire `AddViteApp` wiring + CORS-origin injection, and the **W2 cart-review READ screen** (Moment 3's cold-load render) now exists: `client/src/cart/` binds `GET /carts/mine` through the first `CartViewSchema` (Zod-at-boundary) and `cartQueryOptions` (`404` → empty cart, not an error), with the live `Cart (N)` badge in the header. What remains is screen-pending, each its own modeled slice:

- **Moment 1 (W1 Browse)** — slices 1.2 / 3.1: the `ProductCatalogView` listing and add-to-cart.
- **Moment 3 edits (W2)** — slices 3.2 remove / 3.3 change-qty: the `[-]/[+]/[x]` controls and Convention 3's optimistic-UI, deferred from this read-only W2 screen.
- **Moment 4 (W2 → W3 Checkout)** — slice 4.1 place-order, the OpenTelemetry trace front-door.
- **Moment 5 (W4 Track)** — the `OrderStatusView` single-order status read.

As each screen is built, this narrative grows — each bumping its version and appending to the history below. Nothing in this journey is half-finished silently: every screen named here maps to a modeled slice and a wireframe; the build order is the audit's (Gap #1 first), now W2's read among the screens.

## Document History

| Version | Date       | Notes                                                                                                                                                                                                                                          |
| ------- | ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| v1.0    | 2026-06-14 | Initial commit — the **frontend-mode entry** narrative (ADR 016), the *screen lens* companion to behavior narratives 002 (browse) and 004 (purchase). Threads the storefront journey browse → cart → checkout → track across five Moments tied to workshop § 5.1 wireframes **W1**–**W4**: Land on the storefront (slice 1.2 / W1), Add to cart optimistically (3.1 / W1→badge), Return & edit the cart (3.5 net-new + 3.2/3.3 / W2), Place the order (4.1 / W2→W3), Track the order (`OrderStatusView` read / W4). Foregrounds the keystone — **slice 3.5 "View my open cart"** — as the cold-load read that closes the audit's blocking **Gap #1** (write side customer-keyed, read side `cartId`-keyed). Threads the locked stack stances as context (Zod-at-boundary R3, optimistic-UI + rollback R4, no-push/no-SEO R5, three-services-direct/no-BFF, stubbed-identity seam) without re-deciding them. `slices` frontmatter `[1.2, 3.1, 3.2, 3.3, 3.5, 4.1]`. **Modeled, not yet built**: OpenSpec proposal + code for slice 3.5 (and the deferred `docs/skills/frontend/`) are the next session. |
| v1.1    | 2026-06-15 | **Slice 3.5 server half built** (the first frontend implementation slice). `GET /carts/mine` ships as a customer-keyed read of the existing `CartView` over the partial-unique open-cart index — identity carried in the **`X-Customer-Id` header** behind the `useCurrentCustomer` seam (the resolved transport; query-param vs. header fork closed to header), `404` = "no open cart", `400` = no identity. No new event. The **`docs/skills/frontend/SKILL.md`** v1 seed landed alongside it. Moment 3's keystone read is now real on the server; the **W2 cart-review screen** that consumes it — and the `client/` Vite app / Aspire `AddViteApp` wiring / CORS-origin injection / dependabot npm block — are **deferred to a dedicated frontend-bootstrap PR**. "Forthcoming" updated from *modeled, not yet built* → *partly built (keystone read shipped, screen pending)*. OpenSpec change `slice-3-5-view-open-cart`; retro `docs/retrospectives/implementations/015-slice-3-5-view-open-cart.md`. |
| v1.2    | 2026-06-16 | **W2 cart-review READ screen built** — the first storefront screen. `client/src/cart/` renders the customer's open cart on a cold load by binding slice 3.5's `GET /carts/mine`: `CartViewSchema` (the first per-read-model Zod schema; camelCase wire, default `.strip()`), `cartQueryOptions` + `fetchMyCart` (the first `queryOptions` factory; `NotFoundError`/`404` → `null` empty-cart, **not** an error), `CartPage` (line rows · client-derived **Total** summed in integer cents to avoid binary-float drift · loading/empty/error states), and the live `Cart (N)` header badge (`CartBadge`, a `select`-derived distinct-line count sharing the cart query key). **Read-only**: the W2 `[-]/[+]/[x]` edits (3.2/3.3) and `[ Place Order ]` checkout (4.1 → W3) stay screen-pending as their own command slices; `react-hook-form` + Convention 3 optimistic-UI remain unconsumed until then. **No OpenSpec/workshop change** — a screen-only bind of the existing read contract (ADR 016). Discharged retro 016's deferred **browser-level CORS + OpenTelemetry-trace** verification (SPA → Orders). Frontend skill converged v2 → v3. Prompt/retro `docs/{prompts,retrospectives}/implementations/018-slice-w2-cart-review.md`. |
