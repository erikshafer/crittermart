# Design: Slice 3.5 — View My Open Cart

## Context

This is the first frontend *implementation* slice. The frontend-mode entry (PR #49) modeled the storefront screens; the pre-frontend endpoint audit found one **blocking** read-model gap (Gap #1): the cart's write side is customer-keyed but its only read is `cartId`-keyed, so a cold-loaded SPA cannot render the cart-review screen. Workshop 001 slice 3.5 models the fix as a pure **view/query slice** — a customer-keyed read of the existing `CartView`, no new event.

The resolution this endpoint performs already exists in the codebase: `AddToCart.cs:31-33` resolves the open cart by `customerId` before every add. Slice 3.5 lifts that same query into a read endpoint. The single genuine fork — how customer identity arrives on the request — was presented to the user with previews and resolved before this change was authored (Decision 1).

## Goals / Non-Goals

**Goals:**

- A cold-loaded storefront, holding only the stubbed customer id, can fetch the Customer's open cart with no `cartId`.
- The read reuses the existing open-cart resolution and the existing partial-unique index — no new event, projection, or index.
- The identity transport models the eventual Polecat promotion as a localized swap (header → claim), not a call-site sweep.
- "No open cart" (`404`) stays semantically distinct from "no identity supplied" (`400`).

**Non-Goals:**

- No `client/` scaffolding, no Aspire `AddViteApp` wiring, no W2 screen code — deferred to a dedicated frontend-bootstrap PR (which also resolves the owed Aspire-integration decision and the CORS-origin injection).
- No new event, command, projection, or index (this is a query slice).
- No order-history list (Gap #3) or product-detail read (Gap #2) — separate, non-blocking slices.
- No real identity (ADR 009 stands — identity stays stubbed behind the seam).

## Decisions

### Decision 1 — Customer identity via the `X-Customer-Id` header (user fork)

`GET /carts/mine` binds the Customer's identity from an `X-Customer-Id` request header (`[FromHeader(Name = "X-Customer-Id")]`), not from a query param or a route segment. The route is the literal `/carts/mine`; identity rides ambiently in the header.

**Why over a `?customerId=` query param:** the route already says "mine," so restating the id as a visible query param is redundant, and it lands the customer id in URLs and server logs. The header is the closest round-one stand-in for the authenticated claim Polecat will eventually provide: the frontend `useCurrentCustomer` seam sets `X-Customer-Id` once on the shared HTTP client, and the Polecat promotion swaps that header for a `Bearer` token / claim read with the call sites unchanged. A query param would instead require *removing* `?customerId=` from every caller at promotion time. Both transports are one-liners in Wolverine.Http (verified against current docs: `[FromHeader]` per `headers.md`, query binding per `querystring.md`); the choice is about the promotion path, not the mechanics.

**Alternative rejected:** a route param `/carts/{customerId}` — contradicts the "mine" semantics the workshop (§ 5.1, W2) already locked, and is no more forward-compatible than the query param.

### Decision 2 — A query, not `[ReadAggregate]`

The endpoint resolves the cart with `session.Query<CartView>().Where(v => v.CustomerId == id && v.IsOpen).FirstOrDefaultAsync()` — a LINQ query over the projection — not Wolverine's `[ReadAggregate]`/`[Aggregate]` attributes.

**Why:** `[ReadAggregate]` loads an aggregate by its *stream id* (the `cartId`). Slice 3.5 exists **because** the caller has no `cartId` — it resolves by `customerId` instead. The customer-keyed query is the only shape that fits, and it is the exact query `AddToCart` already runs. The partial-unique open-cart index (`Program.cs:74`) makes `FirstOrDefaultAsync` correct: at most one open cart per customer, so "first" is "the one."

### Decision 3 — `IResult` return, matching the sibling cart-read endpoints

The endpoint returns `Task<IResult>` (`Results.Ok` / `Results.NotFound` / `Results.BadRequest`), matching the existing `CartEndpoint.Get` (`GET /carts/{cartId}`) and `GetAwaitingActivity` (`GET /carts/awaiting-activity`) in `AddToCart.cs:62-84`.

**Why over the skill's "prefer concrete return types" guidance:** the surrounding cart-read endpoints all use `IResult` with `Results.*` — this is a read with genuine runtime branching (200 vs 404 vs 400), the case the skill itself flags as legitimate `IResult` use. Matching the established local idiom wins over importing a different style for one endpoint.

### Decision 4 — A new feature file, no edit to `AddToCart.cs`

The endpoint lands in a new `Features/ViewMyCart.cs`, not appended to the existing `CartEndpoint` class in `AddToCart.cs`.

**Why:** keeps slice 3.5's code in its own file (one slice, one feature file) and honors the no-opportunistic-edits discipline — `AddToCart.cs` (slice 3.1/3.4) is left untouched. Route precedence is unaffected: ASP.NET Core matches the literal `/carts/mine` ahead of `/carts/{cartId}`, the same precedence that already lets `/carts/awaiting-activity` win.

### Decision 5 — Missing identity is `400`, not `404`

A missing or blank `X-Customer-Id` yields `400`; a present identity with no open cart yields `404`. Wolverine binds an absent header to the parameter's default (`null`), so the endpoint guards `string.IsNullOrWhiteSpace(customerId)` up front.

**Why:** conflating the two would muddy the workshop's no-open-cart edge — the storefront treats `404` as "render an empty cart," but a `400` means the request itself is malformed (no identity). The seam always supplies an identity in the real app, so `400` should never fire in practice; it is a correctness guard, not a user-facing path. This guard is an addition beyond the workshop's two GWTs (happy + no-open-cart) and is recorded as a faithfulness note for the post-merge amendment.

## Faithfulness notes (workshop divergences, for the post-merge amendment)

1. **Identity transport resolved to the header.** Workshop § 6 slice 3.5 left "query-param vs. header" as "the slice's OpenSpec/implementation call." Resolved: `X-Customer-Id` header (Decision 1).
2. **A third GWT was added** — missing/blank identity → `400` (Decision 5). The workshop modeled only the happy path and the no-open-cart edge; the guard is needed because header binding defaults a missing header to null.

## Risks / Trade-offs

- **[Header is unauthenticated in round one]** Anyone can pass any `X-Customer-Id` and read that customer's cart. → Acceptable and expected: ADR 009 stubs identity for round one; the header *is* the seam where real auth lands. Named so it is not mistaken for a security regression.
- **[`404` for an empty domain state]** Returning `404` for "no open cart" overloads the status code (the route exists; the resource does not). → Acceptable: it matches the workshop GWT, matches the sibling `GET /carts/{cartId}` `404`, and the storefront treats it as "empty cart," not an error. An empty-`200` alternative was considered and rejected for consistency with the existing cart read.

## Open Questions

*(none — the identity-transport fork was resolved with the user before authoring; client/ scaffolding is explicitly deferred, not open)*
