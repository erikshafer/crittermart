# Prompt: Research 002 — E-Commerce Frontend Stack Survey

**Kind**: pre-code derivative artifact (research)
**Files touched**: `docs/research/ecommerce-frontend-stack.md` (new); `docs/retrospectives/research/002-ecommerce-frontend-stack.md` (new)
**Mode**: solo synthesis — the work is web research, source-quality filtering, paraphrase, citation, and comparative analysis. No multi-persona facilitation.
**Commit subject**: `tidy: research — e-commerce frontend stack survey`

## Framing

CritterMart's vision doc names the frontend stack as the one unresolved structural choice for round one: *"A frontend client sits in front of the services as the customer-facing storefront; the specific frontend stack is TBD for round one."* Every other architectural non-negotiable is locked. This research prompt produces the evidence base for that choice. The decision itself lands in a follow-on ADR; this document is the input.

The frontend landscape has moved fast. Rendering models (SPA, MPA, SSR, SSG, Islands, resumability), framework families (React, Vue, Svelte, Solid, Blazor, HTMX, Hotwire), meta-frameworks (Next.js, Remix, SvelteKit, SolidStart, TanStack Start, Astro, Qwik, Blazor United), and state-management patterns have all shifted between 2022 and 2026. A useful survey must be honest about what is hype, what is mature, what has died, and what is genuinely emerging.

CritterMart's particular constraints — six-day round-one timeline, 50-minute talk with no live coding, customer ID hardcoded into the frontend (no auth in round one), no real-time storefront updates in round one, three Wolverine.Http services as the backend surface, .NET Aspire as the local orchestrator, .NET-shop audience for the talk — combine to make some choices much more viable than others. The research should surface the trade-offs, not pre-decide them.

Sibling research is Research 001 (e-commerce engineering lessons). That document is reference-building and intentionally excludes frontend topics. This one is decision-driving and intentionally focuses on frontend topics. The two are partitioned.

CritterBids' frontend stack (Vite + React + TypeScript + TanStack Query + Tailwind v4 + shadcn/ui + `@microsoft/signalr`) is the in-house precedent and is a reasonable candidate for CritterMart, but the research should not assume the answer. Evaluate it alongside other candidates on equal footing.

## Goal

Produce `docs/research/ecommerce-frontend-stack.md`, a single Markdown document that surveys the modern frontend landscape as it applies to single-front e-commerce, captures sourced lessons from reputable engineers and engineering organizations, presents a comparative analysis of viable candidate stacks for CritterMart, and surfaces a decision-criteria framework that a follow-on ADR can use to name the stack.

The document is decision-evidence, not the decision itself. It does not recommend a single stack. It presents candidates against criteria with sourced trade-offs so the ADR author can make the call with the trail visible.

Sanity-check requirements: every cited source meets the quality bar. Every claim about a framework, library, or rendering model that goes beyond received wisdom is sourced. The candidate-stack comparison is honest about each option's weaknesses, not just its strengths. SignalR / SSE / WebSocket discussion is explicitly framed as long-road material, since round one excludes real-time storefront updates per the vision doc's "What this deliberately is not" list.

## Orientation

Read these in this order before beginning:

