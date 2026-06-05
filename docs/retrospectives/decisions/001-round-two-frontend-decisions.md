---
retrospective: 001
kind: decisions
prompt: docs/prompts/decisions/001-round-two-frontend-decisions.md
deliverable: docs/decisions/015-vite-react-frontend-stack.md, docs/decisions/016-frontend-full-pipeline-ui-first-class.md, docs/decisions/009-polecat-deferred-for-round-one.md (amended), docs/vision.md, CLAUDE.md
date: 2026-06-04
mode: collaborative decision facilitation
session-runner: Claude (Opus 4.8)
---

# Retrospective — Decisions 001: Round-Two Frontend Decisions (stack + full-pipeline UI modeling)

## Outcome summary

The session opened round two with the vision-level frontend conversation and closed four decisions, the user choosing at each genuine fork:

1. **Stack** → **Vite + React + TS + TanStack Query + Tailwind v4 + shadcn/ui** (Candidate A, the CritterBids precedent). Authored as **ADR 015** in the fuller ADR-014 register (rejected alternatives folded into Consequences prose).
2. **Coverage grain** → **the weave**: the frontend runs the full SDD pipeline with **UI first-class in the Event Model** (a `Wireframe` dimension on existing slices + net-new view/query slices), bounded by a **presentation-state guardrail**. Authored as **ADR 016**.
3. **Identity** → **keep hardcoded behind a `useCurrentCustomer` React seam**. Recorded as an append-only amendment note on **ADR 009** (no new ADR — the seam is idiomatic React, effectively free).
4. **Round-two scope** → **backend-first**: complete the stock lifecycle (StockCommitted) before the frontend; CritterWatch parked.

Supporting fold-ins, collaboratively confirmed as the natural completion of ADR 015: the `docs/vision.md` "frontend stack TBD" line and `CLAUDE.md` § Tech stack Frontend cell now name the stack and cross-reference ADR 015/016; the decisions README index gains rows 015 and 016. This is the project's **first `decisions`-kind prompt/retro pair**; the `decisions/` subdirectory was created in this session under both `docs/prompts/` and `docs/retrospectives/`, and the two README population lines register the new kind. The ADR package plus this prompt/retro is one PR (one-prompt-one-PR); committing it is the user's boundary.

## What worked

- **Orientation-first made the criteria pre-determined.** Reading the prompt's list in order — vision doc, then Research 002 and its retro, then ADRs 006 and 009, then the workshop's §§ 3/6/8 — meant that by the time the forks were presented, the decisive constraints (the OTel-trace-spans-the-network *hard success criterion*, ADR 006's no-BFF/frontend-calls-three-services shape, the hardcoded ID already living in the frontend) had already filtered the seven candidates. Research 002 having done the landscape survey meant this session could spend its budget on *deciding* rather than re-surveying.

- **First-option-is-the-recommendation, with previews on the contested forks.** Each fork led with the recommended option first and labeled it; the two big forks (stack, coverage) carried ascii previews. This matched the user's stated preference for concrete options-with-previews and kept the conversation moving from evidence to choice.

- **The user's mid-conversation reframe of Fork 2 strengthened ADR 016.** The session's initial framing called "amend the workshop with UI slices" a near-category-error. The user pushed back: UI and its slices *are* part of Event Modeling. That correction is right — Dymitruk-style Event Modeling treats the **wireframe as a first-class row** (command slices are wireframe→command→event; view slices are event→read-model→wireframe), and round one simply under-ran the view half because no frontend demanded it. The collaboration converted a false binary ("model vs. don't") into the correct question ("at what *grain*"), and ADR 016's weave — full pipeline, UI first-class, bounded by a presentation-state guardrail — is materially stronger than the thinner "narrative-grain only" version first floated.

- **The backend-completeness scan bounded — and de-risked — the user's backend-first choice.** Rather than accept "backend as complete as possible" as open-ended, the session read workshop § 8 and found the gap is *tightly bounded*: `StockCommitted` (§ 8 #3) is the one real in-scope item; § 8 #2 is a tiny optional rider; everything else is explicitly long-road. So backend-first costs ≈ one well-shaped slice, not a second round-one. Surfacing that turned an accepted-risk decision into a low-risk one, and grounded the next-session plan in a concrete shape (`CommitStock` published-language command mirroring the existing `ReleaseStock` per ADR 014).

- **Honoring two house conventions kept the artifacts in-register.** ADR 009's seam note is an append-only dated blockquote (the README's "ADRs are append-only" rule), and ADRs 015/016 use the fuller ADR-014 register because both clear the bar on multiple counts and a contributor needs the why-not as much as the what.

## What was harder than expected

