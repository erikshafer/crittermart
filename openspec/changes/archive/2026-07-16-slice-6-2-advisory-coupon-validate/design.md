# Design — slice 6.2 advisory coupon validate

Slice 6.2 is small and reads-only; this note records the one genuine fork and the conventions the implementation follows. The invariant work is done (slices 6.1/6.3/6.4); nothing here changes a write path.

## Decision 1 — endpoint shape: action route + discriminated status (settled with Erik)

Three shapes were weighed at session start (`AskUserQuestion` with previews); Erik chose the first:

- **Chosen — `GET /coupons/{code}/validate` → `{ code, status, discountPercent? }`, client-priced.** A single `200` whose `status` (`valid` / `invalid` / `exhausted`) maps one-to-one onto the three W2 UI states. Reads `CouponView` + `CouponUsageView`; writes nothing. The client prices the dollar discount from the cart total it already computes in integer cents, so the query stays uncoupled from cart money and the `% → $` math lives in exactly one place per surface (the client for the preview; `PlaceOrder.RedeemWithDcbAsync` for the authoritative charge).
- *Rejected — `GET /coupons/{code}` RESTful resource read* (`{ discountPercent, cap, netCount, available }`, 404 for unknown): leaks raw `cap`/`netCount` usage to the storefront and conflates the domain-empty "unknown code" with an HTTP 404 error, forcing the UI to derive availability.
- *Rejected — server-priced `?subtotal=` variant:* couples the advisory query to the cart subtotal and duplicates the pricing math in two places.

**Why `200` for all three states.** Checking a code is not an error — `invalid` and `exhausted` are ordinary advisory answers, not failures. A discriminated `200` keeps the client's boundary parse a single Zod schema and avoids branching on HTTP status for what are domain outcomes (the `GET /carts/mine` 404-is-domain-state precedent is the *exception* that proves the rule; here every answer is a body).

## Decision 2 — advisory stays advisory (no new guard)

The query reads `CouponUsageView` (the inline advisory projection, immediately consistent but still a projection) to answer `exhausted`. It does **not** open a DCB boundary and does **not** gate checkout. A `valid` answer can still lose the race at checkout (`409 CouponExhausted`), and an `exhausted` answer does not stop the client carrying the code — the checkout may still admit it if a slot freed by cancellation. This is the deliberate advisory-vs-authoritative split (Workshop 003 §3): the query is a convenience that softens the surprise, never the authority. No behavior of slice 6.3 changes.

## Decision 3 — client pricing reuses the cart's integer-cent arithmetic

`CartPage` already sums lines in integer cents (`toCents`/`lineSubtotalCents`) so the displayed Total is penny-exact. The discount preview reuses that same `totalCents`: `discountCents = round(totalCents × discountPercent / 100)`, `newTotalCents = totalCents − discountCents`. This keeps the previewed total consistent with the Total the customer already sees, and mirrors the server's `Math.Round(subtotal × pct / 100, 2)` (the charge the checkout will actually apply). The preview is an estimate against the *current* cart; the authoritative discount is priced server-side at checkout against the cart the server resolves.

## Decision 4 — coupon state is UI-held (reload forgets)

The applied code + its validation live in `CartPage` component state, not a persisted cart event. A reload forgets the applied coupon — accepted round-one behavior (Workshop 003 §8 item 11). The code becomes durable only when it rides `PlaceOrder` as `?couponCode=` and the checkout appends the tagged `CouponRedeemed`. A cart-persistent `CouponApplied` event is the deferred alternative if reload-survival ever outweighs cart-aggregate blast radius.
