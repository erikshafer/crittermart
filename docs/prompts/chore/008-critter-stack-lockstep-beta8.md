# Prompt: Chore 008 — Critter Stack lockstep carried forward (WolverineFx 6.34.0 → 6.43.0, CritterWatch 1.1.0-beta.1 → 1.1.0-beta.8)

**Kind**: chore (dependency upgrade — CritterWatch + WolverineFx version coupling)
**Files touched**: `Directory.Packages.props` (version bumps + coupling comment rewrite); `.github/dependabot.yml` (ceiling); `docs/skills/updating-critter-stack-dependencies/SKILL.md` (spec delta); `docs/retrospectives/chore/008-critter-stack-lockstep-beta8.md` (new, session close)
**Mode**: solo maintenance; no slice, no workshop or narrative changes
**Commit subject**: `chore: Critter Stack lockstep, WolverineFx 6.43.0 + CritterWatch 1.1.0-beta.8`

---

## Framing

Chore 007 restored exact lockstep at WolverineFx 6.34.0 + CritterWatch 1.1.0-beta.1 on 2026-09-09.
Since then Dependabot has opened the CritterWatch half on its own (#219 `CritterWatch`, #220
`Wolverine.CritterWatch`, both 1.1.0-beta.8). #220's Integration tests fail on every host with a
Weasel `TypeLoadException` (`Weasel.Postgresql.Tables.IndexDefinition.get_HasProviderSpecificOptions`):
the newer JasperFx/Weasel line arrived without its matching Wolverine. That is the half-pair trap the
runbook names, and neither PR is mergeable alone.

| CritterWatch | Targets WolverineFx | Pulls Marten | Pulls JasperFx | MessagePack |
| --- | --- | --- | --- | --- |
| 1.1.0-beta.1 (installed) | 6.34.0 | 9.32.0 | 2.63.2 | 2.5.302 |
| 1.0.1 (GA) | 6.29.1 | 9.28.0 | n/a | 2.5.301 |
| **1.1.0-beta.8** | **6.43.0** | **9.43.0** | **2.78.0** | 2.5.302 |

nuget.org's newest WolverineFx is 6.49.0 (published the day of this session). Under the lockstep rule
the target is what CritterWatch declares, so 6.44.0 to 6.49.0 are deliberately not taken.

## Owner decisions in force (carried from chore 007, no new question asked)

1. Exact lockstep, no lead. The CritterWatch trial is still expired, but the pins do not depend on that.
2. A CritterWatch prerelease is preferred when it targets the newest Wolverine.
3. Everything non-Critter-Stack (Aspire, OpenTelemetry, test stack, Swashbuckle, GitHub Actions, the
   `client/` npm tree) ships in the following `tidy: dependabot` PR.

## Goal

Land the Wolverine family and the CritterWatch pair on exactly matching versions in one atomic sweep,
verified by a live Aspire boot, and keep the runbook's facts current.

## Spec delta

`docs/skills/updating-critter-stack-dependencies/SKILL.md` gains the half-pair evidence: a
CritterWatch-only bump fails in CI as a Weasel `TypeLoadException`, not only in the console, so a red
integration job on a CritterWatch-only PR means "take the whole family", not "debug". Dated facts
(current target, MessagePack re-verification) are refreshed.

## Orientation files (read first)

- `Directory.Packages.props` — the COUPLING NOTE.
- `.github/dependabot.yml` — the `WolverineFx*` ignore ceiling.
- `docs/skills/updating-critter-stack-dependencies/SKILL.md` — the runbook being amended.
- `docs/prompts/chore/007-critter-stack-lockstep-restoration.md` and its retro — the direct precedent.

## Working pattern

1. Read the beta.8 nuspecs for the WolverineFx / Marten / JasperFx / MessagePack targets first-hand.
2. Sweep all eight `WolverineFx*` pins and both CritterWatch pins in one pass; rewrite the comment blocks.
3. Move the `.github/dependabot.yml` ceiling to target plus one minor.
4. `dotnet restore --force`, `build`, `test`, `format --verify-no-changes`.
5. Read the resolved transitive line from `project.assets.json`.
6. Live Aspire boot (the only check that sees the console coupling failure), then clean teardown.
7. Review the Wolverine 6.35 to 6.43 release notes for breaking changes against this codebase.
8. Amend the skill; author the retro.

## Deliverable plan

- `Directory.Packages.props` — 8 Wolverine pins, 2 CritterWatch pins, comment blocks.
- `.github/dependabot.yml` — ceiling `>=6.35.0` → `>=6.44.0`.
- `docs/skills/updating-critter-stack-dependencies/SKILL.md` — spec delta above.
- `docs/retrospectives/chore/008-critter-stack-lockstep-beta8.md`.
- Any code fix the sweep forces, in the affected file only.

## Out of scope

- WolverineFx 6.44.0 to 6.49.0 (would lead CritterWatch's target).
- Every non-Critter-Stack dependency and the npm tree (next PR).
- Adopting new Wolverine 6.35 to 6.43 features.
- Alba 9.0 (rc/beta only).
- Enabling Dependabot security alerts (a repo admin setting; the API reports them disabled).
