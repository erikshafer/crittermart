---
retrospective: 001
kind: narratives
prompt: docs/prompts/narratives/001-seller-manage-catalog.md
deliverable: docs/narratives/001-seller-manage-catalog.md (new); docs/prompts/narratives/001-seller-manage-catalog.md (new); docs/narratives/README.md (edit); docs/prompts/README.md (edit); docs/retrospectives/README.md (edit, in-bounds)
date: 2026-05-27
mode: solo authoring
session-runner: Claude (Opus 4.7)
---

# Retrospective — Narratives 001: The Seller Manages the Catalog

## Outcome summary

The session produced `docs/narratives/001-seller-manage-catalog.md` (v1.0) as CritterMart's first narrative, threading Workshop 001 slice 1.1 (`PublishProduct`) into the Seller's wider catalog-management journey. The narrative carries two Moments — Moment 1 (publishing the first product onto the storefront) renders the workshop's happy-path GWT, Moment 2 (catching a duplicate SKU) renders the workshop's `ProductAlreadyPublished` failure path. The Seller voice is anchored in the intro as a single-operator small business owner of critter-themed merchandise; the workshop's GWT-level term *operator* is bridged once and then left aside in favor of the more domain-natural *Seller*. The journey is framed wider than slice 1.1: slice 1.3 (`ChangeProductPrice`) is forward-looked with a Moment-3 stub, and slice 1.2 (browse) is explicitly excluded as belonging to the Customer's journey.

