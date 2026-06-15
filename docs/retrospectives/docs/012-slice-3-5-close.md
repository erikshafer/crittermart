---
retrospective: 012
kind: docs
prompt: docs/prompts/docs/012-slice-3-5-close.md
deliverable: openspec/changes/archive/2026-06-15-slice-3-5-view-open-cart/ (archive move), openspec/specs/shopping-cart/spec.md (delta fold), docs/workshops/001-crittermart-event-model.md (§6 v1.9 amendment), docs/narratives/README.md, docs/prompts/README.md, docs/retrospectives/README.md
date: 2026-06-15
mode: solo synthesis (post-merge close; no scope fork)
session-runner: Claude (Opus 4.8)
---

# Retrospective — Docs 012: Slice 3.5 Close

## Outcome summary

The owed post-merge close for slice 3.5 (`GET /carts/mine`, PR #50). A squash merge does not run
`openspec archive`, so the change sat active (7/8) on `main` with its delta un-folded — the canonical
`shopping-cart` spec showing 7 requirements while the code satisfied 8. This tidy archived the change
and reconciled the workshop with what shipped. Done as the **first half of the frontend-bootstrap
session**, kept as its own `tidy: docs` PR (stacked under the bootstrap branch) per the resolved
two-PR structure. Shipped:

- **OpenSpec change archived.** `openspec archive slice-3-5-view-open-cart` moved the change to
  `archive/2026-06-15-slice-3-5-view-open-cart/` and folded its delta into
  `openspec/specs/shopping-cart/spec.md` — the **8th requirement, "Read the Customer's open cart"**
  (the customer-keyed `CartView` read, three scenarios: `200` / `404` no-open-cart / `400` no-identity).
  `openspec validate --specs` green (4 passed).
- **Workshop 001 § 6 v1.9 amendment** (edit, append-only). A block under slice 3.5 recording the two
  faithfulness divergences the implementation made against the modeled GWTs: **(1)** the identity
  transport — left open at modeling time as "query-param vs. header is the slice's call" — resolved to
  the **`X-Customer-Id` header** (the localized-promotion rationale); **(2)** a **third GWT** beyond the
  two modeled — missing/blank identity → `400`, distinct from the no-open-cart `404`. Plus the
  implementation shape (a `CartView` LINQ query, not `[ReadAggregate]`; `IResult` return; new
  `ViewMyCart.cs`; no edit to `AddToCart.cs`) and the confirmation of **no new
  event/command/projection/index**. A v1.9 Document History row records it.
- **Index READMEs reconciled.** Narratives README 005 → **v1.1** ("partly built" — keystone read
  shipped, screen pending). Prompts + retrospectives READMEs: `narratives` 3→4, `implementations` 14→15,
  and `docs` 10→**12** — the count had silently lagged the round-one-close (011) pair as well, so the
  bump also picked that up plus this 012 pair.

No code, tests, or frozen historical files touched. Tree clean before; the only changes are the archive
move + spec fold + four docs edits + this prompt/retro pair.

## What worked

- **Archive-first ordering.** Running `openspec archive` before writing the amendment meant the workshop
  block could point at the *already-folded* spec ("`shopping-cart/spec.md`, 8 requirements") as a fact,
  not a promise. The CLI did the 7→8 fold deterministically; the amendment just narrates it.
- **Append-only on the workshop body.** The v1.5 amendment block was the format precedent — preserve the
  modeled GWT text (including the now-resolved "query-param vs. header" line as the modeling-time
  record), append the resolution. The diff is purely additive; the modeled scenario set is untouched,
  which is the honest shape for "the model was right; here's how it was realized."
- **`design.md` carried the divergences forward intact.** The two faithfulness notes were staged in the
  change's `design.md` during the slice-3.5 session specifically so this close could lift them without
  re-deriving — and they transferred verbatim. The discipline of staging faithfulness notes at
  implementation time paid off exactly as intended at close time.

## What was harder than expected

- **The README `docs/` count was wrong before I touched it.** It read `(10)` but eleven `docs/`
  prompt+retro files already existed (001–011) — the 011 round-one-close pair had never been counted.
  The handoff named only the 005-narrative and 015-impl lags, so the 011 lag was a surprise found by
  listing the directories rather than trusting the prompt. Bumping to 12 (011 + this 012) made the field
  correct rather than just "less wrong." Lesson below.

## Methodology refinements that emerged

- **Verify index counts against `ls`, never against the prior count.** A README count field drifts
  silently when a session forgets to bump it (here, the 011 pair). The honest move when editing a count
  is to re-count the directory, not to increment the stale number — a stale `+1` perpetuates the lag.
  Counts are derived data; reconcile them to the source (the files) each time they're touched.
- **A squash-merged slice owes a deterministic post-merge close.** PR #50 squash-merged, which left the
  openspec change active. This is the second time the squash/archive interaction has surfaced; the close
  is mechanical (`openspec archive` + the workshop amendment the `design.md` faithfulness notes pre-stage)
  and is cheapest run immediately after merge, before the next session's context buries it. Worth a
  standing "did the last squash-merged slice get archived?" check at session start.

## Outstanding items / next-session inputs

- **The frontend-bootstrap PR is this session's second half** (stacked on this branch): `client/` Vite
  SPA, Aspire `AddViteApp` (three service URLs injected), AppHost-injected CORS origin, dependabot `npm`
  block, the live per-service CORS preflight assertion (deferred since retro 011), and the
  `docs/skills/frontend/SKILL.md` `[planned]`-marker convergence.
- **Live per-service CORS preflight** — still owed at the moment of writing this retro; it lands in the
  bootstrap half now that a real origin (`http://localhost:5173`) is injected and assertable.
- **`tidy: encode-ceremony-rule`** remains overdue (carried since retro 013) — not in this session's scope.

## Spec-delta — landed?

**Named delta landed.** The prompt named: the openspec change archived with its delta folded into
`shopping-cart` (the 8th requirement); a v1.9 workshop § 6 amendment + Document History row resolving
slice 3.5's open identity-transport question (→ header) and recording the added `400` GWT; and accurate
index READMEs. All landed as named. The `shopping-cart` capability went 7→8 requirements via the archive
fold (promotion of a requirement authored in the now-archived change — no *new* modeling). No workshop
*slice* was added or removed. Workshop 001 records the amendment in its `## Document History` (v1.9),
closing the prompt → execute → retro → spec-record loop for slice 3.5.
