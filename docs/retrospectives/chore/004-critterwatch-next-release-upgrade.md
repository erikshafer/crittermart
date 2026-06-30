---
retrospective: 004
kind: chore
prompt: docs/prompts/chore/004-critterwatch-next-release-upgrade.md
deliverable: Directory.Packages.props (CritterWatch + Wolverine.CritterWatch alpha.3 → beta.1; WolverineFx.* 6.12.0 → 6.16.0; coupling + MessagePack-CVE comment rewrites); src/CritterMart.Orders/Ordering/OrdersAwaitingPayment.cs + Shopping/CartsAwaitingActivity.cs (stateless projections + read-shaped row records); src/CritterMart.Orders/Features/PlaceOrder.cs + AddToCart.cs (read-time deadline endpoints); src/CritterMart.Orders/Program.cs (generic projection registration + comments); tests/CritterMart.Orders.Tests/{OrdersAwaitingPaymentProjectionTests,CartsAwaitingActivityProjectionTests,PaymentTimeoutTests,CartAbandonmentTests}.cs; docs/retrospectives/chore/004-critterwatch-next-release-upgrade.md (this file)
date: 2026-06-30
mode: solo maintenance; CritterWatch coupling per Directory.Packages.props + the dependency-update runbook; Marten-9.12 compat fix added when the bump surfaced a projection regression
session-runner: Claude (Opus 4.8)
---

# Retrospective — Chore 004: CritterWatch next-release upgrade (→ beta.1 / Wolverine 6.16.0)

## Outcome summary

CritterWatch moved **`1.0.0-alpha.3` → `1.0.0-beta.1`** and, in lockstep, the whole Critter Stack line moved **WolverineFx.* `6.12.0` → `6.16.0`** (Marten `9.12.0` + JasperFx.Events `2.18.1` pulled transitively). beta.1's nuspec was read first-hand — it targets **GA WolverineFx 6.16.0**, not a prerelease alpha line, which is exactly the condition Erik set for taking the upgrade ("revisit when a GA-Wolverine CW ships"; alpha.4's prerelease-6.14 line was the reason alpha.4 was skipped). This **resolves the standing "DO NOT merge Wolverine 6.13.x" warning** the sanctioned way — past 6.13.x straight to 6.16.0, atomically with CritterWatch — so the stranded Dependabot 6.13.x bumps are obsolete. The MessagePack CVE (`GHSA-hv8m-jj95-wg3x`) is **still unresolved on beta.1** (transitive MessagePack `2.5.302` < the `3.0.214` fix), so the audit suppression stays, comment re-verified-dated.

**The bump was not zero-code.** Marten 9.12 surfaced a projection regression: an inline `SingleStreamProjection` registered as an *instance* (`Projections.Add(new T(arg), …)`) is re-materialized by the runtime, so **constructor-injected state is dropped** — the two "awaiting-list" Bruun todo-list projections read their captured timeout back as `default(TimeSpan)`, so the view's visible `Deadline` silently became "now". Fixed by making both projections **stateless fact-recorders** (store `PlacedAt` / `LastActivityAt`) and moving the deadline policy to the **read side** (the GET endpoints inject the existing `PaymentDeadline` / `CartActivityDeadline` singletons and compute `Deadline = timestamp + duration`). Impact was cosmetic-only — the cancellation/abandonment decisions run off the scheduled messages and the Order/Cart streams, never this view.

Verified three ways: full solution build (0 warnings / 0 errors); **149/149 tests green** (the two integration failures the bump introduced are fixed, nothing else regressed); and a **full live boot** on the real Aspire stack — all three services + the **CritterWatch console came up clean** (PID confirmed; the coupling crash is a startup `TypeLoadException`, so a healthy boot is the proof), and the refactored `GET /orders/awaiting-payment` returned a deadline ~7 min in the future (the `Orders__PaymentTimeout` knob), not "now".

## What worked

