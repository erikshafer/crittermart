# Design: Slice 3.4 — Cart Abandonment

## Context

Slice 4.7 shipped the project's first Bruun temporal automation: `PlaceOrder` schedules an `OrderPaymentTimeout` self-message, a handler fires it against the Order stream's state, and an inline todo-list projection makes the automation observable. Slice 3.4 mirrors that machinery onto the Cart aggregate — with three genuine differences: the deadline **moves** with cart activity (orders got one fixed deadline at placement), the slice carries the round-one **async projection teaser** (ADR 008), and there is **no cross-BC hop** (a cart reserves nothing, so abandoning it releases nothing).

The scheduling-policy question (Workshop § 8 open question 1) and two more forks (report shape, daemon scope) were presented to the user with previews and resolved before this change was authored; see prompt `docs/prompts/implementations/013-slice-3-4-cart-abandonment.md` locked decisions 1–4.

## Goals / Non-Goals

**Goals:**

- Every Cart stream reaches a terminal state: `CartCheckedOut` (slice 4.1) or `CartAbandoned` (this slice).
- The abandonment decision is made against the Cart stream's own fold — never against another read model.
- The async projection teaser exists, is registered, and is provably materializable by rebuild — without an async daemon (ADR 008).
- Exactly one scheduled message in flight per open cart.

**Non-Goals:**

- No edit-handler changes (the 3.2/3.3 endpoints stay untouched — see Decision 1).
- No async daemon, no Aspire daemon configuration, no projection-coordinator hosted service.
- No remarketing flow, no notification to the Customer, no frontend consumption of the report.
- No cross-BC messages.

## Decisions

### Decision 1 — Fire-and-check with a single rolling timeout (user fork 1)

One `CartActivityTimeout` is scheduled when `AddToCart` creates the cart. Edits append events and schedule nothing. When the timeout fires, `CartAbandonmentHandler` reads the cart's fold: closed → no-op; last activity newer than the window → cascade a new `CartActivityTimeout` delayed to `lastActivity + window − now`; otherwise → append `CartAbandoned`.

**Why over schedule-per-activity:** ctx7 verification established that **Wolverine has no API to cancel or remove a pending scheduled envelope** — the workshop's "Wolverine supports both [policies]" (§ 8 item 1) is factually wrong, and literal cancel-and-reschedule is unimplementable. The degenerate form (schedule a new message per activity, no-op the stale ones) works but puts one message in flight per cart edit. Fire-and-check keeps exactly one message in flight per cart, matches the workshop's GWT failure path verbatim ("activity intervened → reschedule"), and leaves the 3.2/3.3 edit handlers untouched.

