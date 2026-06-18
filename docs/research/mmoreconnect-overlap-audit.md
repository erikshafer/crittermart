---
version: v1.0
status: Active
date: 2026-06-17
references:
  - docs/demo-runbook.md (§ URLs & ports)
  - src/CritterMart.AppHost/Program.cs
  - PR #74 (storefront SPA 5173 → 5273)
  - PR #78 (com.docker.compose.project=crittermart container label)
---

# Fixed-surface overlap audit — CritterMart vs MmoReconnect

> **What this is.** Demo-reliability insurance. The author runs the sibling Vite app
> **MmoReconnect** concurrently with CritterMart on the same dev machine (it was MmoReconnect
> grabbing Vite's `5173` with `strictPort` that forced CritterMart's storefront to `5273` in
> [PR #74](https://github.com/erikshafer/crittermart/pull/74)). This audit sweeps **every other**
> fixed surface — service ports, the Aspire dashboard/OTLP/resource-service ports, container
> names, host-published container ports, volumes — to confirm a concurrent boot of both stacks
> can't fail on a collision during the talk.
>
> It is **research / pre-talk verification**, not a decision and not a build order.

## Bottom line

**CritterMart and MmoReconnect are fully disjoint on every fixed surface — no collision, no fix
needed.** The one historical clash (both on Vite's `5173`) was already resolved in PR #74. The
disjointness is provable from pinned config alone — every surface below is a statically pinned
port; the only runtime-variable ports (Aspire-randomized container host ports, the dynamic
CritterWatch endpoint) are self-avoiding by construction — so no concurrent boot was required to
confirm it.

The audit did surface one **gap (not a collision)**: CritterMart has no declared port footprint in
the cross-project convention MmoReconnect maintains. See [Recommendations](#recommendations).

## The surface map

| Surface | CritterMart | MmoReconnect | Clash? |
|---|---|---|---|
| Service / API (HTTP) | `5101` Catalog · `5102` Inventory · `5103` Orders · `5104` CritterWatch console¹ | `5200` api (single) | **No** (51xx vs 5200) |
| Web SPA (Vite dev server) | `5273` (pinned, `strictPort`) | `5173` (Vite default, unpinned)² | **No** (the PR #74 fix) |
| Aspire dashboard UI | `15090` http / `17090` https | `5210` | **No** |
| Aspire OTLP endpoint | `19090` http / `21090` https | `5211` | **No** |
| Aspire resource service | `20090` http / `22090` https | `5212` | **No** |
| Postgres (container host port) | Aspire-randomized (no `WithHostPort`) | Aspire-randomized | **No** (both random) |
| RabbitMQ (container host port) | Aspire-randomized | — (MmoReconnect runs no broker) | **No** |
| Container names | Aspire random suffix (`postgres-<rand>`), grouped under the `crittermart` compose-project **label** (PR #78) | stable names `mmo-reconnect-postgres`, `mmo-reconnect-pgadmin` (`.WithContainerName`) | **No** (distinct) |
| Postgres data volume | none — **ephemeral** (fresh DB each boot) | `mmoreconnect-pgdata` (persistent) | **No** |

¹ CritterWatch's `5104` is pinned in its `launchSettings.json`; the AppHost adds
`WithExternalHttpEndpoints` (no explicit port), so its live port may be Aspire-assigned — the
runbook calls it "dynamic." Either value is clear of MmoReconnect's range.
² MmoReconnect's `vite.config.ts` pins no `server.port`, so its dev server takes Vite's default
`5173` (its OAuth-proxy comment references `localhost:5173/4`). This is the surface CritterMart
deliberately vacated in PR #74.

## Why the two stacks are disjoint

MmoReconnect follows a **fixed-range convention** documented in its repo
(`mmo-reconnect/PORTS.md`): it claims **5200–5219** for itself and pins its Aspire infrastructure
ports (`5210`/`5211`/`5212`) *into that range* via the AppHost's `launchSettings.json`
(`ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL` etc.) rather than leaving them at Aspire's defaults.
CritterMart instead keeps Aspire's high-number infra defaults (`15090`/`19090`/`20090`) and uses
`51xx` for services + `5273` for the SPA. The two footprints simply don't intersect.

## Secondary findings (not collisions)

1. **CritterMart is undocumented in the shared port convention.** MmoReconnect's `PORTS.md` exists
   precisely to coordinate "multiple Critter Stack projects on the same development machine" and
   even reserves a range for CritterBids (`5100–5119`) — but it predates / omits CritterMart.
   CritterMart has no equivalent declaration of its own footprint. Today that footprint is
   *implicit*, scattered across six `launchSettings.json` files and the AppHost. A future port
   addition on either side could collide silently because there's no single place that says "these
   are CritterMart's ports." (This is the gap the [Recommendations](#recommendations) address.)

2. **CritterBids adjacency (out of primary scope, flagged for completeness).** CritterMart's
   service ports `5101–5104` fall inside the `5100–5119` range MmoReconnect's `PORTS.md` *attributes
   to CritterBids*. In practice CritterBids' own Vite apps sit at `5173–5175` and its Aspire infra
   at `15237`/`19240`/`20263` — all clear of CritterMart — so there is no live collision, and the
   author runs MmoReconnect (not CritterBids) alongside CritterMart. But the numeric overlap in the
   *declared* range is a latent ambiguity worth resolving when CritterMart claims its own range.

3. **Two different Docker-Desktop attribution mechanisms across the siblings.** CritterMart (PR #78)
   and CritterBids group their containers with the `com.docker.compose.project` **label**;
   MmoReconnect uses stable `.WithContainerName(...)` **prefixes**. Both are valid and they don't
   conflict. They're also complementary — CritterMart could *additionally* adopt stable container
   names for per-row legibility on top of the grouping label — but that's a DX nicety, not an
   overlap fix.

## Recommendations

All optional and owner's-call — none is demo-blocking, since the stacks are already disjoint.

- **(Primary) Claim a CritterMart port range and declare it.** Adopt MmoReconnect's discipline:
  pick a free range (CritterMart's existing `51xx` services + the `15090/19090/20090` infra are
  already effectively a footprint; the cleanest claim is **`5100–5119`** if CritterBids is
  re-homed, or a fresh range like `5260–5279` covering the `5273` SPA) and record it in a
  `PORTS.md` or a `docs/demo-runbook.md` § so it's a single source of truth.
- **(Coordination) Add CritterMart to MmoReconnect's `PORTS.md` cross-project table.** That table is
  the de-facto machine-wide registry; CritterMart's absence is why this audit was needed. That edit
  lands in the *MmoReconnect* repo, so it's outside this PR — flagged for the owner.
- **(Optional DX) Mirror MmoReconnect's stable container names** in CritterMart's AppHost
  (`.WithContainerName("crittermart-postgres")` / `"crittermart-rabbitmq"`) on top of the PR #78
  grouping label, for at-a-glance legibility of each container row. Cosmetic.

## Method

Static comparison of pinned configuration in both repos (read-only): the two AppHost
`Program.cs` + `launchSettings.json` files, the service `launchSettings.json` files
(`applicationUrl`), both `vite.config.ts`, and `mmo-reconnect/PORTS.md`. No concurrent live boot
was needed — every relevant port is statically pinned, and the runtime-variable ports are
self-avoiding (Aspire allocates free host ports for containers; the CritterWatch endpoint is
dynamic). Cross-checked against CritterMart's own `docs/demo-runbook.md § URLs & ports`.
