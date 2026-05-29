# Prompt: Implementations 006 — Slice 3.1 Add to Cart (Orders skeleton + Cart aggregate, one PR)

**Kind**: per-slice implementation edge (3rd edge of slice 3.1's triangle; narrative was PR #20, proposal PR #22), consolidated one PR
**Files touched**: this prompt; `openspec/changes/slice-3-1-add-to-cart/{design.md, tasks.md}` (new — proposal/specs already landed in #22); `src/CritterMart.Orders/**` (new service); `src/CritterMart.AppHost/{Program.cs, CritterMart.AppHost.csproj}` (wire Orders); `CritterMart.slnx` (add Orders + Orders.Tests); `tests/CritterMart.Orders.Tests/**` (new); `docs/retrospectives/implementations/006-slice-3-1-add-to-cart.md` (forthcoming)
**Mode**: solo, consolidated one-PR slice; collaborative on genuine forks (present options + recommendation, user decides — memory `feedback-collaborate-on-decisions`, `feedback-options-with-previews`); ctx7 / ai-skills as needed
**Commit subject(s)**: `feat: slice 3.1 add to cart (Orders skeleton + Cart aggregate)`

## Framing

Slice 3.1 opens the **Orders** bounded context — CritterMart's event-sourced core — with its first aggregate, the **Cart**. It is also the bootstrap PR for the third service (`CritterMart.Orders`, the second event-sourced one), so it follows the **blueprint-architecture exception** (skeleton + first slice in one PR, as slice 2.1 did for Inventory). The proposal (#22) locked the `cartId` stream key (a new stream per cart lifecycle, parallel to Order's `orderId`) and deferred `design.md` + `tasks.md` to this edge. It left **one fork open**: the mechanism for resolving the customer's open cart from a command that carries only `customerId`.

## Goal

`POST /carts/{customerId}/items { sku, quantity, productSnapshot { name, price } }` adds an item: first add (no open cart) starts a new `Cart` stream (`CartCreated` + `CartItemAdded`) keyed by a generated `cartId`; a subsequent add appends a further `CartItemAdded` to the same open cart. An inline `CartView` projects the lines; `GET /carts/{cartId}` reads it. Proven by the project's **first pure-function unit tests** (the line fold — they light up the green-but-empty CI unit job) plus Alba + Testcontainers integration tests for the two GWT scenarios; `openspec validate --strict` passes.

## Spec delta

`design.md` + `tasks.md` added to the `slice-3-1-add-to-cart` change (the proposal/specs already landed in #22). `shopping-cart` is satisfied by code. The Workshop § 6.1 slice-3.1 wording amendment (the `cartId` keying and the `CartActivityTimeout` deferral, both flagged in the proposal's faithfulness note) is recorded here for a follow-up doc edit.

## Open fork (resolve this edge; present options + recommendation, user decides)

How does the handler resolve the customer's open cart before appending? Proposal named the candidates: a `customerId`-queryable `CartView`, or a dedicated index document. Check the JasperFx ai-skills + CritterStackSamples for guidance before recommending. **Decision is recorded in `design.md`.**

## Orientation

1. **`openspec/changes/slice-3-1-add-to-cart/proposal.md` + `specs/shopping-cart/spec.md`** — the contract (2 scenarios; anchors `crit-001` Cosmic Critter Plush 24.99, `crit-002` Nebula Newt 18.00, `customer-X`).
2. **`docs/narratives/004-customer-purchase.md`** — Moment 1 (human sibling).
3. **`docs/workshops/001-crittermart-event-model.md`** §§ 2, 4, 6.1.
4. **Mirror `src/CritterMart.Inventory` + `tests/CritterMart.Inventory.Tests`** — the event-sourced-service skeleton, `FetchForWriting`, inline single-stream projection, `:5102` launchSettings, Alba + Testcontainers `[Trait("Category","Integration")]` pattern. Orders uses `:5103`, `orders` schema, `StreamIdentity.AsString`.
5. **Stack reality**: `Directory.Packages.props` (Wolverine 6.1 / Marten 9.2 / JasperFx 2.2 / .NET 10); `WolverineFx.RuntimeCompilation` needed; `nuget.config` is nuget.org-only (do not re-add the jasperfx feed — ADR 013).

## Out of scope

- **No remove (3.2), change-quantity (3.3), checkout (4.1).** **No stock check/reservation at add time (4.2).**
- **No `CartActivityTimeout` / `CartAbandoned` (Bruun temporal automation)** — deferred to 3.4; this slice schedules nothing.
- **No RabbitMQ** — Orders is `WithReference(postgres)` only this slice. **No Catalog read** — name + price arrive snapshotted on the command.
- **No README update** — the stale-rows refresh is a separate `tidy: docs` concern, kept out of this feat PR (no opportunistic edits).
- **No `openspec archive`** of this change (archive after merge).
