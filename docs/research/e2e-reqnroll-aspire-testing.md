---
version: v1.0
status: Active
date: 2026-06-22
references:
  - tests/CritterMart.CrossBc.Tests/CrossBcFixture.cs
  - client/playwright.config.ts
  - client/e2e/seeder.spec.ts
  - docs/research/frontend-cross-repo-comparison.md
  - docs/decisions/004-dotnet-aspire-orchestrator.md
  - docs/skills/frontend/SKILL.md
  - Directory.Packages.props
---

# E2E & BDD testing — current state, gaps, and aspiration

> **What this is.** A self-discovery + aspiration note. The author recalled wanting
> Reqnroll / Gherkin-driven E2E tests and wasn't sure whether CritterMart already had
> them or whether it was only ever talked about. This doc records (a) what test coverage
> actually exists today, (b) what explicitly does **not** exist, and (c) the testing layer
> the author wants to build: Reqnroll feature files + reusable steps, driving the full
> stack via `Aspire.Hosting.Testing`.
>
> It is **research / exploratory** — not a decision (no ADR yet) and not a build order
> (no prompt yet). When this effort resumes, this note is the orientation; the next step
> would be an ADR (does CritterMart adopt Reqnroll + Aspire integration testing?) and then
> a workshop/prompt pair if it lands.
>
> **Provenance correction:** the "did we discuss E2E already?" memory traces to a
> conversation about a *different* project. CritterMart's docs and code contain **no**
> mention of Reqnroll, SpecFlow, Gherkin, BDD, or `.feature` files anywhere. This note is
> the first durable record of the aspiration.

## Bottom line

CritterMart today has **two** kinds of "heavy" tests and is missing the one the author
wants:

1. **xUnit cross-BC integration smoke tests** (`CrossBc.Tests`) — boot two services + real
   RabbitMQ + real Postgres in-process, assert the message round-trip. Heavy, real, but
   *not* full-stack and *not* feature-readable.
2. **One Playwright browser E2E spec** (`client/e2e/seeder.spec.ts`) — drives the live
   storefront in Chromium. True browser E2E, but a single startup-race smoke test, and it
   requires the stack to *already be running* (no orchestration).

There is **no** BDD/Gherkin layer and **no** use of `Aspire.Hosting.Testing`. Nothing
launches the whole Aspire-orchestrated stack from a test. That is the gap to close.

## What we have

### .NET test projects (all integration, all Alba + Testcontainers)

Everything under `tests/` targets `net10.0` and is tagged `[Trait("Category", "Integration")]`.
They are integration tests — each boots the real service `Program` via
`AlbaHost.For<Program>()` against a throwaway Testcontainers Postgres, and drives it over
HTTP with `Host.Scenario(...)`. No `WebApplicationFactory` is used directly (Alba wraps it);
**no** pure-unit xUnit project exists on the .NET side.

| Project | Scope | Infra |
|---|---|---|
| `CritterMart.Catalog.Tests` | Catalog service | Postgres (broker stubbed) |
| `CritterMart.Inventory.Tests` | Inventory service | Postgres (broker stubbed) |
| `CritterMart.Orders.Tests` | Orders service (largest, ~20 files) | Postgres (broker stubbed) |
| `CritterMart.Identity.Tests` | Identity (EF Core/Npgsql) | Postgres |
| `CritterMart.CrossBc.Tests` | **Cross-BC, full broker round-trip** | **Postgres + RabbitMQ, live transports** |

Pinned versions (`Directory.Packages.props`): Alba 8.5.2, Testcontainers.PostgreSql 4.12.0,
Testcontainers.RabbitMq 4.12.0, xunit 2.9.3, Shouldly 4.3.0.

### The heaviest .NET test: `CrossBc.Tests`

`tests/CritterMart.CrossBc.Tests/CrossBcFixture.cs` boots **both Orders and Inventory**
against **one `rabbitmq:4` container** and **one shared `postgres:18` container**, with
external Wolverine transports left live (not stubbed), and uses
`TrackActivity().AlsoTrack(...).IncludeExternalTransports()` to await cross-service delivery.
The three smoke tests exercise the real `place → ReserveStock → StockReserved → CommitStock`
(and Release) round-trips over the broker. It uses an `extern alias InventoryApp` to
disambiguate the two `Program` classes.

This is the closest thing to a system test today — but it wires the services together
*by hand in the fixture*, not via the Aspire AppHost, and it talks messages/HTTP, not UI.

### The only browser E2E: Playwright

- `client/playwright.config.ts` — one Chromium project, `baseURL: http://localhost:5273`
  (the Vite storefront). **No `webServer` block** → the AppHost + storefront must already
  be running; the test just drives a live environment.
- `client/e2e/seeder.spec.ts` — the single spec: load `/`, assert the three seeded products
  render with no empty/loading state. A seeder-vs-storefront startup-race smoke test.

Playwright (`@playwright/test`) lives in `client/package.json` (npm), **not** in the .NET
package graph. Frontend unit testing is Vitest + Testing Library, philosophy per
`docs/skills/frontend/SKILL.md`: "extract the hard part as a pure, unit-tested sibling."

### Aspire orchestration (the thing we'd test through)

`src/CritterMart.AppHost` uses Aspire 13.4.3 (`Aspire.Hosting.AppHost`, `.JavaScript`,
`.PostgreSQL`, `.RabbitMQ`) to orchestrate the four services + Postgres + RabbitMQ +
storefront for `dotnet run`. ADR 004 is the decision record.

