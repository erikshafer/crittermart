# Prompt: Chore 004 — CritterWatch next-release upgrade (targeting beta.1 / alpha.5)

**Kind**: chore (dependency upgrade — CritterWatch + WolverineFx version coupling)
**Files touched**: `Directory.Packages.props` (version bumps + comment update); `docs/retrospectives/chore/004-critterwatch-next-release-upgrade.md` (new, session close)
**Mode**: solo maintenance; no slice, no spec artifact changes
**Commit subject**: `chore: upgrade CritterWatch to {version} + WolverineFx to {version}` *(fill in at session time)*

---

## Framing

CritterWatch is a monitoring console for the Critter Stack from JasperFx Software (Jeremy Miller + Babu Nallagatla). CritterMart uses it as both a teaching reference and a real-world test-bed for JasperFx's UI/UX work.

As of `main` (`bd2723d`, 2026-06-28), CritterMart pins:

```
CritterWatch            1.0.0-alpha.3
Wolverine.CritterWatch  1.0.0-alpha.3
WolverineFx.*           6.12.0   (held — see Directory.Packages.props comment)
```

**The version-coupling pattern is load-bearing and has caused one crash already (2026-06-24, PR #100).** CritterWatch is compiled against a specific WolverineFx floor. Running CW alpha.N against a higher WolverineFx version than it targets throws a `TypeLoadException` at startup in `EventSubscriptionAgentFamily.TryRebuildRegisteredProjectionAsync` (Wolverine.Marten's projection-distribution internals). The coupling is per-process — the console and all four services share one CLR, so they must all land on the exact same WolverineFx version.

**The previous Dependabot failure mode:** Dependabot opens one PR for `CritterWatch` and one for `Wolverine.CritterWatch` but leaves `WolverineFx.*` on the old floor. Merging either without the Wolverine bump crashes the console. PRs #94, #99, #102 were all closed for this reason.

**What this session does:** bump all three groups together, atomically, in one PR. Do not merge Dependabot's per-package PRs individually.

---

## Context — CritterWatch releases since alpha.3

**alpha.4** (published 2026-06-25) shipped before this session executes. Its key changes:

- Collapsible left nav pane (#515)
- Fix #504: DLQ totals were incorrectly multiplied across services sharing a PostgreSQL store — directly relevant to CritterMart's shared-pg / schema-per-service topology
- Fix #505: projection alert subject link now navigable
- Fix #503: service filter resets correctly on Dashboard → Active Alerts navigation
- Projections-page polish + agent health tooltip copy (#345 A/B)
- CritterWatch.CommandLine — embedded `cw-*` read CLI + smoke gate

**The next release** (the target of this session) was announced by Jeremy Miller as imminent on 2026-06-28. The version label was not yet confirmed at prompt-authoring time — it may be `1.0.0-beta.1`, `1.0.0-alpha.5`, or another label.

---

## Goal

Ship a single PR that:

1. Identifies the new CritterWatch release version on NuGet
2. Reads its exact WolverineFx floor from the package's dependency manifest
3. Bumps `CritterWatch`, `Wolverine.CritterWatch`, and all `WolverineFx.*` entries in `Directory.Packages.props` to compatible versions
4. Updates the comment block in `Directory.Packages.props` to accurately describe the new coupling
5. Checks the MessagePack CVE suppression (`GHSA-hv8m-jj95-wg3x`) — verify whether the new release resolves it; remove the suppression if yes, leave it with an updated comment if no
6. Runs `dotnet build` to confirm 0 errors / 0 NU1904 advisories
7. Runs `dotnet test` to confirm all tests pass
8. Closes Dependabot PRs #108 and #110 (they only bump CW, not WolverineFx — they are superseded by this PR)
9. Produces the retrospective

---

## Spec delta

No narrative or workshop changes — this session ships no slice behavior. The spec-shaped delta is: **none** (the upgrade changes no domain behavior). The retro confirms the named-none. The `Directory.Packages.props` comment block is the durable operational record.

---

## Orientation files

Read these before starting:

- `Directory.Packages.props` — the single version-authority file; all package versions live here (Central Package Management)
- `docs/research/critterstack-dependency-update-runbook.md` — the operational runbook for Critter Stack dependency updates (authored after the 2026-06-24 incident)
- The memory file at `C:\Users\Erik\.claude\projects\C--Code-crittermart\memory\critterwatch-wolverine-version-coupling.md` — records the coupling pattern and the incident

---

## Version detection (step 0 — do this before editing anything)

The correct sequence to avoid guessing:

```powershell
# 1. Confirm the new release is on NuGet
curl -s "https://api.nuget.org/v3-flatcontainer/critterwatch/index.json" | python -c "import json,sys; d=json.load(sys.stdin); print(d['versions'][-5:])"

# 2. Fetch its dependency manifest to read the exact WolverineFx floor
# (replace VERSION with the new version label, e.g. 1.0.0-beta.1)
curl -s "https://api.nuget.org/v3/registration5-gz-semver2/critterwatch/index.json" | python -c "
import json,sys,gzip
data = sys.stdin.buffer.read()
try: data = gzip.decompress(data)
except: pass
obj = json.loads(data)
for item in obj.get('items', []):
    for pkg in item.get('items', []):
        cat = pkg.get('catalogEntry', {})
        if cat.get('version') == 'VERSION':
            for d in cat.get('dependencyGroups', [{}])[0].get('dependencies', []):
                print(d.get('id'), d.get('range'))
"
```

The `WolverineFx.Marten` range returned is the floor. Target the **latest stable WolverineFx** that satisfies it (check `https://api.nuget.org/v3-flatcontainer/wolverinefx.marten/index.json`).

---

## Deliverable plan

| Step | Action |
|------|--------|
| 0 | Detect the new CritterWatch version and its exact WolverineFx floor (commands above) |
| 1 | Edit `Directory.Packages.props`: bump `CritterWatch`, `Wolverine.CritterWatch`, and all `WolverineFx.*` entries; rewrite the coupling-warning comment to name the new versions and date |
| 2 | Check `GHSA-hv8m-jj95-wg3x` — does the new release resolve the MessagePack CVE? If yes, remove the `<NuGetAuditSuppress>` line; if no, update its comment to note the date re-verified |
| 3 | `dotnet build` — 0 errors, 0 NU1904 advisories |
| 4 | `dotnet test` — all green (baseline: 139/139 as of main) |
| 5 | Close Dependabot PRs #108 and #110 with a comment explaining they are superseded |
| 6 | Commit and open PR with `chore: upgrade CritterWatch to {version} + WolverineFx to {version}` |
| 7 | Author `docs/retrospectives/chore/004-critterwatch-next-release-upgrade.md` |

---

## Out of scope

- Live stack boot / UI verification — that is a follow-on spike or a `/verify` run, not this PR
- Updating the feedback document from PR #103 (that spike stays on its own branch)
- Aspire, OpenTelemetry, Swashbuckle, or test-stack Dependabot bumps (#105, #106, #107, #109) — separate PRs or a tidy bundle
- Any CritterWatch UI/UX changes to CritterMart code itself
- Saga Explorer work — CritterMart has no Wolverine Sagas in round one; the explorer will be empty by design

---

## Coupling invariant — do not violate

> `CritterWatch`, `Wolverine.CritterWatch`, and all `WolverineFx.*` packages **must share the same build session**. Never accept or merge a PR that bumps only one side of this pair.

The coupling comment in `Directory.Packages.props` is the canonical explanation. Keep it accurate and dated.
