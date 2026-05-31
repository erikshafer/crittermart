# Retrospective: Implementations 009 — Slice 4.3 Authorize Payment (stubbed) (+ 4.4 confirm)

**Prompt**: `docs/prompts/implementations/009-slice-4-3-authorize-payment.md`
**Outcome**: shipped — the Order aggregate's second gate, in-process. When the Order-stream `StockReserved` lands, Orders cascades an in-process `AuthorizePayment` (amount = order total) to a stubbed `IPaymentProvider`; the decision is recorded as a Klefter local commit (`PaymentAuthorized` + auth code/amount, or `PaymentAuthFailed`); on approval the aggregate confirms itself with `OrderConfirmed` (status `confirmed`, terminal — bundled slice 4.4). Decline records the failure but does not confirm (4.6 deferred). OpenSpec change `slice-4-3-authorize-payment` `--strict` valid; Narrative 004 → v1.3 (Moment 4). One consolidated PR.
**Tests**: full solution green — **37 total, 0 failures**: Orders 23 (7 unit folds + 16 integration), Inventory 6, Catalog 7 (unchanged), CrossBc 1.

## What shipped

- **Payment events** — `Order/PaymentAuthorized.cs` (Klefter, auth code + amount), `Order/PaymentAuthFailed.cs` (Klefter, no status change), `Order/OrderConfirmed.cs` (terminal success).
- **Provider seam** — `Order/PaymentProvider.cs`: `AuthorizePayment` request, transient `PaymentDecision` reply, `IPaymentProvider`, `StubPaymentProvider` (always approves, `stub-{guid}`). Registered in `Program.cs`; swappable for the decline branch.
- **Handlers** — `Order/PaymentHandlers.cs`: `AuthorizePaymentHandler` (calls the provider, cascades the decision — no stream access) and `PaymentDecisionHandler` (stream-state guard → Klefter commit; approve also appends `OrderConfirmed`). `StockReservedHandler` now returns `AuthorizePayment?` to open the payment gate.
- **Projection** — `OrderStatusView` gains `payment_authorized` + `confirmed` statuses and `Apply(PaymentAuthorized)` / `Apply(OrderConfirmed)` folds; `PaymentAuthFailed` has no fold (audit-only, like `StockReservationFailed`).
- **OpenSpec** — `order-lifecycle` (2 ADDED reqs: authorize payment; confirm when both gates close); `design.md` (6 decisions) + `tasks.md`.
- **Narrative 004 → v1.3** — Moment 4.

## What worked

- **The in-process mirror of 4.2 was nearly mechanical.** Same cascading-handler shape, same Klefter translation, same stream-state idempotency guard — only the transport changed (local instead of broker) and the external party (stubbed provider instead of Inventory). Having 4.2's pattern to copy made the slice fast and the code immediately idiomatic.
- **Verifying the routing precedence up front removed the one real risk.** The whole design hinges on `AuthorizePayment`/`PaymentDecision` staying in-process. The `Program.cs` comment claimed it; the Wolverine docs confirmed it (conventional *local* routing takes precedence over broker routing; `ConventionalLocalRoutingIsAdditive()` is the opt-in to do both, deliberately not called). Confirming before writing meant the cascade worked first try — no "message vanished into RabbitMQ" debugging.
- **The swappable-provider stub policy kept the domain clean.** No magic amount or sentinel customer id in `AuthorizePayment`; the decline test just registers a `DecliningPaymentProvider` on a one-off host. The failure branch is exercised honestly without polluting the happy-path payload.
- **Bundling 4.4 delivered the payoff cheaply.** Because payment is always the second gate, `OrderConfirmed` is appended in the same handler as `PaymentAuthorized` — no separate trigger, no event subscription. The happy path reaches `confirmed` and the talk gets its terminal-state beat.

## What was harder / notable

