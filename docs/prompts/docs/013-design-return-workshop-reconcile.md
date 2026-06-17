# Prompt: Docs 013 — Design-Return: Workshop § 5.1 Reconciliation + client/README Layout Refresh

**Kind**: maintenance / docs surface (design-return cadence interleave — bring the frozen event-model workshop's § 5.1 wireframe dimension back into sync with the shipped W3/W4 frontend wire, and refresh the stale `client/README.md` layout)
**Files touched**: `docs/workshops/001-crittermart-event-model.md` (edit — § 5.1 v1.10 amendment block + frontmatter `version`/`date`/`status` sync + § 9 Document History row); `client/README.md` (edit — refresh the stale `## Layout` block); `docs/prompts/README.md` + `docs/retrospectives/README.md` (edit — `docs/` count 12→13 + population note); `docs/retrospectives/docs/013-design-return-workshop-reconcile.md` (new)
**Mode**: solo synthesis — reconcile the workshop's § 5.1 wireframes against shipped frontend reality (the narrative already carries the corrections; the workshop is the lone straggler), append-only on the frozen timeline; mechanically refresh the README layout. Collaborative on scope: the maintainer chose **Option 1 — workshop reconciliation + README refresh, bundled** via AskUserQuestion at session start, declining the standalone-① variant and the ①+②+③ variant (③ `tidy: encode-ceremony-rule` verified **already encoded** in CLAUDE.md → a no-op).
**Commit subject**: `tidy: design-return — reconcile workshop § 5.1 to shipped W3/W4 wire (+ client README layout)`

## Framing

W4 order-tracking (PR #64, `d94cac1`) was the **fourth consecutive frontend implementation** against the storefront since the #59 ADR design-return (#60 W1, #61 W2-edits, #62 W3, #64 W4 — #63's Order read/write split was itself a `refactor:`/implementation). Per CLAUDE.md § *Design-return cadence*, a fourth consecutive implementation PR without a design interleave "is a signal the design has drifted"; the #64 retro (`implementations/023`, § Methodology refinements) flags the cadence as genuinely due. There is no un-modeled bounded context and no obviously-missing narrative, so the cadence-satisfying move is a **`tidy:` design-return** that closes the *reverse* spec delta the four implementations opened: two canonical-doc claims now contradict shipped reality.

The workshop's § 5.1 Wireframe Dimension (added v1.8) has drifted on two points the frontend slices resolved:

1. **W3's place-order response shape.** § 5.1's W3 bullet still reads "returned by the `PlaceOrder` response `{ orderId, status }`." Slice 4.1 / W3 (PR #62) reconciled this to **`{ orderId }`-only** — the placement has only just kicked off the cross-BC outcome, so the server has no settled status to report at the moment it answers; W3 *reads the order back* (`GET /orders/{orderId}`) for status and total. **Narrative 005 already carries this** (Moment 4 prose + the v1.5 "load-bearing reconciliation" Document History row); the workshop is the straggler.

2. **W4's wireframe over-promises the wire.** § 5.1's W4 wireframe shows a `Placed 2026-06-14 14:02 UTC` line and the prose implies `cancelled` carries one of three reasons (`stock_unavailable` / `payment_declined` / `payment_timeout`). The shipped `OrderStatusView` carries **neither**: its wire shape is `{ id, customerId, status, lines, total }`, and `OrderCancelled` folds to a bare `Status = "cancelled"` (`src/CritterMart.Orders/Ordering/OrderStatusView.cs:44`). W4 (PR #64) confirmed this and rendered an honest generic "Cancelled" with no placed-at line, logging both as backend gaps. The workshop should annotate these as **backend gaps** (a future "enrich `OrderStatusView`" slice), so it stops implying data the wire does not carry.

Separately (mechanical, no spec content), `client/README.md`'s `## Layout` block is **stale**: it describes a `src/routes/` folder and a "HomePage bootstrap placeholder; W1–W4 screens follow," but the SPA pages actually live in feature folders (`src/catalog/`, `src/cart/`, `src/orders/`) with the W1→W4 routes wired in `src/router.tsx`. It is refreshed in the same PR.

Per the **tidy-ceremony rule**, a tidy that authors spec content (a workshop amendment) carries the full prompt/retro pair — this one does. The README refresh rides along as the mechanical half.

## Goal

After this session, the canonical docs match shipped reality:

1. **Workshop 001 § 5.1** carries a **v1.10 amendment block** recording (a) the W3 response-shape reconciliation `{ orderId, status }` → `{ orderId }`-only (the narrative-led correction the workshop never received), and (b) the W4 backend-gap annotation (`OrderStatusView` carries no placed-at timestamp and no cancel reason; both fenced to a future enrich slice). The frozen W3/W4 wireframe text is **left intact** — the amendment is appended, append-only. The frontmatter `version`/`date`/`status` are synced and a v1.10 § 9 Document History row is added.
2. **`client/README.md`**'s `## Layout` block describes the real feature-folder structure (`catalog/`, `cart/`, `orders/` + the shared `api/`, `identity/`, `components/`, `lib/`) and the W1→W4 route map.
3. The **index READMEs are accurate** — `docs/` count 12→13 in both `docs/prompts/README.md` and `docs/retrospectives/README.md`, with the population note extended.

## Spec delta

Workshop 001 gains a **v1.10 § 5.1 amendment block + Document History row** reconciling the W3 place-order response shape (`{ orderId, status }` → `{ orderId }`-only, the correction Narrative 005 v1.5 already carries) and annotating the W4 wireframe's `Placed … UTC` timestamp + cancel reason as **backend gaps** the shipped `OrderStatusView` does not carry (a future enrich-`OrderStatusView` slice). No workshop *slice* is added or removed; no OpenSpec capability changes; no code. The `client/README.md` layout refresh is **mechanical** (no spec content). This reconciles canonical docs with shipped code — it does not alter the modeled scenario set.

## Orientation

Read in this order:

1. **The session handoff** (`crittermart-handoff-design-return.md`) and **CLAUDE.md** — § *Design-return cadence*, § *Tidy ceremony rule*, § *Spec-delta closure loop*, and the append-only amendment discipline.
2. **`docs/workshops/001-crittermart-event-model.md`** — § 5.1 (the W1–W4 wireframes; W3 line ~271, W4 lines ~277–298), the frontmatter (lines 1–19), and § 9 Document History (the v1.1/v1.2/v1.5 amendment blocks are the format precedent; the latest row is v1.9).
3. **`docs/narratives/005-customer-storefront.md`** — Moment 4 (the `{ orderId }`-only place response, already reconciled) and the v1.5/v1.6 Document History rows; confirms the workshop is the lone straggler.
4. **`src/CritterMart.Orders/Ordering/OrderStatusView.cs`** — the read model's wire shape (`{ id, customerId, status, lines, total }`) and the `OrderCancelled` → bare `cancelled` fold (line 44); the source-of-truth for the W4 backend-gap note.
5. **`client/README.md`** (the stale `## Layout`) and **`client/src/router.tsx`** (the real W1→W4 route map) — for the README refresh.
6. **`docs/prompts/docs/012-slice-3-5-close.md`** + **`docs/retrospectives/docs/012-slice-3-5-close.md`** — the closest tidy/docs precedent for prompt/retro shape and the count-reconciliation discipline.

## Out of scope

- **No code, no tests, no OpenSpec change.** The reconciliation is docs/design only; the `OrderStatusView` enrichment (placed-at + cancel reason) is a *future implementation slice*, named here but not built. No `client/` source change beyond the README prose.
- **Do not rewrite the frozen § 5.1 wireframe text.** The workshop is append-only — the W3 `{ orderId, status }` bullet and the W4 `Placed … UTC` line stay as the modeling-time record; the corrections are an appended amendment block. (The frontmatter `version`/`date`/`status` sync is *not* a body edit — it is the standard reconciliation v1.7 already performed.)
- **Do not re-open resolved decisions.** The `{ orderId }`-only response and the W4 honest-cancelled treatment were resolved in #62/#64 and locked in Narrative 005; this session records them in the workshop, it does not re-litigate them.
- **Do not build the deferred surfaces.** The "enrich `OrderStatusView`" slice, the "My Orders" list (Gap #3), the cart identity-transport harmonization, and the Stock pilot are all out — the Stock pilot is the on-deck *implementation* after this design-return, not a rider here.
- **Do not scope-creep ③.** `tidy: encode-ceremony-rule` was verified already-encoded in CLAUDE.md (§ *Tidy ceremony rule*, with its own "…before being encoded here" provenance); it is a no-op and is only *retired* as a carry-forward in the retro, not authored.
- **No opportunistic edits** outside the named files. Other stale claims surfaced mid-session become a DEBT row or a next session, not a rider.

## Deliverable plan

1. **Workshop 001 § 5.1** — append the v1.10 amendment block (under the W4 bullets, before the `---`), covering both the W3 response-shape reconciliation and the W4 backend-gap annotation, in the dated-blockquote precedent shape. Sync frontmatter `version` v1.8→**v1.10**, `date`→`2026-06-16`, `status` to reflect the shipped W1→W4 storefront spine + this reconciliation. Add the v1.10 § 9 Document History row.
2. **`client/README.md`** — replace the stale `## Layout` block with the real feature-folder map + W1→W4 route map.
3. **Index READMEs** — `docs/prompts/README.md` + `docs/retrospectives/README.md`: `docs/` 12→13, population note extended with this design-return reconciliation.
4. **Retro** (`docs/retrospectives/docs/013-design-return-workshop-reconcile.md`) — seven-section format; the spec-delta line forward-confirms the named delta landed; retire the ③ carry-forward with the verified-no-op finding.

## Working pattern

Author the workshop amendment first (the spec-content anchor that satisfies the cadence), then the mechanical README refresh, then reconcile the index counts against `ls` (not the prior count — the v1.8 frontmatter lag and the historically-drifted README counts are the standing reminder), then the retro. One branch (`tidy/design-return-workshop-reconcile`), one PR, containing this prompt, the workshop + README + index edits, and the retro. Nothing else.
