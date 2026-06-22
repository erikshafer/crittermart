---
name: updating-critter-stack-dependencies
description: "How CritterMart updates its JasperFx / Critter Stack dependencies (Wolverine, Marten, Alba, JasperFx core, Weasel, CritterWatch) — bump every package in a family to the latest NuGet version in one clean sweep via Directory.Packages.props, restore+build+test to validate, and summarize the release notes (not just the version delta) so the human knows what actually changed. Use when asked to update the Critter Stack, refresh JasperFx packages, or process the Dependabot critter-stack branches."
cluster: ops
tags: [dependencies, wolverine, marten, alba, jasperfx, critterwatch, nuget, dependabot, maintenance]
---

# Updating Critter Stack Dependencies

The Critter Stack (everything published by **JasperFx Software** — Wolverine, Marten, Alba, JasperFx core, Weasel, Lamar, Polecat, CritterWatch) ships fast and in lockstep: a Wolverine release routinely pins matching Marten / JasperFx / Weasel versions, and the whole family moves together. CritterMart's policy is to **track the latest of the whole family at once**, not to drip one package at a time. This skill is the runbook for doing that update and — crucially — for reporting *what changed*, not just *that versions changed*.

This is an **operational** skill (cluster `ops`), not a code-convention skill. It encodes a procedure and the project-specific facts that procedure needs (which packages are "Critter Stack" here, where they live, the standing CVE suppression, the Dependabot landscape). The Wolverine/Marten *APIs* belong to the upstream JasperFx ai-skills library; this skill never duplicates them.

## When to apply this skill

- "Update the Critter Stack" / "bump Wolverine / Marten / Alba to latest" / "refresh the JasperFx packages."
- Processing the Dependabot `nuget/critter-stack-*`, `nuget/multi-*`, or `nuget/CritterWatch-*` branches (this skill **supersedes** them — see § Dependabot).
- Any time `Directory.Packages.props` JasperFx pins fall behind the latest NuGet stable.

It does **not** cover the non-JasperFx Dependabot branches (Aspire, Swashbuckle, OpenTelemetry, the `npm_and_yarn/client/*` SPA deps, `github_actions/*`). Note those exist, but they are out of scope for a Critter Stack update — leave them for their own session.

## What counts as "Critter Stack" in this repo

Central package management means every version lives in one file: **`Directory.Packages.props`**. The JasperFx-published pins, as of the 2026 line:

| Family | Packages (all move to the same version) |
| --- | --- |
| **Wolverine** | `WolverineFx`, `WolverineFx.Http`, `WolverineFx.Marten`, `WolverineFx.Postgresql`, `WolverineFx.RabbitMQ`, `WolverineFx.EntityFrameworkCore`, `WolverineFx.HealthChecks`, `WolverineFx.RuntimeCompilation` |
| **CritterWatch** | `CritterWatch`, `Wolverine.CritterWatch` (move together) |
| **Alba** (test) | `Alba` |

**Marten, JasperFx core, Weasel, Lamar, Polecat are transitive** here — no direct `<PackageVersion>` entry. They ride in via `WolverineFx.Marten` (Marten) and Wolverine itself (JasperFx/Weasel). Don't add direct pins for them unless a project genuinely takes a direct dependency; bumping Wolverine pulls the matching transitive versions. (Confirm with `grep -rho '"Marten/[^"]*"' src/*/obj/project.assets.json` after restore if you want to *see* the resolved transitive version.)

The header comment in `Directory.Packages.props` names the line — keep it honest (e.g. `Critter Stack 2026 line (Wolverine 6 / Marten 9 / JasperFx 2)`).

## Step 1 — Find the latest stable of each package

Don't trust the Dependabot branch as the latest — Dependabot groups can lag or split (one branch bumped 7 WolverineFx packages but left the `WolverineFx` metapackage behind). Go to NuGet's flat-container index, which is the source of truth and needs no auth:

```bash
for pkg in wolverinefx wolverinefx.http wolverinefx.marten wolverinefx.rabbitmq \
           wolverinefx.postgresql wolverinefx.entityframeworkcore \
           wolverinefx.healthchecks wolverinefx.runtimecompilation \
           alba critterwatch wolverine.critterwatch marten jasperfx; do
  latest=$(curl -s "https://api.nuget.org/v3-flatcontainer/$pkg/index.json" \
    | python3 -c "import sys,json; v=json.load(sys.stdin)['versions']; s=[x for x in v if '-' not in x]; print(s[-1] if s else 'NONE')")
  printf "%-40s %s\n" "$pkg" "$latest"
done
```

Filter `-` to skip pre-release; CritterMart pins **stable** only. Include `marten`/`jasperfx` in the query for *reporting* the transitive line even though they aren't directly pinned.

## Step 2 — Bump in ONE clean sweep, not one at a time

Edit **all** packages in a family to the latest version in a single pass over `Directory.Packages.props`. If WolverineFx is locally `6.8.0` and latest is `6.13.1`, set **every** `WolverineFx*` line to `6.13.1` at once — don't walk them up individually. Same for the CritterWatch pair.

Then validate the whole sweep:

```bash
dotnet restore --force   # --force: a plain restore reports "up-to-date" and skips the new versions
dotnet build --no-restore -c Debug
dotnet test  --no-build  -c Debug   # integration tests need Docker up (Testcontainers: Postgres + RabbitMQ)
```

A clean `restore` + `build` (0 warnings / 0 errors) + green test suite means the sweep is good — ship it.

### Fallback: only go one-by-one if the sweep breaks

