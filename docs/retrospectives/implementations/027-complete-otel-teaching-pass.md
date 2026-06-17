---
retrospective: 027
kind: implementations
prompt: docs/prompts/implementations/027-complete-otel-teaching-pass.md
deliverable: src/CritterMart.{Catalog,Inventory,Orders}/Program.cs (Marten TrackConnections=Verbose + TrackEventCounters + using JasperFx.OpenTelemetry), src/CritterMart.ServiceDefaults/Extensions.cs (AddMeter Marten + Wolverine:*), docs/decisions/005-opentelemetry-tracing-enabled.md (Realization note → fully realized), docs/research/otel-trace-walkthrough.md (new teaching artifact)
date: 2026-06-17
mode: solo; one owner-decided fork (visual-capture approach)
session-runner: Claude (Opus 4.8)
---

# Retrospective — Implementations 027: Complete the OpenTelemetry teaching pass

## Outcome summary

Closed both OTel deferrals that chore/002 left open since 2026-05-28: the Marten verbose tracking + event counters (ADR 005's named-but-unrealized half) and the in-browser trace visual (now a documented, CLI-confirmed walkthrough + screenshot guide). The cross-service purchase trace now shows the event-store layer that was previously invisible.

- **`src/CritterMart.{Catalog,Inventory,Orders}/Program.cs`** — `opts.OpenTelemetry.TrackConnections = TrackLevel.Verbose` + `opts.OpenTelemetry.TrackEventCounters()` in each `AddMarten` block; `using JasperFx.OpenTelemetry` (the `TrackLevel` enum's real home in Marten 9.6.0). No handler, projection, or index touched.
- **`src/CritterMart.ServiceDefaults/Extensions.cs`** — `.AddMeter("Marten")` + `.AddMeter("Wolverine:*")` in `ConfigureOpenTelemetry`'s `WithMetrics` pipeline, so the `marten.event.append` and Wolverine message counters actually export. The wildcard covers `Wolverine:Catalog/Inventory/Orders` from the shared ServiceDefaults without hardcoding a per-service name.
- **`docs/decisions/005-opentelemetry-tracing-enabled.md`** — a `## Realization` note recording the two-phase landing (chore/002 partial → 027 complete); ADR 005 now **fully realized**.
- **`docs/research/otel-trace-walkthrough.md`** (new) — the reproduction script, the observed span hierarchy, the `marten.event.append` event-type table, and the exact Aspire-dashboard navigation for the owner's screenshots.

**Live verification (Aspire boot, 2026-06-17):** all three services booted **Healthy with the new wiring** (no startup crash from the meter wildcard or the Marten OTel config); the full W1→W4 saga from one `POST /orders` drove to `confirmed` — stock `available 100→98`, `reserved 2→0`, `committed 0→2`, three broker hops + two in-process hops under one correlated trace; **no telemetry/export errors** in the boot log. **Tests**: full suite green — **113** (Catalog 9, Inventory 21, Orders 80, CrossBc 3), `dotnet build` clean.

**Spec movement**: ADR 005 gained a Realization note and flipped to fully realized, cross-referencing the new walkthrough artifact. No OpenSpec change (this is infra + ADR realization, not a capability delta).

## What worked

- **The live boot caught a bug a code-only pass would have shipped.** `TrackEventCounters()` emits a metric, but a metric only reaches the dashboard if the metrics pipeline registers `.AddMeter("Marten")` — which `ConfigureOpenTelemetry` never did (it had `.AddSource` on tracing, no meters anywhere). Wiring only the Marten side would have looked done and exported nothing. The `wolverine-observability-opentelemetry-setup` skill names this exact "source without meter" anti-pattern; reading it before writing turned an invisible gap into a one-line fix.
- **Verifying the API against the restored assembly, not the docs, was decisive.** ctx7 returned the Marten *master* shape (`TrackLevel` under `Marten`). The repo pins **9.6.0**, where the enum lives in `JasperFx.OpenTelemetry` (the JasperFx 2.0 consolidation). The build failed three times on `using Marten` / `using Marten.Services` before metadata-reflecting `OpenTelemetryOptions.TrackConnections`'s property type gave the real namespace. This is the project's ctx7 rule earning its keep — and a reminder to reflect the *pinned* assembly when docs and build disagree.
- **The Wolverine meter ≠ ActivitySource nuance saved a silent miss.** The source is `"Wolverine"` but the meter is `Wolverine:{ServiceName}`. A naive `.AddMeter("Wolverine")` would have registered nothing. The `:*` wildcard is the right tool for shared ServiceDefaults across three differently-named services.
- **The fully-automatic saga made the trace a single-call demo.** One `POST /orders` cascades reserve → reserved → authorize → decision → confirm + commit, so the richest possible cross-service trace needs no orchestration — ideal for the talk and for a reproducible walkthrough.

## What was harder / notable

- **The Aspire dashboard's telemetry is not headlessly queryable.** It is in-memory and browser-only; there is no Prometheus scrape endpoint wired (out of scope). So "confirm the counter emits" had to be triangulated: services boot healthy *with the meters registered* (proves no wiring crash), the event-appending saga runs to completion (proves the spans + appends the counter counts are generated), the OTLP exporter is active, and the log shows no export errors. `dotnet-counters` would give a direct counter read but is not installed; installing + fighting its live TUI was diminishing returns given the owner owns the actual visual.
- **Owner-captured screenshots is the honest division of labour, not a shortcut.** The chore/002 precedent (visual = user) plus the talk-prep context (the owner wants to control the exact slide frames) made the "I boot+drive, owner screenshots" fork the right call. The walkthrough documents the observed span tree and exact click-paths so the capture is mechanical.

## Methodology refinements

- **An ADR that names its config verbatim is a no-fork, same as a skill- or convention-settled idiom (retros 024/025/026).** ADR 005 already named `TrackConnections = TrackLevel.Verbose` + `TrackEventCounters()`; the only genuine fork was *how the visual gets captured*, which is process, not code. The heuristic holds across a fourth slice: surface a fork only after confirming no ADR, convention, precedent, or skill already settles the code — here the ADR did, and the owner forked only on the process question.
- **For library API on a pinned version, the restored assembly is the source of truth — reflect it the moment docs and build disagree.** ctx7/master docs are a starting point, not the contract. Metadata-loading the pinned DLL to read a property's type resolved in one step what three `using`-guess rebuilds did not.
- **A "verification-heavy" pick can still carry a real, ADR-bounded code change.** The OTel pass was framed as artifact-only, but realizing ADR 005's deferred half (two infra wirings) was both in-scope and what made the artifact worth capturing. The boot is what proved the code, not just documented the trace.

## Outstanding / next-session inputs

- **Owner action (live):** the Aspire stack is left running with traces in memory (ephemeral). Dashboard `http://localhost:15090` (per-boot login token in the console); the trace correlates to orderId `80ced68e-5056-4b6e-85f0-52e1dc5b4514`. Capture the Traces waterfall + the `marten.event.append` Metrics view, then tear down (`Stop-Process -Name CritterMart.*`).
- **Stale memory to correct (not a code issue):** `StockCommitted` is appended in `CommitStock.cs:21` and the `StockLevelView.committed` field is real — contradicting the "`StockCommitted` deliberately unmodelled" carry-forward in the next-pickup memory. The memory note is stale; the walkthrough reflects the code.
- **Cadence**: this was the **2nd implementation** after the #68 design-return. **One more implementation may run, then a design-return interleave is due** — a narrative or a `tidy:` (all four BCs are workshopped, so not a new workshop). The natural next tidy remains the post-#69 **§6.1 flip + `openspec archive harden-add-to-cart-snapshot`** (open-pick option 4, deliberately not folded here).
- **Carry-forwards (unchanged, non-blocking):** the **"My Orders" list** (Gap #3); the **cart identity-transport harmonization**; `ChangeCartItemQuantity`'s inline `Results.Problem` guard harmonization; `AddToCart` `Quantity>0`/non-blank `Sku` validation; no frontend CI job; focus-ring enhancement; Docker container grouping; **CritterWatch trial expires 2026-07-10**; the flaky `PaymentAuthorizationTests` shutdown race (rerun remedy).

## Spec-delta — landed?

**Named delta landed.** The prompt named: ADR 005 gains a `## Realization` note and flips to fully realized, cross-referencing a new `docs/research/otel-trace-walkthrough.md`. That landed — ADR 005 records the two-phase realization and the cross-reference; the walkthrough exists with the CLI-confirmed reproduction, span hierarchy, event-type table, and screenshot guide. Four-step closure: **prompt named → session executed → this retro confirms → ADR 005 recorded.** No OpenSpec capability delta (infra + ADR realization), honestly named as such.
