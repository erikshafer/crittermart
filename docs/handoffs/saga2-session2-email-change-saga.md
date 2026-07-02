# CritterMart — Handoff: Saga #2, Session 2 of 2 — Identity email-change saga (build)

> **Durable handoff** (version-controlled at `docs/handoffs/`), authored 2026-07-02.
> Parent mission doc: [`saga2-handoff.md`](saga2-handoff.md) — read it in full; this doc narrows it to Session
> 2 and does not repeat its content (saga shape, API gotchas, standing warnings all live there).
> Prior session: [`saga2-session1-adr009-revisit.md`](saga2-session1-adr009-revisit.md) — the ADR-009 revisit,
> merged as PR #119 (`main` @ `d49a110`).

## Mission for this session

The gate is open. Build **Saga #2** — CritterMart's second `Wolverine.Saga`, EF-Core-backed, in Identity:
`RequestEmailChange` → opens an `EmailChange` saga (holds `PendingEmail`, schedules a 24h
`EmailChangeTimeout`) → `ConfirmEmailChange` within the window applies the change, or the timeout drops it.
Full pipeline: **Workshop 002 amendment → OpenSpec proposal → narrative → prompt → implement → retro.**

## What Session 1 settled (read the ADRs, not this summary, for the actual reasoning)

- [`docs/decisions/009-polecat-deferred-for-round-one.md`](../decisions/009-polecat-deferred-for-round-one.md)
  — second amendment (2026-07-02): Identity's "boring CRUD" stance holds against hosting a saga; the
  "no deployed Identity service" clause is now marked stale (registry shipped, slices 5.1–5.4).
- [`docs/decisions/022-convention-sagas-additive-to-pmvh.md`](../decisions/022-convention-sagas-additive-to-pmvh.md)
  (new) — **binding guard on this session's design**: the `EmailChange` saga must do relational things the
  Wolverine way (`DbSet`-mapped entity, plain `Handle`/`StartOrHandle`, `TimeoutMessage`) and must **not**
  drift into re-implementing event sourcing on SQL. Treat this as a constraint to design against, not a
  question to re-litigate.

## Open sequencing question this session should resolve early (a genuine fork — don't just pick one)

Saga #1's actual precedent (Inventory replenishment) **split across two PRs**, not one:

- A design-return PR (#112) shipped the **Workshop 001 amendment + OpenSpec proposal** (`stock-management`
  capability, requirements 2.5–2.7) alone.
- A later implementation PR (retro `implementations/035`) shipped **`design.md` + `tasks.md` + the narrative
  + all the code**, in one PR.

That is the closest in-repo precedent for a saga specifically. But [[feedback-consolidate-slice-prs]] (memory)
says: from slice 1.3 on, Erik prefers a whole vertical slice (narrative + proposal + implementation) in
**one** PR over the split cadence — a "move faster" override of the per-artifact-class default. Those two
signals point opposite ways here. Present this as an explicit fork (2–3 options with previews, per
[[feedback-options-with-previews]]) before committing to a shape:

1. **Mirror Saga #1's split** — design-return PR (workshop amendment + OpenSpec proposal) first, implementation
   PR (narrative + design.md/tasks.md + code + retro) second. Matches the closest precedent; keeps the
   design-return cadence bookkeeping unambiguous.
2. **Consolidate per [[feedback-consolidate-slice-prs]]** — workshop amendment + OpenSpec + narrative +
   prompt + implementation all in one PR. Faster; matches Erik's stated general preference; diverges from
   Saga #1's own precedent for the same kind of work (saga slices specifically).
3. Something in between (e.g., workshop amendment rides in with the OpenSpec+narrative, but code is a
   separate PR) — only if a genuine reason to split differently surfaces once the slices are modeled.

## Orientation files (read first, in order)

1. [`saga2-handoff.md`](saga2-handoff.md) — parent mission: saga shape, plumbing already de-risked (EF-Core
   saga storage rides the existing `AddDbContextWithWolverineIntegration` call, no new infra), Saga API
   gotchas (`StartOrHandle` not split `Start`/`Handle`, mandatory `NotFound(msg)` statics, `[SagaIdentity]`
   lives in `Wolverine.Persistence.Sagas`), standing warnings (Wolverine pin, CritterWatch trial expiry,
   post-talk demo-knob teardown).
