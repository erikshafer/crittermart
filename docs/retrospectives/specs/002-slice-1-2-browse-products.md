---
retrospective: 002
kind: specs
prompt: docs/prompts/specs/002-slice-1-2-browse-products.md
deliverable: openspec/changes/slice-1-2-browse-products/{.openspec.yaml, proposal.md, specs/product-catalog/spec.md} (new); docs/prompts/specs/002-slice-1-2-browse-products.md (new); docs/retrospectives/specs/002-slice-1-2-browse-products.md (this file)
date: 2026-05-28
mode: solo authoring with tool-backed scaffolding (openspec CLI 1.3.1)
session-runner: Claude (Opus 4.7)
---

# Retrospective — Specs 002: Slice 1.2 Browse Products OpenSpec Proposal

## Outcome summary

The session authored the **second edge of the slice 1.2 triangle** — the machine-readable proposal companion to Narrative 002. It produced the openspec change `slice-1-2-browse-products` containing `proposal.md` (with `product-catalog` as a **Modified Capability**, gaining a browse requirement) and `specs/product-catalog/spec.md` (one `## ADDED Requirements` block: the browse requirement with a single GIVEN/WHEN/THEN scenario, **no failure path** — a query slice). The change passes `openspec validate slice-1-2-browse-products --strict`. Anchor data is quote-identical with Narrative 002 and Workshop 001 § 6.1: `crit-001` "Cosmic Critter Plush" `24.99` and `crit-002` "Nebula Newt" `18.00`. Per ADR 011, `design.md` and `tasks.md` were deferred to the implementation session, and the openspec CLI was driven manually (no `/opsx:propose`).

## What worked

- **Capability accumulation held — the slice 1.1 one-capability-per-BC decision is validated.** Slice 1.2 ADDed a browse requirement to the existing `product-catalog` capability cleanly. The CLI's specs instructions confirmed the mechanics: a *modified capability* reuses the existing `specs/<capability>/` folder name, and a *new requirement* (vs. changed behavior) uses `## ADDED Requirements`. The capability now carries publish + SKU-uniqueness (1.1) + browse (1.2) across two change deltas.
- **The slice 1.1 proposal as a working template.** Reusing its proposal shape (Why / What Changes / Capabilities / Impact) and spec-delta shape made this a tight mechanical lift — the second time the openspec format has been exercised, with no validation iteration.
- **Quote-identical anchor data extended once, reused everywhere.** `crit-002` "Nebula Newt" `18.00` — introduced in Narrative 002 to satisfy the workshop's two-product browse scenario — was carried verbatim into the spec scenario. One anchor instance per SKU across workshop → narrative → proposal; the implementation tests inherit the same.
- **Query slices scale the openspec structure down cleanly.** One requirement, one scenario, no failure path, no events. The proposal is small without feeling thin — the structure does not impose ceremony a read-only slice doesn't earn.
- **Manual openspec per ADR 011 held again.** `openspec new change` + `openspec instructions` + author proposal/specs only; design/tasks deferred. The session boundary stayed intact.

## What was harder than expected

- **"Modified capability" vs. "ADDED Requirements" — two senses of *modified*.** The proposal lists `product-catalog` under *Modified Capabilities* (the capability is being extended), but the spec delta uses `## ADDED Requirements` (a brand-new requirement, not a change to existing behavior). The CLI instructions disambiguate: `MODIFIED` is reserved for changing an existing requirement's behavior (and must restate the full block); `ADDED` is for new concerns. Browse is a new concern. Resolved cleanly, but the capability-level vs. requirement-level senses of "modified" are an easy trap for a future author — worth watching when slice 1.3 (`ChangeProductPrice`) lands, which may genuinely MODIFY behavior.
- **Whether to archive slice 1.1 first.** Narrative 002's retro (and slice 1.1's) flagged "consider `openspec archive slice-1-1-publish-product` so `openspec/specs/product-catalog/spec.md` is the live base the 1.2 delta builds on." Decided **not** to archive this session: the 1.2 delta is authored against the change-folder deltas and validates independently; `openspec/specs/product-catalog/` still does not exist, and that is fine for authoring/validating. Bundling an archive into a proposal session would mix artifact-classes. Archiving stays a separate deferred step (see outstanding items).

