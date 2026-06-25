# CritterWatch — alpha.4 upgrade assessment + deep DX/UX round plan

**Author:** Erik Shafer · **Date:** 2026-06-25 · **Branch:** `research/cw-telemetry-spike`
**Status:** planning (hand-off to a higher-effort session)
**Predecessor:** [`cw-feedback-jasperfx.md`](cw-feedback-jasperfx.md) (alpha.3 UI/UX round, 8 entries, 27 screenshots)

This doc has two independent parts:

1. **Part A — alpha.4 upgrade assessment.** Whether/how to move off `1.0.0-alpha.3`.
2. **Part B — the deep round.** A second, deeper pass of inspection/analysis/feedback on the
   **currently installed alpha.3**, adding a Developer-Experience (DX) lens on top of UI/UX.

The two are sequenced, not merged. The deep round runs on alpha.3 (GA-Wolverine, known-good,
live-verified). The alpha.4 upgrade is an optional Phase 0 the session may take first.

---

## Part A — alpha.4 upgrade assessment

### What's available

NuGet has `CritterWatch` and `Wolverine.CritterWatch` `1.0.0-alpha.4` (we're on `alpha.3`).

### Dependency reality (from the alpha.4 nuspec)

`CritterWatch 1.0.0-alpha.4` is compiled against:

| Dependency | Version | Note |
|---|---|---|
| `WolverineFx.Marten` / `WolverineFx.Http.Marten` / `WolverineFx.SignalR` | `6.14.0-alpha.2` | **prerelease** (alpha.3 used GA `6.12.0`) |
| `WolverineFx` (via Wolverine.CritterWatch) | `6.14.0-alpha.2` | **prerelease** |
| `Marten` | `9.11.0-alpha.3` | **prerelease** |
| `JasperFx.Events` | `2.15.0` | |
| `WolverineFx.SignalR` | `6.14.0-alpha.2` | **new transport surface** |
| `Microsoft.AspNetCore.SignalR.StackExchangeRedis` | `9.0.17` | **new — Redis backplane** |

### The coupling still bites (and harder)

The diagnosis from 2026-06-24 holds: there is one `Wolverine.Marten` per process, shared by the
console and all four services, so **every `WolverineFx.*` pin must equal the version CritterWatch was
built against** or the console throws a startup `TypeLoadException`
(`EventSubscriptionAgentFamily.TryRebuildRegisteredProjectionAsync`). alpha.3 → `6.12.0` (GA).
alpha.4 → `6.14.0-alpha.2` (**prerelease**). So adopting alpha.4 drags the *entire solution* onto a
prerelease Critter Stack line, not a GA one. That is a materially different risk than the alpha.3 bump.

### Upgrade steps (if/when taken)

1. In `Directory.Packages.props`:
   - `CritterWatch`, `Wolverine.CritterWatch` → `1.0.0-alpha.4`.
   - All eight `WolverineFx.*` pins `6.12.0` → `6.14.0-alpha.2`.
   - Add an explicit `Marten` pin `9.11.0-alpha.3` (stop relying on transitive resolution so the
     console and services can't drift), and `JasperFx.Events 2.15.0` if needed to satisfy the graph.
   - Add `WolverineFx.SignalR 6.14.0-alpha.2` to the CritterWatch console project.
   - Update the big "HELD AT 6.12.0" comment block to describe the new pin and *why* (prerelease).
2. **AppHost / Aspire:** alpha.4's SignalR backplane likely wants a **Redis** resource. Add
   `Aspire.Hosting.Redis`, wire a Redis container, and reference it from the console host. Verify
   against the upstream `wolverine-integrations-critterwatch-setup` skill (clustered host + Redis
   SignalR backplane section) **before** wiring — the skill is the source of truth for the topology.
3. **Re-audit the CVE:** `dotnet list package --vulnerable --include-transitive`. Only retire the
   `GHSA-hv8m-jj95-wg3x` MessagePack suppress if the graph genuinely no longer pulls a vulnerable
   MessagePack. Do not retire it on faith.
4. **Build + live-verify:** full `dotnet build`, run the stack, confirm the license validates as
   **Trial** (console must run as `Production`, per the trial memo — a dev-tier fallback masks the
   license), confirm telemetry flows and the monitoring console boots clean (no `TypeLoadException`).
5. Run `139/139` tests; live-verify with `docs/demo-traffic.ps1`.

### Recommendation

**Do not block the deep round on the upgrade.** Run the deep round on alpha.3 first (GA Wolverine,
known-good). Treat alpha.4 as either (a) a *separate* upgrade PR after the deep round, or (b) a
*second* deep-round target so JasperFx gets feedback on the latest line — but only once it boots
clean. Adopting prerelease Wolverine+Marten *and* doing a feedback pass at the same time would
confound which layer any friction came from.

> ⚠️ Dependabot guard still stands: do **not** merge Wolverine `6.13.x` bumps (#94/#99). alpha.4 jumps
> to `6.14.0-alpha.2`, so `6.13.x` remains a version nobody's CW was built against.

---

## Part B — the deep round (on alpha.3)

### How this goes deeper than the alpha.3 round

The first round (`cw-feedback-jasperfx.md`) was a **heuristic UI/UX review from static full-page
screenshots** — 8 entries, an operator's task lens. The deep round adds three dimensions:

1. **DX (Developer Experience) lens — new.** Everything a *developer* (not an operator) touches:
   install/wiring, package/version friction, docs & skill accuracy, API ergonomics, error messages
   as a developer reads them, the MCP server surface, and the licensing/dev-tier story.
2. **Interaction-level UX — deeper than screenshots.** Drive the controls, not just photograph them:
   empty→loading→populated→error state transitions, keyboard/focus/ARIA accessibility, contrast,
   narrow-viewport/responsive behavior, dark mode, deep-linking, and the per-row-error stepper path
   under real load.
3. **Closure on round one.** Re-shoot the 8 existing entries to confirm none silently changed, and
   convert each into a crisp repro the JasperFx team can act on.

### DX investigation checklist

- **Install / wiring friction.** Walk the `critterwatch-install` + `critterwatch-setup` skills against
  the actual CritterMart wiring. Where did reality diverge from the skill? (e.g. the
  console-must-run-as-Production gotcha, the `RuntimeCompilation`/Dynamic-codegen requirement, the
  per-service `Wolverine.CritterWatch` registration.) Each divergence is a DX feedback item.
- **Version/dependency DX.** The single-`Wolverine.Marten`-per-process coupling and the resulting
  `TypeLoadException` is *the* headline DX pain — a developer following the happy path with a current
  Wolverine gets a cryptic startup crash. Write it up from a first-time developer's POV: what the
  error says vs. what it means, and what CW could surface instead (a clear "CritterWatch x.y was built
  against WolverineFx z; you have w" preflight check).
- **Licensing DX.** The dev-tier fallback that masks the trial license unless the console runs as
  `Production` (per the trial memo) — document the confusion and propose a louder signal.
- **Error-message DX.** The `StockLevel` "no parameterless constructor" failure (round-one entry 2) is
  also a DX story: the recommended immutable-record aggregate idiom produces a runtime explosion in the
  stepper. Frame it for developers, not just operators.
- **MCP server surface.** `CritterWatch.Mcp` (cross-application MCP server, per the setup skill) — is
  it wired here? If reachable, exercise it and assess the tool/DX surface. If not wired, note the gap.
- **Docs/skill accuracy.** Flag every place the upstream JasperFx ai-skills docs lag the installed
  alpha.3 behavior (candidate fixes land in the ai-skills repo per the deferred-DLQ-skill precedent).

### Deeper UI/UX checklist (interaction, not screenshots)

- **State-transition testing.** For each surface, drive idle → loading → populated → empty-result →
  error and confirm they're visually distinct (round-one entry 5 flagged idle vs. empty-result look
  identical — verify the rest of the app for the same class of bug).
- **Accessibility.** Keyboard-only navigation, visible focus rings, ARIA roles on the custom controls
  (segmented Source group, switches), and color-contrast on the all-red error wall and status chips.
  Consider an axe/Lighthouse pass via the existing Playwright harness.
- **Responsive / narrow viewport.** Round-one entry 4 noted the Projections table clips past the right
  edge even at 2000px. Test sub-1280px and document reflow/clipping.
- **Dark mode** (if present) — contrast and the red error states.
- **Deep-linking & state persistence.** Does selecting a service / stream survive a refresh or a shared
  URL? (Round-one entry 1 proposed clickable stream ids deep-linking into the Stepper — test whether
  any deep-linking exists today.)
- **The two-service-selector conflict** (round-one entry 3) — reproduce the contradictory-scope state
  interactively and capture the exact repro.

### Method & tooling

- **Branch:** continue on `research/cw-telemetry-spike` (this branch already carries the round-one
  packet + screenshot harness + telemetry fodder; the deep round is the same spike, deeper — keep the
  lineage together). It is **not** merged to `main`, so nothing forces a fresh branch. If the session
  also does the alpha.4 upgrade, do that on a *separate* branch off `main` so the upgrade PR is
  cleanly revertable and isn't entangled with feedback artifacts.
- **Light the console** exactly as round one: `Cw__Telemetry=true`, async daemon + `OrderPlacedSignal`
  broadcast + poison endpoint on, then `docs/demo-traffic.ps1 -Continuous -LinesPerOrder 2
  -MaxQuantity 3 -PoisonEvery 7`. (See `cw-telemetry-fodder.md`.)
- **Re-use + extend the Playwright harness** at `docs/research/cw-screenshots/capture-cw.cjs` — add
  interaction scripts (click-through, keyboard, axe injection, narrow-viewport) rather than only
  full-page shots. Keep the manifest pattern.
- **Live-verify** end-to-end before writing conclusions (boot the real stack, drive flows myself).

### Deliverable

A second feedback packet — `docs/research/cw-feedback-jasperfx-deep.md` (or v2 of the existing one) —
structured like round one (prioritized entries, target/effort, evidence path), but with an explicit
**DX section** alongside UI and UX, plus the round-one closure notes. Same tone: every item a
question, not a verdict; embrace the ubiquitous language, teach it in place.

### Post-round housekeeping (carried from the live stack)

- Teardown owed: the stack may still be up with demo-traffic from the prior session.
- POST-TALK cleanup still owed: remove `Payment__DeclineOverAmount=200` after the demo era.