1. **`docs/vision.md`** — particularly the "What this is" section (the TBD line is the one this prompt unlocks), "What this deliberately is not" (no real-time, no auth, no admin UI — these are *frontend* constraints, not just backend), "Success criteria for round one" (the user-facing flows the frontend must support), and "Long road" (where the frontend might grow).
2. **`docs/decisions/`** — skim ADRs 001–010 to understand backend surface. ADR 003 (Wolverine.Http per service) is particularly relevant: the frontend talks to three service endpoints, not a BFF, in round one. The "Long road" mentions a possible BFF; do not assume one.
3. **`docs/context-map/README.md`** — for the integration model. The frontend is, in DDD terms, a downstream conformist to three upstream services. Lessons about API design for storefronts, BFF patterns, and conformist integration are relevant.
4. **`docs/workshops/001-crittermart-event-model.md`** — for the user-facing flows the frontend must wire (browse, add-to-cart, checkout, order placement, order completion). The frontend's job is to make these flows feel coherent.
5. **CritterBids precedent** — if the user can produce a pointer to CritterBids' frontend setup (likely under `C:\Code\critterbids\`), inspect its package.json, vite.config, and any frontend ADRs. Treat it as one candidate among many, not as the answer.
6. **`C:\Code\crittercab\docs\research\` (if present)** — for precedent on research-document structure and source-quality bar. Mirror what works.

## Out of scope

- Do not author an ADR for the frontend stack. That is a separate prompt at `docs/prompts/decisions/` and a separate PR. This prompt produces the evidence; the ADR makes the call.
- Do not capture backend / architectural lessons (process managers, event sourcing, persistence patterns, payments, inventory race conditions). Those belong in Research 001.
- Do not edit any file other than the named research document and its retrospective. Do not edit ADRs, the vision doc, the context map, CLAUDE.md, or the workshop.
- Do not capture frontend lessons specific to marketplaces, vendor portals, multi-channel sales, or admin UIs. Those scopes are on the vision doc's "deliberately not" list.
- Do not capture real-time storefront update mechanisms as a round-one concern. SignalR, Server-Sent Events, WebSockets, Pusher, Ably, and similar are eligible as **long-road material** with a clear flag. The vision doc rules them out of round one explicitly.
- Do not pick a winner. The document presents candidates against criteria; it does not name the chosen stack.
- Do not over-index on the talk. The frontend choice serves both the talk and the SDD pipeline. A choice that makes the demo easy at the cost of teaching nothing is not better than one that teaches something at the cost of slightly more setup.
- Do not cite sources that fail the quality bar. If a topic has no source clearing the bar, leave it out rather than fill it with weaker material.
- Do not reproduce copyrighted material. Paraphrase. At most one direct quote per source, under 15 words, in quotation marks, with attribution.

## Source quality bar

Each cited source must satisfy at least one of:

1. **Framework / library author or maintainer team** publishing on the official venue. Examples: React team, Vercel (Next.js), Remix team / React Router, Svelte / SvelteKit (Rich Harris, Simon Holthausen), SolidJS / SolidStart (Ryan Carniato), Qwik / Builder.io (Misko Hevery), Astro team, TanStack (Tanner Linsley), Blazor team (Steve Sanderson, Daniel Roth), HTMX (Carson Gross), Hotwire / 37signals (DHH).
2. **Established frontend engineering blog** with sustained publishing history. Examples: Shopify Engineering (Hydrogen, performance), Vercel blog, Cloudflare blog, Netlify blog, GitHub Engineering, Builder.io blog, Smashing Magazine, web.dev (Google's performance and CWV studies).
3. **Recognized individual engineer** with verifiable credentials: a sustained body of substantive writing, conference talks at established events (JSConf, React Summit, RemixConf, SvelteSummit, .NET Conf, NDC, QCon), books, or notable open-source contributions. Examples: Addy Osmani, Lee Robinson, Sarah Drasner, Ryan Florence, Michael Jackson, Una Kravets, Surma, Chris Coyier, Sara Soueidan, Heydon Pickering.
4. **Conference talk or write-up** from an established conference, ideally with a transcript or detailed summary.
5. **Book or book excerpt** from a recognized author in frontend, performance, or commerce UX.

Explicit exclusions:

- Random Medium / dev.to / Hashnode articles by authors with fewer than ~5 substantive technical posts, low engagement, or no discoverable engineering footprint.
- Framework-comparison clickbait ("Why X is better than Y in 2026") from authors with no skin in the game.
- Articles that smell LLM-generated: generic phrasing, no concrete examples, no specific benchmarks, no real production stories, formulaic structure.
- Poorly translated content.
- Marketing content from hosting vendors dressed up as engineering posts. Be especially wary here: hosting vendors have strong incentives to promote one stack over another. Vercel content on Next.js, Cloudflare content on Workers, and Netlify content on Edge Functions are all eligible if they meet the quality bar, but they should be balanced with neutral or contrary sources.
- Stack Overflow answers, except when treated as canonical references cited by another reputable source.
- Reddit / Hacker News posts, except authoritative threads from named, established engineers.
- Sources older than ~2022 about specific frameworks (the landscape has shifted too much). Older sources are acceptable for timeless topics: forms UX, accessibility, conversion impact of performance, the SPA-vs-MPA philosophical debate.

Verification standard: secondary-search every author and venue. Does the author have other substantive work? Is the venue real? Are the claims specific (numbers, named systems, benchmark methodology) or hand-wavy? If a source cannot be verified, omit it.

## Output structure

The single file at `docs/research/ecommerce-frontend-stack.md` contains, in this order:

1. **Frontmatter** — `version: v1.0`, `status: Active`, `date: {today}`, and a `references:` list pointing at the orientation docs cited in this prompt.
2. **Header paragraph** — two or three sentences naming the document's purpose (decision-evidence for the frontend-stack ADR), what it is not (not the decision itself, not a recommendation, not exhaustive), and the round-one constraint set in one sentence (six-day timeline, no real-time, no auth, three Wolverine.Http services as the backend surface, Aspire-orchestrated locally).
3. **Sources surveyed** — a table or list naming each source with author, venue, year, one-line credibility note, and link. Group by source where one author or venue contributed multiple cited pieces.
4. **The landscape, by axis** — level-2 (`##`) heading per axis. Each axis surveys the current state of the art with sourced commentary. Suggested axes (the session may revise):

    - **Rendering model spectrum.** SPA, MPA, server-rendered (SSR), static (SSG), incremental static regeneration (ISR), islands, streaming SSR, resumability. What each is, where each shines, where each fails, sourced trade-offs. The classic SPA-vs-MPA debate gets a fair hearing (DHH, Carson Gross, and Misko Hevery on one side; the Vercel / Remix / React camp on the other).
    - **Framework families.** React, Vue, Svelte, Solid, Angular, Blazor (Server, WASM, United / mixed modes in .NET 8+), HTMX, Hotwire / Stimulus / Turbo. Maturity, ecosystem, hireability, learning curve, .NET-friendliness.
    - **Meta-frameworks.** Next.js (App Router), Remix / React Router v7, Nuxt, SvelteKit, SolidStart, TanStack Start, Astro, Qwik City, Blazor United. Where each fits e-commerce, where each is still betting on the future.
    - **State management for commerce.** Server-state vs client-state distinction (TanStack Query, RTK Query, SWR). Cart-state patterns. Optimistic UI for add-to-cart and checkout. Form-state libraries (React Hook Form, Formik, TanStack Form).
    - **Styling and component systems.** Tailwind, vanilla CSS, CSS-in-JS, design tokens. Component libraries (shadcn/ui, Radix UI, Headless UI, Mantine, MUI). Accessibility implications.
    - **Performance and conversion.** Core Web Vitals as a conversion lever, image optimization, JS bundle budgets, lazy loading patterns. Google's CWV-to-conversion studies, Shopify Hydrogen's performance stance, Addy Osmani's "Cost of JavaScript" lineage.
    - **Forms, payments UX, and accessibility.** Checkout flows, form validation patterns, Stripe Elements vs custom payment UIs (relevant even though round one stubs payment), keyboard navigation, screen reader support, error handling.
    - **Real-time (LONG-ROAD ONLY).** SignalR, Server-Sent Events, WebSockets, Pusher / Ably / Pubnub. How each integrates with ASP.NET Core. How each integrates with the candidate frontend stacks. Flag the entire axis as long-road material per the vision doc; do not include it in the round-one candidate scorecard.
    - **Deployment and demo ergonomics.** Static + edge (Vercel, Cloudflare Pages, Netlify), traditional servers, container-based (Docker Compose, Aspire), self-hosted. How each candidate stack runs locally alongside Aspire-orchestrated .NET services. What "one command to demo" looks like for each.
    - **.NET integration story.** ASP.NET Core + frontend templates (the dotnet new templates for React, Angular), Blazor variants, hosting a SPA inside ASP.NET Core vs. running it separately, BFF patterns in .NET, .NET Aspire frontend integration (the AddNpmApp or similar resource types in Aspire 13.x).

