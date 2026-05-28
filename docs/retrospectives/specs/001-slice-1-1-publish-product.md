---
retrospective: 001
kind: specs
prompt: docs/prompts/specs/001-slice-1-1-publish-product.md
deliverable: openspec/changes/slice-1-1-publish-product/{proposal.md, specs/product-catalog/spec.md, .openspec.yaml} (new); openspec/ workspace via openspec init (new); .claude/commands/opsx/*.md + .claude/skills/openspec-*/SKILL.md (new, tool-generated); docs/prompts/specs/001-slice-1-1-publish-product.md (new); CLAUDE.md (artifact-layer map edit); docs/retrospectives/specs/001-slice-1-1-publish-product.md (this file)
date: 2026-05-27
mode: solo authoring with tool-backed scaffolding (openspec CLI 1.3.1)
session-runner: Claude (Opus 4.7)
---

# Retrospective — Specs 001: Slice 1.1 PublishProduct OpenSpec Proposal

## Outcome summary

The session adopted the **openspec CLI (1.3.1)** as CritterMart's OpenSpec-proposal tooling and authored the first machine-readable proposal — the second edge of the slice 1.1 triangle (narrative → **OpenSpec proposal** → implementation prompt). It produced a valid openspec change at `openspec/changes/slice-1-1-publish-product/` containing `proposal.md` (one new capability, `product-catalog`) and a `specs/product-catalog/spec.md` delta with two SHALL-shaped requirements (publish a product; SKUs are unique), each carrying a scenario lifted quote-identically from Workshop 001 § 6.1. The change passes `openspec validate --strict`.

Per the **"Skeleton + first slice"** exception, the one-time `openspec init` bootstrap is bundled into this PR with the first proposal. Init created the `openspec/` workspace at the repo root plus Claude integration (`.claude/commands/opsx/*.md` ×4, `.claude/skills/openspec-*/SKILL.md` ×4). CLAUDE.md's artifact-layer map, § 4a artifact path, and directory-layout block were updated to show `openspec/` as a peer to `docs/`.

This session was preceded by substantial read-only recon (CLI command surface, templates, config, directory-relocatability) before any writes — the recon is what made the integration decisions concrete rather than speculative.

## What worked

- **Tool-backed over freeform.** Adopting the openspec CLI rather than approximating its shape in Markdown gives `openspec validate --strict` as a guardrail that compounds across the project's 17 slices. The decision reversed an earlier weakly-grounded "freeform Recommended" suggestion after the user pushed back; doing recon (`openspec --help`, templates, config) before committing was the right correction.
- **Recon before writes.** Reading the four artifact templates and the `openspec instructions proposal|specs` output up front meant the proposal and spec delta satisfied the validation contract on the first try (both default and `--strict` passed with no iteration).
- **Layered model, fork deferred.** The sibling-vs-layered decision was correctly identified as *only* biting on `design.md` and `tasks.md` — artifacts authored in the implementation session. So this session produced proposal + specs (identical under either model) and deferred the fork. This avoided a premature methodology commitment.
- **Diverging from `/opsx:propose`.** openspec's native propose flow front-loads all four artifacts; CritterMart authors one artifact-class per session. Running `openspec new change` + authoring proposal + specs only (instead of the slash command) kept the session boundary intact. This is the first concrete integration decision between the two systems and it held cleanly.
- **One capability, accumulating requirements.** Modeling the Catalog BC as a single `product-catalog` capability — to which slice 1.1 ADDs the publish requirement and later slices (1.2 browse, 1.3 price change) will ADD more — matches openspec's archive model (one `openspec/specs/product-catalog/spec.md` grows over time) and keeps the BC's spec coherent rather than fragmented per slice.
- **Quote-identical example data carried forward.** `crit-001`, "Cosmic Critter Plush", `24.99` now appear identically in Workshop 001 § 6.1, Narrative 001, and the spec delta. Three artifacts, one anchor instance.

## What was harder than expected

- **The `docs/specs/` path assumption was wrong.** CLAUDE.md § 4a and the artifact-layer map both anticipated `docs/specs/{slice}/proposal.md`. The openspec CLI hardcodes its workspace to `openspec/` at the repo root — investigated directly in the compiled source (`OPENSPEC_DIR_NAME` plus scattered `'openspec'` string literals in `change.js`, `archive.js`, `validate.js`, `item-discovery.js`). Relocating under `docs/` would mean running every command with cwd=`docs/`, which is fragile. Resolved by accepting the repo-root convention and updating CLAUDE.md to show `openspec/` as a peer to `docs/`. This is a divergence from the originally-documented pipeline path and is worth noting for any future contributor who reads the old assumption in git history.
- **openspec's artifact model is richer than "a proposal file."** The initial framing of this session ("author docs/specs/1.1/proposal.md") under-modeled openspec: a change is a folder of four artifacts with a `propose → apply → archive` lifecycle, where archive syncs deltas into main specs. Understanding this took two ctx7 doc fetches plus template reads. The richer model is a net positive (it gives a real spec-lifecycle), but it forced a mid-session re-framing of what "the OpenSpec proposal session" produces.
- **Capability granularity is a judgment call with downstream consequences.** Choosing one `product-catalog` capability vs. per-slice capabilities (`publish-product`, `browse-products`, …) shapes how `openspec/specs/` accumulates on archive. One-capability-per-BC was chosen for spec coherence, but the call isn't obviously right until slices 1.2 and 1.3 land and either validate or strain it. Flagged for revisit.
- **GIVEN in scenarios.** openspec's documented scenario format is WHEN/THEN; Workshop 001's GWTs are GIVEN/WHEN/THEN. Added `GIVEN`/`AND` bullets alongside WHEN/THEN for fidelity. Validation passed (the validator keys on the `####` header and scenario presence), but this is a small format divergence from openspec's canonical example worth watching if stricter validation arrives in a future version.

