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

The SPA is organized by **feature folder**, one per service surface the storefront binds — the frontend
echo of the backend's vertical slices. Each feature folder colocates its page(s), its `queryOptions`
factory (reads), its Zod schema (the wire boundary), and its mutations (writes), so a screen and the
contract it depends on are reviewed together. Shared infrastructure (the fetch client, the identity seam,
the config, the router, the app shell) sits at the root, outside any one feature.

```
src/
  api/client.ts              shared fetch: X-Customer-Id header + Zod boundary parse + typed 404
  config.ts                  the three Aspire-injected service URLs (Zod-validated)
  identity/useCurrentCustomer.tsx   the ADR 009 identity seam (stubbed id today, Polecat claim later)
  router.tsx                 code-based TanStack Router tree (the W1–W4 routes register here)
  components/                AppShell (header + cart badge), RouteNotFound
  lib/utils.ts               cn() class-merge helper
  index.css                  Tailwind v4 + shadcn neutral theme tokens

  catalog/                   W1 — Catalog (GET /products)
    BrowsePage · catalogQueries · catalogSchema
  cart/                      W2 — Cart (GET /carts/mine + edit commands)
    CartPage · CartBadge · cartQueries · cartSchema · cartMutations (optimistic add/remove/change-qty)
  orders/                    W3/W4 — Orders (POST /orders, GET /orders/{orderId})
    OrderConfirmationPage (W3) · OrderStatusPage (W4) · orderQueries · orderSchema
    placeOrderMutation (non-optimistic) · orderStatusJourney (the W4 poll/stepper brain, pure)
```

Route map (code-based, wired in `router.tsx`):

| Screen | Route | Page | Binds |
| --- | --- | --- | --- |
| W1 Browse | `/` | `catalog/BrowsePage` | `GET /products` + optimistic `AddToCart` |
| W2 Cart | `/cart` | `cart/CartPage` | `GET /carts/mine` + remove / change-quantity / place |
| W3 Confirmation | `/orders/$orderId/confirmation` | `orders/OrderConfirmationPage` | `GET /orders/{orderId}` (read-back after place) |
| W4 Tracking | `/orders/$orderId` | `orders/OrderStatusPage` | `GET /orders/{orderId}` (polled to terminal status) |
