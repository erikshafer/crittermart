# Prompt: Rules 001 — CritterMart Round-One Structural Constraints

**Kind**: pre-code derivative artifact (rules)
**Files touched**: `docs/rules/structural-constraints.md` (new); `docs/retrospectives/rules/001-round-one-structural-constraints.md` (new)
**Mode**: solo synthesis — no multi-persona facilitation needed; the work is reading ten ADRs plus four orientation docs and distilling them into a terse imperative list. An Architect-voice sanity-check pass at the end is the only multi-persona moment.
**Commit subject**: `tidy: rules — initial round-one structural constraints from ADRs and workshop`

## Framing

CritterMart's pipeline (per CLAUDE.md § "Supporting Layers — Rules") names a `docs/rules/structural-constraints.md` artifact whose job is to give an AI session-runner a flat list of imperatives readable in seconds. The rationale for each rule lives in its source ADR, the vision doc, the context map, or Workshop 001; this file is the terse pointer surface, not the argument.

Round one's ten ADRs, the vision doc, the context map, and Workshop 001 collectively encode every architectural non-negotiable CritterMart has named so far. The "Architectural non-negotiables" subsection of CLAUDE.md's Routing Layer is a starter list of five bullets; the "Do Not — round one" subsection adds eight more. Both are seeds, not the final shape — this session's job is to distill everything into one flat file with proper cites and grouping.

This is a derivative session: no new constraints, no new design content, no new ADRs triggered. The session reads the existing material and re-presents it for AI consumption.

## Goal

Produce `docs/rules/structural-constraints.md` as a single flat file of round-one structural rules, each as a terse imperative sentence in present tense with a parenthetical cite to its source. Group rules by domain (service topology, persistence, messaging, observability, identity, aggregates/process managers, projection lifecycle, event naming, SDD pipeline discipline, round-one deferrals). No prose-style argumentation in the body; the source artifact is the place for the "why."

Sanity-check requirement: every ADR (001–010) is cited by at least one rule. Every item in vision.md's "What this deliberately is not" list is covered by at least one rule in the deferrals group. ADR 007's four load-bearing event names appear in the event-naming group.

## Orientation

Read these in this order before beginning:

