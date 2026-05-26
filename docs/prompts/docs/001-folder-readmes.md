# Prompt: Docs 001 — Folder READMEs for Routing-Layer Narrowing

**Kind**: maintenance / docs surface (folder-local convention READMEs)
**Files touched**: `docs/skills/README.md` (new); `docs/workshops/README.md` (new); `docs/narratives/README.md` (new); `docs/prompts/README.md` (new); `docs/retrospectives/README.md` (new); `docs/retrospectives/docs/001-folder-readmes.md` (new)
**Mode**: solo synthesis — read CLAUDE.md, the in-repo prompt/retro/workshop/skill precedent files, and the existing folder occupants; distill each folder's local convention; write each README as a thin narrowing-layer surface.
**Commit subject**: `tidy: docs — add folder READMEs for routing-layer narrowing`

## Framing

CritterMart's CLAUDE.md Routing Layer carries an *Artifact layer map* table whose rows point at the folders that hold each artifact kind. The table answers "which folder is for what." It does not answer "what shape do files in this folder take" — that detail lives either in CLAUDE.md's design-pipeline prose (per-kind format conventions) or implicitly in the existing files of each folder.

A fresh AI session lands at a known folder via the routing table, then has to either re-read CLAUDE.md for format conventions or pattern-match against the folder's existing files. Both work, but neither is fast, and pattern-match-against-existing-files breaks down the first time a folder gains its first file (no example to copy from).

A *folder-local README* sits between the routing layer and the individual artifacts: it carries the folder's naming convention, frontmatter shape (where applicable), required-sections list, precedent-file pointer, and any "why this folder looks the way it does" notes that don't belong in CLAUDE.md but do belong somewhere session-discoverable. This session adds five such READMEs across the convention-heavy folders.

The pattern's two failure modes are well-understood: (1) READMEs that become file indexes rot the moment a contributor adds a file and forgets to update the README; (2) READMEs that re-explain what CLAUDE.md already says are dead weight. The mitigation: each README in this session is *convention-shaped*, not *index-shaped*, and *folder-local-detail-shaped*, not *project-purpose-shaped*. CLAUDE.md remains the routing-and-purpose layer; the new READMEs are the convention-and-shape layer.

## Goal

Author five folder READMEs as thin, convention-shaped narrowing surfaces, plus a retrospective that closes the spec delta. Each README must answer "if I'm a session-runner landing in this folder, what local convention do I need to know that CLAUDE.md doesn't already tell me?" — and nothing more.

The READMEs are:

1. **`docs/skills/README.md`** — clarifies that local skill files under this directory are CritterMart-specific divergences from the upstream JasperFx ai-skills library (not duplications), names the one current local skill (`event-modeling/`), documents the per-skill directory + `SKILL.md` filename convention and the frontmatter shape observed in `event-modeling/SKILL.md` (name, description, cluster, tags), and references `_template/SKILL.md` and `DEBT.md` as forthcoming-when-first-needed rather than pre-creating them.
2. **`docs/workshops/README.md`** — documents the two workshop kinds CLAUDE.md names (Event Modeling outputs as the primary; Domain Storytelling captures as the secondary, explicitly skipped for round one), the file-naming pattern `NNN-{bc}-event-model.md` for Event Modeling, the per-slice output discipline (slice number, reads-from list, writes-to list, GWT happy path, explicit failure paths), and a pointer to `docs/skills/event-modeling/SKILL.md` for *how* a workshop is conducted.
3. **`docs/narratives/README.md`** — documents file naming `NNN-{actor}-{journey}.md`, the frontmatter (status, version, slices covered), the Moment structure (context / interaction / system response), the per-session version-bump-and-Document-History rule, and the sibling-artifact relation to OpenSpec proposals (narrative is the human-readable companion to the machine-readable spec; both must agree). Marks the folder as forthcoming for round one's first journey.
4. **`docs/prompts/README.md`** — documents the subdirectory-per-kind structure (currently `rules/`, `workshops/`; with `narratives/`, `skills/`, `decisions/`, `implementations/`, and `docs/` arriving as their first prompt of that kind lands), the file-naming convention `NNN-{slug}.md`, the required sections per CLAUDE.md § 5, the in-repo prompt format observed in `prompts/rules/001-...md` (top-line title, bolded inline metadata, prose sections), the "one prompt = one session = one PR" discipline, and the frozen-at-session-start rule.
5. **`docs/retrospectives/README.md`** — documents the structure-mirrors-prompts/ pattern, the YAML frontmatter shape observed in `retrospectives/rules/001-...md` (retrospective number, kind, prompt path, deliverable path, date, mode, session-runner), the seven required sections per CLAUDE.md § 6, the pre-PR authoring rule, and the **spec-delta — landed?** line as the mandatory closing section.

