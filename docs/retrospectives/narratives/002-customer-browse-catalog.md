---
retrospective: 002
kind: narratives
prompt: docs/prompts/narratives/002-customer-browse-catalog.md
deliverable: docs/narratives/002-customer-browse-catalog.md (new); docs/prompts/narratives/002-customer-browse-catalog.md (new); docs/narratives/README.md (population line); docs/retrospectives/narratives/002-customer-browse-catalog.md (this file)
date: 2026-05-28
mode: solo authoring
session-runner: Claude (Opus 4.7)
---

# Retrospective — Narratives 002: The Customer Browses the Catalog

## Outcome summary

The session produced `docs/narratives/002-customer-browse-catalog.md` (v1.0), CritterMart's second narrative and its **first from the Customer's perspective**, threading Workshop 001 slice 1.2 (Browse and view products). It is the first edge of the slice 1.2 triangle (narrative → OpenSpec proposal → implementation prompt). The narrative carries one Moment (the Customer browsing a populated storefront and seeing the published catalog via `ProductCatalogView`), a "What the Customer does not yet see" non-events section, and a forthcoming-Moments note pointing into the Orders BC (cart/order). Anchor data is `crit-001` "Cosmic Critter Plush" `$24.99` (reused verbatim from Workshop 001 § 6.1 and Narrative 001) plus a newly-introduced second product `crit-002` "Nebula Newt" `$18.00` for the workshop's two-product browse scenario.

The session was **prompt-first** — the prompt was authored before the narrative, correcting the mid-session strain Narrative 001's retro flagged. `docs/narratives/README.md`'s *Current population* line was updated from one narrative to two.

## What worked

- **Prompt-first ordering, cleanly.** Unlike Narrative 001 (whose prompt was authored mid-session and had to be honest about the timing), this session authored the prompt before the narrative because the user named the prompt as a deliverable up front. The "frozen at session start" rule held without strain. This is the ordering future deliberate sessions should default to.
- **No actor-naming bridge needed — second data point.** Workshop 001's GWT term *customer* and the narrative's *Customer* coincide, so the Seller/operator-style bridge Narrative 001 needed did not recur. Pattern sharpening: the bridge is required only when the workshop's technical actor name reads stilted in prose; when they coincide, name it once and move on. Two data points now (001 needed a bridge, 002 did not).
- **Non-events section reused naturally — now 2 of 2 narratives.** "What the Customer does not yet see" (no live stock, no recommendations, no real-time price) landed as naturally as Narrative 001's Seller-side section. Retro 001 set the bar at "2–3 narratives" before lifting it into the README; it has now appeared in both narratives authored. Strong candidate for a README format extension (see refinements).
- **Quote-identical anchor data, extended deliberately.** `crit-001`/Cosmic Critter Plush/`$24.99` carried verbatim; the second product the workshop's 1.2 scenario requires (`crit-002`) was chosen here as "Nebula Newt" `$18.00` so the proposal and tests can adopt it verbatim rather than each inventing one. One anchor instance per SKU across all artifacts.
- **Journey scope wider than slice scope, across a BC boundary.** The narrative threads only slice 1.2 but names the journey's continuation into the Orders BC (cart 3.1, order 4.1) without authoring it — and explicitly defers the *placement* decision (extend this narrative vs. a separate Customer-purchasing narrative) to the session that picks up slice 3.1. This keeps the narrative a journey artifact without overcommitting cross-BC structure.

## What was harder than expected

- **A query slice yields a thin narrative.** Slice 1.2 has no command, no events, and no failure path (Workshop § 5/§ 6.1), so the narrative has a single Moment where Narrative 001 had two. The judgment was that one Moment plus the non-events and forthcoming sections is a complete journey artifact for a read-only slice — the narrative's value here is the *journey context* (who browses, what they can and cannot see, where the journey goes next) that the bare GWT cannot express. A query slice does not need padding to justify a narrative.
- **Where the Customer's cart/order steps belong.** The Customer's journey crosses from Catalog (browse) into Orders (cart, checkout). Whether slices 3.1/4.1 extend *this* narrative (making it a cross-BC Customer journey) or get a separate Orders-context Customer narrative is a real structural choice. Resolved by deferring it explicitly to the slice 3.1 session rather than pre-deciding — the same forward-look-without-overcommit pattern Narrative 001 used for slice 1.3.
- **Inventing `crit-002`'s identity.** The workshop's 1.2 GWT names `crit-002` but gives it no name or price (only `crit-001` is fully specified anywhere). Choosing "Nebula Newt" `$18.00` here means this narrative is now the anchor source for that second product; the proposal and tests must adopt it verbatim or the quote-identical discipline breaks. Flagged as a next-session input.

