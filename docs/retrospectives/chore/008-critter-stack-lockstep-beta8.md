# Retrospective: Chore 008 — Critter Stack lockstep carried forward to beta.8

**Kind**: chore (dependency upgrade — CritterWatch + WolverineFx version coupling)
**Prompt**: [`docs/prompts/chore/008-critter-stack-lockstep-beta8.md`](../../prompts/chore/008-critter-stack-lockstep-beta8.md)
**Date**: 2026-10-12
**Commit subject**: `chore: Critter Stack lockstep, WolverineFx 6.43.0 + CritterWatch 1.1.0-beta.8`

---

## Outcome summary

Landed as planned, plus one dependency-graph fix the sweep forced (a direct Marten pin).

| Package group | From | To |
| --- | --- | --- |
| `WolverineFx*` (8 pins) | 6.34.0 | **6.43.0** |
| `CritterWatch`, `Wolverine.CritterWatch` | 1.1.0-beta.1 | **1.1.0-beta.8** |
| `Marten` *(was transitive, now a direct pin)* | 9.32.0 | **9.43.0** |
| JasperFx, JasperFx.Events *(transitive)* | 2.63.2 | **2.78.0** |
| Weasel.Postgresql *(transitive)* | 9.30.0 | **9.38.0** |
| MessagePack *(transitive)* | 2.5.302 | 2.5.302 *(unchanged, suppression retained)* |
| Alba | 8.5.3 | 8.5.3 *(9.0 is rc/beta only)* |

**Verification:** `restore --force` clean; `build` 0 warnings / 0 errors; **206/206 tests green**
(Catalog 9, Inventory 31, CrossBc 3, Identity 26, Orders 137); `format --verify-no-changes` clean;
and a **live Aspire boot**: the `critterwatch-console` endpoint answered HTTP 200 on `/`, `/health`
and `/alive`, Catalog/Inventory/Orders reported `/health` 200, and every service process stayed up
with no `TypeLoadException`. AppHost torn down cleanly (no leftover processes or containers).

**Spec delta, landed?** **Yes.** `SKILL.md` records that a CritterWatch-only bump fails in CI, and
(the unplanned part, below) that Marten is now a direct pin. `git status` after the boot showed no
incidental `client/package-lock.json` churn.

## What worked

**The runbook paid for itself.** Dependabot's #219/#220 split was diagnosed in minutes: read the
beta.8 nuspec, find WolverineFx 6.43.0, recognize the Weasel `TypeLoadException` in #220's CI log as
the half-pair trap. No bisecting.

**Reading the release notes against the code, not just the version delta.** Of Wolverine 6.35 to 6.43,
two entries say "breaking": 6.41 (`AutoApplyTransactions()` now fails the chain build when two
persistence providers can own a handler) and 6.37 (`ServiceCapabilities.EventModel` type change).
Neither bites: every service has one provider (Identity's `DbContext`/`IDocumentSession` grep hits are
comments), and nothing in `src/` touches `ServiceCapabilities`. Result: zero code changes.

## What was harder than expected

**The runbook's "Marten is transitive, never pin it" rule was wrong for this CritterWatch line.**
With the eight Wolverine pins and CritterWatch moved, `build` was green but the Marten-backed
integration tests (Inventory and Orders at least) failed in 1 ms each, at host startup, with:

```
System.TypeLoadException : Method 'LoadDocumentAsync' in type 'Marten.DocumentStore' from assembly
'Marten, Version=9.41.0.0' does not have an implementation.
```

Cause: `Wolverine.CritterWatch` beta.8 brings JasperFx **2.78.0** into every service, and that line
needs Marten **9.43.0**; `WolverineFx.Marten 6.43.0` only floors Marten at 9.41.0. Catalog, Inventory
and Orders reference `WolverineFx.Marten` but not the full `CritterWatch` package (which is what
declares Marten 9.43.0), so they resolved 9.41.0 while the console project alone resolved 9.43.0.
At beta.1 the two floors happened to coincide (Marten 9.32.0), which is why the rule looked safe.

Fix: `<PackageVersion Include="Marten" Version="9.43.0" />` plus `<PackageReference Include="Marten" />`
in the three Marten services, with a comment naming the nuspec as the source of truth. I chose the
narrow fix over `CentralPackageTransitivePinningEnabled`, which would change resolution for every
package in the solution. This touched three csproj files the prompt did not name; it is the forced
code fix the prompt's deliverable plan allows for ("any code fix the sweep forces").

**A shell trap, not a project one:** in zsh `$path` is tied to `$PATH`, so a `for path in ...` loop in
a verification script silently emptied the command lookup path ("command not found: curl"). Use any
other loop variable name.

## Methodology refinements that emerged

- **Pin Marten to the CritterWatch-declared version.** Encoded in the skill: four places now move
  together (two props blocks, the Marten pin, the two Dependabot ceilings). `.github/dependabot.yml`
  gained a `Marten` ceiling (`>=9.44.0`) so Dependabot cannot float the pin past CritterWatch.
- **Unlike the console-side `TypeLoadException`, this one is visible to `dotnet test`.** The rule "green
  build and test prove nothing about the coupling" is still true for the Wolverine-over-CritterWatch
  direction; it is not true for the Marten/JasperFx floor.
- **Read all of `critterwatch.nuspec`, not only the `WolverineFx` line.** The runbook's grep already
  listed Marten; the lesson is that its result must become a pin, not just a note.

## Outstanding items / next-session inputs

- **MessagePack CVE (GHSA-hv8m-jj95-wg3x) still suppressed.** beta.8 still pulls 2.5.302. Comment
  refreshed. Carried from chore/004, chore/007 and ADR 017.
- **WolverineFx 6.44.0 to 6.49.0 are not taken** (6.49.0 published 2026-10-12). Revisit when a
  CritterWatch release targets them; the ceiling and the Marten pin move with it.
- **Weasel 9.36 to 9.38 migration semantics** (6.43.0 release notes): a changed computed column now
  drops/recreates dependent indexes, and identity is compared as a column difference. Fresh-database
  boots and the full test suite applied the schema without error; the generated DDL was not inspected,
  and an existing long-lived database has not been exercised.
- **New Wolverine features available** (6.35 `MartenOps` additions, 6.36 `[SlicePattern]`, 6.38
  recurring-schedule operability): attractive for a teaching repo, each its own session.
- Dependabot #219/#220 are superseded by this PR; close them after merge. The non-Critter-Stack
  Dependabot PRs are handled by the following `tidy: dependabot` PR.
- **Dependabot security alerts are disabled** on the repository (API 403). Only version-update PRs
  exist; enabling alerts is an admin setting left to the owner.
