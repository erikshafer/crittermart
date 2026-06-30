---
retrospective: 035
kind: implementations
prompt: docs/prompts/implementations/035-slices-2-5-2-7-replenishment-saga.md
deliverable: openspec/changes/slices-2-5-2-7-replenishment-saga/ (design + tasks added; proposal + stock-management spec 3 ADDED authored in the design-return session), src/CritterMart.Inventory/Stock/{Replenishment.cs,BackorderDetected.cs,RequestRestock.cs,RestockArrived.cs,ReplenishTimeout.cs,ReplenishmentEscalated.cs,ReplenishDeadline.cs} (new), src/CritterMart.Inventory/Features/{ReplenishmentNotifications.cs (new),ReserveStock.cs,ReceiveStock.cs}, src/CritterMart.Inventory/Program.cs, tests/CritterMart.Inventory.Tests/{ReplenishmentSagaTests.cs,ReplenishmentSagaIntegrationTests.cs} (new), docs/narratives/008-operator-replenish-backorders.md (v1.0), docs/narratives/README.md (7→8), docs/prompts/README.md (impl 34→35)
date: 2026-06-30
mode: solo implementation (plan-first; Ultraplan handoff attempted, fell back to local)
session-runner: Claude (Opus 4.8)
---

# Retrospective — Implementations 035: Slices 2.5–2.7 — Inventory replenishment saga (CritterMart's first convention `Wolverine.Saga`)

## Outcome summary

Landed CritterMart's **first** convention `Wolverine.Saga`: a SKU-keyed `Replenishment` saga that opens on a `ReserveStock` shortfall (2.5), resolves on a covering restock and narrows on a partial (2.6), and escalates on a config-driven deadline (2.7). Design A (saga-centric) as decided: the saga *is* the backorder state — a Marten-stored document deleted on `MarkCompleted()`, never event-sourced; slice 2.2's refusal path is unchanged and the saga is a separate, additive reaction.

The session opened by **verifying the work wasn't already done** (a stale resume blurb): PR #112 (the design-return) had merged, and commit `73822ab` had committed the openspec change + prompt 035 to `main` under a commit *message* describing the full implementation that was never written. Per the owner's call, `73822ab` is left as-is and this PR is the real `feat`.

All 31 Inventory tests green (21 pre-existing + 5 saga unit + 5 saga integration); `dotnet build CritterMart.slnx` zero errors; `openspec validate slices-2-5-2-7-replenishment-saga --strict` valid. New messages are all Inventory-local; no `CritterMart.Contracts` change, no new `Stock` events, no daemon.

## What worked

- **Plan-first with the API verified against docs, not memory.** The two things that would otherwise have shipped as bugs were both caught at plan/build time by treating Wolverine's saga API as something to look up. `ctx7` was out of monthly quota, so the Wolverine sagas guide was fetched directly (WebFetch) — and the assembly itself was grepped to confirm member/namespace names. The plan named the `NotFound` mandate as a "load-bearing finding" before any code.
- **The pure-unit / integration split paid for itself.** The saga's `StartOrHandle`/`Handle` are plain methods on a POCO — the 5 unit tests exercise the open/max-update/cover/partial/escalate logic with no host, which is exactly the teaching contrast against the Order's PMvH. The 5 integration tests then proved the wiring and, critically, that saga storage rides the existing `IntegrateWithWolverine()` with **no extra Marten registration** (design.md decision 8) — by loading and asserting `Replenishment` documents directly.
- **Running the FULL suite, not just the new tests.** Making the refusal path emit `BackorderDetected` turned the *pre-existing* `a_short_line_refuses…` test into an implicit saga-persistence test. When the saga had the empty-id bug, that pre-existing test went red — a regression the full run surfaced immediately and the new tests alone would not have framed as "you broke the baseline."
- **`OutgoingMessages` for the multi-message refusal return.** Switching `ReserveStockHandler` from `Task<object>` to `Task<OutgoingMessages>` made cascading the unchanged `StockReservationFailed` *and* one `BackorderDetected` per short line unambiguous to Wolverine's codegen.

## What was harder / notable

