---
retrospective: 006
kind: implementations
prompt: docs/prompts/implementations/006-slice-3-1-add-to-cart.md
deliverable: openspec/changes/slice-3-1-add-to-cart/{design.md, tasks.md} (new); src/CritterMart.Orders/** (new service — csproj, Program.cs, appsettings, launchSettings :5103, Cart/{ProductSnapshot,CartCreated,CartItemAdded,CartView}.cs, Features/AddToCart.cs); src/CritterMart.AppHost/{Program.cs, CritterMart.AppHost.csproj} (wire Orders); CritterMart.slnx (Orders + Orders.Tests); tests/CritterMart.Orders.Tests/** (OrdersAppFixture, CartViewProjectionTests, AddToCartTests); docs/prompts/implementations/006-...; docs/retrospectives/implementations/006-... (this file)
date: 2026-05-28
mode: solo, consolidated one-PR slice (blueprint-architecture exception: skeleton + first slice); collaborative on the open fork (options + recommendation, user decided)
session-runner: Claude (Opus 4.8)
---

# Retrospective — Implementations 006: Slice 3.1 Add to Cart (Orders skeleton + Cart aggregate)

## Outcome summary

Opened the **Orders** bounded context: stood up `CritterMart.Orders` (the third service, second event-sourced one — Marten event store, `orders` schema, `StreamIdentity.AsString`, `:5103`, wired into Aspire + the solution) and implemented slice 3.1's **Cart** aggregate. `AddToCart` resolves the customer's open cart and either starts a new `Cart` stream (`CartCreated` + `CartItemAdded`, keyed by a generated `cartId`) or appends a further `CartItemAdded` to the open one; the inline `CartView` projects the lines; `GET /carts/{cartId}` reads it. The change's `design.md` + `tasks.md` were authored this edge (proposal/specs landed in #22). **18 tests green** — including the project's **first pure-function unit tests** (the `CartView` fold), which activate the so-far-green-but-empty CI unit job (PR #19) — plus the two GWT integration scenarios + an HTTP-read check against a real Postgres container. `openspec validate --strict` passes. **Scoped deliberately:** remove (3.2), change-qty (3.3), checkout (4.1), stock check (4.2), and the `CartActivityTimeout`/`CartAbandoned` Bruun automation (3.4) are all out; no RabbitMQ; no Catalog read.

## The open fork — how the open cart is resolved (decided this edge)

The proposal locked `cartId` as the stream key but deferred the resolution mechanism. I checked the JasperFx ai-skills and `JasperFx/CritterStackSamples` before recommending:

- **ai-skills**: `marten-aggregate-handler-workflow` codifies the *opposite* of this problem — the id-on-command/route happy path (`{Aggregate}Id`, `[Aggregate(FromRoute/Header/Claim)]`); there is no "resolve-by-secondary-field" primitive. The sanctioned mechanism for querying by a non-key field is in `marten-advanced-indexes-and-query-optimization`: a computed `Index`, and for a uniqueness rule a **partial `UniqueIndex`** (`IsUnique` + `Predicate`).
- **CritterStackSamples** (`EcommerceMicroservices/Basket`): keys the basket by the **customer** and is a plain **document** that is **deleted** on checkout — "one open basket per customer" rides on deletion, which an event stream can't do. Its `Order` is keyed by a generated `Guid` carrying `CustomerId` as a field. Not authority for an *event-sourced* cart.

**User chose:** keep `cartId`, resolve by querying `CartView` on `customerId`, with **one computed index** that both backs the resolution query and — scoped to open carts via `Predicate "(data ->> 'IsOpen')::boolean = true"` — enforces "one open cart per customer" at the DB. Recorded in `design.md` decisions 1–2.

## What worked

- **The Inventory skeleton transferred almost verbatim.** `Program.cs`, csproj, fixture, launchSettings, and the inline-projection shape are a clean mirror; the only real new ground was the resolution step and the index registration.
- **Keeping the fold pure paid the CI dividend immediately.** The `CartView.Apply` methods take `(event, view)` and mutate — callable directly in a unit test with no Marten runtime. Three untagged tests now run in 32 ms in the `Category!=Integration` job that selected zero tests since PR #19.
- **One index, two jobs.** The partial unique index on `CustomerId` serves the resolution `FirstOrDefaultAsync` *and* guards the one-open-cart invariant — no second document to keep in sync (the rejected alternative).
- **ctx7 caught the exact Marten 9 API** (`Index(x => x.Field, idx => { idx.IsUnique = true; idx.Predicate = ...; })`) before writing, avoiding a guess at the partial-unique syntax.

## What was harder than expected

- **Resolution sits off the Critter Stack happy path.** Unlike Inventory (SKU on every command → `FetchForWriting(sku)`), the Cart command carries only `customerId`, so the handler does a query-then-`StartStream`/`FetchForWriting`. The skills confirmed this is expected when the key isn't on the command; the `IsOpen`-scoped index keeps it idiomatic rather than a hand-rolled lookup.
- **The `IsOpen` field is invariant-bearing before its lifecycle exists.** Every cart is open in 3.1 (no terminal state), so the predicate is trivially "all carts" today — but writing it as a predicate now means 4.1/3.4 inherit the one-open-cart guard with no migration. A small bit of forward-looking modeling justified in `design.md`.

## Methodology refinements that emerged

1. **Check the upstream samples for *applicability*, not just precedent.** The sample's customer-keyed basket looked like a direct answer until its delete-on-checkout assumption surfaced — which doesn't hold for an event stream. The useful output was understanding *why* it doesn't transfer, which sharpened the `cartId` rationale in `design.md`.
2. **A read model can do double duty as a resolution index.** Indexing the projection on the actor id (with a partial-unique predicate for the open-state invariant) avoids a separate lookup document — worth reaching for whenever "find the open X for actor Y" precedes an append.

## Outstanding items / next-session inputs

1. **Workshop § 6.1 wording amendment (the spec delta's doc half).** Workshop 001 § 6.1 still reads slice 3.1 as "a new Cart stream is created for `customer-X`" (implying customerId keying) and schedules a `CartActivityTimeout`. Both were refined by the proposal and realized here (`cartId` keying; timeout deferred to 3.4). A `tidy: docs` edit should amend the workshop's literal wording — kept out of this feat PR (no opportunistic edits).
2. **README refresh** — the stale Wolverine-5+/Marten-9+ rows and the BC table; a separate `tidy: docs` (re-check after #23's partial refresh).
3. **Design-return cadence** — Orders implementation PRs are mounting; the next slice (3.2 remove-item, or 4.1 place-order) should weigh a design-return interleave (a narrative bump or workshop pass) per the cadence rule.
4. **`openspec archive slice-3-1-add-to-cart`** after merge.
5. **Slice 4.1 (place order)** then **4.2 (cross-BC reserve stock)** — 4.2 is the stated next goal and now has its prerequisite (Orders exists). Checkout will flip `CartView.IsOpen` (the index predicate already anticipates it) and start the Order stream.

## Spec-delta — landed?

**Yes.** `design.md` + `tasks.md` authored; `shopping-cart` (add-item: cart creation + line append) is satisfied by code and proven by the two GWT scenarios + unit fold tests + a real-Postgres run; `--strict` passes. The Workshop § 6.1 wording amendment is **named here and deferred** to a `tidy: docs` follow-up (recorded, not dropped) — the proposal's faithfulness note already carries the rationale.

## Process notes

- One PR, `feat:` subject (new service + slice, not `tidy:`). Branch `feat/slice-3-1-add-to-cart` created **before** committing.
- Collaborative on the one genuine fork (resolution mechanism): presented options with previews + a recommendation; the user redirected to research ai-skills/samples first, then chose. Routes (write-by-customer / read-by-cartId) taken on the stated recommendation.
- Pre-existing `CS0618` (`PostgreSqlBuilder()` obsolete) is shared by all three test fixtures; left as-is for consistency (out-of-scope to change here).