5. **Candidate stacks evaluated.** Level-2 heading. Name 4–7 specific candidate stacks, each as a small subsection. Suggested starting set (the session may add, remove, or merge):

    - **A. Vite + React + TypeScript + TanStack Query + Tailwind + shadcn/ui** (the CritterBids precedent).
    - **B. Next.js (App Router) + React + Tailwind + shadcn/ui**.
    - **C. Blazor United (Blazor in .NET 8+, mixed render modes)**.
    - **D. Blazor Server (interactive, single render mode)**.
    - **E. Astro Islands + a small interactive island framework (React, Solid, or Svelte) for cart/checkout**.
    - **F. SvelteKit**.
    - **G. HTMX + ASP.NET Core Razor Pages / MVC**.

    For each candidate, capture:

    ```markdown
    ### {Letter}. {Stack name}

    - **What it is, in one sentence.**
    - **Strengths for CritterMart:** 2–4 bullets, each sourced where the claim is non-obvious.
    - **Weaknesses for CritterMart:** 2–4 bullets, each sourced where the claim is non-obvious.
    - **Demo ergonomics:** how the stack runs locally alongside Aspire-orchestrated .NET services; what "one command to demo" looks like.
    - **Six-day fit:** an honest assessment of whether the stack is battle-tested enough to ship in the round-one timeline.
    - **.NET-shop audience fit:** whether the talk's audience will find the choice familiar, novel-in-a-good-way, or off-putting.
    - **Long-road preservation:** whether the stack can grow into real-time, BFF, auth, admin UI, etc. without a rewrite.
    ```

