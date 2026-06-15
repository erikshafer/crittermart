# CritterMart Storefront (`client/`)

The round-two customer storefront SPA — a Vite + React + TypeScript client-side-rendered app that calls
the three Wolverine.Http services (Catalog, Inventory, Orders) **directly, cross-origin, with no BFF and
no dev proxy** (ADR 006 / 015 / 018). This is the single SPA for round one; the layout is a flat app at
`client/` (promote to a workspace only when a second SPA actually exists).

## Stack (pinned — ADR 015 amendment)

React 19 · Vite 8 · TypeScript 6 (strict) · TanStack Router (code-based) · TanStack Query · Zod ·
Tailwind v4 (`@tailwindcss/vite`) · shadcn/ui · Vitest. Node ≥ 22. `latest`/`*` ranges are not used and
`package-lock.json` is committed.

## Run

```bash
npm install
npm run dev       # Vite dev server on http://localhost:5173 (strict port)
npm run build     # tsc --noEmit + vite build
npm run test      # vitest run
```

Run under Aspire instead (`dotnet run --project ../src/CritterMart.AppHost`) to boot the full topology —
Postgres, RabbitMQ, the three services, and this SPA — with the service URLs injected automatically.

## How the three services are reached

Each service base URL comes from `import.meta.env.VITE_{CATALOG,INVENTORY,ORDERS}_URL`, injected by
Aspire's `AddViteApp` (see `../src/CritterMart.AppHost/Program.cs`). Standalone `npm run dev` falls back
to the services' launchSettings ports (5101 / 5102 / 5103). `src/config.ts` parses these through Zod at
startup. Because there is no proxy, dev issues genuine cross-origin requests exactly like prod — the
CORS allowlist on each service must include this app's origin (the AppHost injects it).

## Conventions

The project-specific conventions (Zod at every wire boundary, optimistic-UI + rollback, the
`useCurrentCustomer` → `X-Customer-Id` header seam, the no-BFF/CORS posture, code-based routing, the
presentation-state guardrail) live in [`../docs/skills/frontend/SKILL.md`](../docs/skills/frontend/SKILL.md).
Library mechanics defer to the installed per-library skills.

## Layout

```
src/
  api/client.ts              shared fetch: X-Customer-Id header + Zod boundary parse + typed 404
  config.ts                  the three Aspire-injected service URLs (Zod-validated)
  identity/useCurrentCustomer.tsx   the ADR 009 identity seam (stubbed id today, Polecat claim later)
  router.tsx                 code-based TanStack Router tree
  components/                AppShell, RouteNotFound (+ shadcn ui/ as components land)
  routes/                    page components (HomePage bootstrap placeholder; W1–W4 screens follow)
  lib/utils.ts               cn() class-merge helper
  index.css                  Tailwind v4 + shadcn neutral theme tokens
```