## Methodology refinements that emerged

1. **Recon-before-adopt is now a demonstrated default for tool integration.** The sequence (version → help → templates → config → source-grep for hardcoded paths → ctx7 docs) turned every speculative integration question into a settled one before writing. Worth repeating whenever CritterMart adopts a new tool.
2. **openspec ≠ CritterMart pipeline 1:1; map the overlaps explicitly.** `proposal.md` ↔ OpenSpec proposal (1:1), `specs/` ↔ new concept (main specs), `design.md` ↔ ADRs (`docs/decisions/`), `tasks.md` ↔ implementation prompts (`docs/prompts/implementations/`). The layered model (openspec underneath, CritterMart conventions win) is the chosen reconciliation. This mapping should be encoded somewhere durable — candidate for the forthcoming openspec-adoption ADR.
3. **Session boundaries override tool-native flows.** openspec wants `/opsx:propose` to scaffold everything at once; CritterMart's one-artifact-class-per-session discipline wins. When a tool's happy-path workflow conflicts with the pipeline's session model, the pipeline wins and the tool is driven manually via its lower-level commands (`openspec new change`, `openspec instructions`, `openspec validate`).
4. **The `docs/prompts/specs/` and `docs/retrospectives/specs/` kinds are bootstrapped.** `specs/` joins `rules/`, `docs/`, `research/` as a CLAUDE.md-list-extending kind. The prompts/retros README *Current population* lines were NOT updated this session (deferred — see outstanding items); a `tidy: docs` sweep should reconcile them, or the next specs session should.

## Outstanding items / next-session inputs

1. **openspec-adoption ADR is warranted — strongly recommend authoring it next.** The decision clears all three of CLAUDE.md's ADR thresholds: (a) reversing it touches every BC (openspec would manage specs across Catalog, Inventory, Orders), (b) the tradeoff is non-obvious (tool guardrails vs. a second top-level workspace and a richer artifact model), (c) the next contributor would otherwise re-derive the layered-model reasoning from scratch. Per the prompt's out-of-scope list, the ADR was deliberately NOT authored this session to avoid scope creep. It should be the next design artifact — possibly before the slice 1.1 implementation prompt, since it governs how `design.md`/`tasks.md` get treated there.
2. **Slice 1.1 implementation prompt (third triangle edge).** Will author `openspec/changes/slice-1-1-publish-product/design.md` and `tasks.md`, make the sibling-vs-layered decision for those two artifacts, and bring up the Catalog service skeleton + code under `src/` (Wolverine.Http + Marten document store + inline `ProductCatalogView` projection). This is the first implementation PR for the Catalog BC and resets the design-return cadence counter.
3. **README population lines need reconciling.** `docs/prompts/README.md` and `docs/retrospectives/README.md` *Current population* lines do not yet mention the `specs/` kind. Not updated this session to keep the diff focused on the proposal + bootstrap; fold into the next `tidy: docs` sweep or the implementation session.
4. **Slash commands require IDE restart.** The `/opsx:propose`, `/opsx:apply`, `/opsx:archive`, `/opsx:explore` commands installed by init take effect after an IDE restart. They were not used this session (we drove the CLI directly); future sessions can use them once restarted.
5. **Schema customization deferred.** If the layered model later wants `design` formally optional (so trivial slices skip it without a validate warning), that is a `openspec/config.yaml` schema edit — a future refinement, not adopted now.
6. **Capability-granularity validation.** Revisit the one-`product-catalog`-capability choice when slices 1.2 and 1.3 land. If it strains (e.g., browse and publish want different spec shapes), split; if it holds, encode "one capability per BC" as a convention.

## Spec-delta — landed?

**Yes.** The prompt's spec delta named:

1. The `openspec/` workspace and `slice-1-1-publish-product` change created — **landed**; `openspec validate --strict` passes.
2. `docs/specs/` *(forthcoming)* in CLAUDE.md realized as the openspec workspace — **landed** as a documented divergence: it became `openspec/changes/` at the repo root (peer to `docs/`), not `docs/specs/`. CLAUDE.md § 4a, the directory-layout block, and the artifact-layer map were updated to match.
3. The `product-catalog` capability becomes the contract for the implementation prompt and code — **landed**; it is the sole capability and its two requirements are the testable contract.
4. Narrative 001 gains its machine-readable sibling; the two must agree — **landed**; the proposal's two spec scenarios correspond to Narrative 001's two Moments (publish happy path; duplicate-SKU rejection), with identical example data.

No spec-delta items dropped. One item changed shape (the path divergence) and is honestly recorded rather than silently absorbed.

## Process notes

- One PR bundles two commit-subject concerns (`tidy: bootstrap openspec tooling`, `docs: add slice 1.1 publish-product OpenSpec proposal`) under the "Skeleton + first slice" exception — the bootstrap is meaningless without a first change to exercise it.
- No `src/` code committed. The Catalog skeleton + code is the implementation session's deliverable.
- The session was conversationally initiated; the prompt artifact was authored mid-session and is honest about its origin in its framing.
- A new memory was saved (`feedback-prefer-tool-backed-over-freeform`) capturing the recon-before-defaulting-to-freeform correction that opened this session.
- Branch: `tidy/openspec-init`. Commit subjects differentiate the bootstrap and proposal concerns within the one PR.
