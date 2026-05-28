---
retrospective: 001
kind: chore
prompt: docs/prompts/chore/001-critter-stack-2026-upgrade.md
deliverable: Directory.Packages.props (version bumps + WolverineFx.RuntimeCompilation); src/CritterMart.Catalog/CritterMart.Catalog.csproj (RuntimeCompilation ref); docs/decisions/012-critter-stack-2026-upgrade.md (new ADR); docs/decisions/README.md (index row); docs/rules/structural-constraints.md (Build/codegen section, v1.1); docs/prompts/chore/001-critter-stack-2026-upgrade.md (new); docs/retrospectives/chore/001-critter-stack-2026-upgrade.md (this file)
date: 2026-05-28
mode: solo maintenance with the upstream JasperFx migration skills as authoritative guidance
session-runner: Claude (Opus 4.7)
---

# Retrospective — Chore 001: Critter Stack 2026 upgrade (Wolverine 6 / Marten 9)

## Outcome summary

The whole stack jumped to the **Critter Stack 2026 line — Wolverine 6.1.0, Marten 9.2.0, JasperFx 2.2.0, Weasel 9.0.1** — resolving the **Marten 8.35.0 critical advisory (NU1904 / GHSA-vmw2-qwm8-x84c)** that PR #8 flagged. The upgrade required **zero application-code changes**: the Catalog service's entire API surface survived the v5→v6 / v8→v9 jump untouched. Wolverine 6's removal of the Roslyn compiler from core was handled by adding `WolverineFx.RuntimeCompilation` (Dynamic mode, round-one posture); Marten 9's flipped `StoreOptions` defaults were adopted as-is. The decision is captured in **ADR 012**, the structural-constraints rule file gained a paired **Build & code generation** section (v1.1), and the `chore/` prompt + retrospective kinds were bootstrapped. This also served as the **design-return-cadence PR** after the first Catalog implementation PR.

Verified three ways: full solution builds (0 errors), both slice 1.1 GWT scenarios pass unchanged, and the upgraded service was booted on Kestrel (Dynamic codegen, no `IAssemblyGenerator` startup throw) and served `PublishProduct` over real HTTP — fresh SKU `crit-099` → 201, duplicate → 409.

## What worked

- **The migration skills as authoritative guidance, then filtered against our actual surface.** `wolverine-migration-v5-to-v6` and `marten-migration-v8-to-v9` enumerate dozens of breaking changes, but mapping each onto *our* code showed almost all are N/A: Catalog is a document store with no aggregates, no projections, no compiled queries, no Newtonsoft, no `IForwardsTo`. The only real blocker was Wolverine's runtime-codegen extraction. Reading the guide *and then asking "does this touch our code?"* turned a scary-looking dual major-version jump into a one-package change.
- **Transitive resolution landed the latest Marten with no explicit pin.** Bumping `WolverineFx.* → 6.1.0` pulled Marten 9.2.0 / JasperFx 2.2.0 / Weasel 9.0.1 transitively. No `Marten` PackageVersion/PackageReference needed — fewer direct deps to maintain, and the advisory cleared.
- **`WolverineFx.RuntimeCompilation` auto-registers.** Referencing the package on the Catalog project (transitive to the test project via ProjectReference) was sufficient; no `UseRuntimeCompilation()` call required. The startup log's "Wolverine code generation mode is Dynamic" line confirmed it without a throw.
- **Real-run verification earned its keep conceptually even though it passed.** The Wolverine codegen change is a *startup-time* failure mode — exactly the kind a green unit-test summary could mask if tests didn't boot the host. Booting on Kestrel made "it actually starts on v6" an observed fact, not an inference.

## What was harder than expected

