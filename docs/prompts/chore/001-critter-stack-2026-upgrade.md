# Prompt: Chore 001 — Critter Stack 2026 upgrade (Wolverine 6 / Marten 9)

**Kind**: chore (cross-cutting dependency upgrade; bootstraps the `chore/` prompt + retrospective kinds)
**Files touched**: `docs/prompts/chore/001-critter-stack-2026-upgrade.md` (new, this file); `Directory.Packages.props` (version bumps + new package); `src/CritterMart.Catalog/CritterMart.Catalog.csproj` (RuntimeCompilation reference); `src/CritterMart.Catalog/Program.cs` (if API surface shifts); `docs/decisions/012-critter-stack-2026-upgrade.md` (new ADR); `docs/decisions/README.md` (index row); `docs/rules/structural-constraints.md` (new Build/codegen section + Document History bump); `docs/retrospectives/chore/001-critter-stack-2026-upgrade.md` (forthcoming, session close)
**Mode**: solo maintenance with the upstream JasperFx migration skills (`wolverine-migration-v5-to-v6`, `marten-migration-v8-to-v9`) as the authoritative guidance
**Commit subject(s)**: `chore: upgrade to Critter Stack 2026 (Wolverine 6 / Marten 9)` + `docs: add ADR 012 and structural-constraints codegen posture` (one PR)

## Framing

PR #8 shipped the first slice on **Wolverine 5.39.3 / Marten 8.35.0**. The restore surfaced **NU1904 — Marten 8.35.0 carries a critical-severity advisory (GHSA-vmw2-qwm8-x84c)**, flagged but deliberately deferred out of the slice. This session is the deliberate response: pivot off normal slice development and jump the whole stack to the **Critter Stack 2026 line — Wolverine 6.1.0 / Marten 9.x / JasperFx 2.x** — which resolves the advisory and modernizes the foundation.

This is a **non-slice, cross-cutting maintenance PR**. It also satisfies the **design-return cadence**: PR #8 was the first Catalog implementation PR, so a non-implementation interleave is exactly what the cadence calls for next.

Three decisions were settled in conversation before this prompt was frozen, and are recorded here as intent:

1. **Wolverine runtime codegen via `WolverineFx.RuntimeCompilation` (Dynamic mode).** Wolverine 6 removed the Roslyn compiler from core, so the default `TypeLoadMode.Dynamic` throws at startup unless the compiler package is referenced. We add `WolverineFx.RuntimeCompilation` and keep Dynamic mode — the "just run it" path that matches round one's no-production-deploy posture. Static/AOT publishing (`codegen write` + `TypeLoadMode.Static`) is **deferred** to a later infra session. *(Marten 9, by contrast, removed runtime codegen entirely and replaced it with source generation that is automatic — no opt-in package, and nothing for us to convert because Catalog has no projections or compiled queries.)*
2. **Adopt Marten 9's flipped `StoreOptions` defaults; do not call `RestoreV8Defaults()`.** The new best-perf defaults (QuickWithServerTimestamps append, `EnableBigIntEvents`, identity-map-for-aggregates, etc.) are safe here: Catalog is a fresh-database document store with **no aggregates and no projections**, so the behavior-change risks the migration skill warns about (aggregate self-mutation, `Int32` version overflow, lambda-projection removal) do not apply. Adopting the defaults keeps the reference architecture pedagogically current for 2026.
3. **Full pipeline ceremony.** This upgrade carries an ADR (012), this frozen prompt, and a retrospective — consistent with the project's per-session discipline and appropriate for an ADR-worthy, advisory-driven, cross-cutting decision.

The upstream migration skills are the authoritative step-by-step source; this prompt does not restate them.

## Goal

The CritterMart solution builds and all tests pass on **Wolverine 6.1.0 / Marten 9.x / JasperFx 2.x**, the Marten 8.35.0 advisory is gone from `dotnet list package --vulnerable`/restore, and the upgraded Catalog service is verified to actually boot (the codegen change only manifests at startup) and serve `PublishProduct` over real HTTP. The decision is captured in ADR 012, the structural-constraints rule file records the new codegen/version posture, and the retro confirms spec-delta closure.

