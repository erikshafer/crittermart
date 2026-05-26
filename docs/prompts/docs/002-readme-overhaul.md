# Prompt: Docs 002 — Root README Overhaul

**Kind**: maintenance / docs surface (root README rewrite to family standard)
**Files touched**: `README.md` (rewrite — currently 10 lines of "TBD" + maintainer block); `docs/retrospectives/docs/002-readme-overhaul.md` (new)
**Mode**: solo synthesis — read the three sibling Critter-family READMEs for structural pattern, read CritterMart's own canonical artifacts for content, then synthesize a README that matches the family skeleton while carrying CritterMart-specific framing.
**Commit subject**: `tidy: docs — overhaul root README to family standard`

## Framing

The CritterMart root `README.md` is currently ten lines: the project name, the word "TBD.", and a maintainer block. A visitor landing at the GitHub repo finds nothing about what the project is, why it exists, what its architecture looks like, how to run it, or where the substantive documentation lives. Meanwhile, the documentation pipeline behind the scenes is rich: a canonical vision doc, a context map, an Event Modeling workshop, ten ADRs, a rules synthesis, folder-local READMEs, an event-modeling skill, and a paired prompt/retro for every session. None of that surfaces from the root.

The three sibling Critter projects (`C:\Code\CritterBids\`, `C:\Code\CritterCab\`, `C:\Code\CritterSupply\`) each carry substantial READMEs that share a recognizable structural skeleton: badge row → tagline blockquote → "What Is X?" framing → distinctive-characteristics section → architecture summary with BC table → tech stack table → getting started → repository structure → documentation routing → contributing → resources → maintainer block. The skeleton is consistent; the content is project-specific. That structural consistency is the **family standard** this session matches.

CritterMart's distinctness within the family is not its domain (single-seller ecommerce is the most familiar of the four — CritterSupply already covers ecommerce more broadly) but its **process**: CritterMart exists as a teaching reference architecture for *Event Sourcing with Marten* and as a deliberate exercise of a documented spec-driven development pipeline. The other Critter projects use the same pipeline ideas but do not foreground them as a first-class concern. CritterMart should. The README is the place to make that visible, not hide it inside `CLAUDE.md`.

This is a derivative session: no new content is invented. Every claim the README makes is sourced from the orientation material below.

## Goal

Produce a rewritten `README.md` that matches the Critter-family structural skeleton and carries CritterMart-specific content drawn from the orientation artifacts. Target length: **180–280 lines** — between CritterCab's 140 and CritterBids' 240, comparable to CritterSupply's 340 minus the section weight CritterSupply spends on mermaid diagrams (which CritterMart does not yet earn).

Every section must be derivable from the orientation material. No new positioning, no new architectural claims, no new commitments. If a candidate sentence has no source artifact, it does not go in the README.

The README must answer, in order:

1. **What is this?** (one paragraph, no more — purpose + family-of-references context)
2. **Why ecommerce, and what makes this one different from CritterSupply?** (the "Event Sourcing with Marten talk" framing + the SDD-pipeline-as-teaching-artifact framing are the two distinguishers)
3. **What patterns does it exercise?** (a small table of patterns × how the storefront demonstrates each, similar in shape to CritterBids' "Why an Auction Platform?" table but populated from CritterMart's workshop, ADRs, and rules)
4. **What's the architecture?** (three separate services — Catalog, Inventory, Orders — plus the round-one stubbed Identity; shared PostgreSQL with schema-per-service; Wolverine over RabbitMQ for cross-service; PMvH for the Order aggregate)
5. **What's the tech stack?** (table aligned with `CLAUDE.md`'s tech stack table)
6. **What's the demo / talk?** (the live-conference-talk framing — this is the section that most distinguishes CritterMart from its siblings)
7. **How do I run it?** (with honest current-state caveat — `src/` is not yet built out for round one)
8. **What does the repo look like?** (project structure — `docs/` is the heart of the project right now; `src/` is forthcoming)
9. **Where's the documentation?** (table pointing at the layered artifacts: vision, context map, workshop, rules, ADRs, prompts/retros, skills, folder READMEs)
10. **What's the SDD pipeline?** (a short section unique to CritterMart in the family — one paragraph + a pointer to `CLAUDE.md` and `docs/prompts/README.md`)
11. **What's deliberately out of scope for round one?** (reference `docs/vision.md` § "What this deliberately is not" — the README summarizes, the vision doc owns)
12. **How do I contribute / work in this repo?** (point at `CLAUDE.md` and the prompt/retro discipline)
13. **Where do I learn more?** (resource links — Wolverine, Marten, JasperFx, blog, etc.)
14. **Who maintains it?** (the existing maintainer block, kept verbatim or with minor formatting alignment to the family)

## Orientation

Read these in this order before beginning. The first three are the **structural pattern source** (read for shape, not content); everything else is the **content source** (read for what to say).

### Structural pattern source — read first

These three files establish the Critter-family README skeleton. Read them for the structural skeleton (section ordering, badge conventions, table shapes, prose density, tone), **not** for content. Do not copy paragraphs verbatim — match the shape and write CritterMart's own words.

1. **`C:\Code\CritterBids\README.md`** — the most polished of the three. Note the "Why an Auction Platform?" patterns table (this is the shape for CritterMart's section #3), the BC table format (shape for CritterMart's #4), the demo-scenario section (shape for CritterMart's #6), the documentation table (shape for #9), and the explicit Roadmap section.
2. **`C:\Code\CritterCab\README.md`** — the most concise. Note the "Technology Versions" table (with version floors and currents), the "What's Distinctive" short bullet list, the "Status" section honestly naming which slices are implemented end-to-end, and especially the **"Companion Library: JasperFx ai-skills"** section — CritterMart should carry an equivalent section, given the project memory's explicit deferral to the upstream library.
3. **`C:\Code\CritterSupply\README.md`** — the most expansive. Note the "Best suited for:" framing (worth adopting for CritterMart given the talk-anchor positioning), the "Short-List of Patterns, Paradigms, and Principles" (worth adapting — keep it shorter for CritterMart given the deliberately narrower scope), and the BC-status table format (the emoji + status indicators model can be borrowed, though CritterMart has only four BCs and most are forthcoming, so the table will be short).

### Content source — what CritterMart actually says

These are CritterMart's canonical sources. Every claim the README makes is sourced from one of these. Cite where ambiguity might arise; otherwise let the README stand as the elevator pitch and the linked artifact stand as the authority.

4. **`CLAUDE.md`** — the routing layer. Particularly the **Routing Layer → Vision / Tech stack / Architectural non-negotiables / Do Not — round one** subsections (these populate sections #4, #5, #11 of the README), the **Two-Phase Shape** and **Operating Disciplines** sections (these inform section #10 — the SDD pipeline summary), and the **Why Each Piece Exists** table (informs the documentation table in section #9).
5. **`docs/vision.md`** — the canonical source of truth for what CritterMart is, why it exists, and what it deliberately is not. Read in full. The "What this deliberately is not" section is the source for the README's section #11; the "Bounded contexts" and project-purpose sections feed sections #1 and #4.
6. **`docs/context-map/README.md`** — the BC topology and cross-BC relationship vocabulary. Source for section #4's BC table and any cross-BC integration claims the README makes.
7. **`docs/workshops/001-crittermart-event-model.md`** — the four-BC event model, the slice list, the demo flow, and the event vocabulary. The "demo scenario" section #6 draws heavily from the workshop's slice list; the patterns table in section #3 draws from the workshop's adjunct patterns and event vocabulary.
8. **`docs/rules/structural-constraints.md`** — the terse imperative summary of architectural constraints. Useful for the README's architecture and tech-stack sections as a backstop ("if I'm about to claim X about architecture, is X consistent with the rules file?").
9. **`docs/decisions/`** (all ten ADRs) — read at skim level. The README does not enumerate ADRs; it links the folder. But the section #4 architecture summary must not contradict any ADR, so a skim pass before writing #4 is required.
10. **`docs/skills/event-modeling/SKILL.md`** — read for the methodology context. The README may briefly note that Event Modeling is the project's design technique (this is one of the patterns the talk demonstrates); the skill file is the destination for "how a workshop is conducted."
11. **`docs/skills/README.md`**, **`docs/workshops/README.md`**, **`docs/narratives/README.md`**, **`docs/prompts/README.md`**, **`docs/retrospectives/README.md`** — the folder-local READMEs added in PR #1. The root README's documentation section (#9) points at these as the entry points for each folder.

## Out of scope

- **Do not author content not present in the orientation material.** The README is purely derivative. If a candidate sentence has no source artifact, it does not go in.
- **Do not edit `CLAUDE.md`, `docs/vision.md`, any ADR, the workshop, the context map, the rules file, or any folder README.** The root README points at all of these; the root README does not amend them. If reading them surfaces a contradiction or staleness, flag it in the retro's *Outstanding items* — do not silently fix it.
- **Do not add badges for artifacts that do not exist.** No CI workflow badge if there is no CI workflow yet; no License badge if there is no `LICENSE` file in the repo; no test-coverage badge without a coverage report. Verify each candidate badge corresponds to a real artifact before placing it. When in doubt, omit and flag in *Outstanding items*. The honest small badge row beats the impressive-looking one with dead links.
- **Do not author a `LICENSE` file, a `CHANGELOG.md`, a `CONTRIBUTING.md`, a `CODE_OF_CONDUCT.md`, or any other companion file.** Each is a separate session if/when the maintainer chooses.
- **Do not author or wire up GitHub Actions CI.** That is its own session with its own ADR-class decisions.
- **Do not include mermaid diagrams unless the content earns it.** CritterMart has only four BCs (one of which is stubbed for round one). The textual BC table is sufficient. If the future state grows complex enough to earn a diagram, that is a future README update, not this one's.
- **Do not duplicate `docs/vision.md` prose in the README.** The README is the elevator pitch; the vision doc is the canonical source of truth. The README cross-links; it does not restate.
- **Do not copy paragraphs verbatim from any sibling README.** Match the structural shape and write CritterMart's own content. Phrases like "An open-source X built on the Critter Stack" or "What Is X?" are family conventions and may be reused as section headings; entire paragraphs are not.
- **Do not commit any code.** This is documentation.
- **Do not include a `Roadmap` section that commits to specific milestones not already captured in the vision doc or ADRs.** A short *Round-one scope + what's deliberately out* section (the README's #11) is correct; an aspirational multi-quarter roadmap is not.
- **Do not edit the existing maintainer block's content.** Format alignment with the family's bullet-or-dot separator style is fine; the links and the "Erik 'Faelor' Shafer" line stay verbatim.

## Output structure

The rewritten `README.md` follows this skeleton (matches the family standard with CritterMart-specific section #10 added):

1. **Title** — `# CritterMart`
2. **Badge row** — only badges for real artifacts. Anticipated honest set: `.NET 10`, `PostgreSQL / Marten`, `Wolverine`, `RabbitMQ`. Add `License`, `Build`, etc. only if the corresponding artifact exists at time of writing.
3. **Tagline blockquote** — one-line elevator pitch. Anticipated shape: *"An open-source single-seller ecommerce reference architecture and conference-talk anchor, built on the [Critter Stack] and exercising a disciplined spec-driven development pipeline."* The session-runner writes the actual line.
4. **`## What Is CritterMart?`** — one paragraph; family-of-references context; talk-anchor framing; SDD-pipeline-as-teaching-artifact framing. End with one sentence on what makes this project distinct from its siblings.
5. **`## Why a Single-Seller Storefront?`** — short purpose paragraph + a patterns × demonstration table modeled on CritterBids' "Why an Auction Platform?" table. The patterns are drawn from the workshop, ADRs, and rules file; the demonstration column is drawn from the workshop slices and the talk framing.
6. **`## Architecture`** — three-services framing + a BC table (Catalog / Inventory / Orders / Identity-stubbed). Each BC row carries: name, responsibility one-liner, storage, status. Below the table, two or three sentences on cross-BC integration (Wolverine over RabbitMQ; Customer-Supplier between Orders and Inventory per the context map; no synchronous service-to-service HTTP). Link to `docs/context-map/README.md` for the full topology.
7. **`## Tech Stack`** — table aligned with `CLAUDE.md`'s tech-stack table. Concern × Choice.
8. **`## The Talk`** *(or `## Conference Talk Demo` — pick one)* — the live-demo / educational-vehicle framing. Anticipated content: what the talk demonstrates, what the audience sees, the relationship between the demo path and the architecture. This is the section that most distinguishes CritterMart from its siblings.
9. **`## Getting Started`** — prerequisites + run-locally. **Honest current-state caveat required**: `src/` is not yet built out for round one (the design phase is largely complete; implementation slices have not yet started). The section names the *intended* run flow (.NET Aspire orchestration per ADR 004) and clearly marks it as forthcoming.
10. **`## Repository Structure`** — directory tree showing `docs/` heavily and `src/` as forthcoming. Briefly describe what each top-level folder is for.
11. **`## Documentation`** — table pointing at the layered artifacts. Document × Purpose. Includes: `docs/vision.md`, `docs/context-map/README.md`, `docs/workshops/README.md`, `docs/narratives/README.md`, `docs/prompts/README.md`, `docs/retrospectives/README.md`, `docs/skills/README.md`, `docs/rules/structural-constraints.md`, `docs/decisions/`, `CLAUDE.md`.
12. **`## Spec-Driven Development Pipeline`** — short section unique to CritterMart in the family. One paragraph explaining that CritterMart exercises a documented SDD pipeline (vision → context map → workshop → narrative + OpenSpec proposal siblings → prompt → execute + retrospective) with strong operating disciplines (one prompt = one session = one PR, no opportunistic edits, spec-delta closure loop). Link to `CLAUDE.md` for the full treatment and to `docs/prompts/README.md` for the per-session convention.
13. **`## Round-One Scope`** — what's in and what's deliberately out. References `docs/vision.md` § "What this deliberately is not" and `CLAUDE.md` § "Do Not — round one". Short — three or four bullets each side.
14. **`## Companion Library: JasperFx ai-skills`** — mirror the CritterCab section. The project defers to the upstream JasperFx ai-skills library for Critter Stack mechanics; local skills under `docs/skills/` are authored only when CritterMart-specific conventions diverge. Reference `docs/skills/README.md` for the layering rationale.
15. **`## Contributing`** — short. Point at `CLAUDE.md` for conventions and `docs/prompts/README.md` for the one-prompt-one-session-one-PR discipline. If no `CONTRIBUTING.md` or `CODE_OF_CONDUCT.md` exists, do not link them.
16. **`## Resources`** — Wolverine, Marten, JasperFx, blog, etc. Aligned with the family format.
17. **`## Maintainer`** — existing block, kept verbatim (links and "Erik 'Faelor' Shafer" line). Format alignment with family is fine.

## Working pattern

This is a synthesis session, not a workshop. Six passes, run sequentially:

1. **Sibling-skeleton pass.** Read the three sibling READMEs end-to-end. Capture the structural skeleton in a working note (section order, badge conventions, table shapes, prose density, where each sibling diverges from the others and why). Do not write any CritterMart content yet.
2. **Source-content pass.** Read CritterMart's orientation artifacts in the order listed (CLAUDE.md → vision.md → context-map → workshop → rules → ADR skim → skills/README + folder READMEs). Capture, in a working note, which artifact each candidate section will source from. Do not write yet.
3. **Draft pass.** Write the README section by section, top to bottom. For each section, cite the source artifact mentally (or in a working comment to be removed before commit). Target each section's length to match the family's prose density — concise where the family is concise, expanded where the family expands.
4. **Derivative-discipline pass.** Re-read the draft with the question: "is every claim derivable from the orientation material?" Cut any sentence that fails. If a sentence is *almost* derivable but needs a small interpretive leap, mark it explicitly (e.g., "the talk anchor framing is summarized from `CLAUDE.md` and `vision.md`") and confirm the source supports the summary.
5. **Badge-honesty pass.** Verify each badge in the draft corresponds to a real artifact in the repo. Remove badges for things that don't exist. Flag any "this badge would be valuable but the underlying artifact doesn't exist yet" in the retro's *Outstanding items*.
6. **Tightness pass.** Read end-to-end with target length 180–280 lines. If the draft exceeds 280 lines, the synthesis has probably leaked into prose that belongs in the linked artifact. Cut. If the draft is under 180 lines, a required section is probably under-developed (most likely the patterns table in #5 or the documentation table in #11).

Author the retrospective at `docs/retrospectives/docs/002-readme-overhaul.md` before opening the PR. The retro follows the established seven-section format (Outcome summary, What worked, What was harder than expected, Methodology refinements, Outstanding items / next-session inputs, Spec-delta — landed?, optional Process notes) per the in-repo precedent at `docs/retrospectives/rules/001-...md` and `docs/retrospectives/docs/001-...md`.

The session is one PR per `CLAUDE.md`'s "one prompt = one session = one PR" discipline. The PR contains exactly this prompt (already authored and uncommitted at the time the session starts), the rewritten `README.md`, and the retrospective. Nothing else.

## Spec delta

The root `README.md` is rewritten from a 10-line TBD placeholder to a Critter-family-standard README in the 180–280 line range. CritterMart visitors arriving at the GitHub repo gain a first-encounter surface: project framing, architecture, tech stack, talk context, documentation routing, contribution conventions, and resource links — all sourced from existing canonical artifacts, none of which are edited by this session. The README closes a discoverability gap that has been open since the project was bootstrapped.

`docs/retrospectives/docs/002-readme-overhaul.md` is authored as the paired retro. The `docs/` prompt kind gains its second occupant in both `docs/prompts/docs/` and `docs/retrospectives/docs/`, validating that the kind subdirectory created by PR #1 is reusable as anticipated.

Forward-compatibility note to record in the retro: the README is an *elevator pitch* surface, not a documentation replacement. Future canonical-artifact changes (vision.md amendments, new ADRs, new BCs, talk-narrative refinement) earn matching README updates in the same PR — same discipline as the folder READMEs from PR #1. The README is the project's outward face; when the inside changes, the face follows.
