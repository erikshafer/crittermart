# Tasks — Harmonize cart command identity

## 1. Backend endpoints
- [x] `AddToCart`: route → `/carts/mine/items`; `[FromHeader] X-Customer-Id`; blank-header → 400.
- [x] `ChangeCartItemQuantity`: route → `/carts/mine/items/{sku}/quantity`; header; blank-header → 400.
- [x] `RemoveCartItem`: route → `/carts/mine/items/{sku}`; header; blank-header → 400.

## 2. Backend tests
- [x] Repoint every cart-add/edit call-site (Orders.Tests + CrossBc.Tests) to `/carts/mine/*` + `X-Customer-Id` header.
- [x] Add a missing-header → 400 test for each of the three commands.
- [x] `dotnet build` + Orders suite + CrossBc suite green.

## 3. Frontend
- [x] `cartMutations.ts`: three hooks drop the path interpolation → `/carts/mine/*` (header rides via the shared client).
- [x] `cartMutations.test.tsx`: URL + header assertions updated.
- [x] SKILL Convention 4: "known divergence … do not fix" → "harmonized".
- [x] `npm run test` + `tsc` + `vite build` green.

## 4. Docs
- [x] Narrative 005: transport moment + version bump + Document History.
- [x] `demo-runbook.md`, `demo-traffic.ps1`, `otel-trace-walkthrough.md`: route refs → `/carts/mine/*` + header.
- [x] Prompt + retro `implementations/032`; README counts.

## 5. Spec
- [x] `shopping-cart`: MODIFY the three command requirements with the header-transport rule + missing-identity 400 scenario.
- [x] `openspec validate harmonize-cart-command-identity --strict`.
