# Retrospective: Implementations 014 — Slice 2.4 Commit Reserved Stock on Order Confirmation

**Prompt**: `docs/prompts/implementations/014-slice-2-4-commit-stock.md`
**Outcome**: shipped — when an order confirms, Orders now cascades `CommitStock { orderId, lines }` to Inventory over RabbitMQ, and Inventory's `CommitStockHandler` converts each SKU's reservation into committed stock. The Stock stream gains its fourth event type (`StockCommitted`), `StockLevelView` gains a `Committed` counter, and the invariant `Available + Reserved + Committed = ΣStockReceived` is assertable. The `PaymentDecisionHandler` return type changed from `Task<Contracts.ReleaseStock?>` to `Task<(Contracts.CommitStock?, Contracts.ReleaseStock?)>` (nullable tuple) — a non-obvious fix after discovering that Wolverine's `object?` return breaks conventional routing over RabbitMQ. OpenSpec change `slice-2-4-commit-stock` `--strict` valid (1 ADDED `stock-management` + 1 MODIFIED `order-lifecycle`); Narrative 004 → v1.8 (Moment 4 amended); Workshop 001 → v1.6 (slice 2.4 + § 8 Q2/Q3 resolved). One consolidated PR.
**Tests**: Orders 63 (unchanged), Inventory 14 (+4: 1 projection fold + 3 integration), CrossBc 3 (+1 commit smoke). Catalog: 7 failures (pre-existing CritterWatch issue, not this slice).

## What shipped

- **The contract.** `CritterMart.Contracts/CommitStock.cs` (`CommitStock`, `CommitStockLine`) — symmetric with `ReleaseStock`/`ReleaseStockLine`, same published-language pattern (ADR 014).
- **The event.** `Stock/StockCommitted.cs` (`StockCommitted(string Sku, string OrderId, int Quantity)`) — the Stock stream's fourth event type, the terminal for the success path.
- **The projection fold.** `StockLevelViewProjection.Apply(StockCommitted)` — `Reserved -= qty`, `Committed += qty`, order removed from `Reservations`. Mirrors `Apply(StockReleased)` exactly in shape.
- **The handler.** `Features/CommitStock.cs` — mirrors `ReleaseStockHandler`: per-SKU `FetchForWriting`, idempotent via `Reservations.Contains(orderId)` guard, one-way (no reply to Orders).
- **The cascade.** `PaymentHandlers.cs` — `PaymentDecisionHandler` approve path returns `(new CommitStock(...), null)`, decline returns `(null, new ReleaseStock(...))`, guard returns `(null, null)`. The nullable tuple gives Wolverine code-gen visibility of both message types.
- **The context map.** Orders→Inventory edge gains `CommitStock` as the third message type (alongside `ReserveStock` and `ReleaseStock`).
- **The workshop amendment.** Workshop 001 → v1.6: slice 2.4 added to § 5 table, GWT scenarios in § 6, § 8 Q2 resolved "no" (no symmetric cancel on stock failure), Q3 resolved "yes" (commit on confirmation).
- **OpenSpec + narrative.** `slice-2-4-commit-stock` change (2 capability deltas); Narrative 004 → v1.8 (Moment 4 gains the Inventory consequence, "no committing" bullet retired).

## What worked

- **The roundtable format.** Before writing any code, a multi-persona roundtable (Staff Architect, Product Owner, Domain Expert, UX Engineer) debated whether `StockCommitted` was the right next step. The unanimous agreement — with each persona bringing distinct concerns (stream completeness, operational clarity, domain accuracy, UI readiness) — validated the decision cleanly and surfaced implementation notes that carried into the design.
- **Mirror-pattern velocity.** The commit path mirrors the release path (slice 2.3) at every layer: contract shape, handler structure, projection fold, idempotency guard, cross-BC test shape. ~80% of the implementation was mechanical transfer from the existing release pattern.
- **The consolidation convention.** Workshop amendment + OpenSpec + implementation + narrative + prompt/retro in one PR. The convention (from `feedback-consolidate-slice-prs`) continues to work well for focused single-slice work.

## What was harder / notable

- **`object?` return type breaks Wolverine conventional routing over RabbitMQ.** This was the session's key technical finding. The initial approach — changing `PaymentDecisionHandler` to return `Task<object?>` — followed Wolverine docs showing `object` as valid for conditional cascading. The cross-BC smoke test failed: `CommitStock` was never routed to Inventory. Root cause: Wolverine's conventional routing provisions outbound exchanges/queues based on types discovered at code-gen time; with `object?`, Wolverine cannot infer at code-gen that `CommitStock` needs an outbound route. The fix — nullable tuple `(CommitStock?, ReleaseStock?)` — gives Wolverine compile-time visibility of both types. Confirmed by observing `Declared Rabbit Mq exchange 'CritterMart.Contracts.CommitStock'` in the startup logs.
- **CS0136 duplicate variable name.** The approve path initially declared `var lines` inside the `if (message.Approved)` block, conflicting with the decline path's `var lines` in the outer scope. Fixed by renaming to `var commitLines`.
- **Catalog tests fail from CritterWatch.** 7 Catalog tests fail with `BrokerInitializationException` because the CritterWatch integration (commit `2b127f4`, pre-existing) added RabbitMQ references to Catalog's `Program.cs` but `CatalogAppFixture` doesn't stub transports. NOT caused by this slice. Flagged but not fixed.

## Methodology refinements

- **When a handler needs to cascade one of N message types conditionally over the broker, use a nullable tuple — not `object?`.** Wolverine's conventional routing requires compile-time type visibility to provision outbound exchanges. This is specific to cross-BC messaging over RabbitMQ; in-process cascading with `object?` may work fine. Candidate for a local Wolverine skill note.
- **The roundtable discussion format — strong personas debating an implementation decision before code — is worth formalizing.** It surfaced domain, architectural, operational, and UX perspectives that a solo decision would have missed. The user expressed interest in a `/roundtable-discussion` skill for reuse.

## Outstanding / next-session inputs

- **Post-merge `openspec archive`** for `slice-2-4-commit-stock`.
- **Catalog test failures** from CritterWatch (commit `2b127f4`) remain pre-existing — `CatalogAppFixture` needs transport stubbing. Not this slice's concern but should be tracked.
- **`tidy: encode` bundle** still overdue (ceremony rule + one-cap-per-aggregate + skills DEBT row 1, user-confirmed for next session).
- **Design-return cadence**: this is the 2nd implementation PR since the last design-return credit. The post-merge tidy or the encode bundle banks the next credit.
- **Vision-level conversation**: with StockCommitted shipped, the "ONE remaining backend gap" identified in the previous session is closed. The next direction (frontend, talk storyboard, round-two modeling) is a vision-level conversation with the user, not a slice session.

## Spec-delta — landed?

**Yes, as named.** The prompt named: `stock-management` 1 ADDED (commit reserved stock on confirmation, 3 scenarios) + `order-lifecycle` 1 MODIFIED (confirm gains CommitStock cascade, 1 scenario); Workshop 001 → v1.6; Narrative 004 → v1.8. All landed: the openspec change validates `--strict`, the workshop carries the slice 2.4 row and resolved § 8 questions, and the narrative's Moment 4 notes the Inventory consequence. One *unnamed* addition in the honest direction: the context map update (Orders→Inventory edge gains `CommitStock`) — a mechanical consequence of the new message type, documented in the proposal's Impact section but not called out in the spec delta. `openspec validate --all --strict`: passing, 1 active change (archived post-merge).