1. **`CLAUDE.md`** — particularly the "Supporting Layers — Rules" subsection (the artifact's purpose statement is the spec for this session), the "Architectural non-negotiables" subsection in the Routing Layer (a starter list), the "Do Not — round one" subsection (more starter rules), and the "Operating Disciplines" subsection (pipeline-discipline rules — one-prompt-one-PR, no opportunistic edits, design-return cadence, spec-delta closure, `tidy:` convention; these all become rules in the SDD-pipeline group).
2. **`docs/vision.md`** — the "What this deliberately is not" section (every bullet there becomes a deferral rule) and the "Bounded contexts" + "Long road" sections.
3. **`docs/context-map/README.md`** — the integration relationships table (Customer-Supplier between Orders and Inventory; no BC-level integration for Catalog in round one; Conformist for Identity), the topology diagram, and the round-one stubs/deferrals section. The "no synchronous service-to-service HTTP" rule lives here as well as in ADR 003.
4. **All ten ADRs in `docs/decisions/`** — `001` through `010`, in numerical order. Each ADR's Decision and Consequences sections are the source for one or more rules. Some ADRs (e.g., 007) yield several rules; some (e.g., 005) yield one.
5. **`docs/workshops/001-crittermart-event-model.md`** — particularly §4 (event vocabulary — ADR 007's four load-bearing event names and the broader workshop event vocabulary are immovable, and this is the place the immovability is enforced), and §7's reinforcement that the async-daemon-driven projections rule from ADR 008 stands as-is for round one.

## Out of scope

- Do not introduce constraints not present in the orientation material. This file is purely derivative.
- Do not edit any file other than `docs/rules/structural-constraints.md` and the retrospective. In particular, do not edit CLAUDE.md, any ADR, the vision doc, the context map, or the workshop. Do not update CLAUDE.md's artifact-layer map to remove "forthcoming" from the `docs/rules/` row — that is a separate `tidy: housekeeping` session if/when the maintainer chooses.
- Do not author per-slice implementation patterns. Those belong in skills (`docs/skills/`), not rules. The distinction: a rule says *what* must be true (terse imperative); a skill says *how* code structurally expresses it (with examples).
- Do not author rationale prose. If a rule needs a "why," the cite carries the rationale by reference; do not restate the ADR's argument.
- Do not commit any code. This is documentation.
- Do not add long-road parking-lot items as their own rules; they belong in the deferrals group as one composite rule citing `docs/vision.md` § Long road and `docs/context-map/README.md` § Long road. The deferrals group records what is *out* of round one, not what will be in round two.
- Do not name slices or vertical features in the rules file. Slices are workshop-level constructs; the rules file is about cross-cutting structural constraints, not about per-slice intent.
- Do not author rules about specific code conventions (file paths, class naming, namespace shape, project layout). Those, if they emerge, belong in `docs/skills/` once the first implementation session has surfaced them. The rules file is for *architectural* constraints, not for *code-stylistic* ones.

## Output structure

The single file at `docs/rules/structural-constraints.md` should contain, in this order:

1. **Frontmatter** — `version: v1.0`, `status: Active`, `date: 2026-05-26`, and a `references:` list pointing at the source ADRs (paths), vision doc, context map, and workshop. The frontmatter is the audit trail for "where did these rules come from."
2. **Header paragraph** — two or three sentences naming the file's purpose (AI session-runner orientation: a flat imperative list readable in seconds), what it is not (not an ADR; no rationale; the cite is the rationale), and the cadence rule for updates (when a new ADR lands or an existing constraint changes, this file gets a paired update in the same PR — a forward-compatibility note for downstream sessions).
3. **The rules list, grouped by domain.** Use level-2 (`##`) headings for groups; one terse imperative per bullet (under ~20 words); one parenthetical cite per bullet pointing at the source artifact. Suggested groupings (the session-runner may revise based on what the synthesis surfaces):

   - **Service topology** — number of services, deployment shape, project independence.
   - **Persistence** — database, schema isolation, document vs event-sourced model selection.
   - **Cross-service messaging** — transport choice, sync/async constraint, handler portability.
   - **Observability** — OTel coverage, instrumentation requirements.
   - **HTTP surface** — Wolverine.Http per service, BFF status.
   - **Identity** — round-one stub, customer-ID flow.
   - **Aggregates and process managers** — PMvH, state guards, idempotency model.
   - **Projection lifecycle** — inline by default, the one async teaser, no daemon driven in demo.
   - **Event naming** — past tense, no Event suffix, ADR 007's four load-bearing names, Workshop 001 vocabulary authority.
   - **SDD pipeline discipline** — one-prompt-one-PR, sibling artifacts, spec-delta closure loop, design-return cadence, no opportunistic edits, `tidy:` commit subjects, vision-doc updates are deliberate.
   - **Round-one explicit deferrals** — the "What this deliberately is not" set from the vision doc plus the "Do Not — round one" list from CLAUDE.md, each rule citing where the deferral lives.

   Groups are not load-bearing — if synthesis suggests a different grouping reads better, use that. Aim for 2–8 rules per group; fold groups smaller than two into a neighbor; consider splitting groups larger than ten.

4. **Document History** — initial `v1.0` stamp dated 2026-05-26, followed by an empty table ready for future entries. Subsequent sessions that touch this file bump the version per CLAUDE.md § 4b and append a one-line note.

**Tone and shape of a rule.** Imperative sentence, present tense, under ~20 words, parenthetical cite at the end. The cite format is `(ADR NNN)`, `(vision.md § Section)`, `(context map § Section)`, `(CLAUDE.md § Section)`, or `(Workshop 001 § Section)`. If a rule has multiple sources, list them comma-separated.

Illustrative examples (the session-runner will produce the actual list):

- *Three deployed services for round one: Catalog, Inventory, Orders. (ADR 001)*
- *Cross-service messaging is Wolverine over RabbitMQ; no synchronous service-to-service HTTP. (ADR 003, context map § Round-one stubs)*
- *The Order aggregate IS the process manager via PMvH; no separate saga state stream, no `Wolverine.Saga` base class. (ADR 007)*
- *Idempotency for the Order process manager is enforced by state guards on the stream, not by Wolverine inbox at handlers. (ADR 007)*
- *Each slice produces both an OpenSpec proposal and a sibling narrative before the implementation prompt. (ADR 010)*
- *Identity is stubbed for round one; customer ID is hardcoded into the frontend. (ADR 009, context map § Round-one stubs)*
- *No async projection daemon is driven in the demo path; one async projection (`CartAbandonmentReport`) is configured as a teaser only. (ADR 008, Workshop 001 § 7)*

The examples above are correct rules but the session-runner is not obliged to use these exact wordings — the synthesis must be its own work.

The retrospective at `docs/retrospectives/rules/001-round-one-structural-constraints.md` follows CLAUDE.md § 6's format: metadata, outcome summary, what worked, what was harder than expected, methodology refinements (e.g., whether the suggested groupings held up, whether any ADR yielded no extractable rule, whether any rule had to draw from multiple sources, whether the ~20-word limit was tight in practice), outstanding items / next-session inputs (specifically: a candidate `tidy: housekeeping` session to update CLAUDE.md's artifact-layer map to drop "forthcoming" from the `docs/rules/` row, plus any rules that surfaced as candidates but were judged out-of-scope per this prompt), and the **spec-delta — landed?** line.

## Working pattern

This is a synthesis session, not a workshop. Five passes, run sequentially:

1. **Inventory pass.** Read each orientation source in the order listed and list every imperative-shaped constraint it encodes. Bullet form, source-tagged. Expect 25–45 candidate rules, with significant duplication across sources.
2. **Deduplication pass.** Constraints often appear in multiple sources (e.g., "no synchronous service-to-service HTTP" is in ADR 001's consequences, ADR 003's decision, and the context map's round-one stubs section). Pick the canonical source for each rule — usually the ADR whose Decision section created it, with the other sources collapsed into a comma-separated multi-cite if material.
3. **Grouping pass.** Sort the deduplicated rules into the suggested groupings (Output Structure § 3). Adjust groupings if the rules don't fit cleanly. Move group boundaries based on what reads well, not on what the prompt's suggested list said.
4. **Terseness pass.** Rewrite each rule into one imperative sentence under ~20 words. Strip rationale; the cite carries the rationale by reference. If a rule does not fit in 20 words and cannot be tightened, consider whether it is actually two rules masquerading as one.
5. **Architect-voice sanity-check pass.** For each ADR (001–010), confirm at least one rule cites it. For Workshop 001 § 4 (event vocabulary), confirm ADR 007's four load-bearing event names (`StockReserved`, `PaymentAuthorized`, `OrderConfirmed`, `OrderCancelled`) appear in the event-naming group. For vision.md § "What this deliberately is not," confirm each bullet has at least one matching rule in the deferrals group. For CLAUDE.md § Operating Disciplines, confirm each named discipline (one-prompt-one-PR, no opportunistic edits, design-return cadence, spec-delta closure, `tidy:` convention) has a rule in the pipeline-discipline group.

Author the retrospective before opening the PR. The session is one PR per CLAUDE.md's "One prompt = one session = one PR" discipline.

## Spec delta

`docs/rules/structural-constraints.md` and `docs/retrospectives/rules/001-round-one-structural-constraints.md` are created. The forthcoming `docs/rules/` directory and the forthcoming `docs/retrospectives/rules/` directory in CLAUDE.md's artifact-layer map become concrete with their first occupants. Downstream implementation prompts (the per-slice loop, starting with the first slice's narrative + OpenSpec + implementation prompt chain) can cite `docs/rules/structural-constraints.md` as a one-line orientation entry rather than enumerating individual ADR cross-references — a meaningful reduction in session-context overhead per prompt.

Forward-compatibility commitment recorded in the retro: any future ADR that changes a structural constraint pairs with a rule-file update in the same PR (the rule-file's Document-History entry records the change). This discipline is what keeps the rule file from drifting out of sync with the ADRs that are its source of truth.
