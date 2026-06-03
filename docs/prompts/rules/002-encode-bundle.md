# Prompt: Rules 002 — Encode Bundle (ceremony rule, one-capability-per-aggregate, projection-conventions skill)

**Kind**: convention encoding (rules) — the pipeline's first `tidy: encode` session
**Files touched**: `CLAUDE.md` (two new convention passages); `docs/rules/structural-constraints.md` (→ v1.3); `docs/skills/marten-projection-conventions/SKILL.md` (new); `docs/skills/DEBT.md` (row 1 → Drained); `docs/skills/README.md` (current-skills list); `docs/workshops/001-crittermart-event-model.md` (frontmatter `version:` only); `docs/workshops/README.md` (frontmatter-versioning convention sentence); `docs/prompts/README.md` + `docs/retrospectives/README.md` (population counts); `docs/retrospectives/rules/002-encode-bundle.md` (new)
**Mode**: solo synthesis — the rule texts and skill content already exist in retrospectives 007–010, retro implementations/013, DEBT.md row 1, and the shipped Orders projection code; the session lifts them into durable convention surfaces. No new decisions are made; three forks were resolved with the user before this prompt was frozen.
**Commit subject**: `tidy: encode — ceremony rule, one-capability-per-aggregate, projection-conventions skill`

## Framing

Round one's modeled implementation set is complete (Workshop 001 v1.5, PR #42). What remains before the vision-level conversation that opens round two is the encoding debt the implementation loop accumulated: three conventions that have each earned their keep through repeated, retro-confirmed use, but which still live only in retrospective archaeology. A session-runner who wants any of them today must re-derive them from four or five retros — exactly the failure mode CLAUDE.md's "Why Each Piece Exists" table predicts for missing rules and skills.

The three conventions and their evidence:

1. **The tidy ceremony rule** — *a tidy that authors spec content (workshop/narrative amendment, spec Purpose) carries the full prompt/retro pair; a purely mechanical tidy (file moves, counts) may run light.* Settled with the user in retro docs/007, held in 008, declared overdue-to-encode in 009, held twice more in 010. Five consecutive confirmations.
2. **One capability per aggregate** — OpenSpec capability granularity follows the aggregate (or document type), not the bounded context. Evidence: `product-catalog` (Catalog's one document type, confirmed 3/3 in retro implementations/003), `stock-management` (Inventory's one aggregate, retro implementations/004), and the decisive case — Orders carrying **two** capabilities (`shopping-cart` for Cart, `order-lifecycle` for Order; retros docs/004, implementations/013). Encoding was deliberately deferred until the Orders shape was final; it now is.
3. **The CritterMart Marten projection conventions** — DEBT.md row 1: instance-registered projections carrying config, `IEvent<T>` metadata folds, conditional deletes via `ShouldDelete`, the load-bearing `partial` keyword, and `Identity<IEvent<T>>` multi-stream routing. Each used 3× across the shipped Orders/Inventory code; the `partial` omission cost slice 3.4 its only failure.

A fourth, smaller item rides along: Workshop 001's frontmatter `version:` field is stale at v1.0 while its Document History reaches v1.5 (surfaced in retro docs/010). The fix pairs with encoding the convention that prevents recurrence (workshop frontmatter tracks Document History, matching the narrative convention).

Three forks were resolved with the user before this prompt was frozen: (1) conventions are encoded to **both surfaces, paired** — CLAUDE.md prose + structural-constraints.md flat imperative; (2) this session files under **rules/002**; (3) the frontmatter fix **and** its convention are both in scope.

## Goal

Lift the three conventions out of retrospective archaeology into their durable homes, so that:

- A session-runner reading CLAUDE.md § Operating Disciplines learns the ceremony rule without reading any retro.
- A session-runner authoring an OpenSpec proposal learns the capability-granularity convention from CLAUDE.md § 4a.
- `docs/rules/structural-constraints.md` (the AI-orientation surface) carries flat imperatives for both, plus the workshop frontmatter-versioning rule, each citing its evidence.
- A session-runner writing a Marten projection in this codebase finds the five CritterMart projection conventions — with in-repo code references — in one local skill file, and DEBT.md row 1 closes.
- Workshop 001's frontmatter agrees with its own Document History.

## Spec delta

This session's canonical specs are the convention surfaces themselves (no narrative or workshop GWT content changes):

