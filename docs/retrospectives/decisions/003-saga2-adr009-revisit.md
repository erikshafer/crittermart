---
retrospective: 003
kind: decisions
prompt: docs/prompts/decisions/003-saga2-adr009-revisit.md
deliverable: docs/decisions/{009-polecat-deferred-for-round-one.md (amendment),022-convention-sagas-additive-to-pmvh.md (new)}, docs/decisions/README.md, docs/prompts/README.md, docs/retrospectives/README.md, docs/handoffs/saga2-session1-adr009-revisit.md
date: 2026-07-02
mode: solo; one fork resolved with Erik via AskUserQuestion before drafting
session-runner: Claude (Sonnet 5)
---

# Retrospective — Decisions 003: Saga #2 gate — the ADR-009 revisit + ADR 022 saga-showcase policy

## Outcome summary

Shipped the gate Saga #2 (Identity email-change confirmation, EF-Core-backed) sits behind: an **ADR-009
amendment** and a **new ADR 022**, landed together per Erik's fork resolution. The amendment settles the
revisit question — Identity's "deliberately boring CRUD" stance **holds**; the `EmailChange` saga is EF
Core doing relational things the Wolverine way and extends, rather than reverses, ADR 009's
persistence-agnostic thesis — and separately corrects the ADR's now-stale "no deployed Identity service"
clause (the customer registry shipped in slices 5.1–5.4) without rewriting the original append-only text.
ADR 022 formalizes, retroactively, the undocumented decision behind Saga #1 (Inventory replenishment, retro
`implementations/035`): CritterMart uses convention `Wolverine.Saga` additively alongside PMvH (ADR 007),
never as a conversion, with two backing stores (Marten, EF Core) as deliberate proof the saga store is
swappable. Both ADRs are indexed in `docs/decisions/README.md`; the `decisions` kind counts in
`docs/prompts/README.md` and `docs/retrospectives/README.md` are bumped 2 → 3. No code, no `openspec`
change, no workshop/narrative edit — decision-only, as scoped.

## What worked

- **The two-vs-one-ADR fork was worth surfacing explicitly, and the answer wasn't obvious in advance.** The
  handoff flagged this as "the genuine fork" and it earned that framing: ADR 022's policy is repo-wide
  (Saga #1 lives in Inventory, has nothing to do with Identity), so folding it into an Identity-scoped ADR-009
  addendum would have misfiled it for the next contributor cross-referencing from Workshop 001 or a future
  Saga #3. Presenting three concrete previews (two ADRs / one combined ADR / ADR-009-only) rather than
  describing the choice abstractly made the scoping argument visible before any drafting time was spent.
- **The existing ADR-015/016 amendment on ADR 009 was a ready-made template.** ADR 009 already carried one
  blockquote amendment; appending a second in the same style (rather than inventing a new amendment
  mechanic) kept the append-only convention honest with zero design overhead.
- **Reading retro 035 directly, not just the research note, sourced ADR 022's Context accurately.** The
  research note (`wolverine-saga-feasibility.md`) describes the *plan*; retro 035 confirms what actually
  shipped (Marten saga storage, separate from the `Stock` stream, additive to the existing refusal path) —
  the ADR's Context cites the shipped reality, not the pre-implementation sketch.

## What was harder / notable

- **Deciding how much of ADR-009's staleness to touch was a judgment call resolved by the append-only rule
  itself, not a fresh decision.** The handoff flagged that ADR-009's "Identity is not a running service" era
  is history, but didn't specify how far to correct it. `docs/decisions/README.md`'s explicit append-only
  rule ("the original is not deleted or rewritten") settled it without needing a second AskUserQuestion: a
  new addendum blockquote naming the stale clause and pointing at the two retros that made it stale, nothing
  rewritten in the original Context/Decision/Consequences.
- **The E2E research's informal "ADR 022" earmark could have collided but didn't.** `docs/research/e2e-*`
  notes floated ADR 022 as a placeholder number; this session's decisions/README.md check confirmed 022 was
  still free at authoring time and took it, per the session-1 handoff's explicit "the earmark yields" note.
  Flagging here so a future E2E-strategy session isn't surprised to find 022 taken — its own ADR lands at 023
  or later.

## Methodology refinements

- **A repo-wide policy decision and a bounded-context-scoped decision are different shapes even when they
  arrive on the same day for the same reason.** The instinct to bundle "does Identity's stance survive" and
  "sagas are additive to PMvH" into one ADR because they were prompted by the same event (Saga #2) was the
  wrong axis to bundle on. The right axis is scope: one decision is Identity-local, the other is a codebase-
  wide pattern policy that predates and outlives Identity. Worth checking scope explicitly, not just
  chronology, before deciding whether two decisions share one ADR.
- **A cross-cutting ADR that "formalizes retroactively" should say so in its own Context, not just in the
  retro.** ADR 022 names Saga #1's undocumented status directly in its Context section so a reader of the ADR
  alone (not this retro) understands why the ADR exists now rather than when Saga #1 shipped.

## Outstanding / next-session inputs

- **Session 2 is now unblocked**: Workshop 002 amendment (email-change slices) → OpenSpec proposal →
  narrative → prompt → implement → retro. Per the parent handoff, author a fresh Session-2 handoff at
  `docs/handoffs/` before that work begins — don't resume directly off this retro.
- **ADR 022's guard is binding on Session 2's design**, not a re-litigable question: the `EmailChange` saga
  must stay relational-things-the-Wolverine-way (a `DbSet`-mapped entity, plain `Handle`/`StartOrHandle`,
  `TimeoutMessage`) and must not drift toward re-implementing event sourcing on SQL.
- **This session satisfies the design-return cadence** ahead of Session 2's implementation work, as the
  session-1 handoff pre-declared — an ADR session is itself a design interleave, not deferred bookkeeping.
- **Carry-forwards (unchanged, non-blocking):** do NOT bump Wolverine past 6.16.0 (CritterWatch 1.0.0-beta.1
  coupling); CritterWatch trial expires 2026-07-10; post-talk delete the four AppHost demo knobs
  (`Payment__DeclineOverAmount`, `Payment__AuthDelay`, `Orders__PaymentTimeout`, `Inventory__ReplenishTimeout`).
  None of these block this session or Session 2's start.

## Spec-delta — landed?

**Named delta landed, exactly as scoped.** The prompt named "new/amended ADR + ADR cross-references only" —
no workshop or narrative amendment this session. Delivered: ADR-009 gained its second amendment addendum;
ADR 022 was newly authored and indexed; both cross-reference each other and ADR 007/ADR 009 as appropriate.
No workshop, narrative, OpenSpec, or code touched. Four-step closure: **prompt named → session executed →
this retro confirms → the decision is durable in `docs/decisions/` itself** (ADRs, unlike workshops/
narratives, have no separate "spec" to sync back into — the ADR *is* the record).
