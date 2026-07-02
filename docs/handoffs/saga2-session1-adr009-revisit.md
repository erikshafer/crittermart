# CritterMart — Handoff: Saga #2, Session 1 of 2 — the ADR-009 revisit (decision-only)

> **Durable handoff** (version-controlled at `docs/handoffs/`), authored 2026-07-02. Scope: **Session 1 only.**
> Parent mission doc: [`saga2-handoff.md`](saga2-handoff.md) — read it in full; this doc narrows it to one
> session and does not repeat its content (saga shape, API gotchas, standing warnings all live there).

## Mission for this session

Produce the **ADR-009 revisit** — a **decision-only session, no code, no workshop amendment**. This is the
gate in front of Saga #2 (Identity email-change, EF-Core-backed `Wolverine.Saga`). Session 2 (workshop
amendment → OpenSpec → narrative → implement → retro) does **not** start until this session's PR merges and a
fresh session-2 handoff is authored.

## The decision to settle

`docs/decisions/009-polecat-deferred-for-round-one.md` established Identity as the deliberately **boring,
non-event-sourced EF-Core foil**. Saga #2 gives Identity its **first stateful consumer**. Settle, in ADR form:

1. **Does "deliberately boring CRUD" still hold** once Identity hosts a saga — or is the saga the intended
   next proof of the persistence-agnostic thesis ("EF Core doing relational things the Wolverine way")?
   **Guard (binding, from the research):** the saga must NOT drift into re-implementing event sourcing on SQL.
2. **Absorb/formalize the saga-showcase decision** Saga #1 never got a dedicated ADR for: *CritterMart uses
   convention `Wolverine.Saga` additively, alongside PMvH (ADR 007) — never as a PMvH conversion.* Today that
   decision is scattered across `docs/research/wolverine-saga-feasibility.md`, Workshop 001 v1.12 § 2, and
   retro `docs/retrospectives/implementations/035-slices-2-5-2-7-replenishment-saga.md`.

Open shape question the session may resolve either way: one amended ADR-009 + one new saga-showcase ADR, or a
single new ADR that does both with a status touch on 009. Recommend one; let Erik pick (see working style).

## Constraints and guidelines

- **No code. No `openspec` change. No workshop/narrative edits.** Deliverables are ADR text only (plus the
  session's prompt/retro pair). Anything implementation-shaped that surfaces goes into the retro's
  outstanding-items list for Session 2 — not into this PR.
- **Pipeline ceremony:** this is a `decisions`-kind session — author `docs/prompts/decisions/NNN-{slug}.md`
  before executing, and `docs/retrospectives/decisions/NNN-{slug}.md` before the PR opens. One session, one PR.
- **Spec delta (name it honestly in the prompt):** *new/amended ADR + ADR cross-references* — no workshop or
  narrative amendment this session (that is Session 2's delta). Retro confirms.
- **ADR format:** follow the established format in `docs/decisions/` (read 007 and 009 as exemplars; index the
  new entry in `docs/decisions/README.md`). Take the **next free number at authoring time** — check the folder;
  don't assume. (Note: the E2E research informally earmarks "ADR 022" — if numbering collides, the earmark
  yields; it was aspirational, not reserved.)
- **Amending ADR-009:** whatever shape is chosen, ADR-009 itself must end up honest — its "Identity is not a
  running service" era is already history (registry shipped, slices 5.1–5.4); the revisit should leave a
  status/addendum trail, not silently rewrite it.
- **Branch/commit:** `docs/{slug}` branch; this is a new artifact, so a `docs:` subject, **not** `tidy:`.
- **Design-return note:** an ADR session satisfies the design-interleave expectation ahead of Session 2's
  implementation work — say so in the prompt so the cadence bookkeeping is explicit.

## Orientation files (read first, in order)

1. [`saga2-handoff.md`](saga2-handoff.md) — parent mission, saga shape, Saga #1 gotchas, standing warnings.
2. `docs/decisions/009-polecat-deferred-for-round-one.md` — the ADR under revisit.
3. `docs/decisions/007-process-manager-via-handlers-for-order.md` — the pattern the saga stance is additive to.
4. `docs/research/wolverine-saga-feasibility.md` — § Candidate #2 (Identity email-change) + the promotion path.
5. `docs/research/identity-ef-core-first-class-expansion.md` — the parent "first-class EF-Core Identity" idea;
   its open question #2 ("does the contrast survive?") is literally this session's question.
6. `docs/retrospectives/implementations/035-slices-2-5-2-7-replenishment-saga.md` — what Saga #1 actually
   taught; source material for the saga-showcase formalization.

## Working style (Erik's standing preferences)

At the genuine fork (one ADR vs. two; how far ADR-009's own text is reshaped), present **2–4 concrete options
with previews and a recommendation** via AskUserQuestion and let Erik decide — do not act on a lean. Prefer
tool-backed artifacts over freeform where a tool exists (n/a for ADRs — plain Markdown in `docs/decisions/` is
the convention). No live-verify needed this session (no code).

## Definition of done

- [ ] New ADR (and any ADR-009 amendment) authored, indexed in `docs/decisions/README.md`
- [ ] Prompt + retro pair in `docs/prompts/decisions/` and `docs/retrospectives/decisions/`
- [ ] Retro confirms the named spec delta (ADR-shaped only) landed; Session-2 inputs listed
- [ ] PR opened (`docs:` subject); after merge: `/post-merge`, then author the **Session 2 handoff** as the
      next `docs/handoffs/` doc before any Saga #2 implementation begins

## Repo state at authoring (2026-07-02)

- `main` @ `edad2c7` (docs: Saga #2 parent handoff, PR #118). A core-docs staleness sweep
  (README/CLAUDE.md/vision.md) was in the working tree when this handoff was authored — if it landed, the
  routing docs already describe the four-service topology and Saga #1; trust them.
- Known pending, unrelated to this session: Dependabot #106/#107/#109; draft PR #103; an orphaned
  `docs/research/README.md` row referencing a never-written `critter-stack-release-notes-2026-06-30.md`.
- Standing warnings (Wolverine 6.16.0 pin, CritterWatch trial expiry 2026-07-10, demo-knob teardown) — see
  the parent handoff § Standing warnings; none block a decision-only session.

## Suggested skills

- `blurb` — to mint the kickstart line pointing at this file.
- `post-merge` — after the session's PR merges (step 1 of the close-out ritual).
- `handoff` — at session close, to author the Session-2 handoff (persist to `docs/handoffs/`, not temp).
- No Critter Stack knowledge skill is needed for the ADR itself; if saga mechanics need re-verification,
  the parent handoff's doc links (wolverinefx.net saga guides) beat re-deriving.
