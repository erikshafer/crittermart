# Prompt: Decisions 003 — Saga #2 gate: the ADR-009 revisit + saga-showcase formalization

**Kind**: decision (ADR). Decision-only session — no code, no OpenSpec change, no workshop/narrative edits.
**Source**: `docs/handoffs/saga2-handoff.md` (parent mission, committed 2026-06-30) narrowed by
`docs/handoffs/saga2-session1-adr009-revisit.md` (this session's scope, authored 2026-07-02). Saga #2
(Identity email-change confirmation, EF-Core-backed) is gated on this revisit before Session 2 (workshop
amendment → OpenSpec → narrative → implement) may begin.
**Files touched**: this prompt; `docs/decisions/009-polecat-deferred-for-round-one.md` (amendment
addendum); `docs/decisions/022-convention-sagas-additive-to-pmvh.md` (new ADR); `docs/decisions/README.md`
(index rows + next-number); `docs/prompts/README.md` + `docs/retrospectives/README.md` (decisions kind
counts); `docs/handoffs/saga2-session1-adr009-revisit.md` (landed alongside, previously untracked);
`docs/retrospectives/decisions/003-saga2-adr009-revisit.md`.
**Mode**: solo; one fork resolved with Erik before drafting (ADR shape — two ADRs vs. one vs.
ADR-009-only, via AskUserQuestion with previews).
**Commit subject**: `docs: ADR-009 revisit + ADR 022 saga-showcase policy — Saga #2 gate (#1 of 2)`

## Framing

`docs/decisions/009-polecat-deferred-for-round-one.md` established Identity as the deliberately **boring,
non-event-sourced EF-Core foil** — proof that Wolverine's handler model is persistence-agnostic. Saga #2
gives Identity its **first stateful consumer** (an `EmailChange` saga, EF-Core-backed). Two questions need
settling before any Session-2 build work: (1) does the "boring CRUD" stance survive hosting a saga, and (2)
CritterMart shipped its **first** convention `Wolverine.Saga` in Saga #1 (Inventory replenishment,
slices 2.5–2.7, retro `implementations/035`) without ever minting a dedicated ADR for the additive-not-PMvH
stance — that gap should close before a second saga makes the pattern look precedent-free.

## Goal

Two ADRs, landed together:

1. **ADR-009 amendment** (addendum, not rewrite — ADRs are append-only per `docs/decisions/README.md`).
   Settles: the "deliberately boring CRUD" stance **holds**; the `EmailChange` saga is EF Core doing
   relational things the Wolverine way (a `DbSet`-mapped `Saga`-derived entity, no event sourcing
   introduced) and *extends* rather than reverses the ADR's persistence-agnostic thesis. Cross-references
   the new ADR 022 for the cross-cutting policy. Separately corrects the now-stale "no deployed Identity
   service" clause in the original Decision section — Identity is a real service today (customer registry,
   slices 5.1–5.4, retros `implementations/033`/`034`) — without deleting or rewriting the original text.
2. **New ADR 022 — Convention Sagas Are Additive to PMvH.** Formalizes, retroactively, the decision Saga #1
   never got a dedicated ADR for: CritterMart uses convention `Wolverine.Saga` **additively**, alongside PMvH
   (ADR 007), never as a PMvH conversion. Names both backing stores (Marten for Inventory, EF Core for
   Identity) as proof the saga store is swappable, extending ADR 009's persistence-agnostic thesis to sagas.
   States the guard from the research: a saga must stay "EF Core/Marten doing relational/document things the
   Wolverine way," not drift into re-implementing event sourcing.

Both ADRs indexed in `docs/decisions/README.md` (022 added, 009's status line unchanged — still Accepted,
now amended twice).

## Spec delta

**New/amended ADR + ADR cross-references only** — no workshop or narrative amendment this session (that is
Session 2's delta, gated on this one landing). Retro confirms.

## Locked decisions (fork resolved with Erik, 2026-07-02)

1. **Two ADRs**, not one combined ADR and not an ADR-009-only fold-in. Rationale: the saga-showcase policy
   is repo-wide (Saga #1 lives in Inventory, not Identity) and misattributing it to an Identity-scoped ADR
   would misfile it for future cross-referencing from Workshop 001/002 or a future Saga #3. ADR-009 gets a
   scoped addendum (consistent with its existing ADR-015/016 amendment precedent); ADR 022 carries the
   cross-cutting policy standalone.

## Orientation files (read first, in order)

1. `docs/handoffs/saga2-handoff.md` — parent mission, saga shape, standing warnings.
2. `docs/handoffs/saga2-session1-adr009-revisit.md` — this session's narrowed scope and constraints.
3. `docs/decisions/009-polecat-deferred-for-round-one.md` — the ADR under revisit (already carries one
   amendment blockquote — the ADR-015/016 precedent for how to append, not rewrite).
4. `docs/decisions/007-process-manager-via-handlers-for-order.md` — the PMvH pattern ADR 022 is additive to.
5. `docs/research/wolverine-saga-feasibility.md` — Candidate #2 (Identity email-change) + the promotion-path
   table naming "an ADR (the saga-showcase decision; cross-references ADR 007)" as Saga #1's owed artifact.
6. `docs/research/identity-ef-core-first-class-expansion.md` — open question #2 ("does the contrast
   survive?"), the question this session settles.
7. `docs/retrospectives/implementations/035-slices-2-5-2-7-replenishment-saga.md` — what Saga #1 actually
   shipped (Marten-stored saga, additive to the refusal path, no PMvH conversion) — source material for
   ADR 022's Context section.
8. `docs/decisions/README.md` — ADR format convention (terse prose: Context/Decision/Consequences) and the
   next free number (022 at authoring time — the E2E research's informal "ADR 022" earmark yields per the
   session-1 handoff's note).

## Working pattern

Draft ADR-009's addendum first (smaller, scoped) → draft ADR 022 (Context pulls from retro 035 + the
feasibility research; Decision states the additive stance + swappable-store proof; Consequences names the
guard and gates Session 2) → index both in `docs/decisions/README.md` → bump the `decisions` kind counts in
`docs/prompts/README.md` and `docs/retrospectives/README.md` → retro → commit → PR.

## Out of scope

- **No code, no `openspec` change, no workshop/narrative edits.** Anything implementation-shaped that
  surfaces (e.g., the exact `EmailChange` saga field shapes) goes into the retro's outstanding-items list for
  Session 2, not into this PR.
- **No rewrite of ADR-009's original Context/Decision/Consequences prose.** Append-only — addendum
  blockquote only, mirroring the existing ADR-015/016 amendment.
- **No Session-2 artifacts** (Workshop 002 amendment, OpenSpec proposal, narrative, prompt, implementation).
  Those begin only after this PR merges and a fresh Session-2 handoff is authored.
- **No live-verify.** Decision-only session, no code to run.