- **`dotnet list package --vulnerable` 403s on the private `jasperfx` feed.** The `--vulnerable` flag enumerates each configured source's vulnerability index; the `jasperfx` feed returns 403 for that query (NU1507 also flags the unmapped second source). Resolved by relying on the **build-integrated NU1904 audit** (the same audit that flagged Marten 8.35.0 originally) — its absence on a forced restore is the authoritative "advisory gone" signal.
- **The docker-compose volume persisted across sessions and masked the 201 path.** Slice 1.1's verification run left `crit-001` in the retained `crittermart-pgdata` volume, so the first upgrade-verification publish returned 409 (correct, but not the happy path). Publishing a fresh SKU (`crit-099`) showed the 201. A reminder that the dev volume is durable across `docker compose down` (without `-v`).

## Methodology refinements that emerged

1. **Map a migration guide's breaking-change list onto the actual code surface before estimating effort or ceremony.** A document-store slice sails through a Marten 8→9 jump that reads as alarming on paper, because the dangerous changes (aggregate identity-map self-mutation, `ILongVersioned` overflow, inline-lambda projection removal, custom `IStorageOperation`) all presuppose features we don't use yet. The estimate should be driven by *intersection with our code*, not by the length of the guide.
2. **Full-pipeline ceremony for a mechanical upgrade is proportionate when a real decision rides along.** The version bump itself is trivial, but the codegen posture (Dynamic + RuntimeCompilation vs. Static/AOT) is a genuine, ADR-worthy choice future contributors would otherwise re-derive. The ADR + structural-constraint pairing is the part that earns the ceremony; the prompt/retro frame it.
3. **A new `chore/` kind is the right home for cross-cutting dependency/build maintenance** — distinct from `tidy:` (artifact maintenance, which has used the `docs/` kind) and from slice `implementations/`.

## Outstanding items / next-session inputs

1. **Static/AOT codegen mode is deferred.** `dotnet run -- codegen write` + `TypeLoadMode.Static` (and dropping the `WolverineFx.RuntimeCompilation` reference in production publishes) belongs to the same future infra session that stands up Aspire and OpenTelemetry. Tracked in ADR 012.
2. **NuGet source mapping.** The `jasperfx` + `nuget.org` sources without package-source-mapping cause NU1507 on restore and the 403 on `--vulnerable`. A `nuget.config` with source mapping would fix both — a small `tidy:`/config task (note: `.claude/settings.local.json` was open in the IDE this session, but settings and nuget.config are separate concerns).
3. **Design-return cadence reset.** This non-implementation PR satisfies the cadence after PR #8. The next session may resume Catalog slices (1.2 browse / 1.3 change price) — up to two more before the next mandatory design-return.
4. **Verification test rows persist** in the docker-compose volume (`crit-001`, `crit-099`); `docker compose down -v` wipes them if a clean local DB is wanted.
5. **README population lines for the new `chore/` kind were intentionally NOT updated** — this frozen prompt did not name `docs/prompts/README.md` / `docs/retrospectives/README.md` in its deliverable plan, so editing them would be an opportunistic edit. Fold `chore/` into the same `tidy: docs` sweep that still owes the `specs/` kind reconciliation (carried from PR #5). This is the discipline holding deliberately, not an oversight.

## Spec-delta — landed?

**Yes.** The prompt named a process/decision delta, not a behavior delta:

1. New ADR cross-reference (ADR 012) added to `docs/decisions/README.md` — **landed**.
2. New structural constraint (Build & code generation posture) added to `docs/rules/structural-constraints.md` with a paired Document History bump (v1.1) — **landed**, per that file's "ADR-changes-a-constraint pairs with a rule-file update in the same PR" rule.
3. The `product-catalog` capability, Narrative 001, and Workshop 001 unchanged; the upgraded code still satisfies the slice 1.1 contract — **honored**; the unchanged tests pass on v6/v9 as the regression guard.

No spec-delta items dropped. No slice behavior shipped, as intended.

## Process notes

- One PR bundles `chore:` (the upgrade) and `docs:` (ADR 012 + structural-constraints + prompt/retro) commit-subject concerns.
- Zero application-code edits — the only `src/` change is the `WolverineFx.RuntimeCompilation` PackageReference in the csproj.
- Branch: `chore/critter-stack-2026-upgrade`.
