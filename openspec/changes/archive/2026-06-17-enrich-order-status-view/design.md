# Design: Enrich OrderStatusView — placedAt + cancellation reason

## Context

W4 order-tracking (PR #64) shipped against the `OrderStatusView` shape `{ id, customerId, status, lines, total }` and logged two gaps: the view carries no placement timestamp and no cancellation reason, so the W4 wireframe's `Placed …` line and per-reason copy were rendered as an honest generic "Cancelled" with no timestamp. The workshop § 5.1 v1.10 amendment and Narrative 005 (v1.6) both fence the fix to a future "enrich OrderStatusView" slice — this one. It is a pure read-model enrichment over the order's existing events: no new event, command, projection, or index.

This is the first round-two slice after the ADR 020 rollout, and it is a clean demonstration of why that split exists: the **read** view surfaces a placement timestamp that the **write** aggregate deliberately does not carry (`Order.cs:23` — "the order tracks no activity timestamp, so no `IEvent<T>` wrapper is needed").

## Goals / Non-Goals

**Goals:**
- W4 binds a real placed-at timestamp and a per-reason cancellation message, replacing the hardcoded sketch / generic copy.
- The existing `OrderStatusView` wire shape is preserved as a superset — W3 and the existing W4 read keep working unchanged.
- No event-contract change: both new fields are sourced from data the streams already hold.

**Non-Goals:**
- No new event, command, projection, or index (this is a view-enrichment slice).
- No change to the order lifecycle, the PMvH handlers, or any cross-BC message.
- No "My Orders" list (Gap #3) or product-detail read (Gap #2) — separate slices.
- No live-push of status (ADR 015 — W4 converges by TanStack Query poll, unchanged).

## Decisions

### Decision 1 — `placedAt` from event metadata, not a new `OrderPlaced` field (settled by convention, not forked)

`OrderStatusView.Create` takes `IEvent<OrderPlaced> e` and reads `e.Timestamp` for `placedAt`; the `OrderPlaced` event payload is unchanged.

**Why, and why this was not a user fork:** the codebase already has a named convention for exactly this — "Marten's using-metadata convention," surfacing an event's append timestamp via the `IEvent<T>` wrapper. `CartView.cs:23` does the analog (`Create(IEvent<CartCreated> e) => new(…, LastActivityAt: e.Timestamp)`); `Cart`, `CartsAwaitingActivity`, `OrdersAwaitingPayment`, and `CartAbandonmentReport` all use it. The placement time **is** the `OrderPlaced` event's occurrence time (the event is appended in the same transaction as the `PlaceOrder` command), so the append timestamp is the honest source. Adding a `PlacedAt` field to the event payload would duplicate what the store already records and invite drift between the field and the append time. With a strong, named local convention and no semantic gap, this is a settled choice, not a genuine fork — so it was decided by precedent rather than put to the owner.

**Alternative rejected:** a `DateTimeOffset PlacedAt` field on `OrderPlaced`, set by the `PlaceOrder` handler. Redundant with metadata; a contract change for no gain.

### Decision 2 — Superset wire shape, additive only

The view keeps `{ id, customerId, status, lines, total }` exactly and *adds* `placedAt` and `cancelReason`. No field is renamed, repurposed, or removed.

**Why:** W3 (`OrderStatusViewSchema`, PR #62) and W4 (PR #64) already bind the existing fields; an additive superset means their existing reads keep deserializing unchanged, and the frontend opts in to the new fields. The new fields are added explicitly to the zod schema, so the screens gain them deliberately rather than by accident.

### Decision 3 — `cancelReason` is the raw reason string, nullable; the frontend maps copy

The view exposes `cancelReason` as the nullable reason string the event already carries (`stock_unavailable` / `payment_declined` / `payment_timeout`), null until cancellation. The frontend maps reason → display copy, exactly as it already maps the status enum → label.

**Why:** the reason is already a closed vocabulary in `CancelReason`; surfacing the raw token keeps the view a faithful projection of the event and puts presentation (the human sentence) on the presentation tier, consistent with how status is handled. A nullable field (rather than an empty string) keeps "never cancelled" distinct from "cancelled with an empty reason."

### Decision 4 — `Apply(OrderCancelled …)` folds the reason; the other folds are unchanged

Only the `OrderCancelled` fold changes (it now sets `CancelReason = e.Reason` alongside `Status = cancelled`); `Create(OrderPlaced)` gains the timestamp. `StockReserved`, `PaymentAuthorized`, and `OrderConfirmed` folds are untouched, and `with`-expressions carry `placedAt` / `cancelReason` forward unchanged.

**Why:** minimal surface. The record's `with` semantics already carry untouched fields forward, so the intermediate folds need no edit.

## Faithfulness notes (workshop divergences, for the post-merge amendment)

1. **The v1.10 "aspirational" fencing is now satisfied.** Workshop § 5.1 v1.10 fenced `placedAt` + per-reason copy to "a future enrich-OrderStatusView slice"; this change ships it. The post-merge amendment updates that note to record the fields as bound/shipped.
2. **`placedAt` is the append timestamp, surfaced via metadata.** The workshop said "surface a `placedAt` from `OrderPlaced`"; realized as `IEvent<OrderPlaced>.Timestamp` (Decision 1), not a new event field — recorded so the amendment is precise about the source.

## Risks / Trade-offs

- **[`placedAt` = append time, not a separate domain "order time"]** For an order, the two coincide (the event is appended in the placement transaction), so there is no gap. Named so it is not mistaken for a distinct domain timestamp; were a distinct business time ever needed (e.g., backdated orders), it would become an explicit event field at that point.
- **[Existing dev streams predate the field]** Aspire Postgres is ephemeral (fresh DB each boot), and the integration suite seeds its own streams, so there is no migration concern in round one. A persisted store would simply replay the inline projection; `placedAt` comes from metadata that already exists on every `OrderPlaced`.

## Open Questions

*(none — the placedAt-source choice was settled by the established CartView/metadata convention before authoring; the W4 binding is in-scope for this slice, not deferred)*
