# ADR 020: Aggregates Are Domain-Named Immutable Write Models; Read Models Are Separate `*View` Projections

**Status**: Accepted

## Context

CritterMart's three event-sourced aggregates — `CartView`, `OrderStatusView`, `StockLevelView` — were each a mutable `class` that played three roles at once: the `FetchForWriting`/`StartStream` **write aggregate**, the inline **snapshot**, and the **served read model** (`GET /carts/mine` returned the raw `CartView`; `OrderStatusView` is also the PMvH process-manager state). This conflates the write model with the read model and exposes the domain aggregate directly over the wire — the API binds the same object the command side mutates, so the aggregate is neither protected nor free to evolve without a contract change.

The naming made the conflation invisible. A `*View` suffix on a type you `StartStream` and `FetchForWriting` mis-describes a write aggregate as a read view, and the codebase was not even internally consistent (`OrderStatusView` carried the suffix while its sibling read models `OrderAwaitingPayment`, `CartAwaitingActivity`, `CartAbandonmentDailyReport` did not). The drift surfaced during a cross-repo review against the sibling **MMO Reconnect ADR 005** ("domain-revealing names; feature-folder-first"), which CritterMart already follows for commands (no `Command` suffix) and events (no `Event` suffix, Workshop 001 § 4) but had not extended to aggregates.

This is a posture, not a spelling preference: **the aggregate is the protected domain write model; a public read is a separate, purpose-built view.** It governs every event-sourced type the project adds, so it earns an ADR with the terse imperative mirrored into `docs/rules/structural-constraints.md`.

## Decision

Two rules, one posture.

1. **An aggregate is a domain-named, immutable, `sealed record` write model.** Named for the domain concept (`Cart`, not `CartView`) — the type system already says it is an aggregate (`FetchForWriting<Cart>`, `Snapshot<Cart>`), so a technical suffix restates what the compiler proves. It is `sealed` and immutable: state changes return a new instance via `this with { … }` (the fold is a pure function of the stream). It carries the write-side invariants (e.g. the open-cart partial-unique index) and is the `FetchForWriting`/`StartStream` target.

2. **A public read is a separate `*View` read model.** The raw aggregate is **never serialized over HTTP** — it is owned by the domain and protected. When a read surface needs the data, a dedicated read model (`CartView`) is projected from the same events as its own inline projection, decoupled from the aggregate; the read path never touches the write model. The `*View` suffix is correct *here*, where the type genuinely is a view, and is created only when a read actually needs one (a write-only aggregate gets no view).

This **refines ADR 008** (inline snapshot projections) rather than reversing it: the inline snapshot is now the *aggregate's*, and the read model is a separate inline projection. MMO Reconnect ADR 005 reached domain-named aggregates independently — parallel prior art that prompted this review, **not a consistency target**; CritterMart decides on its own terms (and the verb-folder corollary in ADR 021 deliberately diverges from that sibling's noun-folder layout).

### Applied this session — the Cart pilot

Cart is the worked example (blueprint architecture; Order and Stock follow in their own PRs). `CartView` the mutable aggregate-class became two `sealed record`s: **`Cart`** (the write aggregate — self-aggregating `Snapshot<Cart>(SnapshotLifecycle.Inline)`, the open-cart unique index, the `FetchForWriting`/`StartStream` target on the five cart write paths) and **`CartView`** (the dedicated read projection the storefront binds, `Snapshot<CartView>(SnapshotLifecycle.Inline)`). The line-fold semantics are a shared `CartLines` helper so read and write stay consistent. The cart's slice folder moved `Cart/` → **`Shopping/`** under the verb-folder convention ([ADR 021](021-verb-feature-folders.md)), which keeps the aggregate the canonical `Cart`. The `CartView` wire shape is preserved exactly, so the W2 frontend (`CartViewSchema`, PR #58) is untouched. All 99 backend tests stay green.

## Consequences

- **The aggregate is protected and free to evolve.** `Cart` can carry write-only state (the activity clock, future invariants) without leaking it to the wire, and `CartView` can diverge toward what the consumer needs (a computed total, fewer fields) without touching the domain model — they are coupled only by the events they both fold.
- **More machinery per read surface.** Two inline projections per stream instead of one, with near-identical folds where the view mirrors the aggregate (true for the cart). The shared `CartLines` helper keeps the duplication to the `with` wrapping; the decoupling is the point, and the cost is paid only where a public read exists.
- **A type ↔ namespace collision is resolved by the verb-folder convention ([ADR 021](021-verb-feature-folders.md)).** A `Cart` type in a `CritterMart.Orders.Cart` namespace is CS0118-ambiguous in cross-namespace callers. Naming the slice folder for its activity — `Shopping/` for the cart, `Ordering/` for the order — lets the aggregate keep its canonical noun (`Cart`, `Order`): a verb namespace never collides with a noun type, and it avoids both a qualified aggregate name (`ShoppingCart`/`CustomerOrder`) and the redundant `CritterMart.Orders.Orders` a plural folder would produce. `Stock` (`StockLevel` ≠ `…Stock`) needs neither. *(An interim `ShoppingCart` aggregate name earlier in this PR was superseded by the verb folder.)*
- **Rejected: keep `*View` on aggregates** (the originally-proposed "codify and keep"). It ratified the conflation — the real defect was serving the raw aggregate, not the name.
- **Rejected: aggregate + boundary mapping** (map `Cart` → a `CartView` DTO at the read endpoint, one projection). Lighter, but the read path queries the aggregate; the dedicated read projection most fully honors "the read never touches the write model" and is the more instructive ES/CQRS pattern for a teaching reference.
- **Rejected: rename the namespace in this PR.** Out of the pilot's scope; it would mix a read/write split with a namespace sweep across many files and muddy the diff.
- **Revisit** when the Order/Stock pilots land (reconcile `OrderStatusView`/`StockLevelView`), or if the namespace-collision aliases prove noisy enough to warrant the pluralization first.

**Refines** [ADR 008](008-inline-projections-async-teaser-no-daemon.md). **Corollary:** [ADR 021](021-verb-feature-folders.md) (verb feature folders). **Mirrored in** [`docs/rules/structural-constraints.md`](../rules/structural-constraints.md). (MMO Reconnect ADR 005 is parallel prior art on domain-named aggregates, not a consistency target — different projects.)
