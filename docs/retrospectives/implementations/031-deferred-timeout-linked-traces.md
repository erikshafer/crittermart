---
retrospective: 031
kind: implementations
prompt: docs/prompts/implementations/031-deferred-timeout-linked-traces.md
deliverable: src/CritterMart.Orders/Observability/TemporalAutomationTracing.cs (new linked-root seam), src/CritterMart.Orders/Ordering/PaymentTimeoutHandler.cs + src/CritterMart.Orders/Shopping/CartAbandonmentHandler.cs ([WolverineLogging(telemetryEnabled:false)] + Envelope + linked-root wrap + outcome tags), tests/CritterMart.Orders.Tests/TemporalAutomationTracingTests.cs (new, 2 tests), docs/demo-runbook.md (Step 6 callout finding → fixed)
date: 2026-06-17
mode: solo; one owner-decided fork (approach), after a feasibility check
session-runner: Claude (Opus 4.8)
---

# Retrospective — Implementations 031: Deferred-timeout linked traces

## Outcome summary

The two Bruun temporal automations (Workshop 001 slices 4.7 + 3.4) no longer balloon the trace that scheduled them. A fired `OrderPaymentTimeout` / `CartActivityTimeout` now runs in its **own span-linked root trace** instead of parenting back into the placement / add-to-cart trace, so the `POST /orders` waterfall — the demo centerpiece — reads ~50 ms whenever it is opened, and the fired deadline stays observable as its own `order.payment.timeout` / `cart.activity.timeout` trace, a span link away from the request that armed it. Production-grade pattern, not a demo hack: it ships the OpenTelemetry idiom for asynchronous follow-up work (links, not parent-child edges).

