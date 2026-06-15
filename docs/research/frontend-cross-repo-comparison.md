---
version: v1.0
status: Active
date: 2026-06-14
references:
  - docs/decisions/006-wolverine-http-per-service-no-bff.md
  - docs/decisions/009-polecat-deferred-for-round-one.md
  - docs/decisions/015-vite-react-frontend-stack.md
  - docs/decisions/016-frontend-full-pipeline-ui-first-class.md
  - docs/research/ecommerce-frontend-stack.md
  - docs/research/pre-frontend-endpoint-audit.md
sibling-repos:
  - CritterBids (C:\Code\CritterBids) — frontend through M9-S6
  - mmo-reconnect (C:\Code\mmo-reconnect) — frontend at Gate 1 (landing-page app shell)
---

# CritterMart Frontend — Cross-Repo Comparison & Plan Refinement

> Compiled 2026-06-14, before CritterMart's first frontend code lands. This is **research /
> decision-evidence**, not a decision. It compares the frontend choices in two sibling
> reference architectures — **CritterBids** (a modular monolith, frontend mature through
> M9-S6) and **MmoReconnect** (a public-launch splash site, frontend at Gate 1) — against
> CritterMart's *planned* frontend (ADR 015 + ADR 016, plus [Research 002](ecommerce-frontend-stack.md)).
>
> The output is a set of **explicit, recommended refinements** to CritterMart's frontend plan.
> The recommendations here feed ADR amendments/additions authored in a *following* session — this
> doc is the evidence, the ADRs are the decision. Audience: AI session-runners and humans both;
> read it before scaffolding `client/` or amending ADR 015.

---

## Why this exists

