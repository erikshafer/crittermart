# ADR 021: Feature/Slice Folders Named for the Activity (Verb); Domain Types Keep Canonical Noun Names

**Status**: Accepted

## Context

CritterMart is organized by vertical slice / feature folder, and the stack (Wolverine + Marten) leans into that — a slice is an *activity* (place an order, add to a cart). ADR 020's read/write split surfaced a concrete C# friction: a domain aggregate named for its noun (`Cart`, `Order`) collides (CS0118) with a folder/namespace named the same singular noun (`CritterMart.Orders.Cart`, `…Order`). The interim fix in this PR was to qualify the aggregate (`ShoppingCart`), but that desyncs the aggregate from the `Cart*` events/commands/view, and the equivalent for Order (`CustomerOrder`) is a forced qualifier (every order is a customer order).

The cleaner resolution is to name the **folder for the activity** — a verb/gerund (`Shopping/`, `Ordering/`) — so the aggregate keeps its **canonical noun** (`Cart`, `Order`): a verb namespace never collides with a noun type. This is a deliberate CritterMart stance, chosen on its own merits (it fits the project's VSA emphasis and keeps the domain vocabulary canonical), **not** a borrowed convention — the sibling MMO Reconnect uses plural-noun folders (`Claims/`), which would here produce the redundant `CritterMart.Orders.Orders`. Different projects, different fit.

## Decision

A feature/slice folder is named for the **activity it owns** — a verb or gerund (`Shopping/`, `Ordering/`); the domain types inside keep their **canonical domain-noun** names (`Cart`, `Order`). The verb namespace carries the disambiguation, so a noun aggregate never needs a technical or qualifying suffix (this is the corollary of ADR 020's "name aggregates for the domain").

This is **per-area judgment, applied where it earns its keep**, not a blanket mechanical rule:

- Where a singular noun folder would collide with its aggregate type, use the activity verb: `Shopping/` → `Cart`, `Ordering/` → `Order`.
- Where there is no collision (`Inventory`'s `Stock/` → `StockLevel`; `Catalog`'s `Products/` → `Product`/`ProductCatalogView`), no change is forced; a maintainer may still adopt a verb for consistency, choosing the natural activity word.

### Applied this session — the Cart pilot

`Cart/` → **`Shopping/`** (namespace `CritterMart.Orders.Shopping`); the aggregate is the canonical **`Cart`** (reverting the interim `ShoppingCart`), fully consistent with `CartCreated` / `CartItemAdded` / `CartView` / `CartLine`. The Order slice will follow with an **`Ordering/`** folder around a canonical `Order` at its pilot. This is folder/namespace **naming** only — full VSA *colocation* (dissolving each service's `Features/` command folder into its slice folder) is a separate, larger step, available later but not required by this convention.

## Consequences

- **Aggregates keep canonical names** in step with their events/commands/read models — no `Cart*` ↔ `ShoppingCart` desync, no forced `CustomerOrder`.
- **It cuts against the .NET folder == noun-namespace default**, so it is documented here (and in `structural-constraints.md`) to not surprise a contributor; verb/activity namespaces are idiomatic in Vertical Slice Architecture.
- **Stored events are unaffected.** Marten's default event type alias is the short type name (`cart_created`), independent of namespace, so renaming `…Cart` → `…Shopping` does not touch existing streams (the integration suite confirms — 99 backend tests green across the rename).
- **Per-area judgment risks mild inconsistency** (some folders verbs, some nouns). Mitigated by "apply where it earns its keep" + this record. The Catalog `Products/` verb, if ever adopted, is an open pick (`Cataloging` / `Listing` / `Publishing`).
- **Rejected: qualify the aggregate** (`ShoppingCart` / `CustomerOrder`) — desyncs from the noun family; `CustomerOrder` is a redundant qualifier. **Rejected: pluralize folders** (`Carts/`) — `Orders/` would collide with the service namespace as `CritterMart.Orders.Orders`. **Rejected: a `using` alias** — leaves a disambiguation wart in every cross-namespace caller. **Supersedes** the interim `ShoppingCart` aggregate name introduced earlier in this same PR.

**Corollary of** [ADR 020](020-domain-write-models-read-views.md) (domain-named aggregates). **Mirrored in** [`docs/rules/structural-constraints.md`](../rules/structural-constraints.md).