The single-sweep is the default *because the family is released in lockstep and rarely breaks mid-family*. **Only** fall back to incremental bumps if the all-at-once restore/build/test fails. Then:

1. Revert to the previous pins.
2. Bump one package (or one sub-family) at a time, running `restore`+`build`+`test` after each, to isolate which package introduced the break.
3. Report the offending package and its breaking change rather than silently pinning it back.

This is the explicit instruction: sweep first, isolate only on failure.

## Step 3 — Read and summarize the release notes (the part that isn't "bump")

A version bump with no explanation is half the job. For **every** family that moved, pull the release notes between the old and new version and summarize the *meaningful* changes — breaking changes, new features the project might use, fixes that touch CritterMart's actual surface (RabbitMQ transport, Marten projections, durable inbox, DLQ, HTTP codegen).

**Wolverine — public GitHub releases.** Tags are uppercase-`V`-prefixed (`V6.13.1`). `gh` auth may be broken in this environment — use the **public** API via `curl` (no token needed for a public repo):

```bash
curl -s "https://api.github.com/repos/JasperFx/wolverine/releases?per_page=30" \
 | python3 -c "
import sys,json
data=json.load(sys.stdin)
want=('6.9','6.10','6.11','6.12','6.13')   # the span you crossed, sans the V
for r in data:
    tag=r.get('tag_name','').lstrip('Vv')
    if tag.startswith(want):
        print('\n== V'+tag+' ('+r.get('published_at','')[:10]+') ==')
        print((r.get('body') or '(no body)').strip()[:2000])
"
```

**Marten / JasperFx / Weasel** publish their own GitHub releases the same way (`JasperFx/marten`, `JasperFx/jasperfx`, `JasperFx/weasel`) — summarize the spans you crossed transitively if they're material.

**CritterWatch is closed-source** (commercial JasperFx product) — **no public GitHub repo, no public changelog**. Don't burn time hunting for one. Its server-side work is usually narrated *inside the Wolverine release notes* (the 6.9–6.11 line was explicitly "mostly about CritterWatch": Polecat ancillary stores, inbox-cleanup batching, DLQ enrichment). Report CritterWatch as "no public notes; related server work visible in Wolverine 6.x notes" and move on.

**Summarize, don't dump.** The deliverable is a short per-family digest: headline change, any breaking changes (call out "none" explicitly when true), and the one or two items that matter to *this* codebase — flagging the rest (e.g. a Kafka overhaul) as not-applicable since CritterMart is RabbitMQ-only.

## Standing gotcha — the MessagePack CVE suppression

`Directory.Packages.props` carries a `NuGetAuditSuppress` for `GHSA-hv8m-jj95-wg3x`. CritterWatch transitively pulls **MessagePack 2.5.x**, which carries that high-severity advisory; the fix (MessagePack ≥ 3.0.214) is a major bump that breaks CritterWatch, so the advisory is suppressed until an upstream CritterWatch release resolves it.

After a CritterWatch bump, **re-check whether the suppression is still load-bearing** — don't assume, and don't drop it blindly:

```bash
grep -rho '"MessagePack/[^"]*"' src/*/obj/project.assets.json | sort -u
```

If it still resolves to `2.5.x`, keep the suppression and refresh its comment to name the new CritterWatch version. If a CritterWatch release finally pulls MessagePack ≥ 3.x, remove the suppression in the same PR and note it.

## Dependabot relationship

Dependabot opens per-family NuGet branches (`dependabot/nuget/critter-stack-*`, `nuget/multi-*`, `nuget/CritterWatch-*`). A manual Critter Stack update done this way **supersedes** them — it goes to the true latest (often higher than the grouped branch) and lands the whole family plus the release-note summary in one reviewed PR. After merging, those Dependabot branches close as stale/superseded; don't also merge them.

Leave the **non-JasperFx** Dependabot branches alone (Aspire, Swashbuckle, OpenTelemetry, `npm_and_yarn/client/*`, `github_actions/*`) — they're out of scope for a Critter Stack update and get their own sessions.

## Quick reference: common mistakes to catch

- **Trusting the Dependabot version as "latest."** Query NuGet's flat-container index; Dependabot groups lag and split.
- **Walking packages up one at a time by default.** Sweep the whole family at once; isolate incrementally *only* after a failed sweep.
- **Plain `dotnet restore` reporting "up-to-date" and silently keeping old versions.** Use `restore --force` after editing the props file.
- **Reporting only the version delta.** Always pair the bump with a release-notes digest; "bumped 6.8.0 → 6.13.1" is not a summary of what changed.
- **Hunting for CritterWatch release notes.** There are none public — read the Wolverine notes instead.
- **Dropping the MessagePack suppression on a CritterWatch bump without checking the resolved transitive version.** Re-verify; keep-and-refresh until upstream moves to MessagePack 3.x.
- **Adding direct `<PackageVersion>` pins for Marten/JasperFx/Weasel.** They're transitive; bumping Wolverine carries them.
- **Sweeping the non-JasperFx Dependabot branches into the same PR.** Different scope, different session.

## See also

- `Directory.Packages.props` — the single source of every pin (central package management).
- Upstream JasperFx ai-skills library — the Wolverine/Marten/Polecat *API* mechanics this skill defers to.
- [CLAUDE.md § Skills](../../CLAUDE.md) — routing-layer framing; the "defer to upstream, write only what diverges" discipline.
- NuGet flat-container index: `https://api.nuget.org/v3-flatcontainer/{id-lowercase}/index.json` — authless latest-version source of truth.
