---
retrospective: 014
kind: docs
prompt: docs/prompts/docs/014-design-return-section-5-1-shipped.md
deliverable: docs/workshops/001-crittermart-event-model.md (§ 5.1 v1.11 amendment + frontmatter sync + Document History row), openspec/ (archived enrich-order-status-view → order-lifecycle 8→9), client/src/orders/orderSchema.ts (3 path-ref fixes), docs/prompts/README.md, docs/retrospectives/README.md
date: 2026-06-17
mode: solo synthesis (design-return cadence interleave; § 5.1 flip-style sub-fork resolved Option A via AskUserQuestion)
session-runner: Claude (Opus 4.8)
---

# Retrospective — Docs 014: Design-Return Workshop § 5.1 "Aspirational → Shipped" Flip

## Outcome summary

The **design-return cadence interleave** owed after the two implementations since the #65 design-return
(#66 Stock read/write split, #67 the `OrderStatusView` enrich). With all four round-one BCs workshopped and
the storefront spine (W1→W4) shipped, the cadence-satisfying move was a `tidy:` design-return closing the one
**reverse spec delta** the last slice honestly fenced — the same shape docs/013 took one cadence earlier. At
the one genuine sub-fork (the § 5.1 flip *style*) the maintainer chose **Option A — a new v1.11 append-only
amendment** (via AskUserQuestion with previews), declining the in-place-edit-of-v1.10 and the hybrid-pointer
variants. Three threads landed in one PR:

- **Workshop 001 § 5.1 v1.11 amendment block** (edit, append-only). A dated blockquote appended *after* the
  frozen v1.10 block, flipping W4's `Placed … UTC` line and per-reason `cancelled` copy from v1.10's
  *aspirational, not bound* to **bound**: slice 025 (PR #67) shipped `placedAt` (genesis `OrderPlaced` append
  metadata via `Create(IEvent<OrderPlaced>)`) + `cancelReason` (folding `OrderCancelled.Reason`), making
  `OrderStatusView` the additive superset `{ id, customerId, status, lines, total, placedAt, cancelReason }`.
  The v1.10 block — and the frozen § 5.1 wireframe text — are left intact; the flip is appended. Frontmatter
  synced (`version` v1.10 → **v1.11**, `date` → 2026-06-17, `status` extended) and a v1.11 § 9 Document
  History row added adjacent to v1.10 (the fence→shipped pair now reads in sequence; the pre-existing
  v1.9/v1.10 ordering quirk left untouched).
- **OpenSpec `enrich-order-status-view` archived** (CLI). Ticked the change's `tasks.md` to 10/10 first (the
  work shipped in #67; the archived snapshot should be honest), then `openspec archive … -y` folded the +1
  ADDED requirement (*Surface placement time and cancellation reason in the order view*) into
  `openspec/specs/order-lifecycle/spec.md` (8 → **9**) and moved the change to
  `archive/2026-06-17-enrich-order-status-view/`. Post-archive: spec valid `--strict`, `No active changes
  found`, requirement count 9.
- **`client/src/orders/orderSchema.ts` path-ref sweep** (edit). The three stale
  `src/CritterMart.Orders/Order/…` comment refs (lines 6, 21, 48) flipped to `Ordering/` (the ADR 021
  verb-folder rename); slice 025's own line-35 `CancelReason` comment was already `Ordering/`. Comment-only;
  `npm run build` (`tsc --noEmit` + `vite build`, 243 modules) green.
- **Index READMEs reconciled.** `docs/` count 13 → **14** in both `docs/prompts/README.md` and
  `docs/retrospectives/README.md`, population note extended with this design-return flip + archive.

No code, no tests, no new OpenSpec change, no live boot. Tree clean before; the only changes are the workshop
+ openspec archive + orderSchema + two-index edits and this prompt/retro pair.

## What worked

- **Recon-before-trust caught a handoff off-by-one before it became a wrong number in the spec record.** The
  handoff (and retro 025's prose) said the archive takes `order-lifecycle` "9 → 10 requirements." The actual
  pre-archive count was **8** (`grep -c '^### Requirement:'`), so the archive produced **9**, not 10. Counting
  the live spec before writing the amendment kept the v1.11 block, the § 9 row, and the index notes all
  citing the true 8→9 — the same "verify against the artifact, not the prior claim" discipline retro 012/013
  named for README counts and the frontmatter `version`, now applied to a requirement count.
- **The append-only convention dissolved the named "fork" into a convention-bound pick — but it was still
  surfaced, per the lifted-autonomy default.** All ten prior amendments (v1.1–v1.10) freeze the original text
  and append a dated block; v1.10 itself says "Frozen § 5.1 wireframe text left intact (append-only)," and its
  W4 bullet was *written as a fence* ("until it ships"). Option A (a v1.11 that records "it shipped") is the
  exact continuation. The retro-025 heuristic (*surface a fork only after confirming no worked precedent
  settles it*) would have justified just picking A — but the standing collaborate-at-forks default and the
  handoff's explicit framing of the flip-style as "the only genuine sub-fork" tipped it to a quick previewed
  AskUserQuestion. The owner picked A; the round-trip was one question with three concrete previews.
- **Ticking the change's `tasks.md` before archiving made the archived snapshot honest.** The change carried
  `0/10` tasks despite shipping in #67 (the boxes were never ticked at merge), and task 5.4 *was* this very
  tidy. Flipping all ten to `[x]` before `openspec archive` means the durable `archive/` record reflects that
  the work landed, rather than freezing a misleading 0/10 — a small honesty edit on the same file-set being
  archived, not an opportunistic out-of-scope change.
- **`replace_all` on the right token swept all three refs without touching the correct one.** The stale token
  `CritterMart.Orders/Order/` cannot match the already-correct `CritterMart.Orders/Ordering/` (the slash after
  `Order` differs), so a single `replace_all` fixed lines 6/21/48 and left line 35 alone — confirmed by a
  post-edit grep showing zero `Order/` and four `Ordering/` refs.

## What was harder than expected

- **`openspec archive` needs `-y` in a non-interactive shell, and the global-vs-npx gotcha is real.** The
  command prompts for confirmation; the Bash tool can't answer, so `-y` is mandatory (not just convenient).
  And the CLI is the **global** `openspec` shim — `npx openspec@latest` fails with "could not determine
  executable to run." Both were pre-flagged in the handoff; heeding them up front avoided the start-of-#67
  fumble. (The archive also emitted a non-blocking `proposal.md` "Why section > 1000 characters" warning —
  cosmetic, on a change now in `archive/`, not worth a retroactive edit.)
- **Placing the v1.11 § 9 row required a judgment call on a pre-existing ordering quirk.** The history table
  runs v1.0…v1.8, then **v1.10, then v1.9** (v1.9's row physically trails v1.10 despite the lower version/older
  date — a prior-session insertion artifact). I placed v1.11 immediately after v1.10 so the fence→shipped pair
  is adjacent and readable, and left the v1.9/v1.10 swap alone (reordering a frozen history table is an
  opportunistic edit). Named here so the placement is auditable rather than silent.

## Methodology refinements that emerged

- **A requirement count is derived data — re-derive it from the spec, never carry it from a handoff.** Retro
  012/013 established this for README counts and the workshop frontmatter `version`; this session extends it to
  the OpenSpec requirement count. The handoff's "9→10" was a carried claim; the spec said 8. The honest move
  when a number will land in a durable artifact is one `grep -c` against the source, every time — claims about
  counts drift exactly like index counts and `tasks.md` checkboxes do.
- **Honest archival is part of the archive step, not a separate scope.** When a change ships before its
  `tasks.md` is ticked (the common reality of a consolidated slice PR), tick the boxes *as part of* archiving
  — the archived snapshot is the durable record and should match what happened. This pairs with retro 025's
  observation that the consolidated one-PR slice can leave its own bookkeeping (here, the change's task list)
  trailing the code; the design-return tidy is where that bookkeeping closes.
- **The design-return can be satisfied by closing a *fenced* delta, not only an *unrecorded* one.** docs/013
  closed an unrecorded reverse delta (the workshop hadn't caught up to shipped W3/W4); docs/014 closes a
  *deliberately-fenced* one (retro 025 named the flip and deferred it post-merge). Both count as the cadence
  interleave — the rule is about *interleaving design attention*, and honoring a named fence is design
  attention. The fence-then-close rhythm (v1.10 fences → slice 025 ships → v1.11 closes) is the spec-delta
  closure loop running across three PRs, visible end-to-end in one § 5.1 amendment thread.

## Outstanding items / next-session inputs

- **The next implementation is an open pick again** — the design-return budget is replenished. Ranked from
  retro 025 / the `next-pickup` memory: **(1)** the `AddToCart` null-snapshot 500 hardening — the only actual
  *defect* (a missing/misnamed `productSnapshot` → HTTP 500 instead of 400 at `CartLine.cs:19`; precisely
  diagnosed in retro 024); a small `validation-boundary` slice. **(2)** the **"My Orders" list** (Gap #3) — a
  new read (`GET /orders?customerId=`) + screen; value-driven, zero urgency. **(3)** the **OTel / in-browser
  visual pass** — the distributed-trace dashboard visual for the 2nd talk; the wire is proven, this is browser
  confirmation; rises as the talk nears. **(4)** the **cart identity-transport harmonization** — architectural
  tidy (4 cart commands / 3 identity transports), a backend + OpenSpec change so an *implementation*, not a
  design-return.
- **Cadence reset.** #66 + #67 were the two implementations; this docs/014 design-return resets the counter.
  The next 2–3 implementations run before another interleave is due — and since all four BCs are workshopped,
  that interleave will again be a narrative or a `tidy:`, not a new BC workshop.
- **Carry-forwards (unchanged, non-blocking):** the flaky `PaymentAuthorizationTests.a_declined_payment…`
  (Wolverine teardown race — `gh run rerun <id> --failed`); no frontend CI job; focus-ring enhancement; the
  NU1507 two-nuget-source warning; Docker container grouping (DX note); **CritterWatch trial expires
  2026-07-10**.

## Spec-delta — landed?

**Named delta landed.** The prompt named a **v1.11 workshop § 5.1 amendment block + Document History row**
flipping the v1.10-fenced W4 placed-at + per-reason cancel copy from *aspirational* to **bound** (slice 025 /
PR #67), plus the **OpenSpec `order-lifecycle` +1 ADDED requirement via archive** (8→9), the comment-path
sweep, and accurate index counts. All landed as named: § 5.1 carries the v1.11 block (append-only, v1.10 left
frozen), frontmatter synced to v1.11, a v1.11 § 9 row recorded; `order-lifecycle` validates `--strict` at 9
requirements with the new one merged and the change in `archive/`; `orderSchema.ts` comments point at
`Ordering/` with the `client` build green; both index READMEs read `docs/` (14). This **closes the tidy
retro 025 fenced** — the four-step loop (prompt names → session executes → retro confirms → spec records) now
runs end-to-end across three PRs: v1.10 (docs/013) fenced it, slice 025 (#67) shipped it, v1.11 (this
session) records it. The workshop now agrees with Narrative 005 v1.7 (which led) and the shipped
`OrderStatusView`. No code, no tests, no new slice.
