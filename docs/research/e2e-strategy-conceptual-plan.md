---
version: v1.0
status: Active
date: 2026-06-22
references:
  - docs/research/e2e-reqnroll-aspire-testing.md
  - docs/workshops/001-crittermart-event-model.md
  - tests/CritterMart.CrossBc.Tests/CrossBcFixture.cs
  - src/CritterMart.AppHost/Program.cs
  - .github/workflows/dotnet.yml
  - docs/decisions/004-dotnet-aspire-orchestrator.md
---

# E2E testing strategy — conceptual plan (Reqnroll + Aspire.Hosting.Testing)

> **What this is.** The high-level, conceptual plan for adding a feature-centric,
> BDD/GWT-driven **end-to-end testing layer** to CritterMart. It is the design-phase
> companion to [`e2e-reqnroll-aspire-testing.md`](e2e-reqnroll-aspire-testing.md): that
> note is the *self-discovery* (what coverage exists, what's missing, the aspiration);
> **this** note is the *strategy* (the shapes, guardrails, CI design, and the decisions
> to make) that downstream sessions turn into implementation plans and then code.
>
> **What it is not.** It is **not** a decision and **not** a build order. It does not
> pick the project layout, write the fixture, or author a feature file. Per the research
> README, a research doc has no authority — it *informs* the binding choice without
> *making* it. The binding choice is **ADR 022** (named in §7); the build work is the
> prompt/retro sessions that follow it. This plan is written to survive a machine/agent
> switch and be the orientation those sessions read first.
>
> **Persona.** Authored from a Senior QA Engineer seat (with a Senior DevOps dash) at the
> user's request — hence the pyramid-placement discipline, the determinism/isolation
> emphasis, and the explicit CI-job design.

## Bottom line

Add a **third heavy test layer** — *system / acceptance E2E* — that boots the **whole
Aspire-orchestrated stack** via `Aspire.Hosting.Testing` and drives curated **customer
journeys** authored as **Reqnroll Gherkin `.feature` files traceable to the workshop GWT
scenarios** (`docs/workshops/001`, §6). It runs as its own `e2e-tests` job in GitHub CI,
separate from the unit and integration jobs. It is the thin top of the pyramid — a small
catalogue of load-bearing journeys and cross-BC seams, **not** a scenario per slice. The
gate before any code is **ADR 022**; the first build is a "skeleton + first slice" PR that
stands up the project, the Aspire fixture, an eventual-consistency await helper, one traced
feature, and the CI job.

## 1. What "E2E" means here, and where it sits in the pyramid

CritterMart already has two heavy layers. This plan adds a **third, distinct** one — it
does not replace what exists:

| Layer | Driver | Boots | Owns | Status |
|---|---|---|---|---|
| Integration (per-service) | xUnit + Alba + Testcontainers | one service | "is this service correct in isolation" | ✅ exists |
| Integration (cross-BC) | xUnit + Alba, hand-wired `CrossBcFixture` | 2 services + broker | "does the message round-trip" | ✅ exists |
| **System / acceptance E2E** | **Reqnroll + `Aspire.Hosting.Testing`** | **whole orchestrated stack** | **"does a customer journey work end to end, against the real topology"** | ❌ **this plan** |
| UI / browser E2E | Playwright | whole stack (pre-run) | "does the storefront render & behave" | ⚠️ 1 spec |

**Thesis.** The new layer is *acceptance-level* and *journey-shaped*. Its unit of
authorship is a Gherkin scenario traceable to a workshop GWT; its unit of execution is a
message/HTTP round-trip through the **Aspire-orchestrated** stack (not hand-wired). It
answers: *"if I change anything and this goes red, a customer-facing promise broke."*

**Guardrail against pyramid inversion.** E2E is the *thin top*. We do **not** re-test every
branch here — failure-path coverage stays in the integration suites. E2E asserts the
**load-bearing journeys** and the **cross-BC seams** no single-service test can see. Target
a small curated catalogue (≈ one feature per BC journey + the place-order saga), not a
scenario per slice.

## 2. The BDD approach — natural fit, not ceremony

This answers Open Question #1 of the predecessor note ("does BDD *show* event sourcing
better, or just add ceremony?"). The case **for**:

- The workshop (`docs/workshops/001`, §6) **already authored GWT scenarios** per slice.
  Today they are prose that dead-ends. Reqnroll `.feature` files make them **executable**,
  closing the loop **model → spec → narrative → running test** that the whole SDD pipeline
  exists to produce.
- Gherkin's vocabulary (`Given a product is published / When the customer checks out /
  Then stock is reserved`) **is the ubiquitous language** — the same nouns the event model
  froze. The step library becomes a living glossary of domain operations.
- For the talk's teaching goal: a green `.feature` file in plain domain English, backed by
  a real event-sourced cross-BC round-trip, is a far better demo artifact than an xUnit
  smoke test.

**Authoring discipline.** Every scenario carries a **traceability tag** back to its source
(e.g. `@slice-4.2 @workshop-001`). A scenario with no workshop/OpenSpec ancestor is a smell
— it means we are testing something the model never described.

## 3. Suite architecture (conceptual — the shapes, not the code)

```
tests/CritterMart.E2E.Tests/         ← new project, net10.0, [Category=E2E]
  Features/                          ← .feature files, organized by journey/BC
    place-order.feature
    browse-and-cart.feature
    stock-lifecycle.feature
  Steps/                             ← reusable step library (the real asset)
    CatalogSteps.cs, CartSteps.cs, OrderSteps.cs, StockSteps.cs
  Support/
    AspireAppFixture.cs              ← boots DistributedApplicationTestingBuilder once
    HttpDrivers/                     ← typed clients per service (thin)
    EventualConsistency.cs           ← poll-until / await-condition helpers
    SeedData.cs                      ← deterministic, per-scenario isolation
```

Four design pillars:

1. **Host once, isolate by data.** Booting the full Aspire stack is expensive, so boot
   **one stack per assembly** (a shared Reqnroll fixture) and isolate scenarios by **unique
   data** (per-scenario customer ids, SKUs) — never by re-booting infra. This is the single
   most important cost/correctness decision; it pre-answers predecessor Open Question #6
   (CI cost).
2. **Drive through the front door only.** Steps talk to services over **HTTP** and observe
   via the **read models / messages** — exactly as a client would. No reaching into
   `IDocumentSession` to assert internal state; assert the *observable* contract (the
   `*View` projections, the HTTP responses). This is what keeps it a true E2E and not a fat
   integration test.
3. **Eventual consistency is first-class.** The hard part here is **async cross-BC flows**:
   `PlaceOrder → ReserveStock → StockReserved → CommitStock` resolves over RabbitMQ, *not*
   synchronously. Unlike `CrossBcFixture` (which uses Wolverine's in-process
   `TrackActivity().IncludeExternalTransports()` to await delivery), an Aspire-hosted stack
   is out-of-process — so steps must **poll a read model to an expected state with a
   timeout** (`Then eventually the order is Confirmed`). A robust await/retry helper is core
   infrastructure, authored once and reused everywhere.
4. **Time and demo-knobs are controllable.** The AppHost bakes in demo affordances
   (`Payment__AuthDelay=20s`, `Payment__DeclineOverAmount=200`, `Orders__PaymentTimeout`).
   E2E must **override these at host-build time** via `Aspire.Hosting.Testing` config
   injection — zero auth-delay for speed, a low decline threshold to exercise the
   cancel/release path deterministically. Tests own their time; the demo defaults don't
   leak in.

## 4. The reusable step library (the durable asset)

The feature files are cheap; the **step library is where value compounds**. Conceptual
taxonomy, composed across scenarios:

- **Given (arrange):** `a product "X" is published with price P`, `stock of N is received
  for "X"`, `a registered customer`, `an open cart containing …`
- **When (act):** `the customer adds "X" to their cart`, `the customer checks out`,
  `payment is declined` (via knob), `the payment window elapses`
- **Then (assert, eventually):** `stock for "X" shows N reserved`, `the order reaches
  "Confirmed"`, `the reserved stock is released`

These map 1:1 onto the event-model commands/events, so the library **is** the domain API in
English. New scenarios should mostly *recompose existing steps*; authoring a brand-new step
is the exception and signals a genuinely new domain operation.

## 5. Scope sequencing — headless first, browser second

- **Phase target = headless system E2E** (Reqnroll over the Aspire-hosted backend). Highest
  value, covers the bulk of the domain (carts, orders, stock, the saga, the cross-BC
  seams), no browser flake.
- **Browser E2E (Playwright) is a later, complementary layer**, not a competitor. The
  existing single Playwright spec gets **reconciled into the pipeline** (it shipped with no
  prompt/retro/ADR — predecessor Open Question #5), and whether Reqnroll *drives* Playwright
  for full-browser-through-stack scenarios is deferred to the ADR. **Recommendation:** keep
  the runners **separate** initially — Reqnroll for backend journeys, Playwright for UI
  render assertions — and unify only if a scenario genuinely needs both.

## 6. CI / DevOps design (the separate job)

A new **`e2e-tests` job** in `.github/workflows/dotnet.yml`, parallel-but-distinct from
`unit-tests` and `integration-tests`, selected by `--filter "Category=E2E"`:

```
build ──┬─ format
        ├─ unit-tests        (Category!=Integration & !=E2E)   fast, no Docker
        ├─ integration-tests (Category=Integration)            Testcontainers
        └─ e2e-tests         (Category=E2E)                    ← NEW: full Aspire stack
```

DevOps decisions to bake in:

- **Filter hygiene.** Today `unit-tests` runs `Category!=Integration`. Adding an `E2E`
  category means that filter must become `Category!=Integration&Category!=E2E`, or E2E
  leaks into the "fast" job. Small but load-bearing.
- **Docker is available** on `ubuntu-latest` (the integration job already relies on it);
  `Aspire.Hosting.Testing` brings up Postgres + RabbitMQ the same way. **Node** is
  additionally needed *only if* the storefront/Playwright is in-scope for the job.
- **Run cadence.** Full-stack boot is the slowest check. Options for the ADR: (a) run on
  every PR (simplest; honors "red = fix it"); (b) gate behind a label / run nightly if
  wall-time hurts. **Recommendation:** every PR while the catalogue is small (a handful of
  scenarios); revisit only if it crosses a time budget.
- **Resilience.** Generous job timeout; `concurrency` cancel-in-progress (already set); a
  **bounded** flaky-retry policy on async-await steps — but treat persistent flakiness as a
  bug in the await helper, not a reason to widen retries.
- **Artifacts.** Publish Reqnroll's living-doc / test results (and Playwright traces +
  screenshots-on-failure when that layer lands) as CI artifacts for triage.
- **Path filter.** The existing `paths-ignore` (docs/openspec/md) already keeps doc-only
  PRs from triggering — E2E inherits that for free.

## 7. How this threads into the SDD pipeline

This is *pre-code design*, so it follows the pipeline's own rules rather than jumping to
code:

1. **ADR 022 — "E2E testing strategy: Reqnroll + Aspire.Hosting.Testing"** (next free
   number; index in [`../decisions/README.md`](../decisions/README.md) ends at 021). Records
   the decision, the layer taxonomy, and resolves the predecessor note's six open questions.
   This is the gate; nothing builds until it lands.
2. **Reconcile Playwright's provenance** — a small `tidy:`-flavored note/retro acknowledging
   the existing spec entered outside the pipeline, so old and new E2E don't drift.
3. **Blueprint slice (the "skeleton + first slice" pipeline exception):** one PR that stands
   up `CritterMart.E2E.Tests`, the Aspire fixture, the await helper, **one** feature file
   traced to **one** workshop GWT — strongest candidate: **the place-order cross-BC happy
   path (slices 4.1 → 4.2 → 4.4)**, because it exercises HTTP + the saga + the broker + two
   services in one scenario — plus the CI job. Build one journey by hand before turning the
   loop loose.
4. **Per-journey loop afterwards:** each subsequent feature file is its own prompt/retro
   pair, recomposing the maturing step library, honoring the design-return cadence.
5. **Spec-delta closure:** each E2E session's delta is "workshop GWT §N now has an
   executable feature file" — recorded back in the workshop's `## Document History`, finally
   closing the model → test loop.

## 8. Open decisions to resolve in the ADR (feeds the next session)

Genuine forks this plan surfaces but does **not** pre-decide:

1. **`CrossBc.Tests` — keep or retire?** Once Aspire boots the real topology, the
   hand-wired fixture may be redundant — *or* kept as the fast in-process path. (Lean: keep
   both initially; in-process is cheaper for tight feedback.)
2. **Seeding under test** — reuse `CritterMart.Seeding` for a deterministic baseline, or
   seed per-scenario via steps? (Lean: minimal/no global seed; scenarios arrange their own
   data for isolation.)
3. **Identity maturation** — today identity rides the `X-Customer-Id` header (the
   Polecat-deferral seam). The step library should **abstract "acting as a customer"** behind
   one step so the eventual header → Bearer-token swap touches one place, not every scenario.
4. **Does Reqnroll drive Playwright?** Browser-through-stack scenarios — possible but
   heavier. (Lean: defer; keep runners separate for round one.)
5. **CI cadence** — every-PR vs. nightly/labelled (§6 above).

## 9. Phased roadmap at a glance

- **Phase 0 — Decide:** ADR 022 + Playwright reconciliation.
- **Phase 1 — Walking skeleton:** E2E project + Aspire fixture + await helper + 1 traced
  feature + CI `e2e-tests` job.
- **Phase 2 — Breadth:** feature catalogue across the BC journeys; step library matures;
  traceability tags enforced.
- **Phase 3 — Browser layer:** fold Playwright in (separate runner) for UI-render
  assertions.
- **Phase 4 — Auth:** evolve the "acting as a customer" step as Identity gains real login.

## Promotion path

- **→ ADR 022** when the binding decision is made (§7 step 1). This note stays put as the
  durable record of how the strategy was shaped.
- **→ prompt** for the Phase 1 skeleton+first-slice session once the ADR lands.
- **status: Superseded** if a later strategy doc overtakes it — cross-reference the
  successor rather than deleting this.
