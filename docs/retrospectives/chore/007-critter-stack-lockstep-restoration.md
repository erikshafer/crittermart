# Retrospective: Chore 007 — Critter Stack lockstep restoration

**Kind**: chore (dependency upgrade — CritterWatch + WolverineFx version coupling)
**Prompt**: [`docs/prompts/chore/007-critter-stack-lockstep-restoration.md`](../../prompts/chore/007-critter-stack-lockstep-restoration.md)
**Date**: 2026-09-09
**Commit subject**: `chore: restore Critter Stack lockstep — WolverineFx 6.34.0 + CritterWatch 1.1.0-beta.1`

---

## Outcome summary

Landed as planned, plus one code fix the sweep forced.

| Package group | From | To |
| --- | --- | --- |
| `WolverineFx*` (8 pins) | 6.19.0 | **6.34.0** |
| `CritterWatch`, `Wolverine.CritterWatch` | 1.0.0-beta.4 | **1.1.0-beta.1** |
| Marten *(transitive)* | 9.x | **9.32.0** |
| JasperFx, JasperFx.Events *(transitive)* | 2.x | **2.63.2** |
| Weasel.* *(transitive)* | 9.x | **9.30.0** |
| MessagePack *(transitive)* | 2.5.302 | 2.5.302 *(unchanged — suppression retained)* |
| Alba | 8.5.3 | 8.5.3 *(already latest stable)* |

**The one-minor lead is retired.** WolverineFx 6.34.0 is exactly what CritterWatch 1.1.0-beta.1 was
compiled against, and both resolve Marten 9.32.0 — the two agree on the nose. Every `WolverineFx.*`
assembly in `project.assets.json` reports 6.34.0 with zero skew, including the ones that arrive
only transitively (`.Http.Marten`, `.SignalR`, `.RDBMS`, `.Newtonsoft`, and the new `.AI`).

**Verification:** `restore --force` clean with no NU1605 downgrades; `build` 0 errors; **206/206
tests green**; `format --verify-no-changes` clean; and a **live Aspire boot** with the
`critterwatch-console` endpoint answering HTTP 200, all four services reporting `/health` 200, and
Catalog serving seeded products from a live Marten 9.32 projection. No `TypeLoadException`.

**Spec delta — landed?** **Yes.** `docs/skills/updating-critter-stack-dependencies/SKILL.md` gained
the "CritterWatch lockstep rule — read this BEFORE sweeping Wolverine" section, plus three new
entries in the common-mistakes list and a corrected framing in Step 1. No `## Document History`
entry was added because no skill file in this repo carries that section; inventing the convention
for one file would have been a silent process change. This retro is the closure record.

## What worked

**Reading the nuspec first turned a judgement call into a lookup.** Three CritterWatch versions were
candidates. `curl`-ing each one's nuspec produced a version-coupling table in about thirty seconds,
and the table made the choice obvious rather than arguable: 1.0.1 is the GA but targets 6.29.1,
while 1.1.0-beta.1 targets 6.34.0. Preferring the beta looks reckless stated bare and is clearly
correct once the targets are visible. This is now the first step in the skill.

**Sweeping sixteen minors at once was the right default.** The skill's "sweep first, isolate only on
failure" instruction held: one pass, one restore, one build. Fifteen Wolverine minors produced
**zero** compile breaks. The single runtime failure that did appear was better diagnosed from the
full sweep than it would have been from fifteen incremental bisection steps.

**The live boot earned its place again.** Build and test were both green *before* the boot, and the
boot was still the only thing that could prove the coupling. Worth restating because the temptation
to skip it grows every time it passes.

## What was harder than expected

**A real regression, in the one service the coupling notes never mention.** Seven Identity tests
failed after the sweep, all one root cause:

```
Wolverine.Configuration.InvalidServiceLocationException: Found service locations while generating
code for POST_register, but ServiceLocationPolicy.NotAllowed is in effect.
Dependency: ServiceType: System.IServiceProvider Lifetime: Scoped
```

`ServiceLocationPolicy.NotAllowed` has been the Wolverine 6 default since the 5→6 upgrade
(chore/001), so the *policy* is not new — what changed somewhere in the 6.19→6.34 span is how
aggressively codegen walks a dependency's **constructor chain**. `UserManager<IdentityUser>` takes
an `IServiceProvider` (ASP.NET Core Identity uses it to resolve token providers lazily), and
`SignInManager<IdentityUser>` takes a `UserManager<IdentityUser>`, so both chains bottom out on the
container itself. These are framework constructors we do not own and cannot make transparent.