- **CLAUDE.md** gains the tidy ceremony rule (§ Operating Disciplines) and the capability-granularity convention (§ 4a OpenSpec proposal).
- **`docs/rules/structural-constraints.md` → v1.3**: three new SDD-pipeline rules (ceremony, capability granularity, workshop frontmatter versioning), each with retro/precedent cites; Document History row.
- **`docs/skills/marten-projection-conventions/SKILL.md`** is created (the project's second local skill); **DEBT.md row 1 → Drained**.
- **Workshop 001 frontmatter `version:` v1.0 → v1.5** (value fix only; no body changes); `docs/workshops/README.md` records the frontmatter-versioning convention.

## Orientation

Read in this order:

1. **Retro `docs/retrospectives/docs/007-slice-4-6-doc-followups.md`** (Methodology refinements) — the ceremony rule's original settled wording.
2. **Retros `docs/008`, `docs/009`, `docs/010`** — the rule holding 2×–5×; 010's Outstanding section names this session's exact scope.
3. **Retros `implementations/003`, `implementations/004`, `docs/004`, `implementations/013`** — the capability-granularity evidence chain (Catalog 3/3 → Inventory → Orders' two aggregates).
4. **`docs/skills/DEBT.md` row 1** — the skill's content inventory.
5. **The shipped projection code** — `src/CritterMart.Orders/Order/OrdersAwaitingPayment.cs`, `Cart/CartsAwaitingActivity.cs`, `Cart/CartAbandonmentReport.cs`, `Cart/CartView.cs`, `Program.cs` (registration), `tests/CritterMart.Orders.Tests/OrdersAwaitingPaymentProjectionTests.cs` (the `new Event<T>(data) { Timestamp = … }` test pattern) — the skill describes what is actually there, by file reference.
6. **`docs/skills/event-modeling/SKILL.md`** (frontmatter + section shape) and **`docs/skills/README.md`** (per-skill convention) — the local-skill format precedent.
7. **`docs/rules/structural-constraints.md`** — current v1.2 content and rule shape (terse imperative + parenthetical cite).
8. **`docs/prompts/rules/001-round-one-structural-constraints.md`** — the rules-kind prompt precedent this prompt mirrors.

## Working pattern

Sequential passes, each producing one deliverable group:

1. **CLAUDE.md pass.** Add the ceremony rule as a new subsection under § Operating Disciplines (after "`tidy:` commit subjects for maintenance" — it qualifies that discipline). Add the capability-granularity convention as a short paragraph in § 4a (OpenSpec proposal). Match the surrounding prose density; no new sections beyond these two.
2. **Rules-file pass.** Append three rules to § SDD pipeline discipline in structural-constraints.md, each under ~20 words with cites; bump frontmatter to v1.3; add the Document History row.
3. **Skill pass.** Author `docs/skills/marten-projection-conventions/SKILL.md` following the event-modeling skill's frontmatter shape and the skills README's per-skill convention. Sections: when to apply; the five conventions (instance registration, `IEvent<T>` metadata folds, conditional deletes, `partial` is load-bearing, `Identity<IEvent<T>>` multi-stream routing), each with its in-repo file reference and the upstream-skill deferral note; the test-construction pattern; common mistakes. Update DEBT.md row 1 status to Drained (pointing at the skill); update skills README's current-skills list and debt note.
4. **Workshop frontmatter pass.** `version: v1.0` → `version: v1.5` in Workshop 001's frontmatter (nothing else in that file). Add the frontmatter-versioning convention sentence to `docs/workshops/README.md` § Output discipline.
5. **Index pass.** Update `docs/prompts/README.md` and `docs/retrospectives/README.md` population counts (rules 1 → 2).
6. **Retro pass.** Author `docs/retrospectives/rules/002-encode-bundle.md` (CLAUDE.md § 6 format) before opening the PR.

## Out of scope

- **No edits to Workshop 001's body** — the frontmatter `version:` value is the only change to that file. The `status: Draft` field is also arguably stale but was not named by retro 010; if judged worth fixing, it is surfaced in the retro, not edited.
- **No edits to any narrative, ADR, openspec spec, or the context map.** This session encodes process and code conventions; it does not touch domain specs.
- **No code changes.** The skill documents shipped code; it does not refactor it.
- **No new ADR.** None of the three conventions meets the ADR threshold (none spans BCs, none has a non-obvious tradeoff that isn't already recorded in the retros the rules cite).
- **No upstream-skill duplication.** The local skill records only CritterMart conventions and defers Marten API mechanics to the upstream `marten-projections-single-stream` / `marten-projections-multi-stream` skills, per the skills README discipline.
- **No vision.md or context-map edits** — the round-two/frontend conversation is the next session, not this one.