- **Two pre-existing tests pinned a now-obsolete truth.** Slice 4.2's `StockReservationOutcomeTests` grant test and the cross-BC smoke both asserted `stock_reserved` as the terminal state with a fixed event count. Slice 4.3 promotes `stock_reserved` to a *transient* state (the approving stub carries the order to `confirmed`), so both broke. Updated in-bounds (the slice's behavior change directly invalidated them): the grant test now asserts only that the grant is recorded; the smoke asserts the grant landed and lets the order proceed. This is the spec-delta closure loop surfacing as test churn — a temporary truth superseded by the next slice.
- **Three hops vs. two.** A two-hop design (provider call + commit in one handler) was simpler, but the three-hop split (request → provider → translate) keeps the external-call boundary free of any stream lock and matches the Workshop § 3 storyboard's `PaymentDecision` reply exactly. Chosen for faithfulness and for not holding a Marten write lock across a (notionally slow, real) provider call. Documented in design.md decision 3.
- **`payment_authorized` is a fold-through status.** On approval the stream appends `PaymentAuthorized` *and* `OrderConfirmed` in one transaction, so the view settles on `confirmed` and `payment_authorized` is only ever the transient intermediate the fold passes through. The unit test pins the `PaymentAuthorized → payment_authorized` fold in isolation so the status isn't dead code.

## Methodology refinements

- **When a slice changes a state's meaning from terminal to transient, grep the test suite for assertions on that state before writing new code.** Both broken tests were predictable from the design (4.3 makes `stock_reserved` transient); catching them by running the full suite is fine, but anticipating them would have folded the updates into the plan rather than surfacing as failures. Candidate habit: when a slice adds a downstream transition off an existing status, list the tests asserting that status as terminal in the prompt's "files touched."
- **"Verify the library guarantee the design depends on" earns its keep again.** 4.2's lesson was the two-host `ApplicationAssembly` bug; 4.3's is the routing-precedence check. Both were single points of failure for the whole slice, and both were cheap to confirm against docs/tests up front. The pattern: identify the one framework behavior the slice *assumes*, and prove it before building on it.

## Outstanding / next-session inputs

- **Slice 4.6 — cancel on payment decline.** The `PaymentAuthFailed` commit this slice records is its precondition. 4.6 appends `OrderCancelled { reason: "payment_declined" }`, **and** publishes a cross-BC `OrderCancelled` to Inventory to release the reserved stock — which needs **Inventory slice 2.3 (release stock / `StockReleased`)**. That cross-BC release is why 4.6 was not bundled here. It is the natural next failure-branch slice (and the first time a cancellation crosses a boundary back).
- **`openspec archive slice-4-3-authorize-payment`** after merge → folds the `order-lifecycle` delta (2 ADDED reqs) into the durable main spec. A post-merge `tidy:` step.
- **Lingering `product-catalog` spec `## Purpose` TBD** — still the slice-1.1 placeholder (carried since retro 006); fold into the next Catalog-touching or `tidy: docs` session.
- **README / index refresh** (Orders BC-status row → payment/confirm; capability/test counts 32→37) — a separate `tidy: docs` concern, kept out of this PR (no opportunistic edits).
- **CritterWatch (ADR 013)** — still deferred; the tier/feed/license question is unresolved (paid feed 401s on CI). User chose to keep deferring it at this session's opening fork.
- **Design-return cadence**: this is the **3rd implementation PR** against Orders since the #28/#30 design-return (4.1 #29, 4.2 #31, 4.3 here). Per the cadence rule the **next PR should be a design-return** — a `tidy: docs` (archive 4.3 + README + the `product-catalog` Purpose), a new narrative, or the next BC's workshop — before another Orders implementation slice.

## Spec-delta — landed?

**Yes.** `order-lifecycle` (ADDED: authorize payment for a reserved order; confirm an order when both gates close) authored and `--strict` valid; satisfied by code (37 green tests incl. the happy/decline/idempotent tracked-session trio). Narrative 004 records Moment 4 in its Document History (v1.3). Workshop § 6.1 slices 4.3 + 4.4 satisfied as written (happy + provider-declines failure path); no workshop amendment required — the § 5 slice table already lists 4.3/4.4 and the GWT matched the code.
