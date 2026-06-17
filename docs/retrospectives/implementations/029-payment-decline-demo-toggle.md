---
retrospective: 029
kind: implementations
prompt: docs/prompts/implementations/029-payment-decline-demo-toggle.md
deliverable: src/CritterMart.Orders/Ordering/PaymentProvider.cs (PaymentDeclinePolicy + threshold-aware StubPaymentProvider), src/CritterMart.Orders/Program.cs (register policy from Payment:DeclineOverAmount), src/CritterMart.AppHost/Program.cs (demo threshold injection), tests/CritterMart.Orders.Tests/StubPaymentProviderTests.cs, docs/demo-runbook.md (Step 5b + affordance box + Known gaps)
date: 2026-06-17
mode: solo
session-runner: Claude (Opus 4.8)
---

# Retrospective — Implementations 029: Config-gated payment-decline toggle (live-demo affordance)

## Outcome summary

Made the slice-4.6 payment-**decline** path triggerable in a live demo via a config threshold — the
richest failure beat (stock reserved, then **released back** when payment fails), previously test-only.
**No new domain behavior**: the entire decline→cancel→`ReleaseStock` chain already existed and was
integration-tested; this slice added only the runtime trigger.

- **`PaymentProvider.cs`** — new `PaymentDeclinePolicy(decimal? DeclineOverAmount)` record; `StubPaymentProvider` now takes the policy and declines when `command.Amount > threshold`, else approves. `DeclineOverAmount` null (the default, and in every test) → approve everything, exactly as round one.
- **`Program.cs` (Orders)** — registers `PaymentDeclinePolicy` from `Payment:DeclineOverAmount`, mirroring the `PaymentDeadline`/`CartActivityDeadline` config-singleton pattern; default unset.
- **`Program.cs` (AppHost)** — injects `Payment__DeclineOverAmount=100` into Orders inside a loud "DEMO AFFORDANCE — remove after the talk" comment block (the single, findable, removable knob).
- **`StubPaymentProviderTests.cs`** — unset approves all; at/under approves; over declines.
- **`docs/demo-runbook.md`** — Step 5b (the decline drive + the reserved-then-released contrast with 5a), a prominent "⚙️ affordance" box (on by default, how to change/remove, where the code lives), and the Known-gaps update.

**Tests**: full backend suite green — **122** (Catalog 9, Inventory 21, Orders **89** — +4 from the new threshold cases, CrossBc 3); `dotnet format --verify-no-changes` clean; AppHost builds. The existing `PaymentAuthorizationTests` (approve via the default stub; decline via the swapped `DecliningPaymentProvider`) are **untouched** — the default-unset policy preserves approve-all, so no test churn.

**Spec movement**: **none, by design** — the behavior is the already-shipped `order-lifecycle` requirement *"Cancel an order when payment is declined"* (slice 4.6). No OpenSpec/workshop/narrative change; the **runbook** is the durable record of the knob.

## What worked

- **The decoupled design made this a trivial, contained change.** Because the decline decision is a Klefter event on the Order stream and the cancellation is a handler reacting to stream state — not a branch inside a service call — the whole compensation path was built and tested slices ago. Reading `PaymentHandlers.cs` confirmed the toggle touches *none* of it; the slice is one record, one constructor, one config read, one AppHost line, one test file. The flagged "effort" turned out to be near-nil because the architecture had already done the hard part.
- **An in-repo precedent settled the shape without a fork.** `PaymentDeadline`/`CartActivityDeadline` are config singletons read from `builder.Configuration` and injected into handlers; `PaymentDeclinePolicy` is the same pattern, so there was no design decision to surface — only the owner's already-made Option-B choice (threshold vs. `DeclineAll`).
- **Default-unset = zero blast radius.** Making approve-all the default (the demo value lives only in the AppHost env injection, which tests don't load) meant the existing approve/decline tests and round-one behavior are completely undisturbed — the affordance is purely additive.
- **Documenting it as "on by default, here's how to remove it" pre-empts the owner's stated fear.** The explicit ask was "so I don't run into this and go 'what the heck?'" — answered with a loud AppHost comment block, the in-code policy comments, and a dedicated runbook box, all naming the one line to delete after the talk.

## What was harder / notable

- **The honest "no spec delta" call.** It would have been easy to manufacture an OpenSpec change, but the decline behavior is already a requirement; the toggle decides *when the stub says no*, a runtime detail. Naming the delta as **none** (and pointing the durable record at the runbook instead) is the correct, non-confabulated closure — the edge case CLAUDE.md's spec-delta loop explicitly allows.
- **Threshold semantics needed a deliberate boundary.** "Over the threshold" declines; "at the threshold" approves (`> t`, not `>= t`). Pinned with an explicit `InlineData(100)` approve case so the boundary is a tested decision, not an accident.
- **The decline `Reason` is intentionally verbose** (`"declined (demo): order total … exceeds threshold …"`) for teaching value in the audit/trace, distinct from `DecliningPaymentProvider`'s terse `"declined"`. It flows only into `PaymentAuthFailed.Reason`; the terminal `OrderCancelled` reason is always `payment_declined`, so the existing assertions are unaffected.

## Methodology refinements

- **"Is it built, or just untriggerable?" is worth asking before scoping a feature.** The flag read as "payment-decline isn't demo-able," which sounds like missing functionality; recon showed the functionality was complete and only the *trigger* was missing. Distinguishing the two collapsed an apparent feature into a one-line affordance. A good habit when a stakeholder asks "how hard is X" — check whether X exists behind a seam first.
- **A demo affordance earns loud, redundant documentation precisely because it is non-production-faithful.** Three pointers (AppHost comment, code comments, runbook box) is not over-documentation here — the knob silently changes order outcomes, so every place someone might debug from should name it. This is the opposite of the usual "don't over-comment" default; the trigger is the surprise factor, not the line count.

## Outstanding / next-session inputs

- **One-time live re-confirm owed.** The threshold *decision* is unit-tested and the decline *chain* is integration-tested, but the two haven't run together on a live boot (over-threshold order → `payment_declined` → reserved stock released back). Whoever next boots the stack should run runbook Step 5b once and confirm `reserved 5 → 0`. Noted in the runbook's "Last verified."
- **Remove the affordance after the talk** (or keep it as a permanent teaching toggle — owner's call). The one AppHost line is the switch.
- **Payment-timeout** remains config-only (shorten `Orders:PaymentTimeout`); not worth a live beat (dead air).
- **Cadence**: #72 (interleave) reset the counter; this is the **1st implementation** since — budget intact. Seed automation remains the other named follow-up.
- **Carry-forwards (unchanged):** no frontend CI job; the flaky `PaymentAuthorizationTests` Wolverine-shutdown race; NU1507; node-orphan hygiene before a boot; CritterWatch trial expires 2026-07-10.

## Spec-delta — landed?

**Named-none, forward-confirmed.** The prompt named **no spec delta** — this is a demo affordance over the already-shipped, already-spec'd slice-4.6 decline path, so there is no OpenSpec, workshop, or narrative change to make. That is exactly what shipped: behavior unchanged, a config-gated trigger added, and the durable record placed in `docs/demo-runbook.md` (the operational home) rather than a spec layer. No confabulated requirement was invented to satisfy the loop.
