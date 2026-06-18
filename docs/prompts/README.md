# docs/prompts/

Per-session intent records, frozen at session start. See [CLAUDE.md § 5](../../CLAUDE.md) for the routing-layer treatment of prompts in the design pipeline.

A prompt is a task-scoped build order for **one session**. Prompts are the durable, version-controlled record of *intent at session start*. They are not living documents — once the session runs, the prompt is frozen as a historical record; corrections happen in the *next* prompt, not via editing the frozen one. The matching retrospective at `docs/retrospectives/{kind}/NNN-{slug}.md` records *what actually shipped*.

## Subdirectory structure

One subdirectory per kind. Per CLAUDE.md § 5, kinds include `workshops`, `narratives`, `skills`, `decisions`, and `implementations`. The list is descriptive, not prescriptive — new kinds appear as the work surfaces them. The in-repo set already extends the CLAUDE.md list with `rules/` (round-one structural-constraints synthesis) and `docs/` (folder-local README maintenance).

## File naming

`NNN-{slug}.md` where `NNN` is the prompt's number within its kind (zero-padded) and `{slug}` is a short kebab-case identifier (e.g., `001-round-one-structural-constraints.md`).

## Required sections

Per CLAUDE.md § 5, every prompt carries:

- **Metadata block** — bolded inline lines naming the kind, files touched, mode (solo synthesis, multi-persona facilitation, etc.), and commit subject.
- **Framing** — context for *why this prompt exists now*, in 1–3 paragraphs.
- **Goal** — what the session must produce, in concrete terms.
- **Spec delta** — what the canonical spec (the narrative or workshop the session satisfies) will gain when this session ships. Spec-shaped terms, not process-shaped.
- **Orientation files** — the files the session-runner must read first, in order, with one-line notes on what each is consulted for.
- **Working pattern** — the passes or steps the session executes.
- **Deliverable plan** — the files the session writes, in order. (Captured in-repo as `Files touched` in the metadata block plus the implicit list in the spec delta and goal.)
- **Out of scope** — what the session explicitly will *not* touch.

## In-repo format precedent

The format observed in [`rules/001-round-one-structural-constraints.md`](rules/001-round-one-structural-constraints.md) is the project convention:

- Top-line title `# Prompt: {Kind} {NNN} — {Title}`.
- Bolded inline metadata (`**Kind**: ...`, `**Files touched**: ...`, `**Mode**: ...`, `**Commit subject**: ...`) immediately under the title.
- Level-2 (`##`) headings for each required section.
- Sustained prose where reasoning is multi-step; bullet lists where content is genuinely list-shaped.

New prompts should mirror this shape unless a session has a deliberate reason to diverge.

## Operating discipline

- **One prompt = one session = one PR.** A prompt corresponds to one working session; that session produces one PR; the PR contains exactly the prompt's named deliverables plus the retrospective.
- **Frozen at session start.** The prompt is not edited after the session begins. Corrections become the next prompt.
- **Spec-delta closure.** Every prompt names its spec delta in 2–4 lines. The paired retrospective confirms whether the delta landed.
- **Branch-per-prompt naming.** Each prompt-driven session runs on its own branch named `{type}/{slug}`, where `{type}` matches the conventional-commit prefix of the session's commit subject (`tidy`, `feat`, `docs`, `fix`, etc.) and `{slug}` is a short kebab-case identifier mirroring the commit's scope. PR #1's branch (`tidy/docs-folder-readmes`) paired with commit subject `tidy: docs — add folder READMEs for routing-layer narrowing` is the in-repo precedent. The convention operationalizes the one-prompt-one-session-one-PR rule and makes the branch ↔ commit ↔ prompt triple one-glance-obvious in `git log` and PR listings.

## Cross-references

- [CLAUDE.md § 5](../../CLAUDE.md) — prompt routing-layer treatment.
- [CLAUDE.md § Operating Disciplines](../../CLAUDE.md) — the one-prompt-one-PR rule and the spec-delta closure loop.
- [`../retrospectives/`](../retrospectives/) — the paired layer of session outcome records.

## Current population