6. **Decision criteria** — a level-2 section that names the criteria a follow-on ADR should weigh, with brief commentary on why each matters for CritterMart specifically. Suggested criteria (the session may revise):

    - Six-day timeline fit.
    - Demo ergonomics (one-command local demo).
    - .NET-shop audience fit.
    - Learning value (does the choice teach something the talk benefits from?).
    - Round-one cut alignment (no real-time, hardcoded customer ID, no auth, no admin UI).
    - Long-road preservation (can the choice grow into real-time, BFF, auth, etc. without a rewrite?).
    - Maintenance cost beyond the talk (is this a stack the maintainer wants to live with for follow-on blog posts and the round-two delivery?).
    - Ecosystem health (active development, broad community, available components for commerce UX).

7. **Cross-cutting themes** — synthesized observations that appeared repeatedly across multiple sources. The "JS shipping cost is conversion cost" theme, the "hydration is overhead" debate, the "server-first is back" cycle, etc.
8. **Open questions for the team** — things the research surfaced that warrant follow-up discussion before the ADR lands. Likely candidates: should the round-one cut on real-time be revisited if a stack makes it cheap? Should a BFF be added if the chosen stack benefits from one? Should the talk include a frontend-stack-justification slide, or leave it offscreen?
9. **Appendix A: Sources considered and rejected** — sources that looked promising but were excluded, with the reason. Editorial audit trail.
10. **Appendix B: Suggested follow-up reading** — sources that did not directly yield a cited claim but are worth bookmarking for the round-two delivery or follow-on blog posts.
11. **Document History** — initial `v1.0` stamp dated `{today}`, followed by an empty table.

## Spec delta

`docs/research/ecommerce-frontend-stack.md` and `docs/retrospectives/research/002-ecommerce-frontend-stack.md` are created. The "the specific frontend stack is TBD for round one" line in `docs/vision.md` becomes unblocked: a follow-on `docs/prompts/decisions/NNN-frontend-stack.md` prompt can author the ADR with this document as its primary evidence base. The vision doc itself is not edited in this PR; the TBD becomes an ADR in a subsequent PR.