- **Read the nuspec, don't guess the floor.** `curl`-ing `wolverine.critterwatch/1.0.0-beta.1/*.nuspec` showed `WolverineFx 6.16.0` (and `WolverineFx.Marten/Http.Marten/SignalR 6.16.0`, `Marten 9.12.0`) directly — turning "is beta.1 safe to take?" from a judgment call into a fact (GA vs prerelease), which is the whole gating question.
- **Empirical root-causing beat speculation.** The failure (`Deadline ≈ now` despite a 10-min config) had two plausible stories (TimeProvider-sourced `e.Timestamp` vs dropped timeout). The unit test passing (direct `new Projection(10min)`) while the integration test failed, plus a one-line hardcode probe (`Add(10min)` → green), isolated it to "Marten doesn't preserve the instance's ctor state" without needing the changelog (ctx7 was quota-blocked all session).
- **The fix is a better design, not just a workaround.** Stateless projection + read-time policy is the more idiomatic CQRS split (the view stores facts; the endpoint applies config). The bump forced the cleaner shape.
- **Live boot earned its keep.** Both the CW coupling (startup `TypeLoadException`) and the projection fix (a live deadline value) are runtime-only truths a green test summary wouldn't fully prove.

## What was harder than expected

- **The Marten 9.12 instance-projection-state change is undocumented-to-us and subtle.** It only manifests through the runtime projection path, so it slipped past the build and the unit tests — only the two integration tests that drive the projection through Marten caught it. Diagnosis took several iterations of arithmetic before the hardcode probe settled it.
- **ctx7 monthly quota was exhausted** (as the prior session flagged). Fell back to the official Aspire/Marten release notes via WebFetch and to first-hand nuspec + empirical probes — no training-data guessing, per the docs rule.

## Methodology refinements that emerged

1. **A Critter Stack dependency upgrade needs BOTH a full `dotnet test` and a live boot — they catch different failure classes.** The CW coupling is a startup crash (live boot only); the Marten projection-state change is a runtime behavior shift (integration tests only). A build + unit tests would have shipped both bugs.
2. **Prefer stateless projections; put per-projection config on the read side.** Constructor-injected projection state is fragile across Marten versions (it re-materializes projections). When a projection needs config, store the raw fact and apply the policy where DI lives (the endpoint), not in the projection instance.
3. **A "version bump" chore can legitimately grow a code fix when the bump breaks behavior** — bundling the fix is correct (the PR can't go green without it); the retro records the divergence from the prompt's "Directory.Packages.props only" plan.

## Outstanding items / next-session inputs

1. **MessagePack CVE still suppressed** — beta.1 transitively pulls MessagePack `2.5.302` (< `3.0.214`). Re-check on the next CritterWatch release; drop the `<NuGetAuditSuppress>` when it moves to MessagePack 3.x.
2. **Consider reporting the Marten 9.12 instance-projection-state behavior upstream** to JasperFx (instance-registered inline projections losing constructor state). The CritterMart fix doesn't need it, but other Critter Stack apps using `Projections.Add(new T(arg), …)` would hit the same.
3. **Dependabot cleanup:** #104 (critter-stack group), #108 (CritterWatch alpha.4), #110 (Wolverine.CritterWatch alpha.4) are superseded by this PR — closed. #105 (aspire group) is superseded by the separate Aspire PR #116. #106 (otel), #107 (test-stack), #109 (Swashbuckle) remain as separate concerns.
4. **Post-talk:** the four AppHost demo knobs still want deleting after the talk (unchanged by this PR).

## Spec-delta — landed?

**None named, and none shipped** — this chore changes no domain behavior. The `Directory.Packages.props` coupling comment is the durable operational record of the new CW↔Wolverine pin. The read-time deadline refactor is an internal restructuring that **preserves the HTTP contract** (the awaiting-list responses still carry `Deadline`); no narrative, workshop, or OpenSpec capability changed, and the unchanged integration assertions (deadline in the future; row vanishes on terminal events) are the regression guard. The retro forward-confirms the named-none.

## Process notes

- One PR bundles `chore:` (the version coupling) and the Marten-9.12 compatibility fix it necessitated.
- Branch: `chore/critterwatch-beta1`.
- The upgrade crosses the previously-held 6.12.0 Wolverine pin to 6.16.0 — Erik's merge is the conscious blessing of that line move; the coupling comment + this retro record why it is now safe (CW beta.1 ships against GA 6.16.0).