**Alternative rejected:** a Wolverine saga with `TimeoutMessage` (the framework's one true cancellation mechanism, via saga completion) — introduces a second persistence model beside the event-sourced cart and contradicts the cascading-over-PMvH lean.

### Decision 2 — `CartView.LastActivityAt` answers the fire-and-check question

The handler's "did activity intervene?" check reads `CartView.LastActivityAt`, folded from the `IEvent<T>` append timestamps of the four activity events (`CartCreated`, `CartItemAdded`, `CartItemRemoved`, `CartItemQuantityChanged`). This honors 4.7's Decision 3 (the stream — via its fold — is the single source of truth; the todo-list view is observable, never authoritative) and avoids a second raw-events query in the handler.

**Consequence:** the `CartViewProjection` activity Apply methods change signature from `Apply(T, CartView)` to `Apply(IEvent<T>, CartView)` — the codebase's **third** `IEvent<T>` metadata fold (after `OrdersAwaitingPaymentProjection` and now `CartsAwaitingActivityProjection`). Existing fold unit tests are updated to wrap events in `Event<T>`. This is the pattern's third use, triggering the skills-debt decision (prompt locked decision 8: DEBT row).

### Decision 3 — Fat `CartAbandoned` event

`CartAbandoned { Reason, Lines, TotalValue }` — the handler snapshots the folded cart's lines and computed total onto the event at append time. The workshop models only `{ reason }`.

**Why:** the daily-rollup report (Decision 4) is a `MultiStreamProjection` that can only fold what is on the events it consumes; reaching back into each cart's stream would need a custom grouper with database lookups. The handler already holds the folded `CartView`, so the snapshot is free. This is the same "record the decision with the data it was made on" idiom that put `lines` on `OrderPlaced`.

### Decision 4 — Daily rollup via `Identity<IEvent<T>>` date grouping (user fork 2)

`CartAbandonmentReportProjection : MultiStreamProjection<CartAbandonmentDailyReport, string>` with `Identity<IEvent<CartAbandoned>>(e => e.Timestamp.ToString("yyyy-MM-dd"))` — one document per UTC calendar day, folding count, total value, and per-SKU counts.

**Verified (ctx7):** Marten documents this exact pattern ("Multi-Stream Projection using IEvent Metadata" — grouping by the date part of the event timestamp). No custom `IAggregateGrouper` is needed.

**Alternative rejected:** per-abandoned-cart document (single-stream, async) — smaller scope but loses the multi-stream teaching artifact and the "analytics-grade report" the workshop § 7 describes.

### Decision 5 — Rebuild-only, no daemon (user fork 3)

The report projection is registered `ProjectionLifecycle.Async` with **no** `AddAsyncDaemon(...)` anywhere. It is materialized exclusively by an on-demand rebuild: `store.BuildProjectionDaemonAsync()` → `daemon.RebuildProjectionAsync<CartAbandonmentReportProjection>(ct)`.

**Verified (ctx7):** `BuildProjectionDaemonAsync()` works against a store with no hosted daemon service — Marten's own projection-testing documentation demonstrates exactly this (register async, run no daemon, rebuild on demand, assert). The integration test mirrors that documented pattern.

**Why over daemon-on:** ADR 008's title is "No Daemon for Round One"; running one — even not-demo-critical — would need an ADR amendment. Rebuild-only is also the stronger teaching beat: the report is *visibly empty* until the rebuild materializes it from events that were there all along.

### Decision 6 — Configuration mirrors 4.7

`Orders:CartActivityTimeout` (default **2 hours** — workshop § 7's ">2h as abandoned" rebuild story implies it) binds to a `CartActivityDeadline(TimeSpan)` singleton consumed by both the `AddToCart` schedule and the `CartsAwaitingActivityProjection`'s visible deadline — one value, two faces, same as `PaymentDeadline`.

### Decision 7 — Spec delta scope: minimal MODIFIED (user fork 4)

Only *Add an item to the cart* is MODIFIED (it gains the schedule-on-creation behavior). Remove/change-quantity/checkout requirements are untouched: their "open cart" phrasing already absorbs abandoned carts through the `NoOpenCart` rejection — the open-cart abstraction does the work, no contract text changes.

## Faithfulness notes (workshop divergences, for the post-merge amendment)

1. **§ 8 open question 1 contains a factual error**: "Wolverine supports both" — it does not; there is no scheduled-message cancellation API. The post-merge workshop amendment must record the correction alongside the fire-and-check resolution.
2. **The GWT label and the GWT behavior disagree**: § 8 says the slice 3.4 GWTs "assume cancel-and-reschedule," but the GWT failure path ("activity intervened → reschedule *when the timeout fires*") describes fire-and-check. The shipped behavior matches the GWT *behavior*; the label was wrong.
3. **`CartAbandoned` is fatter than modeled** (Decision 3): `{ reason }` → `{ reason, lines, totalValue }`.
4. **The 3.2/3.3 slice-table "refresh `CartActivityTimeout`" writes-to clauses dissolve** rather than land as code: under fire-and-check, the edit events' timestamps *are* the refresh. The deferred-twice debt resolves to "no code needed."
5. **The report's grouping key** (UTC calendar day) and document shape (count + total value + per-SKU counts) are implementation decisions the workshop § 7 sketch leaves open.

## Risks / Trade-offs

- **[Reschedule chains under long-lived carts]** A cart edited every ~window-length stays alive with one reschedule per fire — unbounded only if the customer edits forever. → Acceptable; each fire is one cheap read + one scheduled message, and the chain ends at the first quiet window.
- **[Clock semantics]** Fire-and-check compares the event append timestamp (Postgres server time via Marten) against the handler's wall clock (`DateTimeOffset.UtcNow`). Skew between them shifts the effective window by the skew amount. → Acceptable for a 2-hour window; not acceptable for sub-second windows — tests therefore never rely on real-time waits (mirror 4.7's direct-invocation strategy).
- **[Always-stale report]** Rebuild-only means the report is empty/stale until someone rebuilds. → That is the point (ADR 008's teaching beat), but it must be **named in the talk** so it isn't mistaken for a bug. Flagged for the demo storyboard.
- **[`CartView` fold signature change]** Switching activity Apply methods to `IEvent<T>` wrappers touches existing green tests. → Contained: the wrapper change is mechanical (`new Event<T>(data) { Timestamp = … }`), and the fold logic itself is unchanged.

## Open Questions

*(none — all forks resolved at session start; see prompt 013 locked decisions)*