## Methodology refinements that emerged

1. **The "What the [actor] does not yet see" non-events section has earned a README lift.** It now appears in both narratives (001 Seller, 002 Customer) and reads naturally in each. Recommend a `tidy:` edit to `docs/narratives/README.md` adding it as an optional Moment-set sibling in the documented body structure. This clears the "2–3 narratives" bar Retro 001 set.
2. **Actor-naming bridge rule is now expressible.** "Bridge the workshop's actor term at first mention *only when it diverges* from the natural prose term; when they coincide, use the term directly." Two data points support it (Seller/operator diverged → bridge; customer/Customer coincided → no bridge). Candidate for the same `docs/narratives/README.md` `tidy:` as #1.
3. **Narrative-first held for a query slice — third overall data point.** Felt earned: the trivial GWT left all the substance to the journey prose, so there was no SHALL-precision pressure to settle first. Combined with slice 1.1 (001), narrative-first is now 2-for-2 where tried (the slice 1.3 extension and slice 2.1 remain the other suggested trials before considering encoding it).
4. **Prompt-first is achievable when the session is invoked deliberately.** Narrative 001's retro hoped for this; this session delivered it because the user's instruction named the prompt as a deliverable. No tooling needed — just authoring order.

## Outstanding items / next-session inputs

1. **Slice 1.2 OpenSpec proposal (next triangle edge).** A new openspec change adding a **browse** requirement to the *existing* `product-catalog` capability (not a new capability — the one-capability-per-BC decision from slice 1.1 holds). Drive the openspec CLI manually per ADR 011. Inputs from this session: anchor data `crit-001` (Cosmic Critter Plush, `$24.99`) + `crit-002` (Nebula Newt, `$18.00`); the single happy-path GWT (no failure path); the non-events framing maps to the proposal's *out of scope*. **Consider running `openspec archive slice-1-1-publish-product` first** so `openspec/specs/product-catalog/spec.md` is the live base the 1.2 delta builds on.
2. **Slice 1.2 implementation prompt (third edge).** The `GET /products` endpoint reading `ProductCatalogView` (a query over `Product` documents per slice 1.1 `design.md` Decision 1 — not a projection). Mirror the existing `PublishProduct` endpoint/test patterns.
3. **`docs/specs/` → `openspec/changes/` path drift in `docs/narratives/README.md`.** The README's "Sibling artifact" and "Cross-references" sections, and its population commentary ("sibling OpenSpec proposal at `docs/specs/1.1/proposal.md` (forthcoming)"), still reference the pre-ADR-011 path; the proposal is neither at that path nor "forthcoming" (it shipped in PR #5 at `openspec/changes/`). Left unfixed here (out of scope; no opportunistic edits) — fold into a `tidy: docs` sweep.
4. **Deferred README count bumps.** `docs/prompts/README.md` and `docs/retrospectives/README.md` *Current population* lines still say `narratives/ (one ...)`; they are now two. Not named in this session's frozen prompt, so deferred — batch with the still-owed `specs/` and `chore/` population lines into one `tidy: docs` sweep.
5. **Cart/order Moment placement** (extend Narrative 002 vs. a separate Customer-purchasing narrative) — decided at slice 3.1.
6. **Design-return cadence.** This is a design PR (narrative authoring), not implementation. The Catalog implementation-PR counter stays at 1 (slice 1.1 only). Good interleave; room remains before a mandatory design-return.
7. **No new ADR or skill triggered.** All choices sit within existing ADRs and CLAUDE.md disciplines.

## Spec-delta — landed?

**Yes.** The prompt's spec delta named:

1. `docs/narratives/002-customer-browse-catalog.md` created at v1.0, threading slice 1.2 — **landed** (one Moment + non-events + forthcoming).
2. `docs/narratives/README.md` *Current population* moves from one narrative to two, naming the Customer browse narrative — **landed**.
3. The forthcoming slice 1.2 OpenSpec proposal gains its human-readable companion — **landed** (the narrative is the companion the proposal will reference and must agree with).

No spec-delta items dropped. The two other README count bumps (prompts/, retrospectives/) were deliberately deferred (item 4) rather than absorbed opportunistically, consistent with the frozen-prompt scope.

## Process notes

- One prompt, one session, one PR — the PR contains the prompt, the narrative, the narratives-README population edit, and this retro. No code.
- **Prompt-first** this session (improvement over Narrative 001's mid-session prompt).
- Branch: `docs/customer-browse-narrative` (`{type}/{slug}`, type matches the `docs:` commit prefix).
- Commit subject: `docs: add customer-browse narrative covering slice 1.2`.
- `Document History` stamped v1.0; future cart/order threading bumps it (or spawns a sibling narrative) per the deferred placement decision.
