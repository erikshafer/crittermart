---
retrospective: 013
kind: docs
prompt: docs/prompts/docs/013-design-return-workshop-reconcile.md
deliverable: docs/workshops/001-crittermart-event-model.md (§ 5.1 v1.10 amendment + frontmatter sync + Document History row), client/README.md (## Layout refresh), docs/prompts/README.md, docs/retrospectives/README.md
date: 2026-06-16
mode: solo synthesis (design-return cadence interleave; scope fork resolved Option 1 via AskUserQuestion)
session-runner: Claude (Opus 4.8)
---

# Retrospective — Docs 013: Design-Return Workshop § 5.1 Reconciliation

## Outcome summary

The **design-return cadence interleave** owed after four consecutive frontend implementations (#60 W1,
#61 W2-edits, #62 W3, #64 W4 — the #63 Order read/write split was itself a `refactor:`/implementation).
With no un-modeled bounded context and no missing narrative, the cadence-satisfying move was a `tidy:`
design-return that closes the *reverse* spec delta the four implementations opened: two § 5.1 wireframe
claims in the frozen event-model workshop had drifted behind shipped reality. At session start the
maintainer chose **Option 1 — workshop reconciliation + `client/README.md` refresh, bundled** (via
AskUserQuestion with previews), declining the standalone-① and the ①+②+③ variants. Shipped:

- **Workshop 001 § 5.1 v1.10 amendment block** (edit, append-only). A dated blockquote appended under the
  W4 bullets recording two faithfulness corrections: **(1)** W3's place-order response carries **only
  `{ orderId }`**, not `{ orderId, status }` — the placement has only just kicked off the cross-BC outcome,
  so W3 reads the order back (`GET /orders/{orderId}`) for status/total (the correction **Narrative 005
  v1.5 already carried**; the workshop was the lone straggler); **(2)** W4's `Placed … UTC` timestamp and
  per-reason `cancelled` copy are **backend gaps** — the shipped `OrderStatusView` is
  `{ id, customerId, status, lines, total }` with `OrderCancelled` → bare `cancelled`
  (`OrderStatusView.cs:44`), so both are fenced to a future "enrich `OrderStatusView`" slice. The frozen
  W3/W4 wireframe text is left intact; the corrections are appended. Frontmatter synced
  (`version` v1.8 → **v1.10**, `date` → 2026-06-16, `status` → storefront spine W1→W4 shipped) and a v1.10
  § 9 Document History row added.
- **`client/README.md` `## Layout` refresh** (edit). The stale block — a `src/routes/` folder and a
  "HomePage bootstrap placeholder; W1–W4 screens follow" that no longer exist — replaced with the real
  **feature-folder** map (`catalog/` · `cart/` · `orders/` colocating page + `queryOptions` + Zod schema +
  mutations, with shared infra at the root) plus a **W1→W4 route map** table read off `router.tsx`.
- **Index READMEs reconciled.** `docs/` count 12 → **13** in both `docs/prompts/README.md` and
  `docs/retrospectives/README.md`, population note extended with the design-return reconciliation.

No code, no tests, no OpenSpec change. Tree clean before; the only changes are the workshop + README +
two-index edits and this prompt/retro pair.

## What worked

- **Recon-before-options caught two handoff imprecisions before they became wrong work.** Reading the
  artifacts first (not trusting the handoff's framing) surfaced that (a) candidate ③ `tidy:
  encode-ceremony-rule` is **already encoded** in CLAUDE.md — the § *Tidy ceremony rule* even carries its
  own "…before being encoded here" provenance line — so it is a genuine no-op, not deferred work; and (b)
  the README didn't *lack* a Layout section (the handoff said "no feature-folder map") — it *had* one that
  was **stale**, describing a `routes/` folder that no longer exists. Both corrections shaped the
  AskUserQuestion options honestly: ③ was presented as a verified no-op, and ② was scoped as a refresh,
  not an addition.
- **The narrative-led reconciliation was the honest shape.** Narrative 005 (Moment 4 + its v1.5 "load-
  bearing reconciliation" row) already carried the `{ orderId }`-only correction; the workshop was the
  straggler. The amendment block says exactly that — "the workshop is the lone straggler catching up" —
  rather than presenting the correction as new. The four-step closure loop (prompt names → session
  executes → retro confirms → spec records) ran in reverse here: the *code* led, the narrative followed in
  #62, and the workshop closes last.
- **Append-only on the frozen § 5.1 wireframes.** The v1.1 / v1.2 / v1.5 amendment blocks were the format
  precedent — the W3 `{ orderId, status }` bullet and the W4 `Placed … UTC` line stay as the modeling-time
  record; the corrections are an appended dated block. The diff is purely additive; the modeled scenario
  set is untouched, which is the honest shape for "the model sketched it; here is how it was realized."
- **Source-confirmed the W4 gaps, not schema-confirmed.** The amendment cites `OrderStatusView.cs:44`
  (the `OrderCancelled` → bare `cancelled` fold) and the actual record shape — the same read the #64 retro
  flagged as worth doing. Writing "no cancel reason on the wire" against the fold itself, not against an
  inference, keeps the gap note load-bearing for the future enrich slice.

## What was harder than expected

- **The workshop frontmatter `version` had silently lagged at v1.8 while the Document History was at
  v1.9.** The slice-3.5 close (docs/012) added the v1.9 history row but never bumped the frontmatter
  `version` field. Found by reading the frontmatter and the history together rather than trusting either
  alone — exactly the failure mode retro 012 named for the README counts ("verify against `ls`, not the
  prior value"), now recurring one field over. Syncing to v1.10 (the new latest row) corrects the lag in
  passing; the v1.10 history row notes it explicitly so the correction is auditable rather than silent.

## Methodology refinements that emerged

- **Verify carry-forwards before scoping them in — a carried-forward TODO can be already-done.** `tidy:
  encode-ceremony-rule` rode several handoffs as "overdue," but the work had already landed in CLAUDE.md.
  A carry-forward is a *claim about the past*, and like an index count it drifts; confirm it against the
  current artifact before budgeting a session for it. This session retires it: it is a no-op, not debt.
- **A workshop frontmatter `version` is derived data — reconcile it to the latest Document History row,
  never to its own prior value.** The v1.8 field lagged because a prior session bumped the history but not
  the field. The honest move when touching the frontmatter is to set `version` to the newest history row,
  the same way a README count is re-derived from `ls`. Both are the same lesson (retro 012) in two places.
- **The design-return cadence is satisfiable by a reverse-spec-delta reconciliation when there's no new
  design to do.** The cadence rule names "a new narrative | the next BC's workshop | a skill-tidy or
  design-tidy PR." With the storefront spine complete and no un-modeled BC, the fit was the third option:
  a `tidy:` that brings the frozen design artifacts back into sync with shipped code. The cadence is about
  *interleaving design attention*, not necessarily *producing new design* — catching the canonical model
  up to reality counts.

## Outstanding items / next-session inputs

- **On-deck implementation — the Stock pilot (the design-return replenishes the budget).** The last ADR
  020/021 rollout: split `StockLevelView` into a `StockLevel` write aggregate + a dedicated read projection
  in Inventory, mirroring the Cart (#59) + Order (#63) template (no folder rename, no PMvH). Now that the
  cadence is satisfied, this is the next `refactor:`/implementation. It needs an Aspire boot + the orphan
  ritual (kill `CritterMart.*` survivors before `dotnet run` or the build-DLL lock bites); the flaky
  `PaymentAuthorizationTests` host-teardown race (`ChannelClosedException`) reruns green with
  `gh run rerun <id> --failed`.
- **The "enrich `OrderStatusView`" slice (named by this amendment).** Surface a `PlacedAt` from
  `OrderPlaced` and a cancel reason carried on `OrderCancelled`, so the W4 wireframe's timestamp + per-
  reason copy become bound rather than aspirational. A projection change, round-two.
- **`tidy: encode-ceremony-rule` — RETIRED.** Verified already encoded in CLAUDE.md § *Tidy ceremony rule*
  (with its own "…before being encoded here" provenance). No longer a carry-forward; future handoffs
  should drop it.
- **Carry-forward (unchanged, non-blocking)**: the "My Orders" list (Gap #3); the cart identity-transport
  harmonization tidy (a backend + OpenSpec change, so an *implementation*, not a design-return); the
  `AddToCart` null-snapshot 500 (latent backend gap); no frontend CI job; focus-ring enhancement; the
  NU1507 two-nuget-source warning; Docker container grouping (DX note); **CritterWatch trial expires
  2026-07-10**.

## Spec-delta — landed?

**Named delta landed.** The prompt named a v1.10 workshop § 5.1 amendment block + Document History row
reconciling the W3 place-order response shape (`{ orderId, status }` → `{ orderId }`-only) and annotating
the W4 `Placed … UTC` timestamp + cancel reason as backend gaps fenced to a future enrich slice; plus the
mechanical `client/README.md` layout refresh and accurate index counts. All landed as named. The frozen
§ 5.1 wireframe text was left intact (append-only); no workshop *slice* was added or removed; no OpenSpec
capability changed; no code. Workshop 001 records the amendment in its `## Document History` (v1.10),
closing the prompt → execute → retro → spec-record loop for the W3/W4 wireframe reconciliation — the
workshop now agrees with Narrative 005 (which led) and the shipped `OrderStatusView`.
