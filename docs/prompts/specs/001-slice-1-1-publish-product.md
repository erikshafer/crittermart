# Prompt: Specs 001 — Slice 1.1 PublishProduct OpenSpec Proposal

**Kind**: pre-code design (OpenSpec proposal)
**Files touched**: `docs/prompts/specs/001-slice-1-1-publish-product.md` (new); `openspec/` workspace via `openspec init` (new); `.claude/commands/opsx/*.md` + `.claude/skills/openspec-*/SKILL.md` (new, tool-generated); `openspec/changes/slice-1-1-publish-product/{.openspec.yaml,proposal.md,specs/product-catalog/spec.md}` (new); `CLAUDE.md` (artifact-layer map edit); `docs/retrospectives/specs/001-slice-1-1-publish-product.md` (forthcoming, authored at session close)
**Mode**: solo authoring with tool-backed scaffolding (openspec CLI 1.3.1)
**Commit subject(s)**: `tidy: bootstrap openspec tooling` + `docs: add slice 1.1 publish-product OpenSpec proposal` (bundled in one PR per the "Skeleton + first slice" exception)

## Framing

This is the second edge of the slice 1.1 implementation triangle (narrative → **OpenSpec proposal** → implementation prompt). Narrative 001 (the Seller's catalog-management journey) landed in PR #4; this session authors its machine-readable sibling.

The session adopts the **openspec CLI** (1.3.1) as the tool that produces and validates OpenSpec proposals, rather than approximating the OpenSpec shape in freeform Markdown. The decision follows the principle that when a tool exists and is installed that gives guardrails and adherence to a published spec, prefer it over a freeform approximation — guardrails compound across the project's 17 slices. Because `openspec init` is a one-time project bootstrap and is meaningless without a first change to exercise it, the bootstrap and the slice 1.1 proposal are bundled into one PR under CLAUDE.md's **"Skeleton + first slice"** named exception (openspec workspace = skeleton; slice 1.1 proposal = first slice).

Two integration decisions were settled in conversation and are recorded here as session intent:

1. **Layered adoption, not parallel-sibling.** openspec sits *underneath* as the foundation that solidifies the proposal and specs delta; CritterMart's `docs/` conventions sit *on top* and win in conflicts. The sibling-vs-layered distinction only bites on `design.md` (≈ ADRs) and `tasks.md` (≈ implementation prompts) — and those artifacts are authored in the *implementation* session, not here. So the fork is deferred; this session produces only `proposal.md` + the `specs/product-catalog/spec.md` delta, which are identical under either model.
2. **Diverge from openspec's `/opsx:propose` flow.** The native flow front-loads all four artifacts (proposal → specs → design → tasks) in one shot. CritterMart authors one artifact-class per session, so this session deliberately runs `openspec new change` + authors proposal + specs only, leaving design/tasks for the implementation session.

The `openspec/` workspace lives at the repo root because the directory name is hardcoded in the CLI (`'openspec'` string literals throughout the command implementations) and is not relocatable to `docs/` without forking the tool. CLAUDE.md's artifact-layer map is updated to show `openspec/` as a peer to `docs/`.

## Goal

Produce a valid openspec change at `openspec/changes/slice-1-1-publish-product/` containing:

- `proposal.md` — Why / What Changes / Capabilities / Impact, focused on WHY not HOW (implementation details deferred to `design.md`). Introduces one new capability, `product-catalog`.
- `specs/product-catalog/spec.md` — an `## ADDED Requirements` delta with SHALL-shaped requirements and `#### Scenario:` blocks (exactly 4 hashtags) translating Workshop 001 § 6.1's two GWT scenarios (happy-path publish, duplicate-SKU rejection).

The change must pass `openspec validate slice-1-1-publish-product --strict`.

## Spec delta

The `openspec/` workspace and the `slice-1-1-publish-product` change are created; `docs/specs/` *(forthcoming)* in CLAUDE.md's artifact-layer map is realized as the openspec workspace (a divergence from the originally-anticipated `docs/specs/{slice}/proposal.md` path — documented in this prompt and the retro). The `product-catalog` capability becomes the contract the implementation prompt (third triangle edge) and the implementation code must satisfy. Narrative 001 gains its machine-readable sibling; the two must agree.

## Orientation

Read these in this order:

1. **`docs/narratives/001-seller-manage-catalog.md`** — the sibling artifact; the proposal must agree with it. The two Moments map to the two spec scenarios.
2. **`docs/workshops/001-crittermart-event-model.md`** § 2 (Catalog BC: document store, lifecycle moments for audit), § 4 (Catalog event vocabulary: `ProductPublished`), § 5 (slice 1.1 row: command/event/view/reads-from/writes-to), § 6.1 (the two authoritative GWT scenarios).
3. **`CLAUDE.md`** § 4a (OpenSpec proposal routing) and the architectural non-negotiables (Marten document store for Catalog, Wolverine.Http, inline projections per ADR 008, Identity stubbed per ADR 009).
4. **openspec CLI instructions** — `openspec instructions proposal --change <name>` and `openspec instructions specs --change <name>` supply the template, rules, and output paths; follow them as the validation contract.

## Working pattern

1. Recon the openspec CLI (version, command surface, templates, config, directory-relocatability) before any writes — read-only.
2. `openspec init --tools claude` to bootstrap the workspace and install Claude integration. Review the footprint before committing.
3. `openspec new change slice-1-1-publish-product` to scaffold the change directory + `.openspec.yaml`.
4. Author `proposal.md` per `openspec instructions proposal`. One new capability: `product-catalog`.
5. Author `specs/product-catalog/spec.md` per `openspec instructions specs`. Two requirements (publish; SKU uniqueness), each with a scenario lifted from Workshop 001 § 6.1. Quote-identical example data (`crit-001`, "Cosmic Critter Plush", `24.99`) with the workshop and narrative.
6. `openspec validate slice-1-1-publish-product --strict` — must pass.
7. Update CLAUDE.md's artifact-layer map to show `openspec/` as a peer to `docs/` and to correct the `docs/specs/` *(forthcoming)* row.
8. Author the retrospective at session close.

## Out of scope

- Do not author `design.md` or `tasks.md`. They belong to the implementation session, where the sibling-vs-layered decision for those two artifacts gets made.
- Do not run `/opsx:propose` (front-loads all artifacts) or `/opsx:apply` (implements). This is a proposal-authoring session, not implementation.
- Do not write any `src/` code. The Catalog service skeleton is the implementation session's deliverable.
- Do not author the slice 1.1 implementation prompt. That is the third triangle edge.
- Do not edit Workshop 001 or Narrative 001. If a contradiction surfaces, stop and raise it — the workshop is the source of truth.
- Do not customize the openspec schema (e.g., to make `design` optional). That is a future refinement if the layered model warrants it, via `openspec/config.yaml`.
- Do not author the openspec-adoption ADR in this session even if warranted; flag it in the retro as a next-session input.
