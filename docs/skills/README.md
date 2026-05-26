# docs/skills/

Component-scoped patterns local to CritterMart. See [CLAUDE.md § Skills](../../CLAUDE.md) for the routing-layer treatment of where skills sit in the design pipeline.

Local skills under this directory exist **only when a CritterMart-specific convention diverges from the upstream skill** in the JasperFx Critter Stack ai-skills library. The upstream library is the source of truth for Marten, Wolverine, and Polecat patterns — do not duplicate upstream content here. A local skill is authored when the project's convention is genuinely different from what upstream documents, or when the skill embeds CritterMart-specific pipeline integration (BC list, artifact paths, in-project examples) that upstream cannot carry.

## Per-skill convention

- **Directory:** one subdirectory per skill, named in kebab-case after the skill's identifier (e.g., `event-modeling/`).
- **Filename:** `SKILL.md` inside the skill's subdirectory.
- **Frontmatter:** YAML with `name` (kebab-case identifier matching the directory), `description` (one-line summary used for discovery), `cluster` (e.g., `core`), and `tags` (array). See [`event-modeling/SKILL.md`](event-modeling/SKILL.md) as the in-repo precedent.
- **Body:** level-1 heading + prose. Conventional sections include `## When to apply this skill`, technique sections, `## Pipeline Integration`, `## Quick Reference: Common Mistakes to Catch`, and `## See also` (with both downstream skills and external references).

## Current local skills

- [`event-modeling/SKILL.md`](event-modeling/SKILL.md) — the event-modeling methodology skill. Authored as a project skill because it embeds CritterMart-specific pipeline integration (the four BCs, artifact paths, in-project workshop precedent, project-named slices, adjunct event-modeling patterns adopted for CritterMart) that the upstream library does not.

## Forthcoming companions

- `_template/SKILL.md` — authoring template referenced in [CLAUDE.md § Skills](../../CLAUDE.md). Lands when the first need for a second local skill surfaces, or when a template gap is explicitly recognized. Not pre-created — empty templates are ceremony.
- `DEBT.md` — gaps surfaced during sessions but deferred to a future skill-tidy PR. Lands when the first deferred gap is recorded.

## Cross-references

- [CLAUDE.md § Skills](../../CLAUDE.md) — routing-layer framing for skills, including the "defer to upstream, write only what diverges" discipline for round one.
- Upstream JasperFx ai-skills library — global install state and reinstall instructions live in machine-scoped memory (not version-controlled in this repo); pipeline-scoped install instructions live at the library's own install docs.
