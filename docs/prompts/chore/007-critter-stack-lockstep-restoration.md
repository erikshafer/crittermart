# Prompt: Chore 007 — Critter Stack lockstep restoration (WolverineFx 6.19.0 → 6.34.0, CritterWatch beta.4 → 1.1.0-beta.1)

**Kind**: chore (dependency upgrade — CritterWatch + WolverineFx version coupling)
**Files touched**: `Directory.Packages.props` (version bumps + coupling comment rewrite); `.github/dependabot.yml` (ceiling); `docs/skills/updating-critter-stack-dependencies/SKILL.md` (spec delta); `docs/retrospectives/chore/007-critter-stack-lockstep-restoration.md` (new, session close)
**Mode**: solo maintenance; no slice, no workshop or narrative changes
**Commit subject**: `chore: restore Critter Stack lockstep — WolverineFx 6.34.0 + CritterWatch 1.1.0-beta.1`

---

## Framing

Since 2026-07-16 CritterMart has been frozen on **WolverineFx 6.19.0** by a documented coupling
ceiling. CritterWatch 1.0.0-beta.4 is compiled against WolverineFx(.Marten) 6.18.0, and running a
*higher* WolverineFx than the version CritterWatch targets throws a startup `TypeLoadException` in
`EventSubscriptionAgentFamily.TryRebuildRegisteredProjectionAsync`. The owner accepted a deliberate
one-minor lead (app 6.19.0 over a 6.18.0 target) on the reasoning that the trial had expired
2026-07-10, so the console could not boot and the exception was **latent, not active**.

The `Directory.Packages.props` COUPLING NOTE named the condition that lifts the freeze: "a
CritterWatch release that targets 6.19.0+." Two now exist:

| CritterWatch | Targets WolverineFx | Pulls Marten | Pulls MessagePack |
| --- | --- | --- | --- |
| 1.0.0-beta.4 (installed) | 6.18.0 | 9.x | 2.5.302 |
| **1.0.1** (GA) | 6.29.1 | 9.28.0 | 2.5.301 |
| **1.1.0-beta.1** | **6.34.0** | **9.32.0** | 2.5.302 |

Meanwhile Wolverine has reached 6.35.0 — sixteen minors past the freeze. Sixteen Dependabot PRs
have queued behind it.

## Owner decisions taken at session start (2026-09-09, via AskUserQuestion)

1. **The CritterWatch trial is still expired / read-only.** The coupling exception remains latent.
2. **Target exact lockstep at WolverineFx 6.34.0 + CritterWatch 1.1.0-beta.1.** This *retires* the
   one-minor lead rather than extending it. The 1.1.0-beta.1 prerelease is preferred over the 1.0.1
   GA specifically because it targets five more Wolverine minors; CritterMart's stable-only pin
   policy yields to the lockstep rule here.
3. **6.35.0 is deliberately not taken** — it would re-open the lead this session closes.
4. Everything non-Critter-Stack (Aspire, OpenTelemetry, auth stack, test stack, GitHub Actions, the
   `client/` npm tree) is **out of scope**, and ships as a following `tidy: dependabot` PR.

---

## Goal

Land the Wolverine family and the CritterWatch pair on exactly matching versions, in one atomic
sweep, verified by a live Aspire boot; and lift the coupling rule out of a props-file comment into
the runbook skill where the next session-runner will actually read it.

## Spec delta

`docs/skills/updating-critter-stack-dependencies/SKILL.md` gains a **CritterWatch lockstep**
section. The skill currently instructs "bump every package in a family to the latest NuGet
version" — which for `WolverineFx*` is *wrong* and has been wrong since June. The rule has only
ever lived in `Directory.Packages.props` and `.github/dependabot.yml`, so a session-runner
following the skill in good faith would break the console. This session encodes the rule, the
nuspec-reading procedure that derives the target, the three-files-must-agree constraint, and the
live-boot verification that a green build and test suite cannot substitute for.

## Orientation files (read first)

- `Directory.Packages.props` — the COUPLING NOTE is the authoritative record of the rule.
- `.github/dependabot.yml` — the mechanical enforcement (`WolverineFx*` ignore ceiling).
- `docs/skills/updating-critter-stack-dependencies/SKILL.md` — the runbook being amended.
- `docs/prompts/chore/004-critterwatch-next-release-upgrade.md` and its retro — the closest
  precedent: a CritterWatch + Wolverine lockstep bump verified by a live boot, which grew a code
  fix mid-session.
- `docs/decisions/017-critterwatch-integrated.md` — trial licensing, the Production-environment
  gotcha, the accepted MessagePack CVE tradeoff.

## Working pattern

1. Read the CritterWatch 1.1.0-beta.1 nuspec to establish the WolverineFx target first-hand.
2. Sweep all eight `WolverineFx*` pins and both CritterWatch pins in one pass.
3. Rewrite all three comment blocks (Wolverine coupling, CritterWatch, MessagePack suppression).
4. Move the `.github/dependabot.yml` ceiling to the new target-plus-one-minor.
5. `dotnet restore --force` → `build` → `test` → `format --verify-no-changes`.
6. Verify the resolved transitive line (Marten / JasperFx / Weasel / MessagePack) from
   `project.assets.json`; do **not** add direct pins.
7. **Live Aspire boot** — the only check that can see a coupling failure.
8. Amend the skill; author the retro.

## Deliverable plan

- `Directory.Packages.props` — 8 Wolverine pins, 2 CritterWatch pins, 3 comment blocks.
- `.github/dependabot.yml` — ceiling `>=6.20.0` → `>=6.35.0`, comment rewritten.
- `docs/skills/updating-critter-stack-dependencies/SKILL.md` — the lockstep section (spec delta).
- `docs/retrospectives/chore/007-critter-stack-lockstep-restoration.md`.
- Any code fix the sweep *forces*, in the affected file only.

## Out of scope

- **WolverineFx 6.35.0.** Re-opens the lead.
- **Adopting new Wolverine 6.2x/6.3x features** — `AfterCommit`, batched reads,
  `Storage.AppendEvents`, `MartenOps` side-effect ops, the 6.34/6.35 Event Modeling slice
  reporting. All attractive for a teaching repo; all their own sessions. Record as next-session
  inputs in the retro.
- **Every non-Critter-Stack dependency**, including the SSH.NET advisory surfaced by Testcontainers
  4.13.0 — that is the following `tidy: dependabot` PR's job.
- `docs/research/critterwatch-saga-visibility-beta1.md` and the demo-runbook line citing it. Both
  may be stale under 1.1.0-beta.1, but confirming it needs a licensed console. Note, do not edit.
- Alba 9.0.0-beta.1; the Playwright e2e CI wiring.
