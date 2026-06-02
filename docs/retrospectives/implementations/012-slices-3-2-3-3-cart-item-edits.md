# Retrospective: Implementations 012 — Slices 3.2 + 3.3 Cart Item Edits (remove item, change quantity)

**Prompt**: `docs/prompts/implementations/012-slices-3-2-3-3-cart-item-edits.md`
**Outcome**: shipped — the Customer can now remove an item from their open cart (`DELETE /carts/{customerId}/items/{sku}` → `CartItemRemoved`, the project's **first DELETE endpoint**) and change an item's quantity (`POST /carts/{customerId}/items/{sku}/quantity` → `CartItemQuantityChanged`). The `CartView` fold gained **merge-by-SKU semantics** on `CartItemAdded` — the resolution of slice 3.1's explicitly deferred "quantity-merge by SKU is a 3.3 concern" — so cart lines are SKU-keyed and both edit commands are unambiguous. Removing the last line leaves an open, empty cart, which made `PlaceOrder`'s defensive `CartEmpty` guard (shipped in 4.1) **reachable and proven for the first time**. OpenSpec change `slices-3-2-3-3-cart-item-edits` `--strict` valid (1 MODIFIED + 2 ADDED `shopping-cart` requirements); Narrative 004 → v1.6 (Moment 1A). One consolidated PR covering two workshop slices — the scope fork resolved with the user at session start (3.4 gets its own PR).
**Tests**: full solution green — **64 total, 0 failures** (+13): Orders 45 (+4 pure folds, +4 remove integration, +4 change-quantity integration, +1 `CartEmpty` in `PlaceOrderTests`), Inventory 10 (unchanged), Catalog 7 (unchanged), CrossBc 2 (unchanged).

## What shipped

- **Cart events.** `Cart/CartItemRemoved.cs` (`{ Sku }`) and `Cart/CartItemQuantityChanged.cs` (`{ Sku, Quantity }` — absolute quantity, not a delta).
- **The fold, with line identity resolved.** `Apply(CartItemAdded)` now merges by SKU (existing line → quantity sums; first add's snapshot price stays authoritative); `Apply(CartItemRemoved)` drops the line; `Apply(CartItemQuantityChanged)` rewrites quantity in place. The `CartItemAdded.cs` "3.3 concern" comment is resolved.
- **Two endpoints, both keyed on `customerId`** (open-cart resolution, like every cart command): the DELETE (route-only params, no body) and the quantity POST (route params + body), the latter mirroring Catalog's change-price route shape. Guards: `NoOpenCart` 409, `CartItemNotPresent` 409, non-positive quantity 400.
- **The `CartEmpty` path went live.** Add → remove → place-order is now a real, tested journey ending in 409 with no Order stream created.
- **OpenSpec** — `shopping-cart`: 2 ADDED requirements (remove item, change quantity) **plus 1 MODIFIED** (add-item — its view contract changed from one-line-per-add to one-line-per-SKU).
- **Narrative 004 → v1.6** — Moment 1A (changing their mind), slotted in journey order without renumbering Moments 2–6.

## What worked

- **The 4.7-session conventions transferred wholesale and cost nothing to reuse.** Verify-before-wiring as tasks.md group 1 (both facts — `[WolverineDelete]` route-only binding, body + route-param POST binding — confirmed in **one** ctx7 call); the openspec CLI artifact workflow (`new change` → `status --json` → `instructions <artifact> --json`); the FetchForWriting + view-guard endpoint shape. The build compiled clean on the first pass and every test passed on the first run.
- **Presenting the merge-by-SKU fork with code previews settled it in one round.** The three options (merge in fold / reject duplicates / keep duplicate lines) each had a concrete preview of what the cart would look like; the user picked the recommendation immediately. The fork was genuinely the session's only unknown — everything after it was mechanical.
- **The handoff document did its job.** Six verified API facts inherited from 4.7 were *not* re-verified; the two fresh facts this slice needed were named in the prompt before any code existed. Session start to PR was one sitting with no context re-derivation.
- **Both slices in one PR was the right scope.** 3.2 and 3.3 share the open-cart resolution, the guard idiom, the test fixture shape, and the fold file — splitting them would have duplicated orientation and review for no isolation benefit.

## What was harder / notable

- **The spec delta needed a MODIFIED requirement, and that was easy to almost miss.** Merge-by-SKU doesn't just enable the two new commands — it changes the *existing* add-item requirement's view contract ("line items reflect every `CartItemAdded` event" → "lines are keyed by SKU"). Authoring only the two ADDED requirements would have validated fine and **left the durable main spec stale at archive time**, contradicting the shipped fold. The openspec instruction text's "common pitfall" note is what surfaced it.
- **Narrative moment numbering collided with authoring order.** Cart edits belong between Moment 1 (filling the cart) and Moment 2 (checkout), but Moments 2–6 are already cross-referenced from retros, design.md files, and the workshop. Renumbering would have broken those references; appending out of journey order would have made the narrative read wrong. Resolution: **Moment 1A** — journey-ordered placement, stable numbering, with a one-line italic note explaining the convention. Future narratives that backfill pre-checkout moments have a precedent.
- **The quantity-change-for-absent-SKU guard is a spec extension, not workshop text.** The workshop's 3.3 failure path covers only non-positive quantity; rejecting a change to a SKU that isn't in the cart mirrors 3.2's `CartItemNotPresent`. Recorded as design.md faithfulness note 2 — obvious, but the kind of obvious that should be written down rather than assumed.

## Methodology refinements

- **When a slice resolves a deferred decision from an earlier slice, audit the earlier slice's spec requirement for a MODIFIED delta.** A deferral resolution is by definition a change to existing behavior's contract, not just new behavior. The ADDED-only reflex undercounts the delta. (This session's near-miss; candidate convention.)
- **Journey-ordered narrative inserts get letter suffixes (1A, 1B), never renumbering.** Cross-document references to moment numbers are load-bearing; the suffix convention keeps them stable while preserving readable journey order.

## Outstanding / next-session inputs

- **Post-merge `tidy: docs`** (the established pattern): `openspec archive slices-3-2-3-3-cart-item-edits` (folds `shopping-cart` 2 → 4 requirements, 1 modified); extend Workshop § 6.1's slice-3.1 amendment trail with the merge-by-SKU resolution + the `CartItemNotPresent`-on-quantity-change extension (design.md faithfulness notes 1–2); README Orders BC row + folder-README counts (prompts/retros `implementations/` 11→12; narratives 004 row → v1.6; test counts if any README carries them — **none do**, per retro 008's file-shaped-items lesson).
- **Slice 3.4 (cart abandonment) is the last Orders BC slice** — the other Bruun temporal automation + the `CartAbandonmentReport` **async projection teaser** (ADR 008). The handoff at `crittermart-handoff-cart-slices.md` carries its mirror table, genuine forks (cancel-and-reschedule vs fire-and-check; Workshop § 8 open question 1), and the two fresh ctx7 verifications it needs (scheduled-message cancellation/supersession; async projection registration + rebuild under ADR 008's "configured but not demo-critical" constraint). Next prompt = `implementations/013`.
- **Design-return cadence**: this is the **1st implementation PR** since #38's design-return — room for 1–2 more before the next mandatory interleave. The post-merge archive tidy banks the next credit.
- **Marten-pattern third use is now due in 3.4**: `CartsAwaitingActivity*` will likely be the third instance-registered projection and third `IEvent<T>` metadata fold → that session should decide on the `docs/skills/` note (or DEBT row) the 011/008 retros flagged.
- **Carry-forward unchanged**: `StockCommitted` still unmodelled (do not invent); CritterWatch (ADR 013) still blocked on tier/feed/license.

## Spec-delta — landed?

**Yes.** The prompt named: `shopping-cart` +2 ADDED requirements (remove item, change quantity) and Narrative 004 → v1.6 with a new cart-editing Moment. Landed: both ADDED requirements **plus** the MODIFIED add-item requirement the prompt's spec-delta section did not foresee (merge-by-SKU changes the existing contract — see "harder / notable"); Narrative 004 v1.6 Moment 1A with Document History row; 3 design.md faithfulness notes recording the workshop divergences. `openspec validate --all --strict`: 5/5 passed. The delta landed **larger than named**, in the honest direction — the spec records more truth than the prompt predicted.
