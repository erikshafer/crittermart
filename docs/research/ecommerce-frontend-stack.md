---
version: v1.0
status: Active
date: 2026-05-26
references:
  - docs/vision.md
  - docs/context-map/README.md
  - docs/decisions/001-separate-services-topology.md
  - docs/decisions/003-wolverine-rabbitmq-transport.md
  - docs/decisions/004-dotnet-aspire-orchestrator.md
  - docs/decisions/006-wolverine-http-per-service-no-bff.md
  - docs/decisions/009-polecat-deferred-for-round-one.md
  - docs/workshops/001-crittermart-event-model.md
  - docs/research/ecommerce-engineering-lessons.md
---

# CritterMart — E-Commerce Frontend Stack Survey

> Research compiled on 2026-05-26. Sources are first-party framework venues,
> recognized engineering organizations, individual engineers with verifiable
> track records, and conference talks from established events.

This document is **decision-evidence** for the round-one frontend-stack ADR, not the decision itself. It surveys the current frontend landscape as it applies to single-front e-commerce, captures sourced lessons from reputable engineers and engineering organizations, and presents a comparative analysis of viable candidate stacks against CritterMart's specific constraints. It does not recommend a single stack.

CritterMart's round-one constraint set, in one sentence: a six-day timeline, no real-time storefront updates, no auth (customer ID hardcoded into the frontend per ADR 009), three Wolverine.Http endpoints as the backend surface (per ADR 006), and .NET Aspire orchestrating the topology locally (per ADR 004). The follow-on ADR uses this document as its primary evidence base; the vision doc's line that "the specific frontend stack is TBD for round one" becomes unblocked once it lands.

---

## Table of contents