- **`src/CritterMart.Orders/Observability/TemporalAutomationTracing.cs`** (new) — a static `ActivitySource("CritterMart.Orders")` (already collected by `ServiceDefaults`' `AddSource(ApplicationName)` — no extra wiring) + `StartLinkedRoot(name, envelope)`: parses `envelope.ParentId` (the W3C traceparent Wolverine stamped on the delayed envelope) into an `ActivityLink`, clears `Activity.Current`, and starts a new root. Carries the full why.
- **`PaymentTimeoutHandler.cs` / `CartAbandonmentHandler.cs`** — `[WolverineLogging(telemetryEnabled:false)]` suppresses Wolverine's own parented span; the handler injects `Envelope` and wraps its body in `using var activity = StartLinkedRoot(...)`, tagging the outcome (`order.id` + `noop`/`cancelled`; `cart.id` + `noop`/`rescheduled`/`abandoned`). Domain logic byte-for-byte unchanged.
- **`TemporalAutomationTracingTests.cs`** (new, 2) — under an ambient placement Activity set as `Activity.Current`, the timeout is a new **root** (not nested) **linked** back to placement; a context-less envelope yields a clean unlinked root.
- **`docs/demo-runbook.md`** — Step 6 trace-duration callout flips from documenting the balloon-as-finding to documenting the realized fix, resolving its own "a future slice could…" forward-reference.

**Tests**: Orders **91 green** (89 unchanged + 2 new); Catalog 9, Inventory 21 green (Postgres-only suites; CrossBc 3 untouched — RabbitMQ path, exercised in the live boot). `dotnet build` clean; `dotnet format` no-op (already formatted).

**Live verification (Aspire boot, 2026-06-17):** clean boot, all three services Healthy with the changed handlers (no Wolverine codegen issue from the new attribute or `Envelope` param), auto-seed of all three SKUs. Drove both demoed saga routes: happy (crit-001 ×2, $49.98) → `confirmed` in ~1 s; decline (crit-001 ×5, $124.95 > $100) → `cancelled · payment_declined` with crit-001 `reserved` released back to 0 (no leak). **Zero errors/warnings/exceptions** in the boot+run log. Torn down clean (all ports free, no leftover containers).

**Spec movement**: none — observability, no domain behavior change (named "no spec delta" up front; forward-confirmed below). The runbook callout was updated for doc-accuracy.

## What worked

- **The seam test caught the load-bearing .NET gotcha before it shipped.** `ActivitySource.StartActivity(name, kind, parentContext: default)` does **not** create a root when `Activity.Current` is set — an invalid `ActivityContext` is treated as "no override" and it silently re-parents onto Current, re-nesting the timeout right back under placement (the exact bug). Writing the test with an *ambient* placement Activity as `Current` (the real live condition) turned a one-line assumption into a caught failure; the fix is to clear `Activity.Current` before `StartActivity`. Trusting my reading of the API would have produced a fix that looked done and changed nothing.
- **A feasibility check turned a "fragile" option into the recommendation.** The owner's first pitch ("new linked trace") looked too hard — Wolverine exposes no `DeliveryOptions` flag for a new root. But verifying three facts against the pinned 6.8 assembly + ServiceDefaults (`Envelope.ParentId` is the W3C traceparent; `Envelope` is injectable; `AddSource(ApplicationName)` already collects the source) showed the clean version is **documented-APIs-only** — suppress Wolverine's span, emit our own root with a link, standard `System.Diagnostics` for the rest. The "fragile" version is the one that hooks Wolverine's activity creation; we did not need it.
- **Framing the fork as demo-vs-prod surfaced the right answer.** Best-for-demo (suppress entirely) and best-for-prod (a linked root) point in different directions; the compromise (linked root) satisfies both **and** is the teachable, production-grade pattern — strictly better for a reference architecture than hiding the timeout to fake a clean screenshot. The owner asked the demo-vs-prod question directly; answering it, rather than defaulting to the cheapest fix, changed what shipped.
- **The change is inert in the domain tests by construction.** `StartActivity` returns null with no sampler, so `using var` + `?.SetTag` no-op without an exporter — which is why all 89 existing Orders tests (which boot the real Wolverine host and invoke the timeouts) stayed green untouched, while the 2 new tests opt *into* a listener to assert the topology.

## What was harder / notable

- **The trace *visual* is irreducibly owner-territory, so making the timeout fire live had near-zero headless value.** OTLP traces are not headlessly queryable and the Aspire dashboard is Blazor/SignalR (does not render headless — the instrument-vs-system discipline). The live boot's real job was narrow: prove the full stack boots clean with the changed handlers and the demoed flows still work. The deferred-timeout code path itself (which only runs at the deadline) is covered by the 91 integration + unit tests; its dashboard appearance is the owner's eyeball.
- **The cancel-path `ReleaseStock` lands as its own root, not nested under the timeout trace.** The `using var activity` disposes when the handler returns; Wolverine sends the cascaded `ReleaseStock` *after* that, with `Activity.Current` cleared — so it starts a fresh clean root rather than nesting under `order.payment.timeout`. The demo-critical property (placement trace clean) holds, and the cancel-via-timeout path is a ~never-demoed prod safety net. Nesting it would mean inline-publishing within the activity scope (away from the cascading-over-PMvH preference) — a deliberate non-choice, flagged here rather than silently diverging from the approved preview's "timeout → ReleaseStock → Inventory" sketch.

## Methodology refinements

- **When the user asks "best for demo vs best for prod, can we compromise?", answer all three — the compromise is usually the prod-correct pattern and worth the extra lines.** The instinct under a "solidify, minimal" steer is the cheapest fix (suppress). But the cheapest fix was prod-wrong, and the project is a *teaching reference architecture*, so the prod-grade compromise (span links) was the right altitude. The demo-vs-prod decomposition is a reusable lens for "polish" forks.
- **For a behavior that hinges on one library/runtime API semantic, write the self-validating test first — it is faster and surer than reading docs.** The `parentContext: default` rooting question was settled in one test run; no amount of doc-reading would have been as certain. This generalizes the 027 lesson ("reflect the pinned assembly when docs and build disagree") to runtime behavior: when behavior is uncertain, assert it.
- **Verify feasibility before posing a fork, so the options offered are real.** The first fork I drafted under-sold the compromise as "fragile"; a short recon pass (assembly + ServiceDefaults) reclassified it as clean and made it the recommendation. Posing a fork on a stale feasibility read wastes the owner's decision.

## Outstanding / next-session inputs

- **Owner action (live trace eyeball):** to *see* the fix, boot the stack and open a `POST /orders` trace on the Aspire dashboard (`:15090`, per-boot login token in the console) — it stays ~50 ms. To watch the deferred timeout fire as its own linked trace without waiting 10 min, temporarily shorten `Orders:PaymentTimeout` (e.g. add `.WithEnvironment("Orders__PaymentTimeout","00:00:20")` to the `orders` resource in the AppHost, **revert after**), place an order, wait ~20 s, and confirm the placement trace is unchanged while a separate `order.payment.timeout` trace appears, span-linked back.
- **Optional follow-up (flagged, not done):** an ADR 005 one-line cross-reference recording that deferred temporal-automation timeouts use span-linked root traces (anchors the decision next to the OTel ADR). Left out to keep scope tight; trivial to add in a `tidy:` if wanted.
- **Optional follow-up (flagged, not done):** link the cancel-path `ReleaseStock` to the timeout trace (inline-publish within the activity, or set a link) so the compensation reads as one navigable unit — only if a future demo actually shows the timeout-cancel path.
- **Cadence**: an Orders-touching implementation, but observability-only with no spec delta — consistent with the handoff's read that demo-solidification work is "mostly non-BC." Treat as non-binding on the design-return counter (#73 remains the substantive 1st BC-impl since the #72 interleave). If counted as a soft tick, ~1 more before an interleave is due (narrative or `tidy:`; all four BCs are workshopped).
- **Carry-forwards (unchanged, non-blocking):** **POST-TALK CHORE** — delete the `Payment__DeclineOverAmount=100` line in the AppHost; the flaky `PaymentAuthorizationTests` shutdown race (rerun remedy); NU1507 (2 nuget sources, no source mapping) + AppHost SDK pin drift; no frontend CI job; **CritterWatch trial expires 2026-07-10**; cart identity-transport harmonization; product detail (Gap #2); focus-ring enhancement; Docker container grouping.

## Spec-delta — landed?

**Named-none, forward-confirmed.** The prompt named "no spec delta (observability; no domain behavior change)", and that holds: no event, command, projection, aggregate, index, SHALL, narrative Moment, or workshop slice changed — the timeout *behavior* is byte-for-byte identical, only the *trace topology* of the fired message differs. The one durable record updated for accuracy is `docs/demo-runbook.md`'s Step 6 callout (balloon-finding → realized-fix, forward-reference resolved). No OpenSpec capability delta — honestly an observability change, named as such up front and confirmed at close.