CritterMart has **no frontend code yet**. Its plan is ADR 015 (the Vite + React stack), ADR 016
(UI modeled through the full pipeline), and [Research 002](ecommerce-frontend-stack.md), all
authored around 2026-05-26. That plan named the stack *generically* ("Tailwind v4", "TanStack
Query") and deferred every version pin and every sub-library specific. ADR 015 explicitly chose
"Candidate A … the **CritterBids sibling-project precedent**."

Two things have happened since that snapshot:

1. **CritterBids advanced from its first SPA (M8) through three SPAs + a shared package + e2e
   (M9-S6).** It has now *lived* the exact stack CritterMart only named — pinned the versions,
   resolved the deferred questions (routing, real-time integration), and hit real bugs. That lived
   experience is free precedent CritterMart should inherit rather than re-derive.
2. **MmoReconnect appeared as a fresh sibling with a deliberately *different* frontend posture.**
   It is the useful counter-example: it shows where the CritterBids/CritterMart playbook does **not**
   apply, and why. Reading it keeps CritterMart from cargo-culting choices that belong to a
   different problem.

The net effect: CritterMart's plan isn't wrong, it's *under-specified and slightly behind*. This
doc closes that gap.

---

## Maturity snapshot

| Repo | Frontend state | Shape | Where it lives |
|---|---|---|---|
| **CritterBids** | Mature, through **M9-S6** (seller console). | **4-member npm-workspaces monorepo**: `shared` + `bidder` + `ops` + `seller` SPAs + `e2e`. Real-time bidding fully built and battle-tested (latest FE commit: a `react-hook-form` `shouldUnregister` bug fix). | `client/` |
| **mmo-reconnect** | Very early, **Gate 1** (`P1-landing-page-app-shell`) + a Discord OAuth research spike. | Single **2-route prerendered splash site** (Landing + Privacy), email capture, brand iconography. No router, no real-time. | `src/web/` |
| **CritterMart** | **Plan only.** Backend round one complete (17 slices); pre-frontend hardening done (CORS, CI format gate). | Planned: **single CSR SPA** calling three Wolverine.Http services directly (no BFF). | `client/` (forthcoming) |

---

## The shared core (all three agree)

Underneath the divergence, the three repos share an identical foundation. This is the strongest
signal that CritterMart's stack family is the right one — it is independently the choice of both
siblings.

- **React + TypeScript**, built with **Vite**.
- **TanStack Query** as the single owner of server state.
- **Tailwind CSS v4** (via the v4-native `@tailwindcss/vite` plugin + `@import "tailwindcss";`,
  not the v3 PostCSS path).
- **Vitest + React Testing Library** for unit/component tests.

The divergence is entirely in the three axes that encode *intent*: rendering model, real-time
transport, and app topology. Those are covered below.

---

## Pinned versions (both siblings are aggressively current)

Both siblings sit on the mid-2026 bleeding edge. CritterMart's ADRs are version-agnostic, so when
it scaffolds it should adopt these *proven* pins rather than resolve "latest" fresh.

| Dependency | CritterBids | mmo-reconnect | CritterMart (ADR today) | **Recommended pin** |
|---|---|---|---|---|
| `react` / `react-dom` | 19.2 | 19.2 | "React" | **^19.2** |
| `vite` | 8.0 | 8.0 | "Vite" | **^8.0** |
| `@vitejs/plugin-react` | 6.0 | 6.0 | — | **^6.0** |
| `typescript` | 6.0 | ~6.0 | "TypeScript" | **^6.0** (strict) |
| `tailwindcss` + `@tailwindcss/vite` | 4.3 | 4.3 | "Tailwind v4" | **^4.3** |
| `@tanstack/react-query` | 5.101 | 5.101 | "TanStack Query" | **^5.101** |
| `@tanstack/react-router` | 1.170 (code-based) | — | not chosen | **^1.170** (see R2) |
| `zod` | 4.4 | — | implied via shadcn | **^4.4** (see R3) |
| `react-hook-form` (+ `@hookform/resolvers`) | 7.79 / 5.4 | — (raw `useState`) | implied via shadcn | **^7.79 / ^5.4** |
| `class-variance-authority` / `clsx` / `tailwind-merge` | 0.7 / 2.1 / 3.6 | — | implied via shadcn | **0.7 / 2.1 / 3.6** |
| `vitest` | 4.1 | 4.1 | "Vitest" | **^4.1** |
| `@testing-library/react` / `jsdom` | 16.3 / 29.1 | 16.3 / 29.1 | — | **^16.3 / ^29.1** |
| `@playwright/test` | 1.50 (e2e workspace) | — | TBD | **^1.50** (if e2e adopted) |
| `eslint` / `typescript-eslint` | (present) | 10.3 / 8.59 | — | **^10 / ^8.59** |
| Node engine | ≥22 | (24 types) | — | **≥22** |
| `@microsoft/signalr` | 10.0 | — | — | **omit** (no real-time round one) |
| `vite-plugin-pwa` | 1.3 (day one) | — | — | **omit** (no QR/offline beat) |

`@microsoft/signalr`, `vite-plugin-pwa`, `sharp` (OG image gen), and `@fontsource-variable/inter`
are sibling-specific and **out of scope** for CritterMart round two.

---

## The three axes that differ — and the *why* behind each

### Axis 1 — Rendering model

| Repo | Model | Driving constraint (sourced) |
|---|---|---|
| CritterBids | **Pure CSR SPA** | `CritterBids:ADR 012` rejects every meta-framework. It is an *authenticated, conference-demo* app — "SEO is not a driver; attendees scan a QR code and log in." A Node SSR runtime would be a second deployable fighting the modular monolith (`CritterBids:ADR 001`) and a second, weaker backend competing with the Critter Stack. |
| mmo-reconnect | **SSR-prerendered-at-build (custom SSG) → hydrate** | `mmo:ADR 003` + its SEO/splash-resilience research. It is a *public launch page* that must rank, unfurl on Reddit/Discord with zero JS, **and survive a launch-wave "hug-of-death."** So it deploys to **Azure Static Web Apps (CDN edge), and the .NET process never serves it** — splash availability is deliberately decoupled from API compute. SEO *is* a driver here — the inverse of CritterBids. |
| **CritterMart** | **Pure CSR SPA** (like CritterBids) | ADR 015 accepts the hydration cost and the no-SEO trade-off as "a round-two-plus concern," for the same authenticated-storefront reason. The hardcoded-customer-ID identity stub (ADR 009) lives *in the SPA*. |

MmoReconnect's `build` runs `vite build` (client) + `vite build --ssr src/entry-server.tsx` + a
hand-rolled `scripts/prerender.mjs` that renders each route's real HTML+head into the template,
emitting one static file per route. It is **build-time SSG, not a meta-framework** — the same
"backend owns all contracts, frontend ships static" stance CritterBids/CritterMart hold, just with
a prerender pass bolted on for crawlers. **CritterMart does not need this** (see "What not to copy").

### Axis 2 — Real-time transport

| Repo | Transport | Why |
|---|---|---|
| CritterBids | **SignalR**, structured for the single host | Because it is an intentional **modular monolith** (`CritterBids:ADR 001`) deployed as one `CritterBids.Api` host, the hubs are **plain `Hub` subclasses mapped on that host** (`BiddingHub` at `/hub/bidding`, `OperationsHub`), driven by Wolverine handlers in the Relay module that inject `IHubContext<THub>` and push explicitly (`CritterBids:ADR 023`, "path (b)"). They *rejected* the `WolverineFx.SignalR` transport because it pushes to the framework's own `WolverineHub`, not the mapped application hubs clients connect to. The client side (`CritterBids:ADR 026`) is one `SignalRProvider` + a `useListen` hook + a **TanStack Query cache bridge** whose rule is "*push = re-query, never render the payload*." Same-origin; dev via a Vite proxy with `ws: true`, **no CORS** (`CritterBids:ADR 025`). |
| mmo-reconnect | **None — consciously** | Architecture posture is "**no broker, single deployable**." The domain *does* have eventual real-time-ish features (beacon/hail/recognition), but `mmo:Workshop 002` models them as events while leaving "**batch-vs-realtime graduation a later call**" — match notices are "periodic in v1." No SignalR, no SSE, because the current surface (splash + email + soon OAuth) genuinely doesn't need push. |
| **CritterMart** | **None round one** | No real-time storefront updates; one async-projection-as-teaser is backend-only (ADR 008). The "instant" feel of add-to-cart comes from **TanStack Query optimistic updates with rollback**, not a socket (see R4). |

The CritterBids hub design is the textbook case of *deployment topology dictating integration
pattern*: one deployable host → map plain hubs on it + push via `IHubContext` → client treats each
push as a "something changed, re-query" trigger. It is **not** transferable to CritterMart, whose
topology is different (Axis 3) and which has no real-time requirement round one anyway.

### Axis 3 — App topology & how the SPA reaches the backend

This is where CritterMart is **genuinely unique** — neither sibling shares its shape.

| Repo | Topology | CORS posture |
|---|---|---|
| CritterBids | SPA(s) → **one** API host. `client/shared/` is "the frontend analogue of `CritterBids.Contracts`" — one Zod wire-contract surface, many consumers. | Same-origin; **no CORS** (dev Vite proxy). |
| mmo-reconnect | Single SPA → **one** API container, cross-origin at `api.mmoreconnect.com`. | CORS allowlist (apex + www) — the SPA is on a separate CDN host. |
| **CritterMart** | Single SPA → **three** Wolverine.Http services **directly, no BFF** (ADR 006). The SPA *is* the cross-service orchestrator. | **Production CORS unavoidable** (three service origins). Already added in the pre-frontend hardening pass. |

The three-services-no-BFF shape is not incidental — **the OpenTelemetry trace spanning the network
is a hard success criterion** (ADR 015 § Consequences). The real cross-network hop from SPA to three
services *is* the demo beat. Consequences for the frontend:

- CritterMart cannot lean on CritterBids' single-origin proxy trick as cleanly. Either a Vite dev
  proxy fans to **three** targets, or it relies on the CORS already configured. (Open decision — see
  below.)
- CritterMart has **no shared-contract-workspace need**: it is one storefront, one SPA. CritterBids'
  workspace monorepo exists to share a contract surface across four apps; CritterMart has one app.

---

## Recommendations (explicit)

Each recommendation names: the change, the sourced precedent, and the CritterMart artifact it will
refine. The artifact edits are authored in a following session; this doc is the evidence.

### R1 — Pin the siblings' proven versions when scaffolding `client/`

Adopt the **Recommended pin** column above verbatim. Rationale: both siblings independently run this
exact version line in anger; pinning protects CritterMart from a breaking "latest" (Vite 8, TS 6,
ESLint 10, Vitest 4, React 19.2 are all current-major and still churning). Pin with caret ranges and
commit a lockfile.
*Refines:* ADR 015 (add a version-pin note or a companion frontend skill); the forthcoming
`client/package.json`.

### R2 — Adopt TanStack Router (code-based)

CritterBids resolved its deferred routing question → **TanStack Router**, for **shared lineage with
the already-chosen TanStack Query** (one vendor, one mental model) and first-class type-safe routes +
search-params-as-state, over React Router v7's larger training footprint. Wire it **code-based** (no
route-tree codegen plugin) at CritterMart's small route count (browse / detail / cart / track);
migrate to file-based only if routes grow. CritterMart's ADRs never picked a router — this closes a
real gap with a proven precedent.
*Refines:* ADR 015 (routing was unspecified) — add a routing decision or a deferred-question
resolution mirroring `CritterBids:ADR 013`.