## Orientation

Read these in this order before beginning:

1. **`CLAUDE.md`** — particularly the Routing Layer (the *Artifact layer map* table this work supplements), the design-pipeline prose for each affected folder (§§ 2, 3, 4b, 5, 6 cover workshops, narratives, prompts, retros), the Skills supporting-layer section, the *Why Each Piece Exists* table (it informs the cross-references between the new READMEs), and the Operating Disciplines section (one-prompt-one-PR and no-opportunistic-edits constrain the PR shape).
2. **`docs/prompts/rules/001-round-one-structural-constraints.md`** — the in-repo prompt-format precedent. The format observed (top-line `# Prompt: {Kind} {NNN} — {Title}`, bolded inline metadata, `## Framing` / `## Goal` / `## Orientation` / `## Out of scope` / `## Output structure` / `## Working pattern` / `## Spec delta` sections) is the project convention that `docs/prompts/README.md` must document. Read it as the example.
3. **`docs/retrospectives/rules/001-round-one-structural-constraints.md`** — the in-repo retro-format precedent. The YAML frontmatter shape and the seven-section structure are the project convention that `docs/retrospectives/README.md` must document. Read it as the example.
4. **`docs/workshops/001-crittermart-event-model.md`** — the in-repo workshop-format precedent (consult skim-level: filename pattern, slice-numbering scheme, GWT structure, reads-from/writes-to fields). These observed conventions are documented in `docs/workshops/README.md`.
5. **`docs/skills/event-modeling/SKILL.md`** — the in-repo skill-format precedent. The frontmatter shape (`name`, `description`, `cluster`, `tags`), the level-1 heading + prose body convention, and the See-Also section pattern are observed here and must be referenced in `docs/skills/README.md` as the local skill shape.
6. **`docs/context-map/README.md`** — referenced indirectly: the folder's only file IS a README.md, which establishes that "README.md when the README itself is the artifact" is a valid project pattern. That informs the `docs/skills/README.md` framing for `_template/SKILL.md` and `DEBT.md` as forthcoming separate artifacts rather than content folded into the folder README.

## Out of scope

- Do not edit `CLAUDE.md`. The *Artifact layer map* table's *forthcoming* annotations are stale relative to reality (all five affected folders are populated), but a CLAUDE.md update is a separate `tidy: housekeeping` session. Flag in the retro; do not edit here.
- Do not author `docs/skills/_template/SKILL.md`. The template lands in a separate session when the first need for a second local skill surfaces or when a template gap is explicitly recognized. The README references it as forthcoming; do not create it.
- Do not author `docs/skills/DEBT.md`. DEBT is a row-accumulating artifact that earns its existence when the first skill-gap is deferred; pre-creating an empty DEBT.md is ceremony.
- Do not author `docs/specs/README.md`. Per scope discussion this session, OpenSpec-proposal folder conventions are deferred until the first proposal lands and the upstream OpenSpec tool's conventions are observed in practice.
- Do not author `docs/decisions/README.md`. ADR conventions are universally understood; the folder is self-describing via its file numbering and the standard ADR pattern.
- Do not author `docs/research/README.md`. The folder has no conventions to document; exploratory work is by definition shape-free.
- Do not author `docs/context-map/README.md`. The folder's only file IS that README; it is the artifact, not the convention layer.
- Do not author `docs/rules/README.md`. The folder has one file (`structural-constraints.md`) whose header paragraph already documents its purpose; a separate README would duplicate it.
- Do not list files in any new README as a directory index. Index-shaped READMEs rot. Convention-shaped READMEs do not.
- Do not duplicate CLAUDE.md prose. The READMEs supplement, not replace.
- Do not commit any code. This is documentation.

