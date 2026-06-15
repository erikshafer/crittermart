# Prompt: Implementations 016 — Frontend Bootstrap (`client/` Vite SPA + Aspire `AddViteApp`)

**Kind**: foundation / "skeleton" step of the round-two frontend (CLAUDE.md's *skeleton + first slice* exception — the skeleton half). Not a modeled behavioral slice: **no OpenSpec change, no new SHALL** — the decisions are already locked by ADR 015/016/018; this stands up the code those ADRs describe and resolves the one owed *mechanics* fork (Aspire `AddViteApp` integration). The W2 cart-review screen is the **first screen slice that follows**, not part of this PR (one-prompt-one-PR).
**Source**: frozen from the session handoff `crittermart-handoff-2026-06-15-frontend-bootstrap.md` (ephemeral, `%TEMP%`); this is its durable in-repo transcription.
**Files touched**: this prompt; `client/**` (new — flat Vite SPA: `package.json` + committed `package-lock.json`, `vite.config.ts`, `tsconfig.json`, `index.html`, `components.json`, `src/{main,router,config,vite-env.d}.ts(x)`, `src/api/client.ts`, `src/identity/useCurrentCustomer.tsx`, `src/lib/utils.ts`, `src/index.css`, `src/components/{AppShell,RouteNotFound}.tsx`, `src/routes/HomePage.tsx`, tests + `src/test/setup.ts`, `README.md`); `Directory.Packages.props` + `src/CritterMart.AppHost/CritterMart.AppHost.csproj` (Aspire.Hosting.JavaScript 13.4.3); `src/CritterMart.AppHost/Program.cs` (`AddViteApp` + CORS injection); `.github/dependabot.yml` (npm block); the three `*AppFixture.cs` + new `CorsPolicyTests.cs` ×3 (per-service CORS assertion); `docs/skills/frontend/SKILL.md` (v1 seed → v2 converged); `docs/{prompts,retrospectives}/README.md` (counts); `docs/retrospectives/implementations/016-frontend-bootstrap.md` (forthcoming).
**Mode**: solo; three genuine forks presented collaboratively (AskUserQuestion + previews) and resolved with the user **before any code** — they appear below as locked decisions. Stacked on the `tidy/close-slice-3-5` branch (PR #51) so the two diffs don't contend on README counts.
**Commit subject**: `feat: frontend-bootstrap — client/ Vite SPA + Aspire AddViteApp + CORS injection`

## Framing

Slice 3.5 shipped the cold-load read (`GET /carts/mine`); the frontend plan is locked (ADR 015 +amendment / 016 / 018). What has never existed is frontend *code*. This PR is the blueprint-architecture step: stand up the single customer-storefront SPA and wire it into the Aspire AppHost as a managed resource, so one `dotnet run` boots the full topology — Postgres, RabbitMQ, the three services, and the SPA — with the cross-network OpenTelemetry boundary exercised in dev exactly as in the demo. The substance that earns this its own session is the **owed Aspire `AddViteApp` integration decision** (cross-repo-comparison open #2): how the SPA is wired and how the three service URLs reach it.

## Goal

`client/` is a runnable, type-checked, tested Vite + React SPA on the pinned stack (ADR 015 amendment), with a committed lockfile. The AppHost launches it via `AddViteApp`, injects the three service base URLs as `VITE_*_URL` env vars (no proxy — ADR 018), and injects the SPA's pinned origin (`http://localhost:5173`) into each service's `Cors:AllowedOrigins`. Each service has a CORS assertion proving the allowlist admits the SPA origin and rejects others. The `npm` dependabot block is added. The frontend skill converges from its v1 seed (`[planned]` markers) onto the real `client/` files. `npm run build` + `npm run test` green; full .NET solution + `dotnet format` green.

## Spec delta

**No OpenSpec / workshop / narrative change** — this is scaffolding the ADRs already locked, not a modeled slice (no new behavioral SHALL; the storefront's behavior is the screen slices that follow). The canonical-spec movement is the **frontend skill converging v1 seed → v2**: the `[planned]` markers become real `client/` file references (`src/config.ts`, `src/api/client.ts`, `src/identity/useCurrentCustomer.tsx`, `src/router.tsx`, `package.json`+lockfile) and the CORS convention gains the per-service test reference. Narrative 005 stays v1.1 (the bootstrap adds no screen; the journey is unchanged until W2 lands).

## Locked decisions (forks resolved with the user at session start, 2026-06-15)

1. **Flat single app at `client/`** (not a monorepo workspace). Round one is one storefront SPA; the flat layout is the simplest lockfile/dependabot/CI story. Promote to a workspace only when a second SPA actually exists (CritterBids went monorepo only because it genuinely has three). `AddViteApp("storefront", "../../client")`; dependabot `directory: "/client"`.
2. **AppHost injects the CORS origin** (not per-service appsettings). Symmetric with the URL injection — Aspire is the single source of truth for the cross-origin wiring in both directions. The AppHost injects `storefront.GetEndpoint("http")` into each service as `Cors__AllowedOrigins__0`; the dev port is pinned to `5173` (the value `AddFrontendCors` already falls back to) so the origin is deterministic regardless of reference-resolution order. Services are declared before the storefront and never `WaitFor` it, so there is no startup cycle.
3. **Two PRs, tidy first.** The owed slice-3.5 close shipped as `tidy: docs` (PR #51); this bootstrap is `feat:` (PR #52), stacked on it so the README-count edits don't conflict. (The package is the official `Aspire.Hosting.JavaScript` 13.4.3 — sibling-proven, same Aspire line; the env vars are `VITE_`-prefixed and consumed via `import.meta.env` — both verified against current docs before wiring, not contested forks.)

## Orientation

1. **The session handoff** + **CLAUDE.md** — the skeleton-step exception, the frontend tech-stack row, the no-opportunistic-edits and one-prompt-one-PR rules.
2. **ADR 015 (+amendment) / 016 / 018 / 006 / 009** — the locked stack, version pins, no-BFF/CORS posture, the identity seam. **Do not re-litigate.**
3. **`docs/skills/frontend/SKILL.md`** — the v1 seed (the conventions to realize in code, then converge).
4. **The CritterBids sibling** (`C:\CritterBids`) — `src/CritterBids.AppHost/Program.cs` (`AddViteApp` + `WithHttpEndpoint` + `WithEnvironment`), `client/bidder/{package.json,vite.config.ts,src/router.tsx,src/main.tsx}` — the idiom to mirror, *minus* the proxy + SignalR (CritterMart inverts the proxy per ADR 018).
5. **`src/CritterMart.ServiceDefaults/Extensions.cs`** (`AddFrontendCors` — reads `Cors:AllowedOrigins`, falls back to 5173 in Development) and the three services' `appsettings.json` + launchSettings ports (5101/5102/5103).
6. **`tests/CritterMart.Orders.Tests/{OrdersAppFixture,ViewMyCartTests}.cs`** — the Alba fixture + scenario idiom for the CORS test.
7. **Skills**: `find-docs` (ctx7 — verify Aspire `AddViteApp`, Vite 8, TanStack Router code-based, Alba header API **before wiring**); `tailwind`/`shadcn`/`tanstack-query-best-practices`/`zod` (mechanics, as components land); `write-a-skill` (the convergence).

## Working pattern

ctx7 verify-before-wiring → scaffold `client/` (write files, `npm install` to generate the lockfile, `npm run build` + `npm run test` green) → Aspire wiring (package + csproj + Program.cs) → CORS injection + per-service `CorsPolicyTests` → dependabot npm block → `dotnet test` + `dotnet format` green → converge the skill → README counts → retro. One PR (#52), stacked on PR #51; the user merges (#51 first).

## Out of scope

- **No W2 cart-review screen** (or any W1/W3/W4 screen) — the first screen slice is the next session. `HomePage` is a bootstrap placeholder (a wiring check), not a modeled screen.
- **No `CartViewSchema` / per-read-model Zod schemas** — they land with the screen that binds them. The `fetchParsed` boundary-parse primitive + the header seam exist now; the schemas do not.
- **No OpenSpec change, no workshop/narrative amendment** — scaffolding, not a modeled slice.
- **No `nuget.config` change** — `Aspire.Hosting.JavaScript` resolves from nuget.org (the default source is not cleared); the pre-existing NU1507 two-sources warning is out of scope (touching it means the credentialed config file).
- **No SignalR / real-time, no PWA, no SEO/SSR** — explicitly excluded (ADR 015 R5); do not cargo-cult them from the sibling.
- **No real identity** — the `useCurrentCustomer` seam returns the stub; no login.