### R3 — Validate every wire boundary with Zod

Adopt CritterBids' rule: **every incoming HTTP response body is parsed through a Zod schema before
the app trusts it.** This catches backend↔frontend contract drift the moment it happens instead of
letting it propagate into the cache or UI. It is *more* load-bearing for CritterMart than for
CritterBids because CritterMart's SPA talks to **three independently-deployed services** — three
contract surfaces that can each drift. Zod also pairs natively with shadcn/ui's `Form` +
`react-hook-form`, so the schema is written once and reused for form validation and wire validation.
*Refines:* ADR 015 (name Zod-at-the-boundary explicitly); a forthcoming frontend skill.

### R4 — Make optimistic UI + rollback a first-class, documented pattern

ADR 015 already promises "TanStack Query's `onMutate` / `setQueryData` rollback … makes add-to-cart
feel instant" — but it is one line. Promote it to a documented convention, lifting the *discipline*
(not the SignalR machinery) from `CritterBids:ADR 026`:

- **`onMutate`** snapshots the current cache and applies the optimistic change.
- **rollback `onError`** restores the snapshot on any non-2xx.
- **reconcile `onSettled`** invalidates/refetches the authoritative read model so the optimistic
  guess converges on server truth.
- The **read model is the source of truth**, never the optimistic payload — the same "re-query, don't
  render the guess" stance CritterBids applies to pushes, applied here to mutations.