- **The first AskUserQuestion fire was rejected because the contested fork needed discussion first.** The session presented all four forks' analysis in prose and then fired a single four-question AskUserQuestion. The user accepted Fork 1 but wanted to *clarify* Fork 2 before committing the rest. Lesson: for a genuinely contested "big fork," lead with a discussion round and fire the decision tool only once the framing is settled — batching a contested fork with three clear ones forces a premature commit on the one that most needs deliberation.

- **The four forks were not fully independent.** Fork 2 (coverage grain) and Fork 4 (scope ordering) couple — the weave implies a sequence of frontend PRs, which is what StockCommitted interleaves around. Presenting them as four independent questions understated that coupling; it surfaced naturally in conversation but a tighter framing would have named it up front.

- **An overstated methodological claim needed user correction.** Calling the workshop-UI-slices option a "category error" was too strong and briefly mis-framed the most important fork. It was recoverable (and the recovery improved the ADR), but the cleaner path is to hold strong claims about a domain the user knows well more loosely and invite the pushback explicitly.

## Methodology refinements that emerged

1. **Decision conversations are a legitimate `decisions`-kind prompt/retro pair, even with zero code.** Prior ADRs rode under `docs/` or `chore/` kinds alongside the slice that surfaced them. A session whose *primary deliverable is the decision itself* warrants the dedicated kind CLAUDE.md § 5 already named. This is its first use; the pattern is worth repeating for future vision-level forks.

2. **For contested forks, AskUserQuestion belongs *after* a discussion round, not as the opening move.** A multi-question batch is efficient for clear forks but forces premature commitment on a contested one. Sequence: present evidence → discuss the contested fork → fire the tool once framing is settled.

3. **A reconstructed-at-end prompt for a freeform-kickoff session is acceptable, but flag it honestly.** This session began from the user's freeform brief, not a frozen prompt. The prompt was crystallized at session end to capture intent-at-start; the metadata block says so. Future decision sessions opened by a freeform brief should do the same rather than pretend a prompt pre-existed.

4. **Bound "as complete as possible" before accepting it.** When the user frames a scope goal in open-ended terms ("backend as complete as possible"), read the parking lot and enumerate the actual gap set before planning around it. Here it converted an open-ended detour into a one-slice, low-risk step and produced a concrete next-session shape.

## Outstanding items / next-session inputs

The next-session sequence, user-confirmed (#1–4):

1. **ADR package PR** — commit ADR 015 + 016 + 009-note + README + the vision.md/CLAUDE.md fold-ins, paired with this prompt/retro. Authored this session; the commit/PR is the user's boundary. Branch `docs/round-two-frontend-decisions`.
2. **StockCommitted PR(s)** — the reserve→**commit** twin of the existing reserve→release-on-cancel. New `CommitStock` published-language command (Orders→Inventory on `OrderConfirmed`, mirrors `ReleaseStock`/ADR 014) + `StockCommitted` event on the Stock stream + a context-map edge (Orders↔Inventory Customer-Supplier row gains a third message) + Orders slice 4.4 confirm-path cascades `CommitStock`. Likely earns **ADR 017** (clears threshold (a): multi-BC). Fold § 8 #2 symmetry decision in as a rider (lean: keep no-publish). The *how* is settled by ADR 014, so the risk is low.
3. **Frontend design-return PR** — workshop `Wireframe` column + net-new view-slice sketches (proportional, no full re-draw) + the first customer-journey narrative (browse → cart → checkout → track). The opening design interleave per Fork 4.
4. **Frontend per-slice loop** — proposal + prompt + impl + retro per slice, threaded by the narrative, stack per ADR 015.

Open call-outs carried alongside (not blocking):

- **Workshop 001 frontmatter `status: Draft` is stale** (Document History reaches v1.5; the encode session fixed `version:` but not `status:`). Decide the allowed status values and fix it in the next workshop-touching PR (step 2 or 3 both touch it).
- **Conventions-ADR question** (retro rules/002): process rules now cite retros rather than ADRs; consolidate into a conventions ADR only if they keep accumulating. Not actionable yet.
- **CritterWatch (ADR 013)** stays parked — still blocked on the tier/feed/license question.

## Spec-delta — landed?

**Yes.** The prompt named: create ADR 015 and ADR 016 and index them; append the ADR 009 seam note; resolve the vision.md and CLAUDE.md frontend "TBD" lines; record the backend-first next-session sequence. All landed — `docs/decisions/015-vite-react-frontend-stack.md` and `016-frontend-full-pipeline-ui-first-class.md` exist (fuller register, 016 carrying the guardrail table); ADR 009 carries the append-only seam blockquote; the decisions README index has rows 015/016; `docs/vision.md` and `CLAUDE.md` § Tech stack name the stack with ADR cross-references; the 1–4 sequence is recorded above and in the project memory. The new `decisions` kind is registered in both prompts/ and retrospectives/ README population lines. No file outside the prompt's named deliverable set was edited; no code, scaffold, narrative, openspec, or context-map change was made (all deferred to the named downstream sessions).
