# ADR 011: openspec CLI as Proposal Tooling, Grain-Aware Layered Integration

**Status**: Accepted

## Context

[ADR 010](010-openspec-narrative-sibling-pipeline.md) decided *that* each slice carries an OpenSpec proposal alongside a narrative. It did not decide *how* the proposal is produced or where it lives. The `openspec` CLI (1.3.1, by Fission-AI) is installed and offers a richer model than "a proposal file": a *change* is a folder of four artifacts — `proposal.md`, `specs/<capability>/spec.md`, `design.md`, `tasks.md` — with a `propose → apply → archive` lifecycle that syncs deltas into durable main specs.

Two questions followed. First: author proposals with the CLI (tool-backed, validated) or approximate the OpenSpec shape in freeform Markdown? Second: how does the openspec workspace coexist with CritterMart's `docs/` pipeline, given that two of openspec's artifacts overlap existing CritterMart artifacts — `design.md` overlaps ADRs (`docs/decisions/`), and `tasks.md` overlaps implementation prompts (`docs/prompts/implementations/`)?

A terminology note, because the vocabulary collides. ADR 010's *sibling* describes the OpenSpec proposal and the narrative as siblings **of each other**. This ADR's *layered* describes the openspec workspace sitting **under** CritterMart's `docs/` pipeline. Different axes; do not conflate.

Three integration shapes were considered: **freeform** (no tool, hand-written Markdown), **sibling** (openspec owns all four artifacts fully *and* ADRs + implementation prompts also exist as independent peers), and **layered** (openspec is the foundation; CritterMart conventions win on conflict).

## Decision

1. **Adopt the openspec CLI as the tool that authors and validates OpenSpec proposals.** `openspec validate --strict` is a guardrail that compounds across the project's 17 slices; freeform compounds drift. Tool-backed wins over freeform whenever the tool exists and enforces a published spec.

2. **The workspace lives at `openspec/` at the repo root, a peer to `docs/`.** The CLI hardcodes the directory name (`'openspec'` string literals throughout its command implementations); it is not relocatable under `docs/` without forking the tool. CLAUDE.md's artifact-layer map, § 4a, and directory layout reflect this peer relationship. This supersedes the originally-anticipated `docs/specs/{slice}/proposal.md` path.

3. **Integration is grain-aware layered, not sibling and not naive-layered.** openspec is the foundation; CritterMart conventions win on genuine conflicts. The overlaps are grain-mismatched, so artifacts coexist at their natural grain rather than competing:
   - `proposal.md` + `specs/<capability>/spec.md` → **openspec owns outright** (no CritterMart analogue competes).
   - `design.md` (*per-change* technical approach) coexists with **ADRs** (*cross-change* decisions). A change-local `design.md` references the relevant ADR rather than restating it; cross-cutting decisions remain ADRs. `design.md` is optional in openspec and is skipped for trivial slices.
   - `tasks.md` (*live, mutable implementation checklist* that `openspec apply` drives) coexists with the **implementation prompt** (*frozen session-intent record* with framing, orientation, out-of-scope). Intent vs. execution — not duplicates.

4. **Drive openspec manually, one artifact-class per session.** The native `/opsx:propose` flow front-loads all four artifacts at once; CritterMart's one-prompt-one-session-one-PR discipline wins. The proposal session authors `proposal.md` + `specs/` (via `openspec new change` + `openspec instructions`); the implementation session authors `design.md` + `tasks.md`. Session boundaries override tool-native happy paths.

## Consequences

`openspec validate` gives every proposal a structural guardrail, and the spec-delta closure loop in CLAUDE.md keeps proposal, narrative, and code in sync. Capabilities accumulate requirements across slices in one `openspec/specs/<capability>/spec.md` per bounded context (e.g., `product-catalog` grows as slices 1.1 → 1.2 → 1.3 land).

Costs and risks: a second top-level workspace alongside `docs/`; the grain split avoids duplication only if the discipline is held (a contributor must know cross-change reasoning lives in ADRs, change-local in `design.md`). The `/opsx:*` slash commands require an IDE restart to take effect.

Rejected alternatives. **Freeform** — no validation, 17 chances for SHALL-grammar drift. **Sibling** — openspec's `design.md`/`tasks.md` and CritterMart's ADRs/implementation-prompts would partially overlap at mismatched grains, forcing contributors to read both for a complete picture (worse than clean redundancy). **Full adoption** — retiring ADRs and implementation prompts into openspec's `design.md`/`tasks.md` would lose the cross-change grain of ADRs and the frozen-intent character of implementation prompts.

The grain-aware layered model is **stated here but not yet exercised**: this session (the slice 1.1 proposal) produced only `proposal.md` + `specs/`, which are identical under any model. The model is first tested when the slice 1.1 implementation session authors `design.md` and `tasks.md`. If that session contradicts this ADR, amend it — per CLAUDE.md's retrospective-to-design feedback edge.
