---
retrospective: 030
kind: implementations
prompt: docs/prompts/implementations/030-seed-automation.md
deliverable: src/CritterMart.Seeding/CritterMart.Seeding.csproj + Program.cs (zero-dep Aspire-wired seeder console), src/CritterMart.AppHost/Program.cs (seeder leaf resource + URL injection), src/CritterMart.AppHost/CritterMart.AppHost.csproj (ProjectReference), CritterMart.slnx (project entry), docs/demo-runbook.md (Step 3 auto-seed + Known gaps + Last verified)
date: 2026-06-17
mode: solo
session-runner: Claude (Opus 4.8)
---

# Retrospective — Implementations 030: Seed automation (Aspire-wired console, auto-seed on boot)

## Outcome summary

Closed the demo-runbook's **Known Gap #1** ("no seed automation"). Aspire's Postgres is ephemeral, so
the DB was empty on every boot and the demo required hand-curling three products + their stock each run.
A new one-shot console (`src/CritterMart.Seeding`), wired as an Aspire **`seeder`** resource, now POSTs
the canonical seed to the real Catalog/Inventory HTTP endpoints once those services are healthy, then
exits — so `dotnet run` on the AppHost yields a demo-ready stack with zero manual seeding.

- **`CritterMart.Seeding.csproj`** — `Microsoft.NET.Sdk`, `Exe`, **zero package references** (`HttpClient` + `System.Net.Http.Json` come from the shared framework).
- **`Program.cs` (Seeding)** — env-driven URLs (`CATALOG_URL`/`INVENTORY_URL`, localhost fallbacks), a `SEEDING_ENABLED` guard (default on), the canonical `SeedItem[]` set (`crit-001`/`crit-rare`/`crit-deluxe`), an idempotent POST-product-then-receive-stock loop (409 on a duplicate SKU → skip), a hand-rolled transient retry, `Console.WriteLine` progress, exit 0 (1 on hard failure → red on the dashboard).
- **`Program.cs` (AppHost)** — the `seeder` **leaf** node (`WaitFor(catalog).WaitFor(inventory)`, nothing waits on it) with the two service URLs injected exactly like the SPA's `VITE_*_URL` (ADR 018).
- **`AppHost.csproj` + `CritterMart.slnx`** — the `ProjectReference` (so Aspire generates `Projects.CritterMart_Seeding`) and the solution entry.
- **`docs/demo-runbook.md`** — Step 3 reframed (auto-seed first, manual kept as fallback/override + the `SEEDING_ENABLED=false` switch + a canonical-set table), Known Gap #1 flipped to DONE, troubleshooting + "Last verified" updated.

**Tests**: full backend suite green — **122** (Catalog 9, Inventory 21, Orders 89, CrossBc 3), unchanged from the #73 baseline; `dotnet format --verify-no-changes` clean; AppHost + full solution build.

**Live-verified** (real Aspire stack): the `seeder` published all three products + stock with zero manual
seeding (`crit-001`=100, `crit-rare`=1, `crit-deluxe`=100 via the real endpoints), a happy-path order
against the auto-seeded `crit-001` confirmed (stock `100→98`, committed `0→2`), zero boot errors; stack
torn down clean.

**Spec movement**: **none, by design** — the seeder is a *client* of the already-shipped `PublishProduct`
(slice 1.1) and `ReceiveStock` (slice 2.1) endpoints. No OpenSpec/workshop/narrative change; the **runbook**
is the durable record of the new boot behavior.

## What worked

- **Mirroring an existing convention removed the only real design question.** ADR 018 already fixed CritterMart's cross-origin philosophy (explicit URLs injected by the AppHost — the SPA's `VITE_*_URL`). Applying the same shape to the seeder (`CATALOG_URL`/`INVENTORY_URL`) meant the "how does it find the services" sub-fork was *settled by precedent*, not re-decided — so it never went to the owner. Reading `ServiceDefaults.Extensions` was still worth it: it let me justify *not* pulling `AddServiceDefaults` (service discovery + CORS + the ASP.NET framework reference) into a one-shot console.
- **Idempotency fell out of the domain, for free.** `PublishProduct` already returns a modeled 409 on a duplicate SKU (storing nothing). Gating each stock receipt on a fresh 201 made the whole seed a no-op on re-run with no extra bookkeeping — the event-sourced endpoint's own guard did the work.
- **Seeding over HTTP, not over Marten, kept the structural constraint intact.** Driving the real endpoints means the seeder takes no project reference on any service (only two base URLs), so "services don't reference each other" holds — and the seed exercises the real handlers + appends real events, exactly as a client would.
- **The leaf-resource shape makes the seeder safe to fail.** Nothing `WaitFor`s the seeder, so a seed hiccup shows red on the dashboard (visible) but never blocks the services or storefront from coming up. The hand-rolled retry covers the real gap — Aspire's `WaitFor` only guarantees the process started, not that Marten's schema finished applying on the first request.
- **The live boot was the decisive verification.** Unit tests would have meant mocking `HttpMessageHandler` to assert request shapes — low value for HTTP glue. Booting the real stack and seeing the three SKUs seeded + an order confirm against them is a far stronger signal, and it's the new norm anyway.