The CritterMart beats this covers: add-to-cart, update-quantity, remove-from-cart, place-order.
*Refines:* ADR 015 + the customer-purchase narrative (004) + a forthcoming frontend skill.

### R5 — Confirm CritterMart is *not* MmoReconnect (no prerender / no SSG)

Add an explicit one-liner that the no-SEO, pure-CSR stance still holds for an authenticated demo
storefront, so a future contributor who reads MmoReconnect does not cargo-cult its prerender + Static
Web Apps posture. MMO prerenders because crawler-visibility and a hug-of-death-resilient splash are
load-bearing for a public launch; **neither is true for CritterMart**, whose frontend is gated behind
the (stubbed) customer identity and served locally via Aspire for the demo.
*Refines:* ADR 015 (a short "explicitly unlike MmoReconnect" note in Consequences or a comparison
cross-reference to this doc).

---

## What CritterMart should NOT copy

| From | Don't adopt | Why it's sibling-specific |
|---|---|---|
| CritterBids | **npm-workspaces monorepo / `shared/` package** | Exists to share one contract surface across *four* apps. CritterMart is one SPA. A single-app `client/` is correct. |
| CritterBids | **SignalR (`@microsoft/signalr`, hubs, cache bridge)** | No real-time storefront updates round one. Optimistic UI (R4) gives the "instant" feel without a socket. |
| CritterBids | **`vite-plugin-pwa` from day one** | Justified by a QR-code-scan conference beat + weak-WiFi offline shell. CritterMart has no such beat. Revisit only if a demo need appears. |
| mmo-reconnect | **SSR/prerender + Static Web Apps** | Driven by public SEO + launch-wave resilience. CritterMart is authenticated/demo-local and explicitly accepts no-SEO (ADR 015). |
| mmo-reconnect | **Raw `useState` forms, no Zod/RHF** | Fine for a single email field; CritterMart has real forms (checkout, quantity) where shadcn `Form` + RHF + Zod earns its keep. |

