# Retrospective: Docs 006 — Slice 4.2 doc follow-ups (ADR 014, archive, spec Purpose, README)

**Prompt**: `docs/prompts/docs/006-slice-4-2-doc-followups.md`
**Outcome**: shipped — slice 4.2's four named follow-ups drained in one design-return PR under a `docs:` subject (ADR is the lead artifact). The packaging fork (one combined PR vs. split) was resolved with the user before any edit: one PR, `docs:` subject.
**Validation**: `openspec validate --all` → 4 passed (order-lifecycle, product-catalog, shopping-cart, stock-management); no active changes remain.

## What shipped

- **ADR 014 — published-language cross-BC contracts.** Lifts slice-4-2 `design.md` decisions 4 + 5 (the `CritterMart.Contracts` shared-project decision and the three-`StockReserved`s ownership split) into a durable cross-change record. Indexed in `docs/decisions/README.md`.
- **Paired `structural-constraints.md` note.** Three rules added to § Cross-service messaging (Contracts is the published language; it is not a service so referencing it from both services does not breach ADR 001; it owns wire messages only). ADR 014 added to the references frontmatter; version → v1.2; Document History row appended. The ADR-plus-rule-note-in-one-PR convention (that file's header) is honored.
- **Archived `slice-4-2-reserve-stock`** → `openspec/changes/archive/2026-05-31-slice-4-2-reserve-stock/`. The CLI folded the deltas into the durable main specs: `order-lifecycle` `+2 added` (reserve-for-placed-order; cancel-on-stock-failure) and `stock-management` `~1 modified ~1 added` (cross-BC reserve + publish-back; idempotent reservation).
- **Filled the `stock-management` `## Purpose`** — replaced the slice-2.1 `TBD` placeholder with authored prose (Inventory's single capability: Stock-per-SKU streams + `StockLevelView`; 2.1 receive → 2.2 reserve → 4.2 cross-BC supplier role; 2.3 release deferred).
- **README refresh.** Inventory BC row now reads "2.1 receive, 2.2 reserve, 4.2 cross-BC reserve"; Orders row adds "4.2 / 4.5"; the repo-structure block gains `CritterMart.Contracts` (src/) and a `tests/` line surfacing `CrossBc.Tests`; the Getting Started status note adds 4.2/4.5 as the first live RabbitMQ traffic.

## What worked

- **The retro-named follow-up list was the whole work order — again.** Retro 008's "Outstanding" section enumerated exactly these four items (ADR + rule note, archive, Purpose, README), plus the standing CritterWatch deferral. Zero rediscovery cost; the spec-delta-closure loop did its job.
- **`design.md` is the right parking spot for an ADR-bound decision.** Decision 4 was written during the feat session with full context and explicitly flagged "ADR follow-up." This session's ADR is essentially a promotion of that prose to cross-change grain — the grain-aware-layered model (ADR 011) working as designed: change-local rationale in `design.md`, cross-change record in the ADR.
- **The packaging fork was cheap to surface and cheap to resolve.** A single question with concrete commit-subject previews settled the `docs:`-vs-`tidy:` convention tension up front, so no rework.

## What was harder / notable

- **`product-catalog`'s Purpose is *still* `TBD`.** Surfaced while filling `stock-management` (both are sibling main specs). It is a slice-1.1 archive leftover, not named in this prompt or retro 008 — so fixing it here would be an opportunistic edit. Left untouched and recorded below. This is the second time the "archive-touches-a-sibling-spec, eyeball its Purpose" habit (flagged in retro 005) has paid off — but the catch only fires for specs the archive *touches*; `product-catalog` was missed twice because no recent archive touched it.
- **ADR cross-link points into `archive/`.** ADR 014 references `design.md` at its post-archive path (`openspec/changes/archive/2026-05-31-slice-4-2-reserve-stock/design.md`). Correct only because the archive ran in this same PR — the link resolves in the merged tree, not in a pre-archive checkout. A reader bisecting history before this PR would find it under `changes/`.

## Methodology refinements

- **A design-return PR can carry a new ADR, not just maintenance.** Docs 004 / 005 were pure housekeeping; this one banks the design-return credit *and* lands ADR 014. The `docs:` subject (not `tidy:`) keeps the "new artifacts don't use tidy:" convention intact while still serving the cadence rule — a design-return need not be content-free.
- **Standing habit, sharpened:** when filling one main spec's Purpose, scan *all* sibling main specs for unfilled `TBD` placeholders, not only the one the current archive touched. The touch-triggered version of this habit misses specs no recent archive visited (e.g., `product-catalog`).

## Outstanding / next-session inputs

- **`product-catalog` spec `## Purpose` is still the slice-1.1 `TBD` placeholder.** New surfaced item; a one-line fix for a future `tidy: docs` (or fold into the next Catalog-touching session). Not blocking.
- **CritterWatch (ADR 013)** — still deferred; blocked on the tier/feed/license question (paid feed 401s on CI). 4.2 lit up the first live broker traffic, so it remains the natural home when that question resolves.
- **Design-return cadence**: slices 4.1 (#29) + 4.2 (#31) were the 1st and 2nd Orders implementation PRs since the #28/#30 interleave; this design-return PR (ADR 014 + tidy) banks the credit, so the budget resets before slice 4.3.
- **Next slice**: **4.3 — authorize payment (stubbed)**, the second gate — reacts to the Order-stream `StockReserved` and cascades `AuthorizePayment` to the in-process stubbed provider (same cascading-handler shape, now in-process rather than cross-BC).

## Spec-delta — landed?

**Yes** (a small one). The named delta — a new ADR (014) and a paired published-language note in `structural-constraints.md` — landed: ADR 014 is `Accepted` and indexed; the rule file is at v1.2 with three new § Cross-service messaging rules and a Document History row. The openspec side is closure-only: slice 4.2's two-capability delta (authored and `--strict`-valid in #31) folded into the durable `order-lifecycle` + `stock-management` main specs via archive, and the `stock-management` Purpose was authored. No narrative/workshop amendment needed — those landed in #31. `validate --all` green.