2. `docs/decisions/022-convention-sagas-additive-to-pmvh.md` — the guard binding this session's saga design.
3. `docs/decisions/009-polecat-deferred-for-round-one.md` — both amendments, for the "why Identity, why now."
4. `docs/workshops/002-identity-event-model.md` — the Identity event model to amend. Slices run through
   **5.4** (customer registry + OHS/PL integration); the email-change slices are net-new, starting at **5.5**.
   Section 1 already frames Identity as "deliberately boring, not auth" — the amendment extends that frame,
   doesn't fight it.
5. `docs/research/wolverine-saga-feasibility.md` § Candidate #2 — the saga blueprint (illustrative shape,
   both plumbing unknowns resolved from docs, "why this flow and not name/address edits").
6. **Saga #1 reference implementation** — `src/CritterMart.Inventory/Stock/Replenishment.cs` + siblings;
   `openspec/changes/archive/2026-06-30-slices-2-5-2-7-replenishment-saga/`; retro
   `docs/retrospectives/implementations/035-slices-2-5-2-7-replenishment-saga.md` — the template for
   `design.md`/`tasks.md` shape and the unit/integration test split.
7. Identity code: `src/CritterMart.Identity/Program.cs`, `Customers/IdentityDbContext.cs`,
   `Features/RegisterCustomer.cs`.

## Working style (Erik's standing preferences — carried from memory)

Resolve the sequencing fork (above) via AskUserQuestion with previews before committing; present options +
a recommendation at any other genuine fork rather than acting on a lean
([[feedback-collaborate-on-decisions]]); prefer tool-backed artifacts over freeform (`openspec` CLI for the
proposal — [[feedback-prefer-tool-backed-over-freeform]]); when a session touches non-trivial code, boot the
real stack and live-verify end-to-end via the demo runbook, and drive the flow yourself rather than handing
back a URL ([[feedback-live-verify-after-changes]], [[feedback-drive-demo-flows]]).

## Definition of done

- [ ] Sequencing fork resolved with Erik before drafting
- [ ] Workshop 002 amended with the email-change slices (starting 5.5), GWT scenarios included
- [ ] OpenSpec proposal authored + validated (`openspec validate --strict`) — capability is `customer-registry`
      (per CLAUDE.md's one-capability-per-aggregate rule; email-change is a `Customer`-aggregate behavior, not
      a new aggregate)
- [ ] Narrative authored/amended threading the email-change journey
- [ ] `EmailChange` saga implemented per the ADR 022 guard; unit + integration tests (mirror Saga #1's split)
- [ ] Live-verified against the real Aspire stack: request → confirm → applied; request → timeout → dropped
- [ ] Prompt + retro pair(s) authored per whatever PR shape the sequencing fork resolves to
- [ ] PR(s) opened; after merge, `/post-merge` and decide whether Saga #2 needs its own closing handoff or
      whether the mission is complete at that point

## Repo state at authoring (2026-07-02)

- `main` @ `d49a110` (PR #119 merged: ADR-009 revisit + ADR 022). Clean tree.
- Standing warnings unchanged from the parent handoff: do **not** bump Wolverine past 6.16.0 (CritterWatch
  1.0.0-beta.1 coupling); CritterWatch trial expires **2026-07-10** — budget live-verify time before then;
  post-talk, delete the four AppHost demo knobs (`Payment__DeclineOverAmount`, `Payment__AuthDelay`,
  `Orders__PaymentTimeout`, `Inventory__ReplenishTimeout`).

## Suggested skills

- `blurb` — to mint the kickstart line pointing at this file (already done for this handoff).
- `event-modeling` (in-repo skill, `docs/skills/event-modeling/SKILL.md`) — for the Workshop 002 amendment.
- `opsx:propose` / `openspec-propose` — for the OpenSpec proposal.
- `critterstack-arch-new-project-wolverine-efcore`, `wolverine-handlers-efcore`,
  `wolverine-handlers-declarative-persistence` — EF-Core saga implementation patterns.
- `find-docs` (ctx7) for Wolverine EF-Core saga specifics; the parent handoff's direct-WebFetch fallback
  (`wolverinefx.net/guide/durability/efcore/sagas.html`) if ctx7 is quota-blocked again.
- `post-merge` → `handoff` → `blurb` — the close-out ritual once this session's PR(s) land.