Kinds populated for round one: `rules/` (2 — round-one structural-constraints synthesis, encode bundle), `workshops/` (1), `research/` (2 — ecommerce engineering lessons survey, frontend stack landscape), `docs/` (15 — folder-READMEs, README overhaul, housekeeping sweep, slice 3.1/4.1/4.2/4.6/4.7/3.4 and slices 3.2+3.3 doc follow-ups, round-one close reconciliation, slice 3.5 close, the design-return reconciliation of workshop § 5.1 to the shipped W3/W4 wire, and the v1.11 design-return flipping § 5.1's W4 placed-at + per-reason cancel copy from aspirational to shipped (slice 025) and archiving the `enrich-order-status-view` change, and the v1.13 design-return adding the workshop § 6 slice-3.1 add-to-cart malformed-snapshot faithfulness note (#69) and archiving both `harden-add-to-cart-snapshot` (shopping-cart 8→9) and `list-my-orders` (order-lifecycle 9→10), returning the workspace to 0 active changes), `narratives/` (4 — Seller catalog-management, Customer browse, Customer purchase, Customer storefront), `specs/` (2 — slice 1.1 + 1.2 OpenSpec proposals; from slice 1.3 on, proposals ride consolidated slice PRs without a standalone spec-prompt), `chore/` (3 — Critter Stack 2026 upgrade, infra bundle, pre-frontend hardening), `implementations/` (32 — slices 1.1, 1.2, 1.3, 2.1, 2.2, 3.1, 4.1, 4.2, 4.3, 4.6, 4.7, 3.2+3.3, 3.4, 2.4, 3.5, the frontend bootstrap — the round-two SPA skeleton — the Wolverine health-check exposure, a cross-cutting infra slice salvaged from the research-003 spike, the W2 cart-review screen — the first storefront screen — the W1 browse + add-to-cart screen, the first optimistic mutation, the W2 cart-edits screen — remove + change-quantity, the cart's first DELETE and 204-No-Content commands, and the W3 place-order screen — checkout to Order Confirmation, the storefront's first non-optimistic mutation and the OpenTelemetry trace front-door, and the ADR 020/021 Order read/write split — the `Order` write aggregate + `Ordering/` verb folder, the Cart pilot's rollout to the Order BC; and the W4 order-tracking screen — the storefront's first `refetchInterval` poll, converging `OrderStatusView` to its terminal status and completing the W1→W4 storefront spine; and the ADR 020 Stock read/write split — the `StockLevel` write aggregate + the `StockLevelView` read projection in Inventory, the third and final ADR 020 rollout with no folder change (`StockLevel` ≠ `…Stock`), completing the Cart→Order→Stock split; and the `OrderStatusView` enrichment — `placedAt` + `cancelReason` added to the order read model, binding W4's placed-at line and per-reason cancel copy, the first round-two slice to change a read contract; and the `AddToCart` snapshot hardening — a `Validate` guard rejecting a malformed `productSnapshot` with `400` at the boundary instead of a `500` NRE in the shared cart fold, closing the round-one pre-frontend audit's only open defect; and the OpenTelemetry teaching pass — Marten verbose connection tracking + event-append metrics wired across all three services and the metrics meters registered in ServiceDefaults, completing ADR 005's deferred half and documenting the cross-service trace visual owed since chore/002; and the **"My Orders" list** — a customer-keyed order list (`GET /orders/mine`) over the existing inline `OrderStatusView` with no new event, command, projection, or aggregate, closing the round-two storefront's last named deferral (Gap #3) and extending the journey from tracking one order to seeing them all; and the **payment-decline demo toggle** — a config-gated `Payment:DeclineOverAmount` threshold that makes the already-built slice-4.6 payment-decline → cancel → `ReleaseStock` path triggerable in a live demo, no new domain behavior and no spec delta; and **seed automation** — a one-shot `CritterMart.Seeding` console wired as an Aspire `seeder` resource that auto-seeds the canonical demo products + stock on boot through the real HTTP endpoints, closing demo-runbook Known Gap #1, dev/demo infrastructure with no domain or spec change; and **deferred-timeout linked traces** — the fired payment- / cart-timeout now runs in its own span-linked root trace (suppress Wolverine's parented span + emit a new root linked back via the originating envelope's traceparent) instead of parenting into the placement / add-to-cart trace, so the demo-centerpiece `POST /orders` waterfall never balloons to the timeout window, observability-only with no domain or spec change; and **cart command identity harmonization** — the three cart commands (`POST /carts/mine/items`, `…/items/{sku}/quantity`, `DELETE …/items/{sku}`) move from route-keyed to **header-keyed** identity (`X-Customer-Id`), matching the cart read and closing the divergence slices 3.1–3.3 logged as a future tidy; a `shopping-cart` transport change with no event, projection, index, or domain-rule change, place-order's body key deferred as a fast-follow), `decisions/` (2 — round-two frontend decisions: stack + full-pipeline UI modeling, the first session whose primary deliverable is the ADRs themselves; and ADR 020 domain write-models vs. `*View` read models, the first decisions session shipped with a code change — the Cart read/write split pilot). The remaining unused kind (`skills/`) appears as its first session of that kind lands.
