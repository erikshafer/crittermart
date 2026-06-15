---
retrospective: 005
kind: narratives
prompt: docs/prompts/narratives/005-customer-storefront.md
deliverable: docs/workshops/001-crittermart-event-model.md (amended v1.7→v1.8 — § 5.1 wireframe dimension + slice 3.5 + § 6 GWT); docs/narratives/005-customer-storefront.md (new); docs/narratives/README.md (population line); docs/prompts/narratives/005-customer-storefront.md (new); docs/retrospectives/narratives/005-customer-storefront.md (this file)
date: 2026-06-14
mode: solo authoring; collaborative working style (three forks decided with the user before drafting)
session-runner: Claude (Opus 4.8)
---

# Retrospective — Narrative 005: The Customer Shops the Storefront (frontend-mode entry)

## Outcome summary

The **frontend-mode entry** — CritterMart's first real frontend work, realizing [ADR 016](../../decisions/016-frontend-full-pipeline-ui-first-class.md). Two deliverables, one PR:

1. **Workshop 001 amended** (v1.7 → v1.8): a net-new view slice **3.5 "View my open cart"** added to the § 5 table (Orders Cart 4→5, Total 18→19, P0 15→16) with its § 6 GWT (happy path + no-open-cart edge) — a customer-keyed read of the existing `CartView` over the existing open-cart index, **no new event**, closing the audit's **Gap #1**. Plus a new **§ 5.1 Wireframe Dimension**: the presentation-state guardrail restated, a customer-facing slice→screen map, and four ASCII wireframes **W1** Browse / **W2** Cart Review / **W3** Order Confirmation / **W4** Order Status.
2. **Narrative 005 authored** (v1.0): the *screen lens* of the storefront journey, companion to behavior narratives 002 (browse) and 004 (purchase). Five Moments (Land → Add → Return/Edit → Place → Track) tied to W1–W4, threading the locked stack stances (Zod-at-boundary, optimistic-UI + rollback, no-push/no-SEO, three-services-direct/no-BFF, stubbed-identity seam) as established context.

`docs/narratives/README.md` brought to five narratives with a two-lens note. **No code, no OpenSpec proposal** — this session *models* the frontend; the first frontend *implementation* slice (3.5) is the next session.

This is a **design-return PR** (the frontend-mode entry), the right interleave after round one's long implementation run — and the three shaping forks were decided *with the user* up front, the collaborative default holding.

## What worked

- **Deciding the forks before drafting.** New-narrative-005-vs-amend-004, wireframe-subsection-vs-column, and four-screens-vs-fewer were all settled with the user (via options + previews) before a word landed — so scope was certain, not guessed. A clean single pass, exactly as the 004 session found.
- **The two-lens split (behavior vs. screen) is clarifying.** Narratives 002/004 answer "what does the system do?"; 005 answers "what is on screen?" Keeping them separate stopped 005 from re-deriving Klefter/Bruun/broker internals — it points at them as the *status* the Customer watches settle. The split is now a documented precedent in the narratives README for any future actor whose UI journey diverges from its behavior journey.
- **Slice 3.5 as the keystone read.** Modeling Gap #1 first (the audit's instruction) gave the cart-review screen (W2) a real enabling slice rather than hand-waving. The write-side-customer-keyed / read-side-`cartId` mismatch is a genuinely instructive event-sourcing point — the read model needed a *second* access path (by customer, not by id), and the index for it already existed. Clean teaching beat.
- **The wireframes carry the locked decisions as annotations, not re-litigation.** Each W1–W4 caption ties an interactive element to its command/slice/read model and names the stance it exercises (Zod boundary, optimistic bump, the honest-pending `awaiting_confirmation` on W3, no-push on W4, the OTel trace front door). The handoff's "don't re-decide the stack" held — the stack appears only as context.
- **Honest optimism boundary.** Calling out *where* optimistic UI stops (placement, W3 — the cross-BC outcome can't be faked) is more truthful than "everything feels instant," and it lands the read-model-is-truth rule from a second angle.

## What was harder than expected

- **The Wireframe `column` → `dimension subsection` divergence (the promised faithfulness note).** ADR 016 says literally "a `Wireframe` column is added to the slice table." Taken literally, that means editing all eighteen frozen round-one rows to append a tenth column that reads `—` for two-thirds of them (system/operator/seller rows). Chosen instead: leave the frozen nine-column table intact (it gains only the genuinely-new slice 3.5 row) and express the dimension as the focused § 5.1 subsection — the slice→screen map plus the sketches. The behavior ADR 016 wants (wireframes first-class, tied to commands/views) is fully honored; only the literal table shape diverges, for proportionality and readability. This is the project's standard "behavior honored, shape diverged" idiom (cf. the workshop's own v1.2 `ReleaseStock`-not-`OrderCancelled` amendment). Recorded here, as the prompt promised; no ADR amendment needed — ADR 016 already says "proportional ... not a per-slice re-draw," which this realizes.
- **Where the prompt/retro for a dual-deliverable session lives.** The session amends the workshop *and* authors a narrative. Filed under `narratives/005` (prompt-number = narrative-number, the established convention) with the workshop amendment named prominently in the metadata's *Files touched* and foregrounded in the Spec delta — rather than splitting into a separate `workshops/002` prompt. Matches the "consolidate slice PRs" preference; the workshop's own v1.8 Document History entry cross-references `narratives/005` so the amendment is discoverable from the workshop side too.
- **Keeping the narrative from sprawling.** 005 covers six slices across two BCs as a continuous UI arc; the temptation was to re-tell 004's behavior. Held the line by making every behavior reference a *pointer* to 004 (Moments 3/5/6) and keeping 005's prose on the screen and the wire call.