## What we do NOT have

- **No Reqnroll.** Not in `Directory.Packages.props`, not in any `.csproj`.
- **No SpecFlow, Xunit.Gherkin, LightBDD, or any Gherkin runner.**
- **No `.feature` files anywhere.** (Grep hits for "feature file" in docs all mean a
  C#/Wolverine endpoint `.cs` file — the verb-named "feature folder" convention, ADR 021 —
  not Gherkin.)
- **No `Aspire.Hosting.Testing` / `DistributedApplicationTestingBuilder`** — confirmed
  absent from the entire repo. Nothing launches the orchestrated stack from a test.
- **No test pyramid / E2E strategy in `docs/vision.md` or any ADR.** The Playwright suite
  itself (commit `2bc314f`) landed with **no prompt, no retro, no ADR** — it bypassed the
  SDD pipeline. That is a small pre-existing spec-delta gap to reconcile.
- **The only prior E2E discussion** is in `docs/research/frontend-cross-repo-comparison.md`:
  Playwright was flagged as "optional round two — decide explicitly," and that decision was
  never promoted to an ADR. No Gherkin/BDD was ever discussed.

## What we're aspiring to

A **feature-centric, GWT-driven E2E layer** built on:

- **Reqnroll** (the maintained OSS successor to SpecFlow) for Gherkin `.feature` files.
- **GWT feature files** authored from — and traceable back to — the event-model GWT
  scenarios already in `docs/workshops/001-crittermart-event-model.md`. This is a natural
  fit: the workshop slices *already* carry Given/When/Then. Reqnroll feature files would be
  the executable form of those scenarios, closing the loop from model → spec → running test.
- **Reusable step definitions** — a shared step library (e.g. "Given a product is published",
  "When the customer adds it to their cart", "Then stock is reserved") composed across
  scenarios rather than re-authored per feature.
- **`Aspire.Hosting.Testing`** as the host: spin up the full Aspire-orchestrated distributed
  app (all services + Postgres + RabbitMQ + seeding) from the test process via
  `DistributedApplicationTestingBuilder`, then drive it. This replaces the hand-wired
  `CrossBcFixture` boot with the *real* topology — and doubles as a continuous check that the
  Aspire setup itself stays healthy.

### "Is that true E2E?" — the taxonomy

The author's parenthetical is worth answering directly, because the two ideas above sit at
different layers:

| Layer | Driver | What it boots | Browser? | We have it? |
|---|---|---|---|---|
| Integration (cross-BC) | xUnit + Alba | 2 services, hand-wired | No | ✅ `CrossBc.Tests` |
| **System / API-E2E** | Reqnroll + `Aspire.Hosting.Testing` | **whole orchestrated stack** | No (HTTP/message) | ❌ aspiration |
| UI / browser-E2E | Playwright | whole stack (must pre-run) | Yes | ⚠️ 1 spec only |

`Aspire.Hosting.Testing` launches the **entire stack** — so yes, it *is* end-to-end in the
"exercise the whole system together" sense. It is **headless** E2E (HTTP + messaging through
the real topology), not **browser** E2E. The only thing it leaves out is the rendered UI,
which is exactly the slice Playwright covers. The two are complementary, not competing:

- **Reqnroll + Aspire.Hosting.Testing** → feature-readable system tests of the *backend*
  behavior (the bulk of the domain — carts, orders, stock, the message round-trips).
- **Playwright** → the *UI* slice the headless layer can't reach (does the storefront render
  and behave).

Whether Reqnroll *also* drives Playwright for full browser-through-the-stack scenarios is an
open question for the ADR — possible, but heavier; the higher-value first step is Reqnroll
over the Aspire-hosted backend.

## Open questions for the resume / ADR

1. **Adopt Reqnroll at all?** It adds a BDD layer the project doesn't have. Justify against
   the talk's teaching goal — does feature-readable E2E *show* event sourcing better than the
   existing xUnit smoke tests, or just add ceremony?
2. **Where do feature files live?** A new `tests/CritterMart.E2E.Tests` (or `.Acceptance`)
   project, with `.feature` files traced to workshop GWT scenarios.
3. **Replace or augment `CrossBc.Tests`?** If `Aspire.Hosting.Testing` boots the real
   topology, the hand-wired `CrossBcFixture` may become redundant — or stay as the fast path.
4. **Seeding under test.** The Aspire-hosted run includes `CritterMart.Seeding`; tests need a
   deterministic seed (the Playwright spec already depends on the three seeded products).
5. **Reconcile the existing Playwright suite** into the pipeline (its missing prompt/retro/ADR)
   so the new E2E work and the old don't drift.
6. **CI cost.** Booting the full Aspire stack per test run is expensive — decide collection
   sharing / one-boot-per-assembly up front.

## Next step when this resumes

The conceptual strategy has since been drafted — see the companion note
[`e2e-strategy-conceptual-plan.md`](e2e-strategy-conceptual-plan.md) (pyramid placement,
suite shapes, the CI `e2e-tests` job, and the pipeline path). Read it alongside this one:
this note is the *self-discovery*, that note is the *strategy*.

The remaining step is to **author ADR 022** under `docs/decisions/` ("E2E testing strategy:
Reqnroll + Aspire.Hosting.Testing") capturing the decision and the taxonomy above. If it
lands, follow with an implementation prompt for the first `.feature` traced to a workshop
GWT scenario. These two research notes are the durable input that survives the machine
switch.
