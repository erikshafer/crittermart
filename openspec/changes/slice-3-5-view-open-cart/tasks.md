# Tasks: Slice 3.5 — View My Open Cart

## 1. Verify before wiring (ctx7, numbered facts)

- [x] 1.1 `/jasperfx/wolverine` — HTTP parameter binding: `[FromHeader(Name = "...")]` binds a request header to a method parameter; a missing header yields the parameter's default value. Query-string binding also confirmed (`querystring.md`). Verified at session start; shaped Decision 1 + Decision 5.

## 2. The endpoint

- [x] 2.1 `Features/ViewMyCart.cs` — `GET /carts/mine`: `[FromHeader(Name = "X-Customer-Id")] string? customerId` → blank guard (`400`) → `session.Query<CartView>().Where(v => v.CustomerId == customerId && v.IsOpen).FirstOrDefaultAsync()` → `404` if null, else `200` with the `CartView`. `IResult` return, matching the sibling cart-read endpoints. No edit to `AddToCart.cs` or `Program.cs`.

## 3. Integration proof

- [x] 3.1 `tests/CritterMart.Orders.Tests/ViewMyCartTests.cs` — Alba suite over the existing index:
  - open cart returned (`200`, two SKU-keyed lines at snapshot prices) for the matching `X-Customer-Id`;
  - no open cart → `404` (customer never created one);
  - a checked-out cart → `404` (`PlaceOrder` closed it; index frees the customer) — proves terminal carts do not resolve;
  - missing `X-Customer-Id` header → `400`.
- [x] 3.2 Full solution build + test run green (`dotnet test`), Inventory/Catalog/CrossBc suites untouched.

## 4. Deferred frontend skill

- [x] 4.1 `docs/skills/frontend/SKILL.md` — document only CritterMart-specific frontend conventions (version pins, Zod-at-every-boundary, optimistic-UI + rollback, the `useCurrentCustomer` seam → `X-Customer-Id` header, no-BFF/CORS posture, TanStack Router + presentation-state guardrail) and how the SPA consumes `GET /carts/mine`. Defer library mechanics to the installed per-library skills (`tanstack-query-best-practices`, `zod`, `shadcn`, `tailwind`, `react-hook-form`); note it is the v1 seed (first non-`core` cluster), fleshed out as components land. Add the index row to `docs/skills/README.md`.

## 5. Sibling artifacts

- [x] 5.1 `docs/narratives/005-customer-storefront.md` — bump version + Document History row (slice 3.5's read now implemented; W2 cold-load read is real).
- [x] 5.2 `docs/retrospectives/implementations/015-slice-3-5-view-open-cart.md` (+ prompt `015`) — outcome, refinements, spec-delta confirmation, deferred-state call-outs (client/ bootstrap, CORS-origin injection, dependabot npm block).
- [ ] 5.3 `openspec validate slice-3-5-view-open-cart --strict` green; consolidated PR opened.