## Output structure

Each README follows the same shape:

1. **Top-line heading** — `# docs/{folder}/`.
2. **Purpose paragraph(s)** — two to four sentences answering "what is this folder for, and how does it fit into the pipeline?" Cross-reference CLAUDE.md and (where applicable) the routing-layer source-of-truth (the design-pipeline prose section, the supporting-layers section). Brief — not a re-explanation of CLAUDE.md.
3. **Convention block(s)** — the folder's local rules: file naming, frontmatter shape, required sections, in-repo format precedent. Use level-2 (`##`) headings for sub-conventions where the convention is multi-part.
4. **Cross-references** — a level-2 section pointing at sibling layers (the routing layer at CLAUDE.md, the related skill file for workshops, the sibling artifact folder for prompts/retros, the upstream library for skills).
5. **Current population** — for folders that are forthcoming-for-round-one (narratives) or partially populated (workshops, prompts, retrospectives, skills), one line naming the current population state at *count + first occupants* granularity. Not an index — a state-of-the-folder pointer that does not need editing on every file addition.

Target length per README: 30–80 lines. If a README crosses 100 lines, the synthesis has probably leaked into prose that belongs in CLAUDE.md or the relevant skill file.

## Working pattern

This is a synthesis session, not a workshop. Four passes:

1. **Read pass.** Read the orientation sources in the order listed. Note the convention each folder has already established (or will need to establish, for narratives, which has no in-repo precedent yet).
2. **Draft pass.** Write each README in turn: skills first (the strongest "why-this-shape" case — the local-vs-upstream framing is genuinely load-bearing), then workshops, then narratives, then prompts, then retrospectives. The order matters: skills/README.md establishes the "upstream + local divergences" framing; workshops/README.md leans on it (workshops have an upstream methodology in the event-modeling skill); narratives/README.md is similar in shape to workshops; prompts/README.md and retrospectives/README.md are paired and reference each other.
3. **Tightness pass.** Each README is read end-to-end with the question "does this duplicate CLAUDE.md, or does it add the folder-local detail CLAUDE.md doesn't?" Cut any line that fails the test.
4. **Cross-reference pass.** Confirm `prompts/README.md` and `retrospectives/README.md` reference each other; `workshops/README.md` references the event-modeling skill; `skills/README.md` references the upstream library; `narratives/README.md` references its sibling OpenSpec proposal pattern.

Author the retrospective before opening the PR. The session is one PR per CLAUDE.md's "One prompt = one session = one PR" discipline.

## Spec delta

Five new folder READMEs and a retrospective land in the same PR. The `docs/prompts/docs/` and `docs/retrospectives/docs/` subdirectories appear with their first occupants (this prompt and the retro). `docs/skills/` gains its first non-skill artifact (a folder-meta README, not a SKILL.md). Downstream sessions landing in any of the five folders gain a thin convention layer between CLAUDE.md and the artifacts themselves — pattern-match-against-existing-files is no longer the only path to the folder's local shape.

CLAUDE.md's *Artifact layer map* `forthcoming` annotations are not corrected this session (out of scope); the retro flags the staleness for a future `tidy: housekeeping` session.

Forward-compatibility note recorded in the retro: each README's purpose is folder-local convention, not pipeline philosophy. If a future change to CLAUDE.md alters a convention (e.g., a new required prompt section, a different frontmatter field), the matching folder README updates in the same PR.