---

## Open decisions still owed (not resolved here)

These surfaced during the comparison and need a call before or during the first frontend slice. They
are flagged, not decided.

1. **Dev-server reach with three service origins.** ✅ **Resolved → [ADR 018](../decisions/018-frontend-three-services-cors-posture.md)**
   (2026-06-14): rely on the CORS allowlist in **both dev and prod**, no Vite proxy, so dev mirrors
   the prod cross-origin path the OTel trace beat depends on. The CritterBids proxy trick was rejected
   because its only benefit (avoiding CORS) is moot when three origins + no BFF make production CORS
   unavoidable anyway.
2. **Aspire integration mechanics.** ADR 015 names `AddViteApp`; the exact AppHost wiring (one Vite
   resource, env-injected service URLs for the three backends) is unspecified. Sibling reference:
   CritterBids' "Add Aspire orchestration for bidder and ops SPAs with Vite integration" commit.
3. **shadcn/ui confirmation.** ✅ **Confirmed** via the ADR 015 amendment (2026-06-14): shadcn/ui is
   retained (its `cva`/`clsx`/`tailwind-merge` + `react-hook-form` companions are now in the pinned
   set). CritterBids validates it; MMO deliberately skipped it for brand-forward design, but
   CritterMart is a neutral storefront where shadcn fits.
4. **Playwright adoption.** CritterBids runs it (multi-context bid-war e2e); MMO does not. CritterMart
   has no concurrency story that demands multi-context, so e2e is optional round two — decide
   explicitly.

---

## Decision updates (landed in this PR)

The recommendations were taken into the decision layer in the same PR as this spike, structured as
"amend ADR 015 + one new ADR":

- **[ADR 015](../decisions/015-vite-react-frontend-stack.md) amended** (2026-06-14) — a single
  amendment blockquote captures R1 (version pins), R2 (routing resolved → TanStack Router, code-based),
  R3 (Zod at every wire boundary), R4 (optimistic-UI + rollback as a documented pattern), and R5
  (explicitly unlike MmoReconnect). None reverses the original accepted decision.
- **[ADR 018](../decisions/018-frontend-three-services-cors-posture.md) authored** (2026-06-14) — the
  structurally novel three-origin dev-server + CORS posture (Open #1), resolved as "CORS in both dev
  and prod, no proxy," and retroactively recording the production CORS shipped in the pre-frontend
  hardening pass.
- **A forthcoming `docs/skills/frontend/` skill** remains the home for the *how* of R1, R3, and R4
  once the first frontend slice establishes the conventions in code — deferred, not authored here.

---

## Document history

- **2026-06-14** — v1.0. Authored as a comparison spike across CritterBids (frontend through M9-S6)
  and mmo-reconnect (frontend at Gate 1) to refine CritterMart's pre-code frontend plan. Produced
  five explicit recommendations (R1–R5), a not-to-copy list, and four open decisions. In the same PR
  the recommendations landed in the decision layer: **ADR 015 amended** (R1–R5) and **ADR 018
  authored** (three-services dev-server + CORS posture, resolving open decision #1). No application
  code changed.
