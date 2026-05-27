# Prompt: Research 001 — E-Commerce Engineering Lessons Survey

**Kind**: pre-code derivative artifact (research)
**Files touched**: `docs/research/ecommerce-engineering-lessons.md` (new); `docs/retrospectives/research/001-ecommerce-engineering-lessons.md` (new)
**Mode**: solo synthesis — the work is web research, source-quality filtering, paraphrase, and citation. No multi-persona facilitation.
**Commit subject**: `tidy: research — e-commerce engineering lessons survey`

## Framing

CritterMart is being built as a single-seller storefront reference architecture. Round one's structural decisions (three services, Wolverine over RabbitMQ, Marten with schema-per-service, Process Manager via Handlers for orders, identity stubbed) are locked by the ten ADRs and the vision doc. What is missing from the SDD pipeline is a grounding document: a sourced survey of how reputable engineers and engineering organizations have actually built, scaled, and re-platformed single-front e-commerce. The talk's pedagogical claims land harder when the speaker can ground patterns in named war stories rather than appeals to authority.

This is a one-pass research session, not an architectural one. Its job is to produce a reference document that future implementation prompts, skill files, ADRs, and the talk itself can cite by name. Its sibling prompt — Research 002, on the frontend stack — is decision-driving and produces evidence for a specific ADR. This one is reference-building. The two are deliberately partitioned: backend / architectural lessons live here; frontend / UI / rendering / real-time lessons live in 002.

CritterCab's analogous research effort is a precedent if any record of it exists under `C:\Code\crittercab\docs\research\`; pattern-match its rigor, source-quality bar, and tone if so.

## Goal

Produce `docs/research/ecommerce-engineering-lessons.md`, a single Markdown reference document capturing the "life lessons," war stories, and hard-won insights from reputable engineers who have built, scaled, modified, or designed single-front e-commerce platforms. Each captured lesson is attributable to a specific, verifiable source that clears the quality bar named in this prompt.

The document is not a marketing piece, not a listicle, not a survey of vendor claims. It is a sourced, paraphrased synthesis of what people who have actually shipped commerce systems have written or said publicly about doing so. The intended consumer is a future implementation prompt, skill file, or ADR that needs to cite specific lessons by source rather than appeal to "industry best practice."

Sanity-check requirements: every cited source is named with author, venue, and (where applicable) date. Every lesson traces to a specific source; no anonymous "best practice" claims. An appendix records sources considered and rejected, so the editorial filter is auditable.

## Orientation

Read these in this order before beginning:

1. **`docs/vision.md`** — particularly "What this is", "What this deliberately is not", "Bounded contexts", and "Long road" sections. The deliberately-not list is critical for filtering source material; lessons about marketplace dynamics, vendor portals, returns, promotions, real-time storefront updates, or real payment integration are out of round-one scope and most should not be captured. Topics that appear in the long-road section are eligible but should be flagged as long-road material.
2. **`docs/decisions/`** — skim ADRs 001–010 to know which architectural questions are already settled. Lessons that contradict locked-in decisions still belong in the document (they are useful as the talk's "alternatives considered" beat), but they should be flagged as such rather than presented as recommendations.
3. **`docs/context-map/README.md`** — for the integration model between Catalog, Inventory, and Orders. Lessons about service boundaries, customer-supplier relationships, and conformist integration are most relevant when they map to this topology.
4. **`docs/workshops/001-crittermart-event-model.md`** — for the event vocabulary, particularly the four load-bearing event names from ADR 007. Lessons about event naming, command/event distinction, and event modeling discipline land here.
5. **`C:\Code\crittercab\docs\research\` (if present)** — for precedent on document structure, source-quality bar, and tone. Mirror what works; do not recreate the same lessons with different sourcing.

## Out of scope

- Do not capture lessons about marketplace dynamics, vendor portals, multi-channel sales, seller onboarding, two-sided economics, marketplace search ranking, or marketplace fraud. CritterMart is a single-seller storefront; the vision doc's "What this deliberately is not" list enforces that scope.
- Do not capture lessons about frontend technology choice, SPA vs. MPA, framework selection, real-time storefront update mechanisms, or frontend performance. That work is in Research 002. Duplicating it here creates churn between the two documents.
- Do not edit any file other than the named research document and its retrospective. In particular, do not edit ADRs, the vision doc, the context map, the workshop document, or CLAUDE.md.
- Do not author skills, rules, or ADRs derived from the research. Those, if they emerge, belong in separate prompts at `docs/prompts/skills/`, `docs/prompts/rules/`, or `docs/prompts/decisions/` respectively, each paired with its own retrospective.
- Do not include CritterMart-specific design proposals in the lessons. The "Implication for CritterMart" field per lesson is a short reflection of how the lesson might apply or why it does not, not a design recommendation.
- Do not cite sources that fail the quality bar below. If a topic has no source clearing the bar, leave that topic out rather than fill it with weaker material.
- Do not reproduce copyrighted material. Paraphrase. At most one direct quote per source, under 15 words, in quotation marks, with attribution. Do not reconstruct an article's structure via dense paraphrase.

## Source quality bar

Each cited source must satisfy at least one of:

1. **Established company engineering blog** with sustained publishing history. Examples: Shopify Engineering, Stripe blog, Etsy's Code as Craft, GitHub Engineering, Basecamp / 37signals, Gumroad, Big Cartel, Increment magazine, Cloudflare blog, Vercel engineering.
2. **Recognized individual engineer** with verifiable credentials: a sustained body of substantive writing, conference talks at established events (QCon, GOTO, NDC, KubeCon, RailsConf, DDD Europe, Explore DDD, etc.), books, or notable open-source contributions in the space.
3. **Conference talk or write-up** from an established conference, ideally with a transcript or detailed summary available.
4. **Book or book excerpt** from a recognized author in distributed systems, payments, or e-commerce.

Explicit exclusions:

- Random Medium / dev.to / Hashnode articles by authors with fewer than ~5 substantive technical posts, low engagement, or no discoverable engineering footprint elsewhere.
- Articles that smell LLM-generated: generic phrasing, no concrete examples, no specific numbers, no war stories, formulaic structure.
- Poorly translated content (broken English, awkward technical terminology, dropped articles).
- Marketing content from SaaS vendors dressed up as engineering posts.
- Aggregator sites and thin listicles.
- Stack Overflow answers, except when treated as a canonical reference cited by another reputable source.
- Reddit posts, except authoritative threads from named, established engineers.

Verification standard: before citing any source, do a brief secondary check. Has the author published other substantive work? Is the venue real and established? Does the article contain specifics (numbers, named systems, real outages, real trade-offs) that suggest first-hand experience? If a source cannot be verified, omit it rather than guess.

## Output structure

The single file at `docs/research/ecommerce-engineering-lessons.md` contains, in this order:

1. **Frontmatter** — `version: v1.0`, `status: Active`, `date: {today}`, and a `references:` list pointing at the orientation docs cited in this prompt (vision, ADRs, context map, workshop).
2. **Header paragraph** — two or three sentences naming the document's purpose (sourced reference for future implementation prompts, skills, ADRs, and talk material), what it is not (not a design document, not an ADR, not a recommendation), and the quality bar in one sentence.
3. **Sources surveyed** — a table or list naming each source with author, venue, year, one-line credibility note, and link. Group by source where one author or venue contributed multiple cited posts.
4. **Lessons by category** — level-2 (`##`) heading per category. Suggested categories follow; the session may revise based on what the research surfaces:

    - **Cart and checkout** — state management, abandoned carts, idempotency keys, multi-step flows, guest vs. authenticated.
    - **Inventory and stock** — race conditions, oversell prevention, reservation patterns, eventual consistency on stock counts.
    - **Payments** — integration patterns, webhook reliability, retries, reconciliation, PCI scope minimization (capture as relevant even though round one stubs payment).
    - **Order lifecycle** — placement through fulfillment, sagas and process managers, status transitions, refunds and returns.
    - **Product catalog** — variants, SKUs, media, search indexing pipelines.
    - **Search and discovery** — Elasticsearch / OpenSearch / Algolia / Meilisearch, faceting, autocomplete, ranking signals.
    - **Observability and conversion funnels** — instrumenting revenue-impacting paths, error budgets keyed to revenue, OTel patterns.
    - **Resilience and scaling** — flash sales, Black Friday war stories, queueing strategies, backpressure.
    - **Trust and safety** — fraud detection, chargebacks, account takeover.
    - **Migration stories** — monolith-to-services, replatforming, vendor swaps, rewrites that succeeded or failed.

    Each lesson follows this shape:

    ```markdown
    #### {Short, declarative lesson title}

    - **Source**: Author, Venue, Year. [link]
    - **Context**: Brief paraphrase of the original argument or war story (2–4 sentences).
    - **Implication for CritterMart**: How this might apply, or explicitly why it does not (1–2 sentences).
    ```

5. **Cross-cutting themes** — synthesized observations that appeared repeatedly across multiple sources. This is the section where dots are connected between authors.
6. **Open questions for the team** — things the research surfaced that warrant follow-up discussion or ADRs.
7. **Appendix A: Sources considered and rejected** — sources that looked promising but were excluded, with the reason (e.g., "author has no other published work," "post is undated and generic," "clearly LLM-generated," "marketplace-only content"). This is the editorial audit trail.
8. **Appendix B: Suggested follow-up reading** — sources that did not directly yield a captured lesson but are worth bookmarking for the long road.
9. **Document History** — initial `v1.0` stamp dated `{today}`, followed by an empty table ready for future entries.

Tone and shape of a lesson: a short declarative title, a 2–4 sentence context paraphrase, a 1–2 sentence implication. Do not reconstruct the source. Do not over-quote. The cite is the rationale by reference.

## Spec delta

`docs/research/ecommerce-engineering-lessons.md` and `docs/retrospectives/research/001-ecommerce-engineering-lessons.md` are created. The `docs/research/` directory gains its first occupant under the new `research` kind, and `docs/retrospectives/research/` is created alongside it. Downstream implementation prompts, skills, and the talk itself can cite specific lessons by source rather than appealing to "best practice" without attribution — a meaningful improvement in the SDD pipeline's audit trail.

Forward-compatibility commitment recorded in the retro: if a future ADR or implementation surfaces a domain-relevant lesson not yet captured, it is added in a paired `tidy: research —` PR that bumps the document version.

## Working pattern

A research session, not a workshop. Six passes, run sequentially:

1. **Precedent pass.** If `C:\Code\crittercab\docs\research\` exists, read what is there. Match its structure, source-quality bar, and tone unless something is clearly outdated. If no precedent exists, the structure named under "Output structure" stands as written.
2. **Source discovery pass.** Use the web_search tool to assemble a candidate source list from the seed list below plus follow-on searches. Aim for breadth at this stage: 40–80 candidate sources is reasonable. Capture URLs verbatim.
3. **Quality filter pass.** Apply the source quality bar to each candidate. Verify authors and venues by secondary search (does the author have other substantive work? is the venue real?). Cut aggressively; the rejected pile is captured in Appendix A with a one-line reason per entry.
4. **Lesson extraction pass.** Read each surviving source. For each, identify 0–4 lessons that are specific, actionable, and relevant to CritterMart's scope (single-seller storefront, backend / architecture focus, not frontend). Paraphrase each lesson into the lesson shape above. Cite every lesson. Skip sources that yield no lessons after a careful read; record them in Appendix B if still worth bookmarking.
5. **Category and synthesis pass.** Group lessons by category. Identify cross-cutting themes that appeared in more than one source. Write the Cross-cutting themes section. Surface Open questions for the team.
6. **Audit pass.** Confirm every cited source meets the quality bar. Confirm every lesson traces to a specific source. Confirm no marketplace-only material slipped in. Confirm no frontend-specific material slipped in (that is Research 002's territory). Confirm copyright discipline: at most one quote per source, each quote under 15 words in quotation marks with attribution, no reconstructed structure, no dense paraphrase that reassembles the original. Confirm rejected sources are documented in Appendix A.

Seed sources for source discovery (starting points, not a complete list — follow outbound citations from credible sources):

- Shopify Engineering (`shopify.engineering`)
- Stripe blog and docs (`stripe.com/blog`, `stripe.com/docs`)
- Etsy Code as Craft (`codeascraft.com`)
- Increment magazine themed issues
- Gergely Orosz — The Pragmatic Engineer newsletter and blog
- Big Cartel, Gumroad engineering writeups
- High Scalability archives
- Charity Majors, Camille Fournier, Will Larson, Susan Fowler — cross-cutting engineering writing that touches commerce
- Martin Fowler's site for order modeling, idempotent receiver, event-driven patterns
- InfoQ, QCon, GOTO, NDC talk catalogues
- Conference talks from Shopify, Stripe, Etsy, Gumroad engineers

Author the retrospective before opening the PR. One session, one PR, per the in-repo discipline. Branch name: `tidy/research-ecommerce-engineering-lessons`.
