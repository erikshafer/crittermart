# Retrospective: Docs 005 — Slice 4.1 doc follow-ups

**Prompt**: `docs/prompts/docs/005-slice-4-1-doc-followups.md`
**Outcome**: shipped — slice 4.1's three named doc follow-ups drained in one `tidy: docs` PR.
**Validation**: `openspec validate --all` → 4 passed (product-catalog, stock-management, shopping-cart, order-lifecycle); no active changes remain.

## What shipped

- **Archived `slice-4-1-place-order`** → `openspec/changes/archive/2026-05-31-slice-4-1-place-order/`. The CLI created `openspec/specs/order-lifecycle/spec.md` (new durable main spec — the Orders BC's second capability) and appended the "Check out the cart on order placement" requirement to `openspec/specs/shopping-cart/spec.md`.
- **Filled both `## Purpose` sections.** `order-lifecycle` got an authored purpose (Order stream placement → terminal, PMvH, no logistics). `shopping-cart`'s lingering `TBD` placeholder — left by slice 3.1's archive (#28) and flagged in retro 007 — was filled in the same pass.
- **README refresh.** Orders BC-status row now reads "Cart (3.1) + Order placement (4.1); cross-BC reservation (4.2) … forthcoming"; the Getting Started status note adds 4.1 place-order.

## What worked

- **The retro-named follow-up list was the whole work order.** Retro 007's "Outstanding" section enumerated exactly these three items, so this session had zero rediscovery cost — the spec-delta-closure loop did its job: the feat PR named the deferrals, the tidy PR drains them.
- **`openspec archive` did the mechanical spec sync.** Creating the new main spec and appending the cross-capability requirement to the existing one is a CLI move, not hand-editing — the same low-friction path #28 used. Hand work was limited to the two prose Purpose sections.

## What was harder / notable

- **Catching the lingering `shopping-cart` Purpose TBD** required remembering it was a *3.1* leftover, not a *4.1* artifact. It surfaced only because the archive touched the same file. Worth a standing habit: when an archive "updates" an existing main spec, eyeball its Purpose for an unfilled placeholder from a prior archive.

## Methodology refinements

- **Tidy PRs mirror their feat PRs one-to-one.** Slice 3.1 → docs 004; slice 4.1 → docs 005. The pairing keeps the design-return cadence legible and gives each slice a clean two-PR shape (feat carries code + spec delta; tidy carries archive + index hygiene). No change to the pipeline — just a confirmed rhythm.

## Outstanding / next-session inputs

- **None for slice 4.1** — its spec delta is fully closed (narrative bumped in #29, change archived, main specs durable, README current).
- **Design-return cadence**: slice 4.1 (#29) was the 1st Orders implementation PR since #28; this tidy PR banks a design-return credit, so the budget resets — the next 2–3 Orders impl slices can run before the next mandatory interleave.
- **Next slice**: **4.2 — reserve stock cross-BC** (RabbitMQ + Klefter local commits + CritterWatch light up here).

## Spec-delta — landed?

**No spec delta** (housekeeping session). The named-none is forward-confirmed: this PR adds no requirement and no scenario — it closes slice 4.1's delta by archiving the change into durable main specs and authoring their Purpose prose. `validate --all` green.
