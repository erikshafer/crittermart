# Prompt: Docs 018 — Design-Return: Model slice 6.6 (per-customer advisory preview + tailored refusal copy)

**Kind**: design-return (spec-content authoring — a Workshop 003 slice amendment + Narrative 011 Moment amendment as siblings, then the OpenSpec proposal that makes them a machine-readable contract). **Design layer only — no code.**
**Files touched**: `docs/workshops/003-promotions-event-model.md` (edit → v1.4); `docs/narratives/011-customer-redeems-coupon.md` (edit → v1.3); `openspec/changes/slice-6-6-per-customer-preview-and-copy/` (new — `proposal.md` + `specs/coupon-promotion/spec.md`); `docs/prompts/README.md` + `docs/retrospectives/README.md` (edit — `docs/` count 17→18); `docs/retrospectives/docs/018-design-return-slice-6-6-per-customer-preview-and-copy.md` (new)
**Mode**: solo multi-persona synthesis (Domain Expert, Architect, UX, QA), grounded in the validated current-state facts the handoff carried forward. **Two forks resolved with the owner at session start** (`AskUserQuestion`): (1) **one slice, not two** — both follow-ons land as slice **6.6**; (2) **enrich the existing `/validate` endpoint** (optionally-authenticated) rather than adding an `[Authorize]`-gated sibling or hard-`[Authorize]`ing a shipped anonymous route.
**Commit subject**: `docs: model slice 6.6 — per-customer coupon preview + tailored refusal copy (workshop v1.4, narrative v1.3, openspec proposal)`

## Framing

Slice 6.5 (PR #149) shipped CritterMart's second DCB — the composite `(coupon × customer)` boundary enforcing one-redemption-per-customer — and deliberately parked two storefront-UX follow-ons it surfaced ([Workshop 003 §8 item 6](../../workshops/003-promotions-event-model.md), [retro implementations/041](../../retrospectives/implementations/041-slice-6-5-per-customer-redemption-dcb.md)):

1. a **per-customer advisory preview** — the anonymous `GET /coupons/{code}/validate` cannot tell a Customer "you have already used this one," because it holds no identity and no per-customer read model exists; and
2. **tailored copy** for the `CouponAlreadyRedeemedByCustomer` refusal — today a generic inline `Results.Problem` detail, indistinguishable in tone from its `CouponExhausted` sibling.

The [handoff](../../handoffs/coupon-per-customer-preview-and-409-copy.md) validated both against the code (not the docs) and confirmed them unbuilt, then explicitly deferred the design layer to this session. This is also the **design-return cadence interleave** owed after PR #149 — it is the "next narrative / next workshop pass" branch of the rule, not a tidy.

Per the per-slice loop, the workshop and narrative amendments are **siblings** authored before the OpenSpec proposal: the workshop draws the slice and its GWTs, the narrative threads it into the Customer's journey, and the proposal turns both into SHALL statements. All three must agree.

## Goal

After this session a fresh implementation session can build slice 6.6 without re-deriving a single design decision:

1. **Workshop 003 → v1.4** carries slice **6.6** as a table row, a §6.6 GWT set (happy path + the anonymous-caller path + the precedence rule + the release-restores-preview edge), a §5.1 W2 wireframe delta, a §7 entry for the new persisted `CustomerCouponUsageView`, a §4 vocabulary amendment for the event-shape change the preview requires, and a §8 item 6 update retiring both parked follow-ons.
2. **Narrative 011 → v1.3** carries a new **Moment 6** covering both facets from the Customer's perspective, retires the "no per-customer *preview*" leaves-out bullet, and sharpens the advisory caveat rather than dropping it.
3. **OpenSpec change `slice-6-6-per-customer-preview-and-copy`** (no date prefix) validates `--strict`, MODIFYing the *Validate and price a coupon at cart review* requirement and ADDing a per-customer advisory-usage requirement, with the refusal-copy delta folded into the per-customer enforcement requirement.

## Spec delta

Workshop 003 gains **one new slice (6.6)** with its GWT set, one **§5.1 wireframe delta** (the W2 already-used affordance), one **new read model** in §7 (`CustomerCouponUsageView` — the first *persisted* per-customer coupon state), and a **§4 vocabulary amendment** adding `customerId` to the two redemption events. Narrative 011 gains **one new Moment (6)** and retires one leaves-out bullet. The `coupon-promotion` OpenSpec capability gains **+1 ADDED** and **+2 MODIFIED** requirements. No ADR amendment: ADR 024 governs the DCB *enforcement* mechanic, which this slice does not touch. No code.

## Orientation

Read in this order:

1. **`docs/handoffs/coupon-per-customer-preview-and-409-copy.md`** — especially § *Technical grounding*; its facts are validated, do not re-derive them.
2. **`docs/workshops/003-promotions-event-model.md`** — §5 slice table, §5.1 wireframes, §6.5 GWTs, §7 read models, §8 item 6.
3. **`docs/narratives/011-customer-redeems-coupon.md`** — Moment 5 and *What the journey still leaves out*.
4. **`openspec/specs/coupon-promotion/spec.md`** — the *Validate and price a coupon at cart review* and *Enforce one redemption per customer* requirements are the two the delta touches.
5. **`openspec/changes/archive/2026-07-18-slice-6-5-per-customer-redemption-dcb/`** — the closest precedent for proposal + spec-delta shape.
6. **`src/CritterMart.Orders/Promotions/{CouponRedeemed,CouponRedemptionReleased,CouponUsageView}.cs`** — read these to confirm the event payloads and the multi-stream grouping convention before modeling the new view.

## Working pattern

Author the **workshop** amendment first (the slice is the unit of implementation), then the **narrative** (the journey it belongs to), then the **OpenSpec proposal** (the contract both imply). Validate with `npx openspec validate slice-6-6-per-customer-preview-and-copy --strict`. Bump both index READMEs (`docs/` 17→18). Then the retro. One branch, one PR, containing exactly this prompt, the two amendments, the OpenSpec change, the index edits, and the retro.

## Deliverable plan

1. **Workshop 003 → v1.4** — slice-6.6 row, §6.6 GWTs, §5.1 W2 delta, §7 `CustomerCouponUsageView`, §4 event-vocabulary amendment, §8 item 6 update, §9 history row.
2. **Narrative 011 → v1.3** — journey-scope bullet, Moment 6, leaves-out + Forthcoming Moments updates, history row.
3. **OpenSpec change** — `proposal.md` (Why / What Changes / Capabilities / Impact) + `specs/coupon-promotion/spec.md` (2 MODIFIED, 1 ADDED); `--strict` clean.
4. **Index READMEs** — `docs/` 17→18 in both, population note extended.
5. **Retro** (`docs/retrospectives/docs/018-…`) — seven-section format, spec-delta forward-confirmation.

## Out of scope

- **No code, no tests, no `design.md`, no `tasks.md`.** Per CLAUDE.md §4a those two ride the *implementation* session, not the proposal session. This session stops at the proposal.
- **No ADR.** ADR 024 covers the DCB enforcement mechanic only; nothing here reverses across bounded contexts or presents a non-obvious tradeoff worth an ADR. If the implementation session's projection design surfaces one, it authors it.
- **Do not make the preview load-bearing.** The handoff's standing lock: the checkout DCB append remains the sole authority. The preview is advisory even when it is *personally* accurate.
- **No `perCustomerLimit > 1` generalization, no shared-budget DCB, no coupon lifecycle, no standalone Promotions service.** All remain §8 long road.
- **No opportunistic edits** to the other three parked items (shared-budget DCB, doc-tidy, slice-6.2 visual verify).