- **Wolverine's "silent no-op for a missing saga" is not automatic — it throws.** Both the proposal and the research note describe an unmatched `RestockArrived`/`ReplenishTimeout` as a "silent no-op (saga not-found)," phrased as if it were free. It is not: Wolverine throws on a non-`Start` message for a missing/completed saga unless an explicit static `NotFound(message)` method exists per message type ("even if it is an empty, do-nothing method"). The spec's three no-op clauses (no-open-saga restock; post-resolution timeout) depend entirely on `NotFound(RestockArrived)` + `NotFound(ReplenishTimeout)`. Recorded as design.md decision 2.
- **Separate `Start` + instance `Handle` for the same message is the wrong shape — use `StartOrHandle`.** The first cut gave `BackorderDetected` a static `Start` *and* an instance `Handle` (open vs. re-detect). Wolverine resolved that to the *continuation* path: on a not-found saga it built a blank instance, ran `Handle` (which never set `Id`), and tried to `Insert` it → Marten's *"externally-assigned string keys but the document's id is null or empty."* The fix is Wolverine's documented single-method convention `StartOrHandle`, detecting new-vs-existing via `Outstanding == 0` (a blank instance defaults to 0; an open saga always holds ≥ 1). Confirmed `StartOrHandle`/`StartsOrHandles` exist in the 6.12.0 assembly before rewriting.
- **Two smaller API corrections, both caught by `dotnet build`.** `[SagaIdentity]` lives in `Wolverine.Persistence.Sagas`, not `Wolverine.Attributes` (grepped the DLL to find it). And `ILogger<TStaticClass>` is illegal (CS0718) — the sink handlers use the message type as the logger category instead.
- **Process detours, not code.** The Ultraplan cloud handoff failed (403, auth/entitlement) and the session proceeded locally with the owner's go-ahead; `ctx7` hit its monthly quota mid-session (suggested `ctx7 login` / `CONTEXT7_API_KEY`).

## Methodology refinements

- **A proposal's prose "no-op / it just works" is a claim to verify against the runtime, not a spec of behavior.** Two artifacts independently asserted the saga not-found path was silent; the framework's default is the opposite. When a spec describes framework behavior ("the runtime ignores X"), confirm it in the framework's docs/assembly before relying on it — the cost of being wrong is a thrown exception in production, not a failing test.
- **When a secondary/failure path gains a new emission, audit the tests that already exercise that path.** They silently become tests of the new behavior. Here it was a free extra assertion once green, but it was a real (transient) baseline regression while the saga was broken — the full-suite run is the safety net.
- **Record anticipated scope expansions in design.md, don't let them be silent.** The 5th message (`ReplenishmentEscalated`, beyond the prompt's enumerated four) was the owner's escalation choice and was anticipated by the prompt's "escalation sink (location TBD)"; design.md decision 5 names it explicitly so the diff doesn't read as opportunistic drift.

## Outstanding / next-session inputs

- **`openspec archive slices-2-5-2-7-replenishment-saga -y` — post-merge tidy PR** (per the customer-data precedent; syncs the change into `openspec/specs/stock-management`). Not done in this PR.
- **Live-stack verification — offered proactively** per `[[feedback-live-verify-after-changes]]` / `[[feedback-drive-demo-flows]]`: boot the Aspire stack, drive a real shortfall → `RequestRestock` → restock → resolve, and (with `Inventory:ReplenishTimeout` set short) the escalate path, watching the new saga store + auto-scheduled `TimeoutMessage` on the CritterWatch console — the original motivation for wanting a real saga.
- **The misleading `73822ab` commit on `main`** is intentionally left as-is (owner's call); this PR's `feat` commit is the real one. Noted so a future reader of `git log` isn't surprised that the earlier "feat: add … saga" added only docs.
- **Saga #2 — Identity email-change confirmation (EF-Core-backed)** remains gated on an ADR-009 revisit; the swappable-saga-store contrast is its payoff.
- **Auto-restock demo lever (Workshop § 8 item 19)** deferred — a config'd supplier delay à la `Payment:AuthDelay`; distinct from the timeout window shipped here.
- **Design-return cadence:** this is the first implementation PR after the #112 design-return, so cadence is healthy; the next 1–2 Inventory impls are fine before the next interleave.
- **Carry-forwards (unchanged, non-blocking):** do NOT merge Wolverine 6.13.x (Dependabot #94/#99) while CritterWatch alpha.3 pins 6.12.0; CritterWatch trial expires 2026-07-10; POST-TALK delete the demo knobs (`Payment:DeclineOverAmount`, `Payment:AuthDelay`, and now a short `Inventory:ReplenishTimeout` if set in the AppHost); owed owner eyeballs (#77 trace, #78 Docker grouping).

## Spec-delta — landed?

**Named delta landed.** The prompt named the **three ADDED requirements** in the `stock-management` capability — *open a replenishment saga on shortfall* (2.5), *resolve on restock* (2.6), *escalate on timeout* (2.7) — authored in the design-return session at `openspec/changes/slices-2-5-2-7-replenishment-saga/specs/stock-management/spec.md`. This session added `design.md` + `tasks.md` and the code that satisfies them; `openspec validate --strict` is green. Each requirement's GWT scenarios are covered: open + idempotent max-update, cover-and-complete + partial-reduce-stay-open + not-found no-op, escalate-and-complete + post-resolution no-op — across the 5 unit and 5 integration tests. Narrative 008 (v1.0) is the human-readable companion. Four-step closure: **prompt named → session executed → this retro confirms → the `stock-management` spec records it** (on archive, the post-merge tidy).