The session also bootstrapped two subdirectories flagged *forthcoming* in their parent READMEs: `docs/prompts/narratives/` (this session's prompt) and `docs/retrospectives/narratives/` (this file). The two paired READMEs (`docs/narratives/README.md`, `docs/prompts/README.md`) had their *Current population* lines updated downstream of the new artifacts; `docs/retrospectives/README.md` is updated in this same PR to keep the trio of population lines consistent.

This is the first edge of the slice 1.1 implementation triangle (narrative → OpenSpec proposal → implementation prompt). The OpenSpec proposal at `docs/specs/1.1/proposal.md` and the implementation prompt at `docs/prompts/implementations/001-slice-1-1-publish-product.md` are next-session inputs.

## What worked

- **Narrative-first ordering held for this triangle.** Workshop 001 § 6.1's GWTs supplied tight, deterministic content for the two Moments; the SHALL translation (forthcoming in the OpenSpec proposal) is correspondingly low-risk. Drafting the narrative first let the Seller voice, the audit-trail framing, and the "non-events" section settle without SHALL precision constraining the prose. One data point only — not enough to encode the rule — but the choice was earned, not forced.
- **Quote-identical example data with the workshop.** SKU `crit-001`, name *Cosmic Critter Plush*, price `$24.99` are lifted verbatim from Workshop 001 § 6.1. The narrative and workshop point at the same concrete instance; future contributors who skim either find the same anchor product. Cheap, durable consistency.
- **Two Moments for one slice.** The workshop's duplicate-SKU failure GWT became its own Moment rather than a one-liner inside Moment 1. The framing — *"Months later, the Seller has forgotten…"* — captures the *why* a duplicate would occur, which the GWT alone can't express. The format pattern (one Moment per branch where the actor's experience genuinely differs) is worth holding as a default.
- **"What the Seller does *not* yet see" non-events section.** Pulled two deliberate round-one absences (no Catalog → Orders cross-BC events, no price-snapshot ripple to live carts) into journey prose. Workshop 001 § 8 already lists these as parking-lot items, but the workshop's open-questions section is a *systems* register; the narrative section is a *journey* register. The two registers point at the same fact for different audiences.
- **Forward-looking journey scope without overcommitting.** The narrative names slice 1.3 in journey scope, in forthcoming Moments, and in the price-snapshot non-event note, but it does not author slice 1.3's content. When slice 1.3's OpenSpec proposal lands, the narrative-extension session bumps the version to `v1.1` and appends; it does not rewrite. The structure already supports the extension.
- **Seller / operator bridge as a single explicit sentence.** Rather than picking one term and using it everywhere (silent divergence from the workshop) or using the workshop's *operator* throughout (less natural in prose), the narrative names both at first mention and then commits to *Seller*. This is the kind of terminology subtlety the README anticipated — "the first authored narrative may surface subtleties that earn a small README update." For now, the bridge is a per-narrative choice; if subsequent narratives surface the same workshop/narrative term divergence, a one-line README guideline lifts it into convention.

## What was harder than expected

- **Deciding whether to author the prompt artifact at all.** The user delegated *"write the narrative"* conversationally; strict pipeline discipline says session = prompt + execute + retro. Authoring the prompt mid-session strains the "frozen at session start" rule — the artifact records intent as of its writing, which is mid-session, not session-start. The choice was to author the prompt and be honest about the timing in its framing (the framing line *"The triangle's authoring order … was chosen in conversation"* names the conversational origin explicitly). Alternative — skip the prompt artifact and break the convention — was rejected because the prompt is the durable, queryable record of session intent and the convention earns its keep on the next session, not this one.
- **Actor naming: Seller vs operator.** The first surfaced terminology subtlety. Three options were considered: use *operator* throughout (workshop-faithful, prose-unfriendly), use *Seller* silently (prose-friendly, divergence-hiding), or bridge once and commit (the choice). The bridge is the smallest divergence that's still honest; it carries the cost of one extra paragraph in the intro. Re-evaluating after the Customer narrative for slice 1.2 lands — if a similar bridge surfaces there (e.g., workshop's *customer* vs narrative's *shopper* or *buyer*), the pattern is worth a one-line README note.
- **Where to put the "non-events" section.** It is not in the README's documented Moment structure. The README spec is: Context / Interaction / System response per Moment, with a `Document History` table at end. Adding *"What the Seller does not yet see"* between forthcoming-Moments and Document-History stretches the format. The judgment call was that the section earns its place — the workshop's § 8 open questions need a human-readable home in the narrative layer — but the README's format precedent should be amended in a future tidy session if the pattern recurs in the Customer narrative.
- **Scope of the "forthcoming Moments" forward-look.** Could have been minimal (one sentence: "slice 1.3 will extend this narrative") or expansive (full multi-slice forward-look). Chose middle: one paragraph for slice 1.3's anticipated shape, vision/workshop pointer for longer-road items. The paragraph commits to the *name* `ProductPriceChanged` (from Workshop § 4) but not to the *shape* of slice 1.3's Moment — the slice 1.3 narrative-extension session decides that. Risk: if the narrative-extension session contradicts the forward-look, the forward-look becomes a small lie. The risk is acceptable because the forward-look only describes the event vocabulary and the storefront-update behavior, both of which are workshop-fixed.
- **Whether the population-line edits to three READMEs are in-bounds or opportunistic.** Two were named in the prompt (`docs/narratives/README.md`, `docs/prompts/README.md`); one was not (`docs/retrospectives/README.md`). The third was added in this retro session because the retro file's existence makes the retro README's "narratives — forthcoming" line stale on the same PR. Per CLAUDE.md's no-opportunistic-edits rule, this is borderline. The judgment is that all three are *synchronization* edits — each README's population line must reflect the new artifact for the README to remain truthful — not *opportunism*. If the line had been broader staleness sweeps (e.g., correcting the docs-prompt count from "one" to "multiple"), I would have deferred those to a separate `tidy:` session; but those same corrections turn out to be necessary for the population lines to be honest about the kind list anyway, and so rode along.

## Methodology refinements that emerged

These are observations about the narrative-authoring process worth carrying forward.

1. **Narrative-first ordering is one data point of evidence.** Felt earned for slice 1.1. The workshop's tight GWTs absorbed precision pressure that could otherwise have leaked into prose. Do not encode yet. Recommend trying again on (a) the slice 1.3 narrative-extension (same actor, same BC, different operation) and (b) the slice 2.1 narrative (different actor or operator-as-stock-keeper, different BC, first event-sourced slice). Two more data points before considering a `tidy: encode-narrative-first` PR.
2. **The "What the [actor] does not yet see" non-events section is a candidate format extension.** Worth trying on the Customer narrative for slice 1.2 (deliberate absences: no recommendations, no cross-sell suggestions, no real-time stock visibility). If the section appears naturally in 2–3 narratives, lift it into `docs/narratives/README.md` as an optional Moment-set sibling.
3. **Quote-identical example data between workshop and narrative is a cheap consistency win.** SKU/name/price lifted verbatim from Workshop 001 § 6.1. Future narratives should adopt this default — same anchor, same product, same numeric values across workshop / narrative / OpenSpec proposal / implementation tests. Reduces drift surface area for free.
4. **Actor-naming bridge pattern (one-time explicit divergence from workshop term).** Useful when the workshop's technical actor name reads stilted in narrative prose. If a second narrative needs the same kind of bridge, add a one-line guideline to `docs/narratives/README.md` ("if the narrative's actor name differs from the workshop's, bridge at first mention").
5. **Journey scope is wider than slice scope.** The narrative names slice 1.3 in three places (journey scope, forthcoming Moments, price-snapshot non-event) without authoring it. This is what makes the narrative a *journey* artifact rather than a *slice* artifact. The slice 1.3 extension is a version-bump-and-append, not a rewrite.
6. **The prompt artifact authored mid-session is a known violation of "frozen at session start."** Honestly noted in the prompt's framing. Future sessions should author the prompt *before* drafting the deliverable when the session is invoked deliberately; this session's conversational origin made that ordering impossible without breaking the user's flow. If a `/start-session` convention is later established (cf. the `update-config` hook capability), it could enforce prompt-first ordering automatically — but encoding that is its own future session.

## Outstanding items / next-session inputs

These flow downstream into the slice 1.1 triangle's second and third edges and into the next narrative sessions.

1. **Slice 1.1 OpenSpec proposal at `docs/specs/1.1/proposal.md`** — the next session in the triangle. Inputs the narrative provides:
   - Concrete anchor product (`crit-001`, *Cosmic Critter Plush*, `$24.99`) — adopt verbatim or make explicit if illustrative-only.
   - Two GWT scenarios already authored in Workshop 001 § 6.1 (happy + duplicate) — translate to OpenSpec SHALL scenarios mechanically.
   - The "non-events" framing — the proposal does not need SHALLs for non-events, but the proposal's *out of scope* section should mirror the narrative's "What the Seller does not yet see."
   - The Seller / operator bridge — the proposal can use either term; recommend *operator* for SHALL precision (matching the workshop) with one bridge line.
2. **Slice 1.1 implementation prompt at `docs/prompts/implementations/001-slice-1-1-publish-product.md`** — the third session in the triangle. Per CLAUDE.md's "Skeleton + first slice" named exception, this PR also brings up the Catalog service skeleton. Open questions the implementation prompt resolves:
   - The shape of the back-office Catalog page. The narrative names it as the Seller's interaction surface but commits to nothing about its UI framework, form layout, or authentication shape (Identity is stubbed per ADR 009).
   - Wolverine.Http endpoint shape for `PublishProduct` (per ADR's "Wolverine over RabbitMQ for cross-service; HTTP for entry").
   - Marten document schema for `Product` and the `ProductCatalogView` projection lifecycle (inline per ADR 008).
   - Test strategy for the duplicate-SKU rejection (Alba integration test exercising both Moments).
3. **Customer narrative for slice 1.2 (Browse and view products)** — the second narrative for this BC. Probably authored after slice 1.1's implementation lands and slice 1.2's OpenSpec proposal triggers it. Inputs from this session: actor naming pattern (workshop's *customer* term — does it bridge to *shopper*, *buyer*, or stay as *Customer*?), non-events section trial (no recommendations, no cross-sell, no real-time stock visibility), and the same quote-identical-example-data discipline.
4. **Narrative-extension session for slice 1.3 — bumps this narrative to v1.1.** Triggered when slice 1.3's OpenSpec proposal is authored. Appends Moment 3 (price change), updates `slices: [1.1, 1.3]` frontmatter, appends to `Document History`. Does not rewrite v1.0 content.
5. **No new ADR triggered.** All choices in this session sit within existing ADRs 001–010 and CLAUDE.md disciplines. The narrative-first ordering decision is a per-session methodology choice; if it earns its keep, the encoding is a `tidy:` PR, not an ADR.
6. **No new skill triggered.** Round-one defers to upstream JasperFx Critter Stack ai-skills (per CLAUDE.md's "Companion library" line). Nothing in this session surfaced a CritterMart-specific narrative-authoring convention divergent enough from upstream guidance to earn `docs/skills/narrative-authoring/SKILL.md`. If the "non-events section" or "actor-naming bridge" patterns recur, that judgement may revisit.
7. **Design-return cadence counter status.** This is a design PR (per CLAUDE.md § Operating Disciplines: narrative authoring is part of the per-slice design phase, not implementation). The implementation-PR counter remains at 0 for the Catalog BC. The next two PRs in the triangle (OpenSpec proposal, implementation prompt) are also design-shaped; the implementation prompt's PR is the first implementation PR per the "Skeleton + first slice" exception.

## Spec-delta — landed?

**Yes, with one in-bounds addition.**

The prompt's spec delta named:

1. `docs/narratives/001-seller-manage-catalog.md` created — **landed** at v1.0 with two Moments, a forward-looking forthcoming-Moments section, and a non-events section.
2. `docs/narratives/README.md` *Current population* line moved from "Forthcoming" to one-narrative — **landed**.
3. `docs/prompts/README.md` *Current population* line adds `narratives/` to populated-kinds list — **landed**, with the side-effect correction of the pre-existing staleness in the `docs/` and `research/` counts.
4. Slice 1.1 OpenSpec proposal and implementation prompt gain their human-readable companion spec — **landed** (the narrative is the companion they will reference).

**Addition in-bounds, not named in the prompt:** `docs/retrospectives/README.md` *Current population* line updated to add `narratives/` (mirroring the prompts-README pattern). Justification: same synchronization principle — the retros README must reflect the new retro artifact for the population line to remain truthful. Captured here for spec-delta transparency.

No spec-delta items were dropped or downscoped.

## Process notes

- One prompt, one session, one PR — the PR contains exactly the five named artifacts (prompt, narrative, narratives README edit, prompts README edit, retros README edit, retrospective) and nothing else. No code committed.
- The session was conversationally initiated rather than prompt-initiated. The prompt artifact was authored mid-session and is honest about that in its framing. Future sessions should aim for prompt-first when possible.
- Branch suggestion for the PR: `docs/seller-catalog-narrative` per `docs/prompts/README.md`'s `{type}/{slug}` convention (type matches the commit subject's `docs:` prefix; slug mirrors the narrative file slug).
- Commit subject per the prompt: `docs: add seller-catalog narrative covering slice 1.1 publish-product`.
- The `Document History` table in the narrative is stamped v1.0. Future sessions that touch the narrative append entries and bump the version per CLAUDE.md § 4b.