Fix, per the upstream `wolverine-troubleshooting-service-location-codegen` skill's guidance for
irreducible third-party factories — allow-list the two types rather than loosen the global policy:

```csharp
opts.CodeGeneration.AlwaysUseServiceLocationFor<UserManager<IdentityUser>>();
opts.CodeGeneration.AlwaysUseServiceLocationFor<SignInManager<IdentityUser>>();
```

Two lines in `src/CritterMart.Identity/Program.cs`, inside the existing `UseWolverine` block, with a
comment explaining why these two and nothing else. Every other handler and endpoint in the solution
stays under the strict default. Notably the tightening found a *genuine* opacity rather than a false
positive, and only in the EF-Core service — the three Marten services were untouched.

**The failure surfaced in the least obvious place.** All seven failures named `POST_register`, but
five of them were in `LogInTests` — those tests register a user as a fixture step, so a broken
`/register` cascaded into the login suite. Reading only the failure *names* would have suggested an
auth regression rather than a codegen one.

## Methodology refinements that emerged

**A prerelease can be the conservative choice.** The skill said "CritterMart pins stable only." Here
the stable pin (1.0.1) would have cost five Wolverine minors, and the prerelease was the option that
*restored* strict lockstep. Encoded as a named exception rather than left as a judgement call: for
CritterWatch, the Wolverine target governs, not the release channel.

**"Three files must agree" is now written down.** The coupling lives in two `Directory.Packages.props`
blocks *and* the `.github/dependabot.yml` ceiling. Nothing previously said so in one place, and a
session that updated two of the three would leave Dependabot fighting the pins. Now in the skill.

**Green build + green tests is not evidence about the coupling.** Added to the mistakes list
explicitly. It is the most plausible-sounding wrong conclusion available in this workflow.

**A live Aspire boot mutates `client/package-lock.json`.** The AppHost's `AddViteApp` resource runs
an install against the client, and on macOS npm rewrites the lockfile — here it stripped the `libc`
platform fields from optional dependencies, a 30-line deletion unrelated to anything this session
touched. It was reverted before committing. Worth knowing generally: **after any local AppHost boot,
check `git status` for an incidental `client/package-lock.json` diff** and revert it unless the
lockfile is genuinely part of the session's deliverable. An unnoticed one would ride into a PR as
mystery churn, or worse, silently undo a deliberate lockfile update.

## Outstanding items / next-session inputs

- **MessagePack CVE still suppressed.** CritterWatch 1.1.0-beta.1 pulls MessagePack 2.5.302; the fix
  is ≥ 3.0.214. Re-verified unaddressed 2026-09-09. Comment refreshed to name the new version. Carried
  forward from chore/004 and ADR 017.
- **SSH.NET advisory (GHSA-q939-rpr3-3284)** now surfaces on restore via Testcontainers 4.13.0. Not
  this session's scope; Testcontainers 4.14.0 fixed it, and the following `tidy: dependabot` PR takes
  4.15.0.
- **Wolverine 6.35.0 deliberately not taken.** Next CritterWatch release re-opens the question; the
  ceiling in `.github/dependabot.yml` is the tripwire.
- **`docs/research/critterwatch-saga-visibility-beta1.md` may be stale.** It documents Explore →
  Workflow as a pre-1.0 stub at CritterWatch beta.1. Under 1.1.0-beta.1 that may be fixed, but
  confirming it needs a licensed console. `docs/demo-runbook.md:438` cites it. Left untouched.
- **Behavior changes in the span, worth a look when the demo is next rehearsed:**
  - **6.21** — the per-message "successfully processed" log dropped from `Information` to `Debug`.
    If the demo's narration goes quiet, restore with
    `opts.Policies.MessageSuccessLogLevel(LogLevel.Information)`.
  - **6.21** — `wolverine-execution-time` is now a floating-point histogram (same name and unit).
    Touches the OpenTelemetry story in ADR 017.
  - **6.33 / Weasel 9.30.0** — the schema differ now compares character lengths in both directions,
    so a model narrower than an existing column can emit a failing narrowing `ALTER`. Harmless here
    (Aspire and Testcontainers databases are both ephemeral); would matter against a persistent DB.
- **Feature adoption candidates surfaced by the release notes**, each its own session: `AfterCommit`
  (6.29), batched reads and `Storage.AppendEvents` (6.28), `MartenOps` store-operation side effects
  (6.35), and — most on-theme for this project — the 6.34/6.35 **Event Modeling** work where a
  declared model is reconciled against the application built from it.
