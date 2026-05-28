# ADR 012: Critter Stack 2026 Upgrade (Wolverine 6 / Marten 9)

**Status**: Accepted

## Context

Slice 1.1 (PR #8) shipped on **Wolverine 5.39.3 / Marten 8.35.0**. The NuGet restore audit flagged **NU1904 — Marten 8.35.0 carries a critical-severity advisory (GHSA-vmw2-qwm8-x84c)**, surfaced in the slice 1.1 retrospective and deliberately deferred out of that slice as a package-line decision. Meanwhile the **Critter Stack 2026 line is GA** — Wolverine 6, Marten 9, JasperFx 2 — which carries the advisory fix and modernizes the foundation.

Two questions came with the upgrade. First, **Wolverine 6 extracted the Roslyn runtime compiler out of core `WolverineFx`**, so the default `TypeLoadMode.Dynamic` (dev/test mode) throws at startup unless a compiler is supplied — either the opt-in `WolverineFx.RuntimeCompilation` package (keep Dynamic) or pre-generated code with `TypeLoadMode.Static` (AOT/production). Second, **Marten 9 flips several `StoreOptions` defaults** to best-performance values (`QuickWithServerTimestamps` append, `EnableBigIntEvents`, identity-map-for-aggregates, etc.), revertable in one line via `RestoreV8Defaults()`. (Marten 9, unlike Wolverine, removed runtime codegen *entirely* and replaced it with automatic source generation — no opt-in, and nothing to convert for a project with no projections or compiled queries.)

## Decision

1. **Upgrade the whole stack to the Critter Stack 2026 line.** `WolverineFx.* 6.1.0`, which transitively resolves **Marten 9.2.0**, **JasperFx 2.2.0**, **Weasel 9.0.1**. This resolves the Marten 8.35.0 advisory. No explicit `Marten` pin is needed — the transitive resolution already lands on the latest 9.x.

2. **Restore Wolverine runtime codegen via `WolverineFx.RuntimeCompilation`, keeping `TypeLoadMode.Dynamic`, for round one.** This is the "just run it" path that matches round one's no-production-deploy posture. Pre-generated code + `TypeLoadMode.Static` (AOT-clean, trimmer drops Roslyn) is the production follow-up, **deferred** to the same future infra session that stands up Aspire and OpenTelemetry.

3. **Adopt Marten 9's flipped `StoreOptions` defaults; do not call `RestoreV8Defaults()`.** The behavior-change risks the migration guidance warns about — aggregate self-mutation under `UseIdentityMapForAggregates`, `Int32` version overflow on multi-stream projections, removed inline-lambda projection registration — all require aggregates or projections that the Catalog document store does not have. Adopting the defaults keeps the reference architecture current for 2026.

## Consequences

The advisory is gone (`dotnet restore` audit clean; no NU1904). The upgrade required **zero code changes** — the API surface the Catalog service uses (`AddMarten(...).IntegrateWithWolverine().ApplyAllDatabaseChangesOnStartup()`, `StreamIdentity.AsString`, `AutoApplyTransactions`, `CreationResponse`, `WolverineContinue`, `ValidateAsync`, `CheckExistsAsync`, `Store`, `Events.StartStream`) is stable across the jump. The full solution builds, both slice 1.1 GWT scenarios pass unchanged, and the upgraded service was verified to boot on Kestrel (Dynamic codegen, no `IAssemblyGenerator` throw) and serve `PublishProduct` (201 / 409) over real HTTP.

Costs and risks: Dynamic mode pays a first-use codegen cost at startup — acceptable for round one, and the explicit reason the Static-mode follow-up is named rather than forgotten. Marten's storage is now source-generated and built lazily on first use (validation moved off `StartAsync`); `ApplyAllDatabaseChangesOnStartup()` plus the document-type-exercising tests keep that covered. A second NuGet source (`jasperfx`) without source mapping makes `dotnet list package --vulnerable` 403 on that feed; the build-integrated NU1904 audit against nuget.org is the authoritative vulnerability signal and it is clean.

Rejected alternatives. **Pin Marten 8.x** — leaves the critical advisory unaddressed; the entire trigger for this session. **`RestoreV8Defaults()`** — unnecessary given no aggregates to protect, and a line we would only later remove. **Static mode now** — premature for round one; adds a `codegen write` build step and Dockerfile wiring with no production target yet. **.NET 8** — moot; the 2026 line drops net8.0 and CritterMart already targets net10.0.

This ADR introduces a new structural fact (the build/codegen posture); `docs/rules/structural-constraints.md` gains a paired Build & code generation section in the same PR, per that file's header rule.
