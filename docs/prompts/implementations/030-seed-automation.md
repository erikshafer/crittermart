# Prompt: Implementations 030 — Seed automation (Aspire-wired console, auto-seed on boot)

**Kind**: dev/demo infrastructure — a new console resource that seeds the ephemeral DB on boot. **No domain behavior, no spec delta.** It drives the *existing* `POST /products` (Catalog) and `POST /stock/{sku}/receipts` (Inventory) HTTP endpoints; it adds no event, command, handler, projection, aggregate, or saga, and changes no spec.
**Source**: the `docs/demo-runbook.md` smoke-test work surfaced "no seed automation" as **Known Gap #1** — Aspire's Postgres is ephemeral (fresh empty DB every boot), so every boot needs the manual Step-3 curl seed. The owner picked **seed automation** from the open-pick, and within it the **console-seeder-auto-on-boot** shape (over a standalone console or a `.http` file).
**Files touched**: this prompt; `src/CritterMart.Seeding/CritterMart.Seeding.csproj` (new — `Microsoft.NET.Sdk`, `Exe`, net10.0, zero NuGet deps); `src/CritterMart.Seeding/Program.cs` (new — the seeder); `src/CritterMart.AppHost/CritterMart.AppHost.csproj` (add `ProjectReference` to Seeding); `src/CritterMart.AppHost/Program.cs` (add the `seeder` resource node — `WaitFor` catalog+inventory, inject `CATALOG_URL`/`INVENTORY_URL`, a leaf node the storefront does NOT wait on); `CritterMart.slnx` (add the project); `docs/demo-runbook.md` (Step 3 auto-seed + Known-gaps update); `docs/{prompts,retrospectives}/README.md` (implementations 29 → 30); `docs/retrospectives/implementations/030-seed-automation.md` (forthcoming).
**Mode**: solo. **Two forks already resolved with the owner** (AskUserQuestion): (1) which open-pick → **seed automation**; (2) seed shape → **console seeder, auto on boot**. The sub-shape — explicit URL injection + zero-dep console + hand-rolled retry, *not* `AddServiceDefaults`/service-discovery — is **settled by convention** (ADR 018 explicit-URL philosophy, the SPA's `VITE_*_URL` precedent), not re-decided.
**Commit subject**: `feat: seed automation — Aspire-wired console seeds products+stock on boot`

## Framing

CritterMart's Aspire Postgres has no data volume — it is wiped on every `dotnet run`. Today the
runbook's Step 3 hand-curls a product (Catalog) and a stock receipt (Inventory) after each boot, three
times over for the three demo SKUs (`crit-001` happy, `crit-rare` insufficient-stock, `crit-deluxe`
payment-decline). That manual step is the single biggest friction in the repeatable demo loop and the
runbook's named Known Gap #1.

A console seeder wired as an Aspire resource removes it: one `dotnet run` boots the stack **and** loads
the canonical seed, so the storefront's browse page is populated and all three demo routes
(happy / 5a / 5b) are drivable immediately, no manual setup. The seeder talks to the **real HTTP
endpoints** (not Marten directly), so it exercises the real `PublishProduct` and stock-`receipts`
handlers and — critically — takes **no project reference on any service** (honoring the "services don't
reference each other" structural constraint; the seeder isn't a service either, it just needs two base
URLs).

## Goal

`dotnet run --project src/CritterMart.AppHost` boots the full stack and, once Catalog + Inventory are
healthy, the `seeder` resource POSTs the canonical seed set and exits 0. The DB now holds the three demo
products + their stock; the SPA browse page lists them; happy / 5a / 5b are all demo-able with zero manual
seeding. The seed is **idempotent** (re-running against an already-seeded DB is a no-op: `POST /products`
409s a duplicate SKU, so each item gates its stock receipt on the 201). A `Seeding:Enabled` flag (default
on) lets an operator turn auto-seed off to restore the manual Step-3 workflow. Backend suite stays green;
format clean; AppHost builds.

## Spec delta

**None.** This is dev/demo infrastructure over already-shipped, already-spec'd endpoints. `PublishProduct`
(slice 1.1) and `ReceiveStock` (slice 2.1) are unchanged; the seeder is a client of them. No OpenSpec
change, no workshop or narrative change. The **runbook** (`docs/demo-runbook.md`) is the durable record of
the new boot behavior. The retro forward-confirms the named-none.

## Orientation

1. **CLAUDE.md** — one-prompt-one-PR; `{type}/{slug}` branch (`feat/seed-automation`); structural constraint (services don't reference each other — the seeder honors it by going over HTTP with only base URLs).
2. **`src/CritterMart.AppHost/Program.cs`** — the composition root. Mirror the SPA's explicit-URL injection (`catalog.GetEndpoint("http")` → `VITE_CATALOG_URL`) for `CATALOG_URL`/`INVENTORY_URL`; add the seeder as a leaf `AddProject` node with `WaitFor(catalog).WaitFor(inventory)`.
3. **`docs/demo-runbook.md`** — Step 3 (the manual seed this replaces), the curl quick-ref (the exact request shapes), Known gaps (#1 = no seed automation; this closes it).
4. **`src/CritterMart.Catalog/Features/PublishProduct.cs`** — `PublishProduct(Sku, Name, Description, Price)`; 409 on duplicate SKU (the idempotency hook).
5. **`src/CritterMart.Inventory/Features/ReceiveStock.cs`** — `ReceiveStock(Quantity)` at `POST /stock/{sku}/receipts`.
6. **`src/CritterMart.ServiceDefaults/Extensions.cs`** — what `AddServiceDefaults` provides (read to *justify NOT using it* in a tiny console; the explicit-URL path is lighter and ADR-018-faithful).

## Working pattern

`CritterMart.Seeding.csproj`: `Microsoft.NET.Sdk`, `<OutputType>Exe</OutputType>`, no `PackageReference`
(shared-framework `HttpClient` + `System.Net.Http.Json` only). → `Program.cs`: read `Seeding:Enabled`
(default true) and `CATALOG_URL`/`INVENTORY_URL` (localhost 5101/5102 fallback) from env; a canonical
`SeedItem` list; for each item POST the product with a **retry** wrapper (services may be "running" before
Marten's schema/JIT is ready), then on 201 POST the stock receipt, on 409 log "already seeded" and skip;
`Console.WriteLine` progress (Aspire captures stdout); exit 0 on success. → AppHost: inject the two URLs,
`AddProject<Projects.CritterMart_Seeding>("seeder").WaitFor(catalog).WaitFor(inventory)`; add the
`ProjectReference`. → `CritterMart.slnx`: add under `/src/`. → build (incl. AppHost) + full backend suite +
`dotnet format`. → **live-verify** the auto-seed end-to-end per the runbook (the new norm). → runbook Step 3
+ Known gaps. → README counts + retro. One PR; **owner merges**.

## Deliverable plan

- **`CritterMart.Seeding.csproj`** — console, Exe, net10.0, zero NuGet deps.
- **`Program.cs` (Seeding)** — env-driven URLs, `Seeding:Enabled` guard, canonical seed set, idempotent POST-product-then-receipt with retry, stdout logging, exit 0.
- **`Program.cs` (AppHost)** — inject `CATALOG_URL`/`INVENTORY_URL`, add the `seeder` leaf resource (`WaitFor` catalog+inventory).
- **`CritterMart.AppHost.csproj`** — `ProjectReference` to Seeding.
- **`CritterMart.slnx`** — add the project under `/src/`.
- **`docs/demo-runbook.md`** — Step 3 now auto-seeds on boot (manual kept as override/fallback); Known Gap #1 closed; document the `Seeding:Enabled` override.
- **Docs** — README counts 29 → 30; retro 030.

## Out of scope

- **No domain behavior** — no event, command, handler, projection, aggregate, or saga; the seeder is a client of existing endpoints.
- **No spec / workshop / narrative change** — `PublishProduct` and `ReceiveStock` are unchanged.
- **No `AddServiceDefaults`/service discovery in the seeder** — explicit-URL injection (ADR 018 / the SPA precedent) is the lighter, more faithful choice for a one-shot console; deliberately not pulling the CORS/health-check/framework-reference stack into it.
- **No carts/orders seeding** — only products (Catalog) + stock (Inventory); carts and orders are driven live in the demo.
- **No data volume / persistent DB** — Aspire Postgres stays ephemeral by design; auto-seed is the answer to the wipe-on-boot, not persistence.
- **No automated tests for the seeder** — it is HTTP glue against live services (like the AppHost wiring, which is also not unit-tested); correctness is established by the live-verify boot, recorded in the retro.
- **No removal of the payment-decline demo affordance** — that AppHost line is a separate post-talk chore.
