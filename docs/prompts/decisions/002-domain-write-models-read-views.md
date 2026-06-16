# Prompt: Decisions 002 — Domain write-models vs. `*View` read models (ADR 020 + Cart pilot)

**Kind**: decision (ADR) + the refactor that pilots it. The first CritterMart ADR shipped with a code change (mirrors MMO Reconnect's ADR-005 prompt/retro shape).
**Source**: a mid-session course-correction (2026-06-16). A cross-repo naming review (MMO Reconnect ADR 005) surfaced that CritterMart's event-sourced "aggregates" are named `*View` **and served raw to the API** — conflating the write aggregate with the read model. Erik's correction: aggregates are domain-owned, immutable, `sealed` write models named for the domain (`Cart`, not `CartView`); a public read uses a **separate** `*View` model; the raw aggregate is never serialized.
**Files touched**: this prompt; `docs/decisions/020-domain-write-models-read-views.md` (new ADR); `docs/decisions/README.md` (index row + next number); `docs/rules/structural-constraints.md` (naming/separation rule, v1.4 + history); `src/CritterMart.Orders/Cart/{CartLine,Cart,CartView}.cs` (split aggregate ↔ read model); `src/CritterMart.Orders/Program.cs` (projection registration + index placement); the five cart write paths (`Features/{AddToCart,RemoveCartItem,ChangeCartItemQuantity,PlaceOrder}.cs`, `Cart/CartAbandonmentHandler.cs`); `tests/CritterMart.Orders.Tests/CartProjectionTests.cs` (reworked from `CartViewProjectionTests.cs`); `docs/{prompts,retrospectives}/README.md` (decisions counts); `docs/retrospectives/decisions/002-domain-write-models-read-views.md`.
**Mode**: solo; three forks resolved with Erik before any code (stance → codify-and-separate; sequencing → ADR + Cart pilot; read mechanism → dedicated read projection).
**Commit subject**: `refactor: split Cart write aggregate from CartView read model (ADR 020)`

## Framing

CritterMart's three event-sourced aggregates — `CartView`, `OrderStatusView`, `StockLevelView` — are each a mutable `class` that simultaneously plays three roles: the `FetchForWriting`/`StartStream` **write aggregate**, the inline **snapshot**, and the **served read model** (`GET /carts/mine` returns the raw `CartView`). `OrderStatusView` is even the PMvH process-manager state. This conflates write and read concerns and exposes the domain model over the wire. The correction is a read/write separation: a domain-named immutable write aggregate (`Cart`) and a separate, purpose-built read model (`CartView`) that the API binds. Cart is the **pilot**; Order and Stock follow in their own PRs.

## Goal

ADR 020 records the stance; the Cart slice pilots it. `Cart` becomes a `sealed record` write aggregate (immutable, `with`-based `Apply`, self-aggregating `Snapshot<Cart>(SnapshotLifecycle.Inline)`), carrying the open-cart unique invariant. `CartView` becomes a **dedicated read projection** (its own inline `Snapshot<CartView>`), the only type the HTTP surface serves — its wire shape preserved so the W2 frontend (`CartViewSchema`, PR #58) is untouched. The five cart write paths `Query`/`FetchForWriting`/`StartStream` **`Cart`**; the read endpoints keep querying `CartView`. `dotnet build` + the full Orders test suite green (Testcontainers).

## Spec delta

**New ADR 020** (Accepted) — "domain write-models; `*View` read models are separate; never serve the raw aggregate." `docs/decisions/README.md` gains its row and bumps next-number. `docs/rules/structural-constraints.md` gains the naming/separation rule (v1.4). No workshop/narrative change — this restructures *how* the model is expressed, not the modeled behavior; the wire contract is preserved. ADR 020 **refines ADR 008** (the inline snapshot is now the *aggregate's*; the read model is a separate inline projection) without reversing it.

## Locked decisions (forks resolved with Erik, 2026-06-16)

1. **Codify + separate** (not "keep `*View`"). Aggregates are domain-named, `sealed`, immutable write models; a public read is a separate `*View` model; the raw aggregate is never serialized.
2. **ADR + Cart pilot in one PR** (blueprint architecture). Order + Stock are follow-up PRs reusing the template.
3. **Dedicated read projection** (not aggregate+boundary-mapping). `CartView` is its own inline `SingleStreamProjection`/`Snapshot`, fully decoupled — the read path never touches the `Cart` aggregate.

## Orientation

1. **MMO Reconnect ADR 005** (`C:\Code\mmo-reconnect\docs\decisions\005-naming-and-feature-folders.md`) — the sibling's domain-revealing-names decision this aligns with (and extends with read/write separation).
2. **`marten-projections-single-stream` skill** — the immutable self-aggregating record pattern (static `Create`/`Apply` returning `with`; `Snapshot<T>(SnapshotLifecycle.Inline)`).
3. **`src/CritterMart.Orders/Cart/CartView.cs`** (the current conflated type) + **`Program.cs`** (projection registration + the open-cart index) + the five write paths + `tests/.../CartViewProjectionTests.cs` (the fold tests to rework).
4. **ADR 008** (`docs/decisions/008-inline-projections-async-teaser-no-daemon.md`) — the inline-snapshot stance this refines.

## Working pattern

ctx7/skill-verify the immutable projection API → `CartLine.cs` (shared value object + fold helper) → `Cart.cs` (aggregate) → `CartView.cs` (read model) → `Program.cs` (register both, move the unique index to `Cart`) → the five write paths (`CartView`→`Cart`) → rework the fold tests → `dotnet build` + `dotnet test` green → ADR 020 + structural-constraints + README index/counts → retro. One PR.

## Out of scope

- **Order and Stock aggregates** — `OrderStatusView`/`StockLevelView` stay as-is this PR (their own follow-up PRs reuse the Cart template). `PlaceOrder`'s `StartStream<OrderStatusView>` is left untouched.
- **Frontend changes** — the `CartView` wire shape is preserved, so `CartViewSchema` and the W2 screen are unchanged.
- **Workshop/narrative amendments** — behavior + wire contract unchanged; no modeled-spec movement.
- **Namespace/folder renames** — `Cart/` stays; if the `Cart` type ↔ `...Cart` namespace collision bites, fully-qualify rather than rename.
- **Other suffix conventions** (`*Endpoint`, `*Handler`) — ADR 020 covers aggregates vs read models; broader suffix policy is not relitigated here.