## Spec delta

No narrative or workshop changes — this session ships no slice behavior. The spec-shaped deltas are: a **new ADR cross-reference** (ADR 012, Critter Stack 2026 upgrade) added to `docs/decisions/README.md`; a **new structural-constraint** (Build & code generation posture: Wolverine runtime codegen via `WolverineFx.RuntimeCompilation`/Dynamic for round one; Marten codegen is source-generated with no `codegen write` step) added to `docs/rules/structural-constraints.md` with a paired Document History bump, per that file's "an ADR that changes a constraint pairs with a rule-file update in the same PR" rule. The `product-catalog` capability and Narrative 001 are unchanged; the upgraded code must still satisfy the existing slice 1.1 contract (proven by the unchanged tests passing).

## Orientation

Read in this order:

1. **`wolverine-migration-v5-to-v6`** + **`marten-migration-v8-to-v9`** skills — the authoritative breaking-change lists. The blockers that touch *our* code: Wolverine runtime-codegen removal (the startup throw) and the package line; everything else (Newtonsoft extraction, `IForwardsTo`, inline-lambda projections, aggregate self-mutation, namespace moves for `OperationRole`/`SnapshotLifecycle`) is N/A because Catalog is a document store with no projections, aggregates, or Newtonsoft usage.
2. **`docs/retrospectives/implementations/001-slice-1-1-publish-product.md`** — the Outstanding items section flagged this advisory as the trigger for this session.
3. **`Directory.Packages.props`** + **`src/CritterMart.Catalog/`** + **`tests/CritterMart.Catalog.Tests/`** — the actual surface being upgraded.
4. **`docs/decisions/011-...md`** + **`docs/decisions/README.md`** — ADR format and index convention (ADR 012 follows the established `# ADR NNN: Title` / Context / Decision / Consequences shape).
5. **`docs/rules/structural-constraints.md`** — the flat constraint list + its header rule on paired ADR/rule updates and its Document History convention.

## Working pattern

1. Bump `Directory.Packages.props`: `WolverineFx.Http` + `WolverineFx.Marten` → 6.1.0; add `WolverineFx.RuntimeCompilation` 6.1.0; pin `Marten` to the latest 9.x. Add the `WolverineFx.RuntimeCompilation` reference to `CritterMart.Catalog.csproj`.
2. Restore + confirm the resolved transitive line is Marten 9 / JasperFx 2 / Weasel 9 and the advisory is gone.
3. Build the full solution; fix any namespace/API breakages flagged by the compiler (expect few — verify `StreamIdentity`, `ApplyAllDatabaseChangesOnStartup`, `AutoApplyTransactions`, `CreationResponse`, `WolverineContinue`, `ValidateAsync` survive). Resolve any CS0104 ambiguous references with `using` aliases per the Marten skill.
4. Run the tests (both GWT scenarios). Then a **real docker-compose run** — boot the upgraded service and exercise happy (201) + duplicate (409) over HTTP, because the codegen change is a startup-time failure mode that in-memory tests share but a real Kestrel boot best demonstrates.
5. Author ADR 012; add the Build/codegen structural-constraint + Document History bump; add the decisions README row.
6. Author the retrospective; update the `next-pickup` memory (stack now v6/v9, advisory resolved, codegen posture).

## Out of scope

- **No Static/AOT publishing.** Dynamic mode + `WolverineFx.RuntimeCompilation`; `codegen write` + `TypeLoadMode.Static` is a deferred infra concern.
- **No `RestoreV8Defaults()`** — adopt the v9 defaults (decision 2).
- **No new slice behavior.** The `product-catalog` contract, Narrative 001, and Workshop 001 are untouched; the unchanged tests are the regression guard.
- **No Aspire AppHost, no OpenTelemetry, no RabbitMQ.** Still deferred from the slice 1.1 scope decisions; this session does not pull them forward.
- **No opportunistic refactors** of the slice 1.1 code beyond what the compiler requires for the upgrade.
- **No CLAUDE.md tech-stack edit** — that table carries no version numbers, so the upgrade does not change it.