Forward-compatibility commitment recorded in the retro: if the frontend landscape shifts meaningfully (a major framework release, a meta-framework reaching 1.0, a Blazor mode evolution), this document gets a paired update in the same PR as any downstream ADR revision, with a Document-History entry recording the change.

## Working pattern

A research session with comparative-analysis output. Seven passes, run sequentially:

1. **Precedent pass.** If `C:\Code\crittercab\docs\research\` exists, read what is there. Mirror its structure and source-quality bar. Also inspect CritterBids' frontend layout (likely `C:\Code\critterbids\src\` or similar) to ground the "Candidate A: CritterBids precedent" subsection in real artifacts rather than memory.
2. **Source discovery pass.** Use the web_search tool to assemble a candidate source list from the seed list below plus follow-on searches. Aim for 50–100 candidate sources at this stage. Capture URLs verbatim. Be deliberately broad: include sources from rival camps (DHH vs. Vercel, Carson Gross vs. Lee Robinson, Misko Hevery vs. Dan Abramov, Blazor vs. JS frameworks).
3. **Quality filter pass.** Apply the source quality bar. Verify authors and venues. Cut aggressively; the rejected pile goes in Appendix A.
4. **Axis pass.** For each axis in the "Landscape, by axis" section, extract sourced commentary from the surviving sources. Aim for balance: where opinions diverge, present both sides. Cite every non-obvious claim.
5. **Candidate-stack pass.** For each of the 4–7 candidate stacks, write the subsection. Be honest about weaknesses. Tie each strength and weakness to either a cited source or a CritterMart-specific constraint (timeline, audience, Aspire, no real-time, etc.). Resist the temptation to pre-decide; this section's job is to lay out the trade-offs, not name the winner.
6. **Criteria and synthesis pass.** Write the Decision criteria section. Write the Cross-cutting themes section. Surface Open questions for the team.
7. **Audit pass.** Confirm every cited source meets the quality bar. Confirm the real-time axis is flagged as long-road throughout. Confirm no candidate is treated as the foregone conclusion. Confirm the document does not name a winner. Confirm copyright discipline (≤15 word quotes, ≤1 per source, no reconstructed structure). Confirm rejected sources are documented in Appendix A.

Seed sources for source discovery (starting points, not a complete list — follow outbound citations):

- **Framework / library official venues.** React docs and blog (`react.dev/blog`), Vercel blog (`vercel.com/blog`), Remix blog (`remix.run/blog`), React Router v7 blog and docs, Svelte and SvelteKit blogs, SolidJS blog and Ryan Carniato's posts, Qwik blog and Misko Hevery's posts (`builder.io/blog`), Astro blog, TanStack docs (`tanstack.com`), Microsoft DevBlogs on Blazor and ASP.NET Core, Daniel Roth and Steve Sanderson, HTMX docs and Carson Gross's writing, Hotwire / 37signals / DHH posts.
- **Engineering organization blogs.** Shopify Engineering (especially Hydrogen and Storefront), Cloudflare blog (Workers, edge rendering), Netlify blog, Builder.io blog, GitHub Engineering, Smashing Magazine, web.dev for Google's CWV and conversion studies.
- **Recognized individuals.** Addy Osmani (`addyosmani.com`, Google), Lee Robinson (Vercel), Ryan Florence and Michael Jackson (Remix), Una Kravets, Surma, Chris Coyier, Sara Soueidan, Heydon Pickering, Sarah Drasner. Gergely Orosz's Pragmatic Engineer for frontend-trend takes that cross-reference what others say.
- **Conferences.** JSConf, React Summit, RemixConf, SvelteSummit, .NET Conf, NDC, QCon, GOTO. Talk catalogues are searchable; transcripts and write-ups are the preferred form.
- **Comparison studies.** Google's HTTP Archive Almanac (annual web performance reports), web.dev case studies on conversion and CWV, Builder.io's framework comparison work (with appropriate skepticism given Qwik affiliation).

Do not commit or push the output artifact document.
