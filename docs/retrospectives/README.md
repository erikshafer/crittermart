# docs/retrospectives/

Per-session outcome records, authored before the session's PR opens. See [CLAUDE.md § 6](../../CLAUDE.md) for the routing-layer treatment of retrospectives in the design pipeline.

Each retrospective pairs with a prompt at `docs/prompts/{kind}/NNN-{slug}.md`. The prompt records *intent at session start*; the retro records *what actually shipped, what was harder than expected, what the methodology refined to, and whether the spec delta closed*. The retro is the feedback edge — it surfaces methodology refinements before they evaporate and explicitly closes (or honestly fails to close) the spec delta the prompt named.

## Subdirectory structure

Mirrors [`docs/prompts/`](../prompts/). Each kind subdir of prompts has a matching kind subdir under retrospectives. New kinds appear in retrospectives when they appear in prompts.

## File naming

Same as the paired prompt: `NNN-{slug}.md`. The number and slug match the prompt's filename so the pairing is one-glance-obvious.

## Frontmatter

Each retrospective carries YAML frontmatter with:

- `retrospective` — the retrospective number (matches the paired prompt's number).
- `kind` — the kind subdirectory (e.g., `rules`, `workshops`, `docs`).
- `prompt` — the path to the paired prompt (e.g., `docs/prompts/rules/001-round-one-structural-constraints.md`).
- `deliverable` — the path to the primary artifact (or comma-separated list of artifacts) the session produced.
- `date` — the session's date (ISO 8601).
- `mode` — the working mode (solo synthesis, multi-persona facilitation, etc.).
- `session-runner` — identifier for the agent or contributor who ran the session.

## Required sections

Per CLAUDE.md § 6:

- **Outcome summary** — what the session produced, named concretely.
- **What worked** — choices, disciplines, or passes that paid off.
- **What was harder than expected** — friction points, judgment calls, decisions that took longer than anticipated.
- **Methodology refinements that emerged** — observations about the *process* that should carry forward to future sessions.
- **Outstanding items / next-session inputs** — what surfaced but was honestly deferred; what the next session in this area should know.
- **Spec-delta — landed?** — explicit confirmation (or honest negative) of whether the prompt's named spec delta closed.

## In-repo format precedent

The format observed in [`rules/001-round-one-structural-constraints.md`](rules/001-round-one-structural-constraints.md) is the project convention:

- YAML frontmatter (see above).
- Top-line title `# Retrospective — {Kind} {NNN}: {Title}`.
- Level-2 (`##`) headings for each required section.
- Optional `## Process notes` section at the bottom for one-shot observations that don't fit the standard six.

## Operating discipline

- **Authored before the PR opens.** The retro is not a post-PR write-up; it is part of the session's deliverables and lands in the same PR as the artifact.
- **Spec-delta closure is mandatory.** Every retro names whether the prompt's spec delta landed. A retro that finishes without addressing the spec delta is incomplete.
- **Methodology refinements carry forward.** A future session reading this folder should be able to find the prior session's methodology learnings without rerunning them.

## Cross-references

- [CLAUDE.md § 6](../../CLAUDE.md) — retrospective routing-layer treatment.
- [CLAUDE.md § Operating Disciplines](../../CLAUDE.md) — the spec-delta closure loop and the one-prompt-one-PR rule.
- [`../prompts/`](../prompts/) — the paired layer of session intent records.

## Current population

Kinds populated for round one: `rules/` (2 — round-one structural-constraints synthesis, encode bundle), `workshops/` (1), `research/` (1 — frontend stack landscape; the ecommerce-lessons research prompt has no paired retro), `docs/` (14 — folder-READMEs, README overhaul, housekeeping sweep, slice 3.1/4.1/4.2/4.6/4.7/3.4 and slices 3.2+3.3 doc follow-ups, round-one close reconciliation, slice 3.5 close, the design-return reconciliation of workshop § 5.1 to the shipped W3/W4 wire, and the v1.11 design-return flipping § 5.1's W4 placed-at + per-reason cancel copy from aspirational to shipped (slice 025) and archiving the `enrich-order-status-view` change), `narratives/` (4 — Seller catalog-management, Customer browse, Customer purchase, Customer storefront), `specs/` (2 — slice 1.1 + 1.2 OpenSpec proposals), `chore/` (3 — Critter Stack 2026 upgrade, infra bundle, pre-frontend hardening), `implementations/` (28 — slices 1.1, 1.2, 1.3, 2.1, 2.2, 3.1, 4.1, 4.2, 4.3, 4.6, 4.7, 3.2+3.3, 3.4, 2.4, 3.5, the frontend bootstrap — the round-two SPA skeleton — the Wolverine health-check exposure, a cross-cutting infra slice salvaged from the research-003 spike, the W2 cart-review screen — the first storefront screen — the W1 browse + add-to-cart screen, the first optimistic mutation, the W2 cart-edits screen — remove + change-quantity, the cart's first DELETE and 204-No-Content commands, and the W3 place-order screen — checkout to Order Confirmation, the storefront's first non-optimistic mutation and the OpenTelemetry trace front-door, and the ADR 020/021 Order read/write split — the `Order` write aggregate + `Ordering/` verb folder, the Cart pilot's rollout to the Order BC; and the W4 order-tracking screen — the storefront's first `refetchInterval` poll, converging `OrderStatusView` to its terminal status and completing the W1→W4 storefront spine; and the ADR 020 Stock read/write split — the `StockLevel` write aggregate + the `StockLevelView` read projection in Inventory, the third and final ADR 020 rollout with no folder change (`StockLevel` ≠ `…Stock`), completing the Cart→Order→Stock split; and the `OrderStatusView` enrichment — `placedAt` + `cancelReason` added to the order read model, binding W4's placed-at line and per-reason cancel copy, the first round-two slice to change a read contract; and the `AddToCart` snapshot hardening — a `Validate` guard rejecting a malformed `productSnapshot` with `400` at the boundary instead of a `500` NRE in the shared cart fold, closing the round-one pre-frontend audit's only open defect; and the OpenTelemetry teaching pass — Marten verbose connection tracking + event-append metrics wired across all three services and the metrics meters registered in ServiceDefaults, completing ADR 005's deferred half and documenting the cross-service trace visual owed since chore/002; and the **"My Orders" list** — a customer-keyed order list (`GET /orders/mine`) over the existing inline `OrderStatusView` with no new event, command, projection, or aggregate, closing the round-two storefront's last named deferral (Gap #3) and extending the journey from tracking one order to seeing them all), `decisions/` (2 — round-two frontend decisions: stack + full-pipeline UI modeling, the first session whose primary deliverable is the ADRs themselves; and ADR 020 domain write-models vs. `*View` read models, the first decisions session shipped with a code change — the Cart read/write split pilot). The remaining unused kind (`skills/`) appears as its first session of that kind lands.