## What was harder / notable

- **Confirming `System.Net.Http.Json` is in the shared framework.** The zero-dependency goal hinged on `PostAsJsonAsync` and `HttpStatusCode` resolving without a NuGet package. They do for a `net10.0` `Microsoft.NET.Sdk` console (just needs explicit `using System.Net;` + `using System.Net.Http.Json;` — the default implicit-usings set includes `System.Net.Http` but not those two). Verified by building the project in isolation before wiring anything.
- **The seeder's `[seed]` stdout does not appear in the AppHost's own console.** Aspire routes each child resource's output to the **dashboard's** structured logs, not the parent process stdout. So the redirected boot log showed no `[seed]` lines — momentarily alarming — but the seeded *data* (queried via the endpoints) is the real proof, and the runbook now points operators at the `seeder` resource log in the dashboard, not the AppHost console.

## Methodology refinements

- **"Abandoned scaffold" deserves a literal check.** The `CritterMart.Seeding` folder existed with `bin/`/`obj/` artifacts, which reads like "there's a project to resurrect." A Glob showed only generated files — no `.csproj`, no `Program.cs` — so it was leftover build output, not salvageable source. Cheap recon ("what is actually in there?") changed the task from "revive" to "create fresh" and avoided a confused half-merge with stale artifacts.
- **Pick the resilience mechanism to match the host, not the house style.** The services get retry via `AddStandardResilienceHandler` (DI + `IHttpClientFactory`). A one-shot console with two calls doesn't earn that machinery; a five-line retry loop is more legible and keeps the zero-dependency property. Faithfulness to the repo's *patterns* (explicit URLs, real endpoints) matters more than reusing its *plumbing* in a context that doesn't fit.
- **Infra/demo tooling is an implementation PR for the prompt/retro pair, but not for the BC cadence counter.** It carries a full prompt + retro (it writes real code) yet isn't "an implementation PR against a bounded context," so — like #74's port move — it doesn't advance the design-return counter. Worth stating so the next session doesn't miscount the interleave budget.

## Outstanding / next-session inputs

- **Cadence**: #72 (interleave) reset the counter; #73 was the 1st implementation since; **this (#? seed automation) is cross-cutting infra — no BC-cadence tick** (like #74). Budget for the next design-return interleave is effectively unchanged: ~1 more BC implementation before an interleave is due.
- **The seeder seeds products + stock only.** Carts and orders are still driven live in the demo (intentional — the demo *is* placing orders). If a future demo wants pre-placed orders, that's a separate seed item set.
- **POST-TALK CHORE still owed** (unchanged): remove the `Payment__DeclineOverAmount=100` AppHost line after the talk; the seeder change does not touch it.
- **Candidate follow-ups remaining**: cart identity-transport harmonization (the leading BC slice), OTel/in-browser visual pass, product detail (Gap #2), the MmoReconnect-overlap audit.
- **Carry-forwards (unchanged):** no frontend CI job; the flaky `PaymentAuthorizationTests` Wolverine-shutdown race; NU1507 (the seeder surfaces it too — same two-source CPM cause); node-orphan hygiene before a boot; CritterWatch trial expires 2026-07-10.

## Spec-delta — landed?

**Named-none, forward-confirmed.** The prompt named **no spec delta** — the seeder is dev/demo
infrastructure over already-shipped, already-spec'd endpoints (`PublishProduct`, `ReceiveStock`). That is
exactly what shipped: no event, command, handler, projection, aggregate, or spec change; the durable record
of the new boot behavior lives in `docs/demo-runbook.md` (the operational home), not a spec layer. No
requirement was confabulated to satisfy the loop.
