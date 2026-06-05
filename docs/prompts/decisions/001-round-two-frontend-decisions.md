# Prompt: Decisions 001 — Round-Two Frontend Decisions (stack + full-pipeline UI modeling)

**Kind**: decision conversation (decisions) — the pipeline's first `decisions`-kind session; the kind was named in CLAUDE.md § 5 but never used (prior ADRs rode under `docs/` or `chore/` kinds alongside the slice that surfaced them). This prompt crystallizes the user's freeform session-opening brief into the frozen intent record.
**Files touched**: `docs/decisions/015-vite-react-frontend-stack.md` (new); `docs/decisions/016-frontend-full-pipeline-ui-first-class.md` (new); `docs/decisions/009-polecat-deferred-for-round-one.md` (append-only amendment note); `docs/decisions/README.md` (index +015/+016); `docs/vision.md` (frontend "TBD" line resolved); `CLAUDE.md` (§ Tech stack Frontend cell); `docs/prompts/README.md` + `docs/retrospectives/README.md` (register the `decisions` kind); `docs/retrospectives/decisions/001-round-two-frontend-decisions.md` (new)
**Mode**: collaborative decision facilitation — present 2–4 concrete options per genuine fork with tradeoffs and a recommendation; the user decides. No code, no scaffold.
**Commit subject**: `docs: ADR 015 + 016 — round-two frontend decisions (stack + full-pipeline UI modeling)`

## Framing

Round one is complete: all seventeen modeled slices shipped across Catalog / Inventory / Orders, the first talk (ImprovingU) is delivered, and the methodology-encoding debt is drained (PR #43). The vision-level conversation that opens round two has one forcing function — a **functional UI must exist before the second talk** (the online .NET user group). The Event Model currently has zero frontend slices; UI exists only as storyboard "moments" in workshop § 3. This is a real vision-vs-model gap, and the session's job is to *decide*, not merely explore.

[Research 002](../research/ecommerce-frontend-stack.md) already produced the decision-evidence: seven candidate stacks against CritterMart-specific constraints, deliberately naming no winner. Its retro named the follow-on ADR as the first downstream consumer. This session is that consumer — plus three adjacent decisions the frontend forces: how the frontend threads the SDD pipeline, whether the hardcoded customer ID ([ADR 009](../decisions/009-polecat-deferred-for-round-one.md)) needs revisiting, and where the frontend sits in round-two scope.

## Goal

Land four decisions, each with options-and-recommendation, the user choosing:

1. **Stack** — what frontend tech, and is it ADR-worthy? (Expected: yes.)
2. **Coverage grain** — does UI become modeled, traceable work (workshop slices), a thin client outside the modeled boundary, or a weave of the two? The big fork.
3. **Identity** — does a real frontend force revisiting ADR 009's hardcoded customer ID now, or stays deferred?
4. **Round-two scope** — where does the frontend sit relative to other round-two candidates (StockCommitted unmodelled; CritterWatch blocked on tier/feed/license)?

Produce the resulting ADR(s) and a next-session plan. Do **not** write code, scaffold a frontend project, or author an implementation prompt — those are downstream sessions.

## Spec delta

The canonical spec that gains content is the ADR layer plus the vision doc:

- **ADR 015** (Vite + React frontend stack) and **ADR 016** (frontend modeled through the full pipeline, UI first-class, presentation-state guardrail) are created and indexed.
- **ADR 009** gains an append-only amendment note recording the `useCurrentCustomer` seam.
- **`docs/vision.md`** and **`CLAUDE.md` § Tech stack** resolve the frontend "TBD" to the named stack with ADR cross-references.
- The next-session sequence (backend-first: ADR package → StockCommitted → frontend design-return → frontend per-slice loop) is recorded in the retro.

## Orientation

Read in this order:

1. **`docs/vision.md`** — the "What this is" TBD line this session unblocks; success criteria (the OTel-trace-spans-the-network requirement is load-bearing); "What this deliberately is not" and "Long road" (frontend constraints and growth).
2. **`docs/prompts/research/002-ecommerce-frontend-stack.md` + its retro + `docs/research/ecommerce-frontend-stack.md`** — the decision-evidence: seven candidates, eight criteria, six open questions.
3. **[ADR 006](../decisions/006-wolverine-http-per-service-no-bff.md)** — no BFF; the frontend calls three Wolverine.Http surfaces directly. The shape that decides the stack.
4. **[ADR 009](../decisions/009-polecat-deferred-for-round-one.md)** — the hardcoded customer ID already lives in the frontend.
5. **`docs/workshops/001-crittermart-event-model.md` §§ 3, 6, 8** — how UI is currently modeled (storyboard "moments"; the `View` column with no `Wireframe` dimension; § 8 open question 3 = `StockCommitted`).
6. **`docs/context-map/README.md`** — the "presentation-layer composition, not BC integration" line; the Orders↔Inventory Customer-Supplier relationship StockCommitted extends.
7. **[ADR 010](../decisions/010-openspec-narrative-sibling-pipeline.md) and [ADR 014](../decisions/014-published-language-contracts-project.md)** — house ADR registers (terse vs. fuller); 014 is the template for consequential cross-cutting ADRs.

## Working pattern

A collaborative decision conversation, not a synthesis pass:

1. **Orientation pass.** Read the list above; let the constraint set (hard OTel-trace criterion, no-BFF, hardcoded-ID-in-frontend, .NET-shop audience) pre-determine the criteria before presenting options.
2. **Fork-presentation pass.** For each of the four forks, present 2–4 concrete options with sourced tradeoffs and a recommendation as the first option. Use AskUserQuestion with rich previews for the contested forks — but lead with discussion on a genuinely contested fork before firing the tool.
3. **Iterate.** Let the user reframe. (Fork 2 in particular: the user's instinct that UI belongs in Event Modeling is correct — the wireframe is a first-class row — and reframes the fork from "model vs. don't" into "at what grain.")
4. **Authoring pass.** Once decided, author ADR 015 (fuller 014-register) and ADR 016 (fuller register + guardrail table); append the ADR 009 seam note (append-only); update the decisions README index.
5. **Fold-in pass.** Resolve the vision.md and CLAUDE.md "TBD" lines as the natural completion of ADR 015 (collaboratively confirmed, not opportunistic).
6. **Retro + index pass.** Author this session's retro; register the new `decisions` kind in both prompts/ and retrospectives/ README population lines.

## Out of scope

- **No code, no frontend scaffold, no `src/` changes.** The stack is decided here; it is built in a downstream per-slice loop.
- **No implementation prompt.** That is a later session against ADR 015/016.
- **No workshop `Wireframe` amendment, no view-slice sketches, no customer-journey narrative.** Those are the frontend design-return PR (sequence step 3), not this decision session.
- **No StockCommitted modeling.** Its shape is *previewed* for the next-session plan (`CommitStock` published-language command + `StockCommitted` event + context-map edge), but the workshop amendment and per-slice loop are a separate PR (sequence step 2).
- **No CritterWatch work.** Parked; still blocked on tier/feed/license.
- **No narrative, openspec, or context-map edits.** This session settles decisions; the realizing artifacts come later.