## Methodology refinements that emerged

1. **Capability-granularity is partially confirmed.** The slice 1.1 retro flagged "revisit one-`product-catalog`-capability at 1.2/1.3." Slice 1.2 (a query requirement) ADDed to the capability without strain — half the validation. Slice 1.3 (`ChangeProductPrice`, which mutates an existing product and may MODIFY the publish requirement's surface) is the remaining test. If 1.3 also fits, encode "one capability per bounded context" as a convention.
2. **For query slices, the proposal is naturally minimal.** A read-only slice yields one requirement + one scenario + no failure path. This is a useful calibration point: openspec ceremony tracks slice complexity, it does not impose a floor.
3. **Two unarchived changes now touch one capability.** This is the first time the change-folder model holds more than one delta against `product-catalog`. It validates fine, but it makes the archive-ordering question concrete (see outstanding items) — archiving is the step that merges deltas into the durable main spec.

## Outstanding items / next-session inputs

1. **Slice 1.2 implementation prompt (third triangle edge).** Authors `design.md` + `tasks.md` (per ADR 011 grain) and the `GET /products` endpoint + tests. Inputs: the browse requirement + its scenario; `ProductCatalogView` is a **query** over `Product` documents (slice 1.1 `design.md` Decision 1), so the endpoint queries `session.Query<Product>()` and projects to `ProductCatalogView` — no Marten projection. Mirror the existing `PublishProduct` endpoint/test patterns. Anchor data `crit-001` + `crit-002`.
2. **`openspec archive` is now doubly deferred.** Both `slice-1-1-publish-product` and (once shipped) `slice-1-2-browse-products` are complete-but-unarchived changes on `product-catalog`. Archive order matters for clean accumulation (1.1 then 1.2 → main spec gains publish, uniqueness, browse in order). Recommend archiving both in one deliberate step after slice 1.2's implementation lands, or as a dedicated `tidy:`/chore.
3. **`docs/specs/` → `openspec/changes/` path drift** in `docs/narratives/README.md` (carried from Narrative 002 retro) — still a `tidy: docs` concern.
4. **README count bumps** — `docs/prompts/README.md` and `docs/retrospectives/README.md` *Current population* lines still omit the `specs/` and `chore/` kinds and lag the `narratives/` count; this session added a second `specs/` prompt + retro. Not named in this frozen prompt, so deferred — batch all of these into one `tidy: docs` sweep.
5. **Design-return cadence.** Still a design PR; the Catalog implementation-PR counter stays at 1 (slice 1.1 only). The slice 1.2 implementation PR will make it 2.
6. **No new ADR or skill triggered.**

## Spec-delta — landed?

**Yes.** The prompt's spec delta named:

1. The `slice-1-2-browse-products` openspec change created — **landed**; `openspec validate --strict` passes.
2. The `product-catalog` capability gains a browse requirement (its second) — **landed**; one `## ADDED Requirements` block.
3. Narrative 002 gains its machine-readable sibling; the two must agree — **landed**; the spec scenario corresponds to Narrative 002's Moment 1, with identical anchor data.

No spec-delta items dropped. `design.md`/`tasks.md` correctly deferred (not dropped) to the implementation session.

## Process notes

- One prompt, one session, one PR — the PR contains the prompt, the openspec change (`.openspec.yaml`, `proposal.md`, `specs/product-catalog/spec.md`), and this retro. No code, no design/tasks, no README edits.
- **Prompt-first** this session.
- Branch: `docs/slice-1-2-browse-proposal`.
- Commit subject: `docs: add slice 1.2 browse-products OpenSpec proposal`.