## Methodology refinements that emerged

1. **Behavior-lens vs. screen-lens narratives is a reusable pattern.** When a journey has both a rich domain behavior *and* a rich UI, two narratives over the same slices — one per lens — beat one narrative trying to do both. They share slices and must agree. First applied here (002/004 behavior, 005 screen); noted in the narratives README.
2. **ADR 016's "Wireframe column" reads as "Wireframe dimension."** A focused subsection (slice→screen map + sketches) is the proportional realization for a wide, mostly-non-customer-facing slice table. If a future BC's table is small and customer-heavy, the literal column may fit better — decide per workshop.
3. **Model the blocking read slice before sketching the screen that binds to it.** Slice 3.5 was modeled (table row + GWT) and *then* the W2 wireframe was drawn against it — so the screen has a real enabling read, not a wish. The audit's "Gap #1 first" instruction is good general practice for view-slice modeling.
4. **A net-new slice *does* update the frozen slice table** (counts included), unlike a shipped-implementation amendment (which the v1.x history left "at model-level intent"). The distinction: 3.5 is a new *model* fact; the earlier amendments were *implementation* divergences from already-modeled slices.

## Outstanding items / next-session inputs

1. **First frontend implementation slice — slice 3.5 (View my open cart).** The OpenSpec proposal (a read requirement on the `shopping-cart` capability — `GET /carts/mine` resolving the customer's open `CartView` by identity, returning the one open cart or 404; the GWT happy + no-open-cart edge already in workshop § 6) **and** the implementation (the endpoint over the existing index; identity via the ADR 009 `useCurrentCustomer` seam — query-param vs. header decided in the proposal). The cleanest first slice: no new event, unblocks W2. Drive openspec via the CLI (ADR 011 / tool-backed preference).
2. **`docs/skills/frontend/SKILL.md` — author it in that first slice's session, not before.** Deferred by design so it documents the conventions (version pins, Zod boundary, optimistic-UI + rollback) as the *code* establishes them. Named as a forward pointer in ADR 015, the audit, Narrative 005, and this retro.
3. **Gaps #2 / #3 fold per the audit.** Gap #2 (product detail) is folded into W1 — implement a `GET /products/{sku}` only if the SPA wants server-side single-product fetch. Gap #3 ("My Orders" list, `GET /orders?customerId=`) is named on W4 and deferred to round-two-plus; verify `OrderStatusView` carries `CustomerId` when it is picked up.
4. **`client/` scaffolding** (Vite app, pinned `package.json`, Aspire `AddViteApp` wiring, three service URLs injected) rides the first implementation slice or a dedicated bootstrap PR — open decision #2 from the cross-repo comparison (Aspire integration mechanics) is owed then.
5. **README population drift (known cost).** `docs/prompts/README.md` and `docs/retrospectives/README.md` population lines now lag by one (narratives 3→4 incl. this 005 pair); left to a sweep per scope discipline, as the 004 retro established for consolidated PRs.
6. No new ADR or skill triggered by this session. ADR 016 is realized, not amended.

## Spec-delta — landed?

**Yes.** Workshop 001 gained its first round-two frontend amendment (v1.8): § 5.1 Wireframe dimension (W1–W4), net-new view slice 3.5 in the § 5 table (counts updated) with a § 6 GWT — closing Gap #1 at the model layer. New Narrative 005 created at v1.0 (the screen lens; companion to 002 + 004). `docs/narratives/README.md` population → five narratives with the two-lens note. No OpenSpec proposal, no code — correctly out of scope (next session).

## Process notes

- **Design-return PR** (frontend-mode entry) — the right interleave opening round two, not a fourth consecutive implementation PR. Branch `docs/frontend-mode-entry`; commit subject `docs: frontend-mode entry — wireframe dimension (W1–W4 + slice 3.5) + customer-storefront narrative`.
- **One prompt = one session = one PR.** Workshop amendment + new narrative + README + prompt + this retro, all in one PR — the consolidated shape, with the workshop amendment named in the prompt's deliverable plan (not an opportunistic edit).
- **Slice 3.5 is modeled, not implemented** — flagged in the workshop GWT note, the narrative's Forthcoming, and outstanding item 1, so the non-terminal state is never silent.
- Collaborative working style: the three shaping forks were the user's, taken via options-with-previews before drafting.