- [Sources surveyed](#sources-surveyed)
- [The landscape, by axis](#the-landscape-by-axis)
  - [Rendering model spectrum](#rendering-model-spectrum)
  - [Framework families](#framework-families)
  - [Meta-frameworks](#meta-frameworks)
  - [State management for commerce](#state-management-for-commerce)
  - [Styling and component systems](#styling-and-component-systems)
  - [Performance and conversion](#performance-and-conversion)
  - [Forms, payments UX, and accessibility](#forms-payments-ux-and-accessibility)
  - [Real-time (LONG-ROAD ONLY)](#real-time-long-road-only)
  - [Deployment and demo ergonomics](#deployment-and-demo-ergonomics)
  - [.NET integration story](#net-integration-story)
- [Candidate stacks evaluated](#candidate-stacks-evaluated)
- [Decision criteria](#decision-criteria)
- [Cross-cutting themes](#cross-cutting-themes)
- [Open questions for the team](#open-questions-for-the-team)
- [Appendix A: Sources considered and rejected](#appendix-a-sources-considered-and-rejected)
- [Appendix B: Suggested follow-up reading](#appendix-b-suggested-follow-up-reading)
- [Document history](#document-history)

---

## Sources surveyed

Sources cleared the quality bar named in the prompt: framework or library author publishing on the official venue, established engineering-organization blog, recognized individual engineer with sustained body of substantive writing or conference talks, established conference talk with transcript or write-up, or book by a recognized author. Each cited source was verified by checking the author's other work, the venue's publishing history, and the specificity of claims (named systems, concrete benchmarks, real production stories) rather than hand-wavy framework comparison.

| Source | Author / Org | Venue | Year | Credibility note |
| --- | --- | --- | --- | --- |
| Hypermedia-Driven Applications essay | Carson Gross | htmx.org/essays | 2022 | htmx creator; Montana State professor; co-author of *Hypermedia Systems* (book); SE Radio guest. |
| Does Hypermedia Scale? | Carson Gross | htmx.org/essays | 2023 | Same author; scale-dimensions analysis of HDA architecture. |
| When Should You Use Hypermedia? | Carson Gross | htmx.org/essays | 2022 | Same author; explicit fit / not-fit guidance. |
| The future of htmx | Carson Gross & Alex Petros | htmx.org/essays | 2025 | jQuery-style API stability commitment; quarterly releases. |
| SE Radio episode 671: Carson Gross on HTMX | Sriram Panyam, host | Software Engineering Radio | 2025 | Long-form interview with htmx creator; covers fit cases. |
| Turbo handbook | Hotwire team / DHH | turbo.hotwired.dev | ongoing | Hotwire's persistent-process model; navigation-without-SPA pitch. |
| Hydration is Pure Overhead | Misko Hevery | builder.io / dev.to | 2022 | AngularJS co-creator; Builder.io CTO; framing essay for resumability. |
| Qwik talk: Rethinking web application design | Misko Hevery | wearedevelopers.com | 2022 | Conference talk on resumability vs. hydration with live demos. |
| New Qwik JavaScript Framework Seeks Faster Web Apps | InfoQ news | infoq.com | 2022 | Established tech journalism venue; quotes from Hevery's talks. |
| Beyond Signals: The Next Big Shift in Web Reactivity | Ryan Carniato | gitnation.com (JSNation US 2025) | 2025 | Solid creator's keynote on signals proliferating across frameworks. |
| A Decade of SolidJS | Ryan Carniato | dev.to/ryansolid | 2024 | Sustained writing on fine-grained reactivity; Principal Engineer at Sentry. |
| SolidJS Creator on Fine-Grained Reactivity | Loraine Lawson | The New Stack | 2025 | Reporting on Carniato's JSNation talk; cross-reference for the claim. |
| Svelte 5 And The Future Of Frameworks | Rich Harris interview by Smashing Mag | smashingmagazine.com | 2025 | Svelte creator; runes architecture; the framework-as-Rails ambition. |
| Svelte 5 release announcement | Rich Harris / Svelte team | svelte.dev/blog | 2024 | First-party release announcement. |
| Introducing Next.js Commerce 2.0 | Lee Robinson | vercel.com/blog | 2023 | Vercel VP of DevX; App Router + RSC commerce template; cite Amazon-100ms-1% study. |
| Next.js App Router documentation | Next.js team | nextjs.org/docs/app | 2026 | First-party framework venue. |
| How We Built Hydrogen | Shopify engineering | shopify.engineering | 2022 | First-party engineering blog; Hydrogen design history. |
| Hydrogen and Oxygen fundamentals | Shopify | shopify.dev | 2026 | First-party documentation; the React Router v7 foundation. |
| Hydrogen January 2026 release | Shopify | hydrogen.shopify.dev/updates | 2026 | First-party release notes; Storefront API 2026-01. |
| Astro Storefront (withastro/storefront) | Astro team | github.com/withastro/storefront | 2024–2026 | First-party; Astro's own shop runs on it; cites SolidJS for islands. |
| Astro Islands Architecture (docs) | Astro team | docs.astro.build | ongoing | First-party documentation of the islands model. |
| TanStack Start: Release Candidate | TanStack | tanstack.com/start | 2025 | First-party RC docs; client-first full-stack React; built on TanStack Router and Vite. |
| TanStack Start at React Summit US 2025 | Tanner Linsley | gitnation.com | 2025 | TanStack creator's keynote; conference with transcript. |
| TanStack Query optimistic-updates guide | TanStack | tanstack.com/query/v5/docs | ongoing | First-party docs; the canonical pattern for `onMutate` / `setQueryData` rollback. |
| Tailwind CSS v4.0 release announcement | Tailwind team | tailwindcss.com/blog | 2025 | First-party; CSS-first `@theme` model; design tokens as native CSS variables. |
| Top Headless UI libraries for React in 2026 | GreatFrontEnd | greatfrontend.com/blog | 2026 | Cross-references shadcn/ui Base UI support added in 2025. |
| Radix UI Primitives | Radix / WorkOS | workos.com / radix-ui.com | ongoing | First-party docs; the WAI-ARIA primitive layer. |
| ASP.NET Core Blazor render modes | Microsoft Learn | learn.microsoft.com | 2026 | First-party documentation of the four render modes (Static, Interactive Server, Interactive WebAssembly, Interactive Auto). |
| How Blazor's Unified Rendering Model Shapes Modern .NET Web Apps | Allen Conway / Visual Studio Magazine | visualstudiomagazine.com | 2025 | Conference write-up of Conway's Blazor Summit talk; per-component render mode selection. |
| Building a Full-Stack App with React and Aspire | Maddy Montaquila / Microsoft | devblogs.microsoft.com/dotnet | 2025 | First-party walkthrough of `AddViteApp` integration into Aspire. |
| Aspire for JavaScript developers | Aspire team | devblogs.microsoft.com/aspire | 2026 | First-party Aspire 13 JavaScript hosting story; `AddNpmApp`, `AddViteApp`, `AddJavaScriptApp`. |
| Aspire Community Toolkit Node.js hosting extensions | Microsoft Learn | learn.microsoft.com/dotnet/aspire | 2026 | First-party docs on the Vite / Yarn / pnpm integration package. |
| Second-guessing the modern web | Tom MacWright | macwright.com | 2020 | Sustained body of substantive writing; the essay that names the SPA-plus-server-rendering norm and questions its cost-benefit. |
| The business impact of Core Web Vitals | web.dev (Google) | web.dev/case-studies/vitals-business-impact | ongoing | First-party Google case-study collection (Redbus, Vodafone, Renault). |
| Core Web Vitals and eCommerce Conversions | Born Digital | born.mt | 2026 | Cites the Deloitte-for-eBay study on LCP and INP correlated with session-based conversion. |
| Inclusive Components (book) | Heydon Pickering | inclusive-components.design / Smashing | 2019, revised 2021 | Book; chapter-per-component accessibility patterns; canonical reference. |
| The Cost of JavaScript talks (2017, 2019, 2022, 2023) | Addy Osmani | conferences and speakerdeck.com/addyosmani | 2017–2023 | Google Chrome engineering-manager; sustained series; the JS-as-cost framing. |
| Stripe Web Elements documentation | Stripe | docs.stripe.com/payments | ongoing | First-party docs on tokenized payment fields; the SAQ A reduction. |
| Stripe PCI compliance guide | Stripe | stripe.com/guides/pci-compliance | ongoing | First-party explanation of the SAQ A path via Elements / Checkout. |
| ASP.NET Core SignalR JavaScript client | Microsoft Learn | learn.microsoft.com | ongoing | First-party docs on `@microsoft/signalr` client and `withAutomaticReconnect()`. |
| CritterBids frontend-stack research | Erik Shafer | docs/research/frontend-stack-research.md (CritterBids repo) | 2026-04 | Sibling-project precedent; same author; covers Vite vs. Next.js for a Critter Stack showcase with overlapping (but not identical) constraints. |

A larger set of sources was read for triangulation but is not cited because they did not clear the bar; those appear in Appendix A.

---

## The landscape, by axis

Each axis below surveys the current state of the art with sourced commentary. Where opinions diverge, both sides are presented. Where a claim is non-obvious, it is sourced.

### Rendering model spectrum

The landscape now spans roughly six rendering models, and a working understanding of all of them is required to evaluate any candidate stack:

- **Single-page application (SPA).** All rendering and routing happen in the browser; the server is an API. Tom MacWright's 2020 essay "Second-guessing the modern web" named the SPA-plus-server-rendering pattern as the emerging norm and questioned the cost-benefit. Michael Rawlings's follow-up on Marko summarized the trade-off: techniques like bundle-splitting and server-side rendering alleviate the cost but introduce their own caveats.
- **Multi-page application (MPA).** Classic server-rendered pages, one round-trip per navigation. The cost is full page reloads; the benefit is no client-side runtime to ship. Carson Gross's "Hypermedia-Driven Applications" essay reframes the MPA-versus-SPA debate by extending HTML with declarative AJAX so the network-level architecture stays REST-shaped while the UX gets faster.
- **Server-side rendering with hydration (SSR + hydration).** The dominant React pattern from roughly 2018 onward, codified by Next.js Pages Router and Remix. Misko Hevery's 2022 essay "Hydration is Pure Overhead" argues that hydration is, by definition, redoing on the client what the server already did. Addy Osmani's "Cost of JavaScript" lineage names the "uncanny valley" symptom: the HTML is visible and the app appears ready, but interactions do nothing until the JS has loaded and hydrated.
- **Static site generation (SSG) and incremental static regeneration (ISR).** Build-time rendering with optional periodic re-rendering. Strongest for content-heavy pages (catalog browse) where the data changes slowly relative to traffic. The Astro team's `withastro/storefront` repo notes the pattern of on-demand rendering with CDN caching to deliver pages at HTML speed.
- **Islands architecture.** Pioneered by Marko, popularized by Astro, also supported by Fresh and 11ty. Static HTML by default; specific components ("islands") opt in to client-side interactivity. Astro's storefront repo combines islands with SolidJS components specifically because SolidJS has the smallest runtime cost of the mainstream interactive frameworks.
- **Resumability.** Qwik's signature model. Hevery's framing in the Qwik talks: instead of serializing the rendered HTML and then re-executing the framework client-side to attach listeners, Qwik serializes listener and state information directly into the HTML, and the client picks up ("resumes") without re-executing. The pitch is constant-time startup independent of application size.

The SPA-versus-MPA philosophical debate is not settled. DHH's Hotwire pitch — sending HTML over the wire rather than JSON — and Carson Gross's HDA essays both argue that the SPA-plus-API split is a complexity tax that most applications cannot justify. The Vercel / React Server Components camp argues the opposite: that server-first React with selective client interactivity is the next-generation default. For a single-seller storefront with a six-day timeline and no real-time updates, either framing can be made to work; the choice is partly aesthetic.

### Framework families

The major framework families as of mid-2026:

- **React.** Still the dominant frontend framework by usage; the Vercel / Meta core team has steered the ecosystem toward Server Components and Server Functions. React Server Components ship code execution to the server and exclude that code from the client bundle; the trade-off is a server runtime and a `'use client'` / `'use server'` mental model that Lee Robinson's writing acknowledges has taken time for the community to absorb.
- **Vue and Nuxt.** Mature, slightly smaller ecosystem than React; Nuxt is the meta-framework. Not deeply researched here because the .NET-shop audience is more likely to encounter React in employer codebases.
- **Svelte 5.** Rich Harris's "ground-up rewrite" landed in October 2024 with the `$state` / `$derived` / `$effect` rune model, which makes reactivity explicit rather than compile-time-magical. In a January 2025 Smashing Magazine interview Harris framed the long-term Svelte ambition as wanting a Rails or Laravel equivalent for JavaScript. Scalable Path's review notes the gap that still exists in plugin ecosystem and integration depth compared with Next.js or Nuxt.
- **Solid.** Ryan Carniato's framework; the original fine-grained-reactivity-via-signals JS framework. At JSNation US 2025 Carniato observed that Svelte and Vue had both joined Solid in the fine-grained rendering camp, validating the model that Solid pioneered. Components only execute once on mount in Solid; subsequent updates target specific DOM nodes via reactive bindings, not virtual-DOM diffs.
- **Angular.** Mature but increasingly orthogonal to the e-commerce frontend conversation; the community has gravitated toward React, Vue, and Svelte for storefront work.
- **Blazor.** Microsoft's C#-in-the-browser story. As of .NET 8, the unified Blazor Web App template supports four render modes (Static Server, Interactive Server via SignalR, Interactive WebAssembly, Interactive Auto), selectable per component via the `@rendermode` directive. The Visual Studio Magazine write-up of Allen Conway's Blazor Summit talk emphasizes that per-component render-mode selection lets developers mix strategies in one app. Static Server suits SEO pages; Interactive Server suits dashboards needing centralized state; Interactive WebAssembly suits offline-first or client-heavy scenarios; Interactive Auto starts on Server while the WebAssembly bundle downloads in the background, then switches.
- **HTMX.** Carson Gross's hypermedia-driven library. The "When Should You Use Hypermedia?" essay names the fit cases: form-driven applications, CRUD UIs, nested UIs with well-defined update areas. The misfit cases: high-frequency client interactions (Gross's Google Maps example) where network latency on every event would dominate UX. The library's "future" essay commits to jQuery-style API stability with minimal feature growth — a deliberate counter-position to the JS framework churn cycle.
- **Hotwire (Turbo + Stimulus + Strada).** DHH's framework, packaged into Rails by default and available as standalone JS. Turbo's handbook describes the persistent-process model: the same speed feel as an SPA without the client-side router or carefully managed state. The .NET-ecosystem analogue is less mature; Turbo is usable from any backend but the documentation and community lean Rails.

### Meta-frameworks

The meta-framework layer has consolidated significantly between 2022 and 2026:

- **Next.js (App Router).** Vercel's flagship; the most widely-used React meta-framework. Lee Robinson's "Introducing Next.js Commerce 2.0" post lays out the App Router commerce pattern: React Server Components for data fetching above the fold, Suspense for streaming, Server Actions for mutations. The post cites the Amazon study of 100-millisecond delays costing 1% of sales as the conversion-impact framing.
- **React Router v7 (formerly Remix).** Remix renamed to React Router v7 in late 2024; Shopify's Hydrogen migrated off Remix v2 to React Router v7 in 2025 (per the Hydrogen with React Router documentation in Sentry's integration guide). The Weaverse 2026 write-up frames the architectural pitch: loaders, actions, and nested routes as a single mental model without the `'use client'` / `'use server'` boundary.
- **Nuxt** (Vue) and **SvelteKit** (Svelte). Each is the canonical meta-framework for its base framework. SvelteKit has improved between Svelte 4 and Svelte 5, but Scalable Path's 2025 review notes that the third-party plugin ecosystem is still narrower than Next.js or Nuxt.
- **SolidStart.** Solid's meta-framework. Smaller community than the others; production deployments exist but the ecosystem is younger.
- **TanStack Start.** Tanner Linsley's full-stack React framework, hit Release Candidate in late 2025 and 1.0 in early 2026 per Alex Cloudstar's review and InfoQ's RC announcement. Built on TanStack Router (compile-time type-safe routing) and Vite. The framework's explicit pitch is "client-first": no React Server Components, no `'use client'` directive split, full-document SSR plus streaming plus server functions as additions over a client-side foundation rather than a different programming model. Supports React and Solid.
- **Astro.** Multi-framework islands meta-framework. Astro's own shop is served by the `withastro/storefront` repository, which uses Astro's on-demand rendering with CDN caching, `astro:actions` for type-safe endpoints, `astro:assets` for image optimization, and SolidJS islands for interactive surfaces.
- **Qwik City.** Qwik's meta-framework; integrates the resumability model with routing and data loading.
- **Blazor United.** Microsoft's term for the unified Blazor Web App template that supports the four render modes per component (Microsoft Learn render-modes documentation).

The fitness of each meta-framework for a CritterMart-style architecture depends on what the meta-framework's server tier wants to do. Frameworks designed around server-side data fetching (Next.js App Router, Remix / React Router) introduce a question: where do they fit in a topology where the data tier is already three Wolverine.Http services? They become a fourth tier — effectively a BFF — which ADR 006 explicitly defers out of round one.

### State management for commerce

Modern React commerce divides state into three categories, each handled differently:

- **Server state.** Cached, asynchronous, owned by a remote system; the TanStack Query (formerly React Query) model. The official optimistic-updates documentation describes the `onMutate` / `setQueryData` rollback pattern: cancel in-flight queries for the same key, snapshot the previous value, write the optimistic value, and on error restore from the snapshot. TanStack Query's docs call optimistic updates the canonical way to make a cart's add-to-item feel instantaneous while the network request is in flight.
- **Client (UI) state.** Form drafts, modal open/closed, navigation menus. React's built-in `useState` / `useReducer` cover the simple cases; Zustand is the popular lightweight library for the cross-tree cases.
- **URL state.** Filter, search, and route parameters. TanStack Router's headline pitch (and TanStack Start's, by extension) is compile-time type-safe URL state, eliminating the "is this parameter a string or a number?" runtime question.

For e-commerce specifically, the cart is the canonical hard case: the cart lives partly in local storage (anonymous browsing), partly in session storage (after sign-in for guest-checkout flows), and partly on the server (after an authenticated user persists it). The TanStack Query optimistic-update pattern handles the wire-time gap between "user clicked Add to Cart" and "server confirmed" without requiring a separate client state library; the cart is a server-state query, mutation invalidates it, and optimistic updates make the UI feel instant.

In Astro's storefront, cart state is held server-side via session and read by islands through `astro:actions` calls; the local-storage approach is described as the alternative in the Astro storefront documentation but is not what the official storefront uses.

### Styling and component systems

The styling story has consolidated more than the framework story:

- **Tailwind CSS v4.** The January 2025 release moved Tailwind from a JavaScript-config-file model (`tailwind.config.js`) to a CSS-first model: design tokens are declared inside `@theme` blocks in CSS, and Tailwind exposes them as native CSS variables that are usable anywhere in the application. The Oxide engine (Rust) is roughly an order of magnitude faster than the v3 PostCSS pipeline.
- **Vanilla CSS, CSS Modules, CSS-in-JS.** Vanilla CSS is enjoying a renaissance because modern CSS (container queries, `@layer`, nesting, custom properties) now does much of what CSS-in-JS was invented to do. The CSS-in-JS approaches (Emotion, styled-components) are losing momentum; React Server Components do not play well with runtime CSS-in-JS, accelerating the shift.
- **Component libraries — Radix UI, Headless UI, Base UI, React Aria.** Headless / primitive libraries provide accessibility behavior (focus management, ARIA attributes, keyboard interaction) without styling. The GreatFrontEnd 2026 survey notes Radix UI reached around 9 million weekly downloads in late 2024; Base UI (maintained by MUI) became the actively-maintained alternative when shadcn/ui added Base UI support as an alternative primitive layer in 2025.
- **shadcn/ui.** Not a library in the npm sense; the CLI copies component source code (built on Radix or Base UI plus Tailwind) directly into the project. The project owns the code thereafter. The DEV community 2026 review puts shadcn/ui in a category distinct from both "monolithic" libraries (Material UI, Ant Design) and pure headless libraries (Radix, Base UI).
- **Mantine, Chakra UI, MUI.** Batteries-included libraries; faster to ship a generic UI, harder to make distinctively branded. Each has trade-offs around bundle size and theming flexibility documented in Makers' Den's 2025 comparison.

For a six-day timeline, the Tailwind-plus-shadcn/ui combination is the highest-velocity choice for React because the CLI hands the project ready-built accessible components that look polished out of the box.

### Performance and conversion

The case for treating performance as a conversion lever rather than a vanity metric is well-documented:

- **Core Web Vitals to revenue.** Born Digital's 2026 summary cites the Deloitte-for-eBay study finding that every 100 milliseconds of LCP delay correlated with a roughly 1.1% drop in session-based conversion. Google's web.dev case-studies page documents specific companies' results: Vodafone's 31% improvement in LCP correlated with 8% more sales; Redbus saw 7% page-load improvement turn into 100% mobile conversion-rate gains on certain pages.
- **The Amazon 100-millisecond / 1% data point.** Cited by Lee Robinson in the Next.js Commerce 2.0 announcement; the figure circulates widely in the Vercel and Shopify orbits and frames why performance investments earn out.
- **Cost of JavaScript.** Addy Osmani's 2017–2023 talk series documents that download and CPU-execution time, not network bandwidth alone, are the dominant JS performance costs. The "uncanny valley" — page is visually loaded, but JS has not hydrated, so interactions silently fail — is the worst conversion-killing failure mode because it is invisible to monitoring that only watches request latency.
- **The hydration debate.** Misko Hevery's "Hydration is Pure Overhead" essay argues that any architecture which serializes server-rendered HTML and then re-executes the framework client-side is inherently wasteful. The Qwik camp's pitch is resumability; the Astro / Marko camp's pitch is islands (no hydration except where required); the React / Next.js camp's pitch is React Server Components (less code shipped, but the remaining client code still hydrates). Each addresses the diagnosed problem differently.

For a single-seller storefront with a six-day timeline, the conversion-lever framing is most relevant for the talk's narrative — the slide that explains why the talk's audience should care about whatever the chosen frontend stack does to JS bundle size — rather than as a discriminator between candidates. A round-one CritterMart with any of the candidate stacks below will be fast enough to ship; the conversion data matters for the "what would round two look like" conversation, where a frontend choice that handicaps Core Web Vitals would be a hard problem to undo.

### Forms, payments UX, and accessibility

Three sub-axes are worth surfacing even though round one stubs payment per the vision doc:

**Forms.** The two-library React landscape:

- **React Hook Form** is mature, widely adopted, uncontrolled-input by design, and small in bundle. It is the most common choice in the React ecosystem; LogRocket's 2025 comparison notes its larger community and slightly smaller bundle.
- **TanStack Form** is the newer alternative from Tanner Linsley's team. The Formisch 2026 comparison and Croct's 2026 review both highlight first-class memoization, framework-agnostic core (works across React, Vue, Angular, Solid, Lit, Svelte), per-validator triggers (`onChange`, `onBlur`, `onSubmit`), and built-in async validation with debouncing.
- **Formik** is the legacy choice; Croct's review notes it is now in maintenance mode without major new features.

Schema-validation libraries (Zod, Valibot, Yup) plug into all of the above. Zod has the deepest community; Valibot is lighter; either pairs well with React Hook Form or TanStack Form.

**Payments UX (long-road, since round-one CritterMart stubs payments).** Stripe's documentation makes a strong case for Stripe Elements or Stripe Checkout over custom payment UIs: both flow sensitive card data directly to PCI-validated Stripe servers via hosted iframes, and both qualify the integrating site for SAQ A (the simplest PCI compliance form). The pattern matters for CritterMart's round-two enhancements: even though the talk's payment beat is stubbed, the "Long Road" line about real payments has a clear best-practice answer when the time comes.

**Accessibility.** Heydon Pickering's *Inclusive Components* (Smashing Magazine, 2019, revised 2021) is the canonical reference; chapter-per-component coverage of accordions, modals, tables, notifications, tabs, toggles, with code patterns that are framework-agnostic. The accessibility implications of framework choice are not subtle: a Tailwind-plus-shadcn/ui project gets Radix's WAI-ARIA-compliant primitives for free; a vanilla-Svelte or vanilla-HTML project requires the developer to wire accessibility patterns by hand, with Heydon's book as the reference. For a six-day timeline, "accessibility behaviors come from a primitive library" is materially less risky than "developer remembers to add ARIA attributes everywhere."

### Real-time (LONG-ROAD ONLY)

Per the vision doc's "What this deliberately is not" list, real-time storefront updates are out of round-one scope. This axis is included for long-road planning only and should not appear in the round-one candidate scorecard.

The .NET ecosystem real-time options:

- **SignalR.** Microsoft's first-party library; the canonical real-time channel for ASP.NET Core. The official `@microsoft/signalr` JavaScript client has documented integrations with React (Round The Code's tutorial), Angular, and vanilla JS. Microsoft Learn documents the `withAutomaticReconnect()` client method that handles disconnect/reconnect logic. Long-road-relevant because CritterBids (sibling project) chose SignalR for its real-time auction bidding surface.
- **Server-Sent Events (SSE).** Lighter-weight than WebSockets; works over standard HTTP/2; one-way (server → client) only. Adequate for "stock just went out" or "your order shipped" notifications.
- **WebSockets directly.** ASP.NET Core supports WebSockets natively; SignalR layers reconnection, fallback transports, and message dispatch on top.
- **Third-party services (Pusher, Ably).** Lift the real-time concern out of the application entirely; cost money; introduce a dependency.

If round-two CritterMart introduces a real-time concern — most plausibly stock-level changes visible across browsing sessions, or cart-abandonment recovery prompts — SignalR is the obvious .NET default, and the candidate frontend stack's SignalR client-integration story becomes a discriminator. None of the candidate stacks below blocks SignalR; React, Solid, Svelte, Blazor, and HTMX all have working SignalR integration patterns.

### Deployment and demo ergonomics

The round-one demo story has a single hard requirement: one command boots the entire topology including the frontend, locally, with the Aspire dashboard surfacing the OpenTelemetry traces (per ADR 005). The viable deployment shapes:

- **Static SPA served by ASP.NET Core or by Aspire-managed reverse proxy.** Vite SPA builds to static `dist/` files; in development Aspire runs `npm run dev` on the Vite dev server alongside the .NET services. The `AddViteApp` helper from the Aspire Community Toolkit registers the Vite app as a managed Aspire resource so it appears in the dashboard.
- **Frontend as a separate Node.js process (Next.js, Nuxt, SvelteKit).** Aspire's `Aspire.Hosting.JavaScript` package (formerly `Aspire.Hosting.NodeJs`, renamed in Aspire 13) supports `AddJavaScriptApp` for any package.json scripts and `AddViteApp` for Vite-specific projects. The Aspire blog notes the rename and the three entry points: `AddJavaScriptApp` for any npm-scripted project, `AddViteApp` for Vite-specific scenarios, and the container-based deployment for production.
- **Frontend hosted inside ASP.NET Core (Blazor, MVC + HTMX).** No separate Node process; the entire frontend ships with the .NET runtime. Demo ergonomics are simplest here: one process, one port, one debugger session.
- **Edge deployment (Vercel, Cloudflare Pages, Netlify, Shopify Oxygen).** Out of round-one scope; round-one runs locally under Aspire. Worth noting because the long-road question of "where does this run in production?" intersects with framework choice.

For the round-one talk specifically, the simplest demo story is the one where the Aspire dashboard shows every resource (services, broker, database, frontend) in one place and one click starts everything. Both `AddViteApp` (separate Node frontend) and Blazor-hosted-in-ASP.NET-Core (no separate Node frontend) achieve this; the difference is whether there is one process or two in the dashboard graph.

### .NET integration story

The .NET frontend integration story spans four patterns:

- **ASP.NET Core Razor Pages or MVC with HTMX or Hotwire.** The classic "server-rendered with progressive interactivity" pattern. HTMX adds AJAX-via-HTML-attributes; no client-side framework runtime, no build step required. Carson Gross's "Hypermedia on Whatever You'd Like" essay names the resulting freedom: the backend technology is the developer's choice, the frontend is just HTML.
- **ASP.NET Core Web API plus a separate Node-hosted SPA (Vite, Next.js, SvelteKit, etc.).** The pattern documented by Maddy Montaquila in the .NET Blog "Building a Full-Stack App with React and Aspire" post. The frontend project lives in a sibling directory, the Aspire AppHost wires it in via `AddViteApp` or `AddNpmApp`, and the frontend talks to the API over HTTP.
- **Blazor (Server, WebAssembly, or United / mixed).** Microsoft's first-party in-browser-C# story. With .NET 8's render-mode unification, a single Blazor Web App project can mix Static Server (SEO-friendly catalog pages), Interactive Server (real-time-via-SignalR dashboards), Interactive WebAssembly (offline-capable client logic), and Interactive Auto (start on server, switch to WebAssembly after bundle download) — all via `@rendermode` directives. The Conway / Visual Studio Magazine write-up names this per-component flexibility as the .NET 8+ pitch.
- **Blazor Server only.** A single-render-mode subset of the above. The connection is SignalR; UI updates flow over the persistent connection. Simplest mental model of any Blazor variant; the trade-off is that every interaction round-trips to the server.

The "BFF in .NET" pattern is documented separately (Damien Bowden's writing, the BFF library family) but is out of scope for round one per ADR 006.

---

## Candidate stacks evaluated

Seven candidate stacks are evaluated below. The order is the alphabetical labeling from the prompt; it is not a ranking. Each subsection follows the same structure so the comparison can be read across, not just within.

### A. Vite + React + TypeScript + TanStack Query + Tailwind + shadcn/ui

**What it is, in one sentence.** A client-side-rendered React SPA built with Vite as the dev server and bundler, TanStack Query for server-state caching and optimistic mutations, Tailwind v4 for styling, and shadcn/ui for copy-paste accessible Radix-or-Base-UI-backed components.

**Strengths for CritterMart:**

- **Battle-tested precedent in the sibling project.** CritterBids' frontend-stack research (April 2026) selected this same combination after explicit deliberation, naming Vite as the de facto SPA scaffolding choice since Create React App's February 2025 deprecation. Reusing a known-good combination materially reduces the six-day-timeline risk.
- **First-class Aspire integration.** The Aspire Community Toolkit's `AddViteApp` helper, documented in Microsoft's Learn pages and the .NET Blog's React-plus-Aspire walkthrough, registers the Vite dev server as a managed Aspire resource that appears in the dashboard alongside the .NET services.
- **The .NET-shop audience recognizes the pieces.** React is the dominant frontend framework in the .NET-shop labor market; Tailwind has crossed into the mainstream; TanStack Query is widely-used enough that referencing it in the talk should not require a definition slide.
- **Optimistic-update story for the cart.** The TanStack Query optimistic-updates documentation describes the exact `onMutate` / `cancelQueries` / `setQueryData` / `onError`-rollback pattern that makes add-to-cart feel instant. This is the round-one slice that benefits most from snappy UI feedback.

**Weaknesses for CritterMart:**

- **JS bundle ships to the client.** Per Addy Osmani's "Cost of JavaScript" lineage, a React SPA pays the full hydration cost on first load. For a small site this is acceptable; for a content-heavy storefront it leaves Core Web Vitals on the table. The Astro / Qwik / Blazor-Static-Server camps would all do better here, in theory.
- **SEO requires extra work.** Catalog browse pages are the highest-SEO-value content in any storefront. A pure SPA serves an empty `index.html` to crawlers without additional configuration (Vite SSR, or pre-rendering). Round one does not require SEO but round two might.
- **It is a known-good choice, not a teachable one.** The talk's pedagogical weight is on event sourcing and the Critter Stack, not on the frontend; a choice that the audience already understands does not add a teaching beat. This is a feature, not a bug, given round-one priorities.

**Demo ergonomics.** `AddViteApp("frontend", "./frontend")` in the AppHost; Aspire starts the Vite dev server alongside the three Wolverine.Http services; the dashboard surfaces all four resources plus RabbitMQ and PostgreSQL.

**Six-day fit.** Highest of any candidate. The sibling-project precedent eliminates the "what library do we use for X" debates that would otherwise consume design budget.

**.NET-shop audience fit.** Strong. Familiar territory; no novelty risk.

**Long-road preservation.** Strong. Adding SignalR via `@microsoft/signalr` (the route CritterBids has already taken) is straightforward; promoting to a BFF later involves adding a new Wolverine.Http project, not rewriting the frontend; SSR or pre-rendering can be added incrementally via Vite plugins or migration to TanStack Start without changing the React component code.

### B. Next.js (App Router) + React + Tailwind + shadcn/ui

**What it is, in one sentence.** A full-stack React meta-framework that ships pages as React Server Components by default, with Suspense streaming and Server Actions for mutations, hosted as a Node.js process alongside the .NET services.

**Strengths for CritterMart:**

- **React Server Components reduce client-shipped JavaScript.** Lee Robinson's Next.js Commerce 2.0 post frames the benefit: as much work as possible runs on the server, including data fetching, and the client receives a smaller bundle. Streaming Suspense lets above-the-fold content land before the rest of the page is ready.
- **Mature commerce reference template.** The `vercel/commerce` repository is one of the most-studied commerce reference codebases in the React ecosystem; the BigCommerce and Shopify forks demonstrate that the App Router patterns can be adapted to different backends.
- **Built-in optimizations.** Next.js Image, font optimization, automatic code splitting, and per-route caching come for free; the alternative is wiring them by hand.

**Weaknesses for CritterMart:**

- **The Next.js server tier becomes a fourth service.** ADR 006 defers a BFF for round one; introducing Next.js's server tier effectively creates one, with Server Actions and Route Handlers acting as a pass-through to the three Wolverine.Http services. The CritterBids precedent rejected this on principle for an architecturally similar reason: "the backend is the Critter Stack."
- **`'use client'` / `'use server'` boundary.** Lee Robinson and the React Router v7 / Hydrogen camp (Weaverse's 2026 React-Router-vs-Next analysis) both acknowledge that the directive split adds a mental-model tax. For a six-day timeline this is a real friction point.
- **Vercel-lensed defaults.** Next.js is a Vercel product; many of its first-class deployment ergonomics assume Vercel hosting. Aspire-orchestrated local development works, but production hosting is a separate conversation.
- **Slower dev server than Vite.** Less of a difference in 2026 than it was in 2023, but Vite's hot-module-reload is still the snappiest.

**Demo ergonomics.** `AddNpmApp("frontend", "./frontend", "dev")` in the AppHost; the Next.js dev server appears in the Aspire dashboard. Slightly more configuration than `AddViteApp` because Next.js wants control of its own port and middleware.

**Six-day fit.** Medium. The framework is mature, but the architectural debate ("are we adding a BFF or not?") needs to be settled before code is written. Six days is not generous for that debate plus implementation.

**.NET-shop audience fit.** Medium. Next.js is well-known in the .NET-shop labor market; React Server Components and Server Actions are less well-known and would need a slide of context in the talk.

**Long-road preservation.** Strong. Next.js routes can be promoted to a real BFF (calling all three services and composing) without rewriting the components; SSR is already on; real-time can be added via SignalR with a small client-component island.

### C. Blazor United (Blazor in .NET 8+, mixed render modes)

**What it is, in one sentence.** A single ASP.NET Core project where every component declares its render mode (`@rendermode StaticServer`, `InteractiveServer`, `InteractiveWebAssembly`, or `InteractiveAuto`), with C# and Razor as the component language and no separate Node-hosted frontend.

**Strengths for CritterMart:**

- **The audience codes in C#.** Allen Conway's Blazor Summit talk (Visual Studio Magazine write-up) emphasizes that per-component render-mode selection lets a single team work across the rendering spectrum without switching languages. For a .NET-shop talk audience this is the choice that requires zero context-shift.
- **Per-component render-mode flexibility.** Catalog pages can be Static Server (SEO-friendly, no client JS); product detail pages can be Interactive Auto (server-first, WebAssembly-second); the cart can be Interactive Server with SignalR for any future cross-tab synchronization.
- **No separate frontend project.** Demo ergonomics are simplest: one process, one debugger, one OpenTelemetry trace from click-to-handler that does not cross a Node-to-.NET process boundary.
- **First-party Microsoft support.** Render modes, hosting integration, and the AppHost / Aspire story are all on the same supported release cadence as the rest of .NET 10.

**Weaknesses for CritterMart:**

- **Talks-to-Wolverine.Http question.** Blazor server components could in principle call the Wolverine.Http endpoints across the network, but the more natural pattern in Blazor is to inject services and call directly. The talk benefits from a clear narrative: the frontend calls three HTTP services across a real network boundary so the OpenTelemetry trace spans the call. Blazor Server's natural injection pattern would shortcut that, weakening the trace-demo beat.
- **Mental-model novelty for non-Blazor .NET shops.** Blazor is widely-known but not universally-used; many .NET shops still default to JavaScript SPAs. The talk's audience may be more interested in "how do I use the Critter Stack with the frontend I already have" than "how do I use Blazor."
- **The four render modes are a teaching topic in their own right.** Per Conway's talk, the rendering-mode trade-offs are non-trivial; introducing them into a 50-minute event-sourcing talk eats the budget for the actual subject matter.

**Demo ergonomics.** Single project, hosted in-process; trivial Aspire integration. Best of any candidate on raw setup time.

**Six-day fit.** Medium-to-strong if the team has prior Blazor experience; medium if not. The render-mode learning curve adds risk if Blazor is new.

**.NET-shop audience fit.** Strong if the audience leans Blazor-curious; medium otherwise. The talk could land "look, the entire app is C#" as a positive beat for the right audience and a "you locked your frontend to Microsoft" beat for the wrong one.

**Long-road preservation.** Strong. SignalR is Blazor Server's native transport; promoting from stubbed to real Identity (with Polecat) is a smaller jump than from JavaScript-SPA to Polecat; admin UI surfaces are simple to add.

### D. Blazor Server (interactive, single render mode)

**What it is, in one sentence.** Blazor with `@rendermode InteractiveServer` only; UI updates flow over a SignalR connection between server and browser; no WebAssembly download, no static SSR rendering, no per-component mode mixing.

**Strengths for CritterMart:**

- **Simplest Blazor mental model.** No render-mode decisions to make; every component is interactive-server. Reduces the surface area of "what does this code do" questions during the talk.
- **Talks-to-SignalR-already.** The persistent connection is already there for the UI; adding any future real-time storefront update is a matter of calling `IHubContext` from a Wolverine handler.
- **Zero JS bundle.** No WebAssembly to download; no React bundle to ship; UI changes are diffs over the wire.

**Weaknesses for CritterMart:**

- **Latency-coupled UX.** Every interaction round-trips to the server; the cart's add-to-item, the search box's autocomplete, the form's validation all wait on the SignalR round-trip. For a local-demo this is invisible; for a remote production user it is not.
- **Scalability profile.** Each connected user holds a server-side circuit; horizontal scaling requires sticky sessions or distributed circuit state. Not a round-one concern but a long-road one.
- **Same trace-demo dilution as Blazor United** (above), and arguably more so since the framework's natural pattern is server-side service injection.

**Demo ergonomics.** Same as Blazor United, minus the render-mode selection.

**Six-day fit.** Strong for teams with prior Blazor experience; the simplest Blazor variant to author.

**.NET-shop audience fit.** Same as Blazor United.

**Long-road preservation.** Medium. The single-render-mode commitment is harder to back out of than a per-component-mode mix; if round two needs offline-capable client logic, the project would migrate to Blazor United at that point.

### E. Astro Islands + a small interactive island framework (React, Solid, or Svelte) for cart/checkout

**What it is, in one sentence.** Astro pages serve static HTML by default, with selected components ("islands") opting into client-side interactivity using a small framework — Solid is Astro's own first-party choice in the `withastro/storefront` repository.

**Strengths for CritterMart:**

- **Best-in-class Core Web Vitals story.** The Astro storefront's documentation positions islands as the architecture for ultra-fast initial loads with selective interactivity; per the Born Digital / Deloitte-for-eBay data, this maps directly to higher conversion at scale.
- **First-party commerce precedent.** The `withastro/storefront` repository is Astro's own shop, using on-demand rendering with CDN caching, `astro:actions` for type-safe endpoints, and SolidJS islands explicitly for the smallest possible runtime cost.
- **Multi-framework flexibility.** Astro components can wrap React, Vue, Svelte, or Solid components on a per-island basis; the team can mix as needed.

**Weaknesses for CritterMart:**

- **Two frameworks to reason about.** Astro for the page and SolidJS (or React, or Svelte) for the island. The mental-model cost is real, particularly under a six-day timeline; the talk would need to acknowledge the split if asked about it.
- **Novelty for the .NET-shop audience.** Astro is much less common in .NET shops than React or Blazor; introducing it adds a "what is this?" beat that pulls focus from the event-sourcing material.
- **Tooling-integration unknowns.** Aspire integration is generic-Node (via `AddJavaScriptApp` or `AddViteApp`) but Astro-specific patterns within an Aspire-orchestrated topology are less documented than React+Vite or Next.js+Aspire.
- **Round-one cart is the only interactive surface.** Islands shine when most of the page is static and a few elements are interactive; CritterMart's round-one storefront has a small interactive surface (cart, search, checkout), which fits, but the architectural advantage is modest at this scale.

**Demo ergonomics.** `AddJavaScriptApp("frontend", "./frontend", "dev")` or `AddViteApp` works; the Astro dev server appears in the Aspire dashboard. Slightly more setup than the React+Vite path because Astro defaults differ.

**Six-day fit.** Medium. The framework itself is approachable, but the two-frameworks-in-one-project model adds learning friction if the team has not used Astro before.

**.NET-shop audience fit.** Weak-to-medium. The talk would need to spend talk-budget explaining Astro that could otherwise be spent on event sourcing.

**Long-road preservation.** Strong. Adding SignalR to a single island (the cart) is straightforward; the static pages remain static.

### F. SvelteKit

**What it is, in one sentence.** A Svelte 5 application with SvelteKit as the meta-framework providing routing, server-side load functions, form actions, and hybrid SSR/SSG rendering.

**Strengths for CritterMart:**

- **Smallest mental-model footprint of any meta-framework.** Rich Harris's Smashing Magazine interview frames Svelte 5 as wanting to be approachable enough that productivity does not require a degree in the framework. The runes (`$state`, `$derived`, `$effect`) are explicit and small in surface area.
- **Smallest output bundle.** Svelte compiles components to imperative DOM updates rather than shipping a virtual-DOM runtime; the production bundle is among the smallest of any JS framework choice.
- **Strong server-side ergonomics.** SvelteKit load functions and form actions cleanly map to the Wolverine.Http endpoints; the framework's server-side story is well-documented and stable.

**Weaknesses for CritterMart:**

- **Smaller ecosystem than React.** Scalable Path's 2025 review names the gap: fewer integrations with CMSs, auth providers, edge-caching providers; smaller component-library ecosystem (shadcn-svelte exists but lags shadcn/ui).
- **Novelty for the .NET-shop audience.** Even more so than Astro; Svelte is less common in .NET-shop teams than React, Vue, or Blazor.
- **LLM-assist quality is lower.** This matters more than it used to: AI-pair-programmed code quality correlates with the training-data corpus size, and Svelte's corpus is smaller than React's. CritterBids' frontend research called this out explicitly as a reason to default to mature, ubiquitous technologies.

**Demo ergonomics.** `AddViteApp` (SvelteKit uses Vite) or `AddJavaScriptApp`; appears in the Aspire dashboard.

**Six-day fit.** Medium. The framework is productive but the ecosystem gaps mean more bespoke work for things that come built-in elsewhere.

**.NET-shop audience fit.** Weak. Same talk-budget concern as Astro, with less commercial-Critter-Stack-shop overlap.

**Long-road preservation.** Strong on the framework side, weaker on the ecosystem side; the gaps mostly close with time.

### G. HTMX + ASP.NET Core Razor Pages / MVC

**What it is, in one sentence.** Server-rendered HTML from ASP.NET Core, with HTMX attributes (`hx-get`, `hx-post`, `hx-swap`) extending HTML to handle AJAX, partial updates, and server-sent events without a client-side JavaScript framework.

**Strengths for CritterMart:**

- **Smallest possible client runtime.** Carson Gross's "Hypermedia-Driven Applications" essay makes the architectural argument: HTML is the data format, the browser is the hypermedia client, and scripting is augmentation rather than the primary mechanism. The client-side JavaScript footprint is roughly the size of the htmx library itself (single-digit kilobytes).
- **No build step required.** HTMX is a single `<script>` tag; the project does not need a Node.js process at all. Aspire only orchestrates the .NET services, RabbitMQ, and PostgreSQL.
- **Idiomatic for the talks-to-three-services pattern.** Each Wolverine.Http endpoint returns either JSON (for API consumers) or HTML fragments (for the HTMX frontend). The pattern is well-documented and aligns with the Wolverine.Http vertical-slice philosophy.
- **The talk could land a beat on simplicity.** "Look, no client framework" reads as a strong story for an audience tired of JS framework churn.

**Weaknesses for CritterMart:**

- **Misfit for high-frequency interactions.** Carson Gross's own "When Should You Use Hypermedia?" essay names the limit: any UI driven by rapid client-side updates (drag-and-drop, autocomplete with sub-100ms feedback, rich text editing) is the wrong fit. CritterMart's round-one cart manipulation, search, and checkout sit comfortably inside the HDA sweet spot, but any future round-two interactive surfaces (live ops dashboards, animated transitions) would not.
- **Optimistic UI requires more work.** The TanStack Query optimistic-update pattern is built into the React ecosystem; HTMX's equivalent is `hx-swap-oob` (out-of-band swaps) and manual handling of the "what if the server rejects?" case. Doable, but lower automation.
- **Smaller .NET-shop community than Blazor or React.** HTMX is well-known in 2026 but not universally; the talk audience may have heard the name without having shipped a real HDA app.
- **Frontend testing story.** End-to-end tests work (Playwright), but the unit-test story is weaker because there is no component model to mount and assert against. CritterBids called this out (in a different context) as a reason to prefer a real frontend framework.

**Demo ergonomics.** No frontend process to orchestrate; HTMX is served by the .NET service that owns the page. Best ergonomics of any candidate on raw simplicity.

**Six-day fit.** Strong if the team has shipped HDA work before; medium if not. The architectural pattern is small but does require thinking about HTML fragments as the over-the-wire format.

**.NET-shop audience fit.** Medium. The talk could pitch HTMX as "the alternative" — there is a real audience that has been wanting to hear someone say "you don't need a JS framework for this" out loud.

**Long-road preservation.** Mixed. Most enhancements (admin UI, returns slice, promotions) fit HDA naturally. A few (real-time storefront updates, live dashboards, mobile-optimized rich interactions) push against the architecture and would suggest adding a JS island for that surface — at which point the architectural simplicity argument weakens.

---

## Decision criteria

The follow-on ADR should weigh the candidates against the criteria below, in roughly the order shown (highest-priority first). The criteria are derived from the vision doc, the round-one ADRs, and the talk's pedagogical goals; the order reflects what the round-one slice list (browse, cart, checkout, place-order) actually demands.

1. **Six-day timeline fit.** Can the team ship the round-one success criteria in the window? Battle-tested choices beat novel ones at this scale; the SDD pipeline is a learning surface in its own right and should not also be a frontend learning surface.
2. **Demo ergonomics.** Can the talk run the topology with one command? Does the Aspire dashboard surface the frontend as a resource? Does the OpenTelemetry trace span cross a real HTTP boundary so the trace-demo beat lands?
3. **.NET-shop audience fit.** Will the talk's audience recognize the pieces, or will the choice require talk-budget to explain? The talk's subject is event sourcing with Marten, not the frontend; budget spent on the frontend is budget not spent on the subject.
4. **Round-one cut alignment.** The vision doc explicitly excludes real-time updates, auth, and admin UI from round one. The choice should not introduce machinery for things that round one does not need.
5. **Learning value.** Does the choice teach something the talk benefits from? A choice that the audience already knows is positive on criterion 3 and roughly-neutral here; a choice that introduces a teachable architectural beat is positive here and negative on criterion 3. The trade-off is real.
6. **Long-road preservation.** Can the choice grow into real-time, BFF, auth, admin UI, and richer interactions without a rewrite? Round-one CritterMart explicitly avoids these surfaces, but the choice should not actively foreclose them.
7. **Ecosystem health.** Active development, broad community, available components for commerce UX, LLM training corpus size for AI-assisted authoring. The CritterBids frontend research's "LLM-friendly by default" principle applies here: under AI-assisted development, training-data scale is a real cost.
8. **Maintenance cost beyond the talk.** Round two of the talk lands at an online .NET user group; follow-on blog posts ("You Don't Need Cosmos DB," "Swapping the Bus," and similar) will reuse the codebase. The choice should be one the maintainer wants to live with for several more months.

The criteria are intentionally not weighted. Different reasonable people would weight them differently; the ADR records the weighting that was applied.

---

## Cross-cutting themes

Five themes appeared repeatedly across the surveyed sources:

**1. JS shipping cost is conversion cost.** Addy Osmani's 2017–2023 talk series, the web.dev case-studies, the Deloitte-for-eBay LCP study cited by Born Digital, and Lee Robinson's Amazon-100ms-1% citation all converge: every kilobyte of JavaScript on the critical path is, at scale, a measurable conversion drag. For CritterMart specifically this is more useful as talk-narrative framing than as a candidate discriminator (any of the seven candidates is fast enough for round one); for round-two planning it is a real consideration.

**2. Hydration is the design problem; framework families are competing solutions to it.** Misko Hevery names the diagnosis; Qwik answers with resumability, Astro/Marko answer with islands, React/Next answer with React Server Components, Blazor answers with per-component render modes. The frameworks differ on the cure; they agree on the disease. This pattern is what made the 2022–2026 framework landscape so visibly active.

**3. The server-first cycle has returned.** Tom MacWright's 2020 essay, DHH's Hotwire pitch, Carson Gross's HDA architecture, Lee Robinson's RSC-first commerce template, and Shopify's Hydrogen-on-React-Router-v7 architecture all push in the same direction: server-rendered HTML as the default, client-side interactivity as augmentation. Whether the augmentation is React Server Components, htmx attributes, Hotwire Turbo frames, or Astro islands, the underlying movement is consistent.

**4. Type safety end-to-end is now a baseline expectation.** TypeScript strict mode, Zod or Valibot at the wire boundary, TanStack Router's compile-time type-safe URLs, TanStack Form's schema-derived types, Hydrogen's GraphQL codegen — every candidate stack assumes this. The 2018-era "TypeScript is optional" framing is gone.

**5. AI-assisted development reweights "ecosystem maturity."** The CritterBids frontend research's "LLM-friendly by default" principle reflects a real shift: novelty is a cost not just in human-onboarding time but in AI-assist quality, because LLMs perform better on mature, ubiquitous technologies with deep training corpus coverage. Svelte and Astro pay this cost relative to React; htmx pays it less because the surface area is small and stable.

---

## Open questions for the team

These surfaced during research and warrant team discussion before the ADR lands:

1. **Should the round-one cut on real-time be revisited if a stack makes it cheap?** Blazor Server has a SignalR channel by default; if the chosen stack also has one (Blazor United, or React + `@microsoft/signalr`), is there a small real-time addition that strengthens the talk without violating the timeline? Or does the discipline of "no real-time round one" matter as a principle independent of cost?
2. **Should a BFF be added if the chosen stack benefits from one?** ADR 006 defers a BFF. A Next.js or TanStack Start choice would effectively introduce one (the server tier becomes a BFF in practice). Should ADR 006 be revisited in light of the frontend choice, or should the frontend choice be constrained to fit ADR 006?
3. **Should the talk include a frontend-stack-justification slide?** If the choice is non-obvious (Blazor in a JS-leaning audience, or htmx in a React-leaning audience), a slide of context may be necessary. If the choice is the obvious one (Vite + React for a JS-leaning audience), the slide can be skipped or compressed.
4. **How important is the OpenTelemetry trace-spans-the-network-boundary demo?** A Blazor variant that injects services rather than calling over HTTP would weaken this beat. Is the trace demo a hard requirement of the talk, or could the trace be shown from a different vantage point (Wolverine handlers calling each other across RabbitMQ, for example)?
5. **Where does CritterBids' frontend-stack-research output bind CritterMart?** CritterBids has already done deep deliberation on Vite + React + TanStack Query + Tailwind + shadcn/ui for an architecturally adjacent project. Should CritterMart treat that as a default-from-precedent and revisit only if a CritterMart-specific reason emerges? Or should CritterMart re-deliberate from scratch?
6. **Does the talk audience profile shift the choice?** The first delivery is internal at ImprovingU (likely a more diverse stack background); the second is an online .NET user group (likely a more uniformly .NET-leaning audience). Does that shift the .NET-shop-audience-fit weighting between deliveries?

---

## Appendix A: Sources considered and rejected

Sources surfaced during research but excluded for failing the quality bar, with the reason:

- **Most Medium / dev.to articles by unknown authors.** Excluded by default; only Medium / dev.to posts by an author with verifiable other engineering work (Ryan Carniato's dev.to writing, Misko Hevery's Builder.io blog cross-posts) were considered.
- **Listicle "X vs Y in 2026" articles by unsigned or single-post authors.** Excluded; these tend to be SEO-optimized derivative summaries rather than primary engineering writing.
- **Weaverse blog (weaverse.io).** Weaverse is a Hydrogen-ecosystem vendor with skin in the game; the React-Router-vs-Next.js and Remix-3-Beta posts were read for the Hydrogen migration timeline (which they get right per cross-reference with Shopify's first-party docs) but not cited because the editorial framing is sales-influenced.
- **Most LinkedIn posts.** Excluded as a primary source; the LinkedIn posts by Addy Osmani and Brittney Postma were used to cross-reference talks but not cited independently.
- **"Pragmatic Engineer" and similar paid-newsletter content.** Not cited here because none of the published-public Pragmatic Engineer pieces surfaced were directly on frontend stack choice; they would be appropriate sources for related topics.
- **System-design-interview content on Medium / LinkedIn.** Excluded; the "How Shopify Built X" posts are typically derivative summaries of the same Shopify engineering blog corpus.
- **AI-content-smell posts.** Several search results had the generic-phrasing / no-concrete-examples / formulaic-structure pattern that signals LLM-generated content; these were excluded without further analysis. Three of the "Tailwind v4 best practices" results in source-discovery had this profile.
- **Crystallize, AstroMerchant, Snipcart vendor pages.** Vendor marketing pages that surfaced under Astro-commerce searches; useful for context that "Astro is being used for commerce" but not cited as engineering-claim evidence.
- **Stack Overflow answers.** Not cited; treated as potential signal for what to search for next, never as primary evidence.

The rejections are deliberate. A future research session should re-evaluate the bar if a rejected source is later promoted by a Tier-1 source citing it.

---

## Appendix B: Suggested follow-up reading

Sources that did not directly yield a cited claim but are worth bookmarking for round-two work and follow-on blog posts:

- **The complete Astro `withastro/storefront` repository.** Reading the source is the fastest way to understand what an Astro-Solid commerce architecture actually looks like in production. Astro's own shop runs on it.
- **`vercel/commerce` and `bigcommerce/nextjs-commerce` repositories.** The two most-studied Next.js App Router commerce reference codebases.
- **Shopify Hydrogen Cookbook.** The 2026 Hydrogen release notes refer to it; recipes for B2B, metaobjects, infinite-scroll pagination. Useful even outside a Shopify context as a "patterns for commerce frontends" reference.
- **Tanner Linsley's TanStack Start talks at React Summit US 2024 and 2025.** The framework hit 1.0 in early 2026 and is the most credible client-first alternative to Next.js for React commerce work.
- **Heydon Pickering, *Inclusive Components* (Smashing Magazine, 2019, revised 2021).** Already cited above; the canonical chapter-per-component accessibility reference for any frontend stack.
- **Carson Gross, Adam Stepinski, and Adam Petros, *Hypermedia Systems* (book).** The expanded book-length argument behind the htmx essays. Even if HTMX is not the chosen stack, the book is the strongest single articulation of the hypermedia-driven architecture case.
- **Misko Hevery's complete Qwik talk series.** "Hydration is Pure Overhead," "Rethinking Web Application Design," and follow-ons. The strongest case for the resumability model.
- **DHH's writing on Hotwire and the "No Build" philosophy.** Even outside a Rails context, the architectural argument for sending HTML over the wire is worth reading.
- **Microsoft Learn's Blazor render-modes documentation.** Comprehensive coverage of the four modes plus prerendering plus the Interactive Auto behavior. The Visual Studio Magazine write-up of Conway's Blazor Summit talk is the talk-length summary.
- **Aspire JavaScript hosting documentation.** The first-party walkthrough of `AddViteApp` / `AddJavaScriptApp` is the canonical reference for any frontend-plus-Aspire integration. Reads quickly.
- **The CritterBids `docs/research/frontend-stack-research.md`.** The sibling-project precedent. Substantially more detail on the Vite-vs-Next.js debate than this document covers; worth re-reading before the ADR is authored.

---

## Document history

| Version | Date | Author | Notes |
| --- | --- | --- | --- |
| v1.0 | 2026-05-26 | Erik Shafer (Claude Opus 4.7 session-runner) | Initial research pass. Primary sources: Carson Gross (htmx), Misko Hevery (Qwik), Ryan Carniato (Solid), Rich Harris (Svelte), Lee Robinson (Vercel/Next.js), Tanner Linsley (TanStack), Shopify Engineering (Hydrogen), Astro team (`withastro/storefront`), Tom MacWright (macwright.com), DHH (Hotwire), Addy Osmani (Cost of JavaScript), Heydon Pickering (Inclusive Components), Microsoft Learn / DevBlogs (Blazor render modes, Aspire JavaScript), Tailwind CSS team (v4), Stripe (Elements / PCI compliance), web.dev (CWV business impact), Born Digital (Deloitte-for-eBay LCP data). Surveyed ten landscape axes; evaluated seven candidate stacks (Vite+React, Next.js, Blazor United, Blazor Server, Astro Islands, SvelteKit, HTMX+Razor) without naming a winner. Decision criteria, cross-cutting themes, and open questions captured for the follow-on ADR. Real-time material flagged as long-road only per vision doc § "What this deliberately is not." |
