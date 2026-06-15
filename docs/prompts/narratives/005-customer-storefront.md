# Prompt: Narrative 005 — The Customer Shops the Storefront (frontend-mode entry)

**Kind**: pre-code design (narrative + workshop amendment) — the ADR 016 frontend-mode entry
**Files touched**: `docs/prompts/narratives/005-customer-storefront.md` (new, this file); `docs/workshops/001-crittermart-event-model.md` (amended — new § 5.1 Wireframe dimension + slice 3.5 row + § 6 GWT + v1.8 history); `docs/narratives/005-customer-storefront.md` (new); `docs/narratives/README.md` (population line); `docs/retrospectives/narratives/005-customer-storefront.md` (forthcoming, session close)
**Mode**: solo authoring — collaborative working style (the three shaping forks decided with the user before drafting)
**Commit subject**: `docs: frontend-mode entry — wireframe dimension (W1–W4 + slice 3.5) + customer-storefront narrative`

## Framing

This is the **frontend-mode entry** — CritterMart's first real frontend work, and the realization of [ADR 016](../../decisions/016-frontend-full-pipeline-ui-first-class.md) ("UI first-class in the Event Model"). Backend round one is complete (all 18 modeled slices shipped); the frontend *plan* is locked — [ADR 015](../../decisions/015-vite-react-frontend-stack.md) (Vite + React + TanStack Query + Tailwind v4 + shadcn/ui, version-pinned, TanStack Router, Zod-at-boundary, optimistic-UI + rollback), [ADR 018](../../decisions/018-frontend-three-services-cors-posture.md) (SPA → three services directly, CORS in dev and prod, no proxy), and [ADR 006](../../decisions/006-wolverine-http-per-service-no-bff.md) (no BFF). None of those are re-litigated here; this session threads them as established context.

ADR 016 names the realizing artifacts: a **proportional** workshop amendment (the wireframe dimension plus sketches of the net-new view slices — *not* a per-slice re-draw) and **one or more customer-journey narratives** threading the wireframe-bearing slices into a coherent browse → cart → checkout → track experience. This session produces both, in one PR (the consolidate-slice-PRs working preference). The [pre-frontend endpoint audit](../../research/pre-frontend-endpoint-audit.md) is the build-order input: it found **Gap #1 (open-cart-by-customer)** is the one *blocking* read-model gap, so it is modeled **first**, as net-new **slice 3.5 ("View my open cart")** — a view/query slice that adds no new events and exposes the existing `CartView` over the partial-unique open-cart index (`Orders/Program.cs:74`) by customer identity.

Three forks were decided with the user before this prompt was frozen:

1. **A new narrative (005), the screen/wireframe lens** — *not* an amendment of 002 (browse) or 004 (purchase). Those two carry the system-*behavior* of the same journey (streams, events, cross-BC hops); 005 is the orthogonal **UI lens** (what is on screen, screen-to-screen). This mirrors the 004-vs-002 split: a distinct journey gets its own narrative.
2. **The Wireframe dimension lands as a focused new § 5.1 subsection**, leaving the frozen 9-column round-one § 5 table intact (it gains only the new slice 3.5 row). ADR 016's literal word is "column," but the dimension is realized as a subsection for proportionality (the round-one table is already 9 columns × 18 rows, mostly system/operator rows that carry no customer wireframe). The column→subsection divergence is a deliberate faithfulness call, flagged in the retro.
3. **Four wireframes this pass** — W1 Browse/Listing (folds product-detail Gap #2), W2 Cart Review (slice 3.5, the blocking Gap #1), W3 Order Confirmation, W4 Order Status/Tracking. Gap #3 ("My Orders" list) is named and deferred.

## Goal

Two deliverables in one PR:

1. **Amend Workshop 001** (v1.7 → v1.8): add net-new **slice 3.5 "View my open cart"** to the § 5 slice table (with updated counts), add a new **§ 5.1 "Wireframe dimension (round-two, ADR 016)"** subsection — a customer-facing slice→screen map plus the four ASCII wireframes **W1–W4** tied to their commands and views — add a **§ 6 GWT** subsection for slice 3.5 (happy path + the no-open-cart edge), and stamp a v1.8 Document History entry. Restate the ADR 016 presentation-state guardrail inline. Proportional; the frozen round-one table and GWTs are otherwise untouched.
2. **Author `docs/narratives/005-customer-storefront.md`** (v1.0) — the Customer's storefront journey from the **screen lens**, threading slices 1.2, 3.1, 3.2/3.3, **3.5**, 4.1, and the order-status read, as Moments tied to W1–W4. Each Moment names its read model, its command/slice, and the locked frontend stance it exercises (Zod-at-boundary, optimistic-UI + rollback, three-services-direct/CORS, no-push, stubbed-identity seam) — as established context, not new decisions.

Follow `docs/narratives/README.md` (frontmatter, Moments, Document History) and `docs/workshops/README.md` output discipline (frontmatter `version:` tracks Document History; every slice carries ≥1 GWT happy path; failure/edge paths explicit).

## Spec delta

Workshop 001 gains its **first round-two frontend amendment** (v1.8): a Wireframe dimension (§ 5.1, W1–W4), a net-new view slice **3.5** in the § 5 table (Orders Cart 4→5, Total 18→19, P0 15→16), and a § 6 GWT for it — closing Gap #1 at the *model* layer. A **new narrative 005** is created at v1.0 (the screen lens; companion to behavior narratives 002 + 004). `docs/narratives/README.md` population → five narratives. No OpenSpec proposal and no code (those are the *next* session — the first frontend implementation slice, slice 3.5).

## Orientation

Read in this order:

1. **`docs/research/pre-frontend-endpoint-audit.md`** — the build-order input. Gap #1 (open-cart-by-customer) is blocking → slice 3.5, modeled first; Gaps #2/#3 fold into W1/W4. The write-side-customer-keyed / read-side-`cartId` mismatch is the crux 3.5 resolves.
2. **`docs/decisions/016-frontend-full-pipeline-ui-first-class.md`** — the decision this session realizes: wireframe first-class, the presentation-state guardrail (reads-a-fact → view slice + wireframe; produces-a-fact → attach wireframe; pure presentation → not an event), and the "proportional amendment" instruction.
3. **`docs/decisions/015-vite-react-frontend-stack.md`** (+ amendment) and **`docs/decisions/018-...cors-posture.md`** — the locked stack and three-services/CORS posture the narrative threads as context (do not re-decide).
4. **`docs/workshops/001-crittermart-event-model.md`** §§ 2 (Orders Cart aggregate), 3 (storyboard "UI moments" — the sketches this amendment finally draws), 5 (slice table + the Cart block 3.1–3.4), 6.1 (slices 3.1–3.4, 4.1 GWTs).
5. **`docs/narratives/002-customer-browse-catalog.md`** and **`004-customer-purchase.md`** — the behavior of browsing and buying; 005 is their screen lens (reference, don't duplicate). Reuse the anchor data (`crit-001` Cosmic Critter Plush `$24.99`, `crit-002` Nebula Newt `$18.00`, two-line total `$103.98`).
6. **`docs/context-map/README.md`** — Catalog has no BC-level integration; product fields reach the Cart only via the frontend snapshot (presentation-layer composition). The SPA is the cross-service orchestrator (no BFF).

## Working pattern

1. Author this prompt (done).
2. Amend Workshop 001: insert slice 3.5 into the § 5 table + update the count lines; author § 5.1 (intro citing ADR 016 + the column→subsection note; the slice→screen map; the four W1–W4 ASCII sketches tied to commands/views, calling out the Gaps and the optimistic-UI/no-push stances); add the § 6 slice-3.5 GWT (happy + no-open-cart edge); stamp v1.8 in frontmatter + Document History.
3. Author Narrative 005: frontmatter (`actor: Customer`, screen-lens note, `slices: [1.2, 3.1, 3.2, 3.3, 3.5, 4.1]`); journey-scope intro (the screen lens, companion to 002/004); Moments 1–5 (Land → Add → Return/Review → Place → Track) each tied to a wireframe + slice + read model + the locked stance it exercises; a "What the Customer does not yet see (on screen)" section; Forthcoming (the first frontend implementation slice 3.5 + the deferred `docs/skills/frontend/`); Document History v1.0.
4. Update `docs/narratives/README.md` population → five narratives, noting 005 is the screen-lens companion.
5. Author the retrospective at session close: spec-delta closure; the column→subsection faithfulness note; the modeled-not-implemented flag for slice 3.5; the deferred frontend skill.

## Out of scope

- **No OpenSpec proposal and no code.** Slice 3.5's proposal + implementation are the *next* session (the first frontend slice). This session models; it does not build.
- **No `docs/skills/frontend/SKILL.md`.** Deferred by design until the first slice establishes the conventions in code (ADR 015; handoff). Named as a forward pointer only.
- **No re-litigation of the locked stack** (TanStack Router, Zod-at-boundary, CORS-in-dev-no-proxy, version pins, optimistic-UI). Threaded as context; not re-decided. No new ADR.
- **No `client/` scaffolding.** No `package.json`, no Aspire `AddViteApp` wiring — those ride the first implementation slice.
- **No per-slice wireframe re-draw** of round-one backend/system/operator slices. Only the four customer-facing W1–W4 are sketched (ADR 016 "proportional").
- **No edits to other narratives (002/004) or other workshops.** If a contradiction with the behavior narratives surfaces, stop and raise it.
- **No prompts/retros README population edits** (deferred to a sweep, per the 004-retro lesson on consolidated-PR drift); only `docs/narratives/README.md` is brought current here.
