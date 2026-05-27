# E-Commerce Engineering Lessons for CritterMart

> Research compiled on 2026-05-26. Sources are reputable engineering blogs,
> books, and conference talks from individuals and companies with track records
> in single-front e-commerce, payments, and high-volume transactional systems.

CritterMart is a single-seller storefront reference architecture built on the
Critter Stack (.NET 10, Wolverine, Marten, RabbitMQ, PostgreSQL). It exists to
anchor an Event Sourcing with Marten talk and to exercise a Spec-Driven
Development pipeline. The lessons collected here are filtered for that lens:
single-front commerce (not marketplace), event-driven .NET (not language
specific), and modest scale (three deployable services, three event-sourced
aggregates).

The goal of the document is not to be exhaustive. It is to surface the
specific engineering pitfalls and architectural choices that mature
e-commerce engineers have written or spoken about publicly, so that CritterMart
can make those choices deliberately rather than by accident.

---

## Table of Contents

- [How This Document Was Built](#how-this-document-was-built)
- [Sources Surveyed](#sources-surveyed)
- [Lessons by Category](#lessons-by-category)
  - [Cart and Checkout](#cart-and-checkout)
  - [Inventory and Stock](#inventory-and-stock)
  - [Payments and Webhooks](#payments-and-webhooks)
  - [Order Lifecycle and Sagas](#order-lifecycle-and-sagas)
  - [Product Catalog and Search](#product-catalog-and-search)
  - [Performance and Conversion](#performance-and-conversion)
  - [Observability](#observability)
  - [Resilience and Scaling](#resilience-and-scaling)
  - [Architecture and Modularity](#architecture-and-modularity)
  - [Trust, Safety, and Compliance](#trust-safety-and-compliance)
  - [Internationalization](#internationalization)
  - [Migration Stories](#migration-stories)
- [Cross-Cutting Themes](#cross-cutting-themes)
- [Open Questions for the Team](#open-questions-for-the-team)
- [Appendix A: Sources Considered and Rejected](#appendix-a-sources-considered-and-rejected)
- [Appendix B: Suggested Follow-Up Reading](#appendix-b-suggested-follow-up-reading)

---

## How This Document Was Built

### What counts as a good source

E-commerce is a domain with an enormous volume of low-quality writing —
SaaS-vendor blogs dressed up as engineering posts, derivative listicles, and
LLM-generated "Top 10 ways to optimize checkout" content. To keep this
document honest, sources were tiered:

- **Tier 1 — first-party engineering blogs from named engineers.** The Shopify
  engineering blog, Stripe's engineering blog and developer blog, Etsy's Code
  as Craft, and AWS Prescriptive Guidance are the primary sources. These have
  sustained publishing histories of a decade or more and articles are signed
  by named authors who have spoken at established conferences.
- **Tier 2 — recognized individual engineers with verifiable track records.**
  Brandur Leach (ex-Stripe, ex-Heroku) on idempotency and Postgres queues,
  Mathias Verraes on event-sourced modeling, Greg Young on event sourcing
  failure modes, Charity Majors on observability, Will Larson on migration
  discipline, Martin Fowler on monolith-first, Susan Fowler on production
  readiness, Sam Newman on BFFs. All have books, sustained blog histories, or
  conference-talk catalogs at QCon / GOTO / DDD Europe / RubyConf.
- **Tier 3 — Gergely Orosz's Pragmatic Engineer deep-dives.** Used carefully:
  Gergely has a strong source verification practice (named current employees
  rather than ex-employees), and his Stripe engineering culture pieces are
  reviewed by Stripe's CTO before publishing.

Derivative summaries on Medium, dev.to, and SaaS-vendor blogs were read for
triangulation but are not cited unless the author has a separately verifiable
engineering footprint.

### Methodology and editorial filter

For each candidate source I checked: (a) is the author findable elsewhere
with substantive other work, (b) is the venue real and dated, (c) does the
piece contain specifics — numbers, named systems, war stories, real
trade-offs — that suggest first-hand experience? If any of those failed, the
source was dropped. Promising-but-rejected sources are logged in
[Appendix A](#appendix-a-sources-considered-and-rejected).

### How to read the "Implication for CritterMart" notes

Each lesson ends with a short note on how it maps to CritterMart's vision.
CritterMart is deliberately small — three services, three event-sourced
aggregates, no marketplace dynamics, no real payment integration in round
one, no backoffice. Many lessons from companies at Shopify or Stripe scale
need to be scaled *down*, not copied. The notes try to be honest about
which lessons fit at CritterMart's scale, which apply only as future-state
discipline, and which should be deliberately ignored.

---

## Sources Surveyed

Grouped by venue. Where multiple posts from one venue are cited, individual
URLs appear inline in the lessons.

**Shopify Engineering** (`shopify.engineering`). Sustained publishing since
2010; covers BFCM scaling, sharding, the modular monolith, flash-sale
queueing, storefront rewrites, and inventory reservations. Cited posts:

- Kirsten Westeinde, "Deconstructing the Monolith: Designing Software that
  Maximizes Developer Productivity" (2019).
- Philip Müller, "Under Deconstruction: The State of Shopify's Monolith"
  (2020).
- "A Pods Architecture To Allow Shopify To Scale" (2018).
- Maxime Vaillancourt, "How Shopify Reduced Storefront Response Times with a
  Rewrite" (2020).
- "Surviving Flashes of High-Write Traffic Using Scriptable Load Balancers"
  Parts I and II (2017).
- "Capacity Planning at Scale" (Kir Shatrov, 2020).
- "Performance Testing At Scale — for BFCM and Beyond" (2023).
- "We replaced Redis with MySQL for inventory reservations — and it scaled"
  (2026).
- "MySQL Database Shard Balancing at Terabyte Scale" (2021).

**Stripe Engineering and Stripe.dev** (`stripe.com/blog`, `stripe.dev/blog`).
Authoritative on payments, API design, and webhook reliability. Cited posts:

- Brandur Leach, "Designing robust and predictable APIs with idempotency"
  (Stripe blog, 2017).
- Stripe API Reference, "Idempotent requests."
- "A primer on machine learning for fraud detection" (Stripe blog, 2016) and
  "How we built it: Stripe Radar" (Ryan Drapeau, Stripe.dev).
- Jake Zimmerman, "Sorbet: Stripe's type checker for Ruby" (Stripe.dev,
  2022).
- "A guide to PCI compliance" (Stripe Docs, multi-year canonical reference).

**Brandur Leach** (`brandur.org`). Former Stripe and Heroku engineer.
Canonical writing on idempotency keys, Postgres queues, and transactional
job drains. Cited posts:

- "Implementing Stripe-like Idempotency Keys in Postgres" (2017).
- "Transactionally Staged Job Drains in Postgres" (2017).
- "Postgres Job Queues & Failure By MVCC" (2015).
- "River: a Fast, Robust Job Queue for Go + Postgres" (2023).

**Etsy Code as Craft** (`etsy.com/codeascraft`). Sharded MySQL, search,
docs-as-code. Cited posts:

- "Migrating Etsy's database sharding to Vitess" (2026).
- "Two Sides For Salvation" (canonical primary-primary pair architecture
  post, ongoing reference).

**Martin Fowler** (`martinfowler.com`). "MonolithFirst" (2015) plus
"Event Sourcing" (2005). Foundational vocabulary for both topics.

**Mathias Verraes** (`verraes.net`). Independent DDD consultant, founder of
DDD Europe. Cited posts:

- "Messaging Patterns: Natural Language Message Names" (2019).
- "A Functional Foundation for CQRS/ES" (2014).
- "Practical Event Sourcing" (talk, multiple venues).

**Greg Young**. Coined CQRS. Cited talks:

- "Why Event Sourced Systems Fail" (Highload fwdays, 2020).
- "Event Sourcing" (GOTO 2014).

**Gergely Orosz, The Pragmatic Engineer** (`newsletter.pragmaticengineer.com`).

- "Inside Stripe's Engineering Culture" parts 1 and 2 (Dec 2023, Jan 2024),
  written with input from David Singleton (then-Stripe CTO).

**Sam Newman** (`samnewman.io`). Cited post: "Backends For Frontends" (2015,
foundational reference, credits Phil Calçado for coining the term at
SoundCloud).

**Will Larson**. *An Elegant Puzzle: Systems of Engineering Management*
(Stripe Press, 2019). Author was an engineering leader at Digg, Uber, and
Stripe.

**Susan Fowler**. *Production-Ready Microservices: Building Standardized
Systems Across an Engineering Organization* (O'Reilly, 2016). Author
standardized over a thousand microservices at Uber, later at Stripe.

**Charity Majors** (`charity.wtf` and conference talks). Co-founder/CTO of
Honeycomb; cited via Pragmatic Engineer interview on observability (2025)
and *Observability Engineering* (O'Reilly, 2022, co-authored with Liz
Fong-Jones and George Miranda).

**AWS Prescriptive Guidance** and **Microservices.io** (Chris Richardson).
Used as canonical references for the transactional outbox pattern and the
saga pattern respectively. Chris Richardson is the author of *Microservices
Patterns* (Manning, 2018) and the original CloudFoundry.com.

**Google web.dev case studies**. Cited "Milliseconds Make Millions" (study
commissioned by Google, conducted by 55 and Deloitte, 2019) for the
performance-to-conversion link.

---

## Lessons by Category

### Cart and Checkout

#### Carts are state machines, not bags of items

- **Source**: Sylius project, RFC issue #4060 "Order state machine" (2016);
  Chris Patterson, "Sagas, State Machines, and Abandoned Carts" (LosTechies,
  2015). [link](https://github.com/Sylius/Sylius/issues/4060),
  [link](https://lostechies.com/chrispatterson/2015/09/11/sagas-state-machines-and-abandoned-carts/).
  Sylius is the dominant open-source Symfony e-commerce framework; the
  cited RFC is the project lead's formalization of cart-vs-order modeling.
  Chris Patterson is the author and maintainer of MassTransit.
- **Context**: Both sources land on the same model — a cart is the
  `cart` state of an Order entity, transitioning to `new` on checkout
  completion, `abandoned` on timeout, and `cancelled` or `closed` from
  there. The abandonment transition is fired by a scheduled timeout
  triggered by observing the cart-event stream, not by a separate
  process polling for stale carts.
- **Implication for CritterMart**: The CritterMart vision already models
  Cart and Order as event-sourced aggregates and uses Bruun's temporal
  automation pattern (`CartActivityTimeout` schedules `CartAbandoned`).
  The Sylius and Patterson treatments are independent confirmation that
  this is the canonical shape, not an idiosyncrasy of the Critter Stack.
  Worth noting: Sylius uses one Order entity with a `cart` state instead
  of two aggregates; CritterMart's split into Cart and Order is a
  deliberate teaching choice (different streams, different lifecycles,
  cleaner Place Order narrative), not the only defensible model.

#### Single-page checkout reduces abandonment, but the variable that actually moves the number is friction at the first step

- **Source**: Baymard Institute's 2024 e-commerce checkout study (the
  canonical industry source for checkout abandonment statistics, cited
  in nearly every reputable post on the topic); summarized through
  Bemeir's checkout-flow optimization case study (2026).
  [link](https://bemeir.com/articles/checkout-flow-optimization-cart-abandonment-case-study/).
  Baymard is the most-cited UX research firm in commerce engineering;
  Bemeir is a mid-market commerce-engineering consultancy whose case
  study contains specific dropoff percentages by checkout step.
- **Context**: Average e-commerce checkout abandonment is around 70%, and
  the largest single dropoff is typically between the cart and the first
  step of checkout (~29% in the Bemeir case). Surface-level fixes (popups,
  remarketing emails) treat symptoms; the durable wins come from
  engineering the flow itself — fewer fields, transparent shipping cost,
  guest checkout, inline validation.
- **Implication for CritterMart**: CritterMart's vision deliberately
  excludes checkout-UX optimization from round one. The lesson to carry
  forward is that the *architecture* should not preclude these fixes
  later: the cart-to-order boundary should accept a guest customer
  identifier, the shipping-cost surface (if added in a future round)
  should be queryable cheaply from the cart, and the cart aggregate
  should not require fields the storefront wouldn't have until the
  payment step.

#### Cart abandonment is a projection of the event stream, not a separate workflow

- **Source**: Chris Patterson, "Sagas, State Machines, and Abandoned Carts"
  (LosTechies, 2015).
- **Context**: Patterson's implementation observes `ItemAdded`, `Submitted`,
  and a scheduled `CartExpired` signal, all keyed off a single
  correlation ID (the cart ID). No external job polls the database for
  stale carts — the saga schedules its own timeout and is woken by
  the message bus when the timeout fires or earlier signals supersede it.
- **Implication for CritterMart**: CritterMart's vision specifies
  `CartAbandoned` via scheduled `CartActivityTimeout`, which matches
  the pattern. The naming convention (`*Timeout` for the scheduled
  signal, the resulting domain event in past tense) is worth
  preserving as a project-wide rule. Worth adding to the local
  `event-modeling` skill if not already there: the scheduled-timeout
  signal is not the same kind of message as a domain event, and
  conflating them in the Place Order narrative would confuse the
  talk audience.

---

### Inventory and Stock

#### Reservation should hold during *payment*, not on add-to-cart

- **Source**: Shopify Engineering, "We replaced Redis with MySQL for
  inventory reservations — and it scaled" (2026).
  [link](https://shopify.engineering/scaling-inventory-reservations).
  Shopify Help Center: "Shopify Checkout" (canonical documentation of
  the policy).
- **Context**: Shopify's oversell-protection system places a short
  reservation hold only when the buyer submits payment information,
  not when items go into the cart. If payment fails or times out, the
  hold is released. Reserving on add-to-cart is a known anti-pattern
  at Shopify scale: it tanks availability for honest buyers because
  carts that never check out hoard stock.
- **Implication for CritterMart**: CritterMart's Place Order flow
  reserves stock at the point where the Order aggregate transitions
  into the payment-pending state, not when items are added to the
  Cart. This is the right shape. The single-seller flash-drop
  scenario where every-buyer-must-have-cart-reservation does exist
  (limited sneaker drops, concert tickets) but it is a different
  product category and should be modeled separately if it ever lands
  in CritterMart's scope.

#### The bottleneck is connection saturation, not query speed

- **Source**: Shopify Engineering, same article as above.
- **Context**: When Shopify migrated their oversell-protection reservation
  system from Redis to MySQL, the team spent weeks optimizing queries and
  locks before realizing the actual limit was database connection usage
  in shared code paths the reservation system didn't even own. The
  reservation system has to be a "safe neighbor" — sustain throughput
  without monopolizing connections that cart updates, payment
  processing, and order creation also need.
- **Implication for CritterMart**: Shared PostgreSQL with schema-per-service
  (CritterMart's choice) inherits this problem. The reservation handler
  in the Inventory BC and the order-write handler in Orders share the
  same connection pool by default. Worth surfacing in an ADR: how is
  the Marten connection pool sized, and does it allocate per BC or per
  process? At CritterMart's scale this is academic, but the question
  is the right one to have answered before the talk.

#### Webhooks for inventory sync drift; reconcile periodically

- **Source**: Upzone HQ analysis of Shopify inventory discrepancies (2026),
  citing IHL Group's 2024 research and ChannelAdvisor's 2023 multichannel
  selling study. [link](https://upzonehq.com/academy/shopify/shopify-inventory-discrepancies/).
  Upzone is a warehouse-operations consultancy with specific operational
  numbers; the underlying research is from established commerce-industry
  analysts.
- **Context**: The reported figures are stark: webhook delivery from
  Shopify to downstream systems has a 1–2% miss-or-out-of-order rate at
  scale, and 18% of multichannel sellers experience overselling at
  least once per month. The lesson is not about Shopify webhooks
  specifically. It is the more general claim that any event-based
  inventory sync drifts, and the only known fix is scheduled
  reconciliation (cycle counts, audit trail review, idempotent
  recompute) on top of the event stream.
- **Implication for CritterMart**: CritterMart is single-seller and has no
  external inventory sync in round one, so this does not bite today.
  The architectural seam to leave open is the ability to *recompute*
  stock from the Inventory event stream on demand without disrupting
  the inline `StockLevelView` projection. Marten's
  `ResetAllDataAsync` and async projection rebuild support this
  natively — the discipline is to use that capability deliberately,
  not to bolt on an inventory-reconciliation job later.

#### Inventory thresholds (safety stock) hide reality from the system

- **Source**: HotWax Commerce, "Reduce Overselling on Shopify Using
  Inventory Thresholds" (2024). [link](https://www.hotwax.co/blog/reduce-overselling-on-shopify-by-using-inventory-thresholds).
  HotWax is an Apache OFBiz-based commerce-platform vendor; the post
  contains a specific industry statistic (retail inventory accuracy
  averages 65–70%) sourced from peer-reviewed retail research.
- **Context**: Real inventory is rarely as accurate as the system thinks
  (damage, theft, miscounts). A common compensating mechanism is to
  set a *threshold* below the physical count that hides some stock from
  the sellable surface — a safety buffer. The trade-off is real: the
  storefront sees lower availability than reality.
- **Implication for CritterMart**: Not in scope for round one but worth
  noting that the Inventory aggregate's `available` projection is not
  necessarily the same as `quantity_on_hand`. If a thresholding rule
  ever lands, it should be a separate projection / read model rather
  than a property baked into the Stock aggregate itself, so that the
  event stream remains the canonical record of physical movements.

---

### Payments and Webhooks

#### Idempotency keys are a contract, not an optimization

- **Source**: Brandur Leach, "Designing robust and predictable APIs with
  idempotency" (Stripe blog, 2017); same author, "Implementing Stripe-like
  Idempotency Keys in Postgres" (brandur.org, 2017); Stripe API Reference,
  "Idempotent requests." [link](https://stripe.com/blog/idempotency),
  [link](https://brandur.org/idempotency-keys),
  [link](https://docs.stripe.com/api/idempotent_requests).
- **Context**: Stripe's POST endpoints accept an `Idempotency-Key`
  header. The server saves the resulting status code and response body
  the first time a key is seen, and replays the same response on
  subsequent requests with the same key, regardless of whether the
  first call succeeded or failed. The implementation Brandur describes
  involves treating the idempotency record itself as a small state
  machine with recovery points, so that a partially-completed
  request can resume from where it left off after a crash. Stripe's
  documentation explicitly notes "saving the resulting status code and body of the first request" as the persistence model.
- **Implication for CritterMart**: Payment is stubbed in round one, but
  the *shape* of the contract between the Order process manager and
  the payment provider should match this pattern from day one. The
  command the Order handler emits to the (stubbed) payment system
  should carry an idempotency key derived from the Order ID plus the
  step (e.g., `{order_id}:authorize`). When a real provider eventually
  lands, the wire format does not need to change. Wolverine's
  durable-messaging retry semantics make this *especially* important
  because retries are silent — the handler must be safe to invoke many
  times with the same logical input.

#### Webhook receivers are responsible for at-least-once handling

- **Source**: Stripe Docs, "Receive Stripe events in your webhook endpoint"
  and the canonical reliability summary in Hooklistener's Stripe Webhooks
  Implementation Guide (2026), which paraphrases Stripe's behavior
  accurately. [link](https://www.hooklistener.com/learn/stripe-webhooks-implementation).
- **Context**: Stripe guarantees at-least-once delivery, never
  exactly-once. Retries follow exponential backoff for up to three days.
  After that the endpoint is disabled and must be manually re-enabled.
  Four practical rules emerge from the docs and from public post-mortems:
  return 2xx within the timeout window (Stripe's is ten seconds);
  deduplicate by `event.id` server-side; do not rely on event ordering;
  process the side effects asynchronously after acknowledging.
- **Implication for CritterMart**: In round one, the Place Order saga
  receives messages from the Inventory BC and the stubbed payment
  system over Wolverine/RabbitMQ — a private bus, not a public
  webhook. But Wolverine retries have the same property: a handler
  must be idempotent because it can run more than once. The four
  rules transfer directly. When a real payment provider lands, the
  inbound webhook handler is a thin shell that translates HTTP into a
  Wolverine command and immediately ACKs; the actual processing
  happens on the bus, with the same idempotency guarantees as every
  other CritterMart handler.

#### The Idempotency-Key plus the request-body match is the safety contract

- **Source**: Stripe API Reference, "Idempotent requests" (Stripe Docs,
  canonical).
- **Context**: One overlooked detail from Stripe's documentation: the
  idempotency layer compares the incoming parameters against the
  original request and errors if they differ. The server's promise is
  not "if I see this key, return the cached response" — it is "if I see
  this key with these inputs, return the cached response; if I see this
  key with *different* inputs, fail loudly so the bug is visible."
- **Implication for CritterMart**: The Wolverine command shape from
  Order → Payments should carry both the idempotency key and the
  payload that authorized the original request (amount, currency,
  customer reference). If a retry arrives with a mismatched payload,
  the handler should refuse rather than process — this is how a
  programming bug is caught instead of silently double-charging or
  silently under-charging. Worth a skill file or rule entry.

#### Tokenization keeps the storefront out of PCI scope, but only if you do not write the PAN anywhere

- **Source**: Stripe Docs, "A guide to PCI compliance"
  ([link](https://stripe.com/au/guides/pci-Compliance)); AWS Security Blog,
  "How to use tokenization to improve data security and reduce audit
  scope" (last updated 2023).
- **Context**: PCI DSS scope is determined by where cardholder data is
  stored, processed, or transmitted. The standard tokenization play
  (Stripe Elements, Stripe Checkout, Stripe.js) routes the raw card
  data from the buyer's browser directly to Stripe's PCI-validated
  servers, returning only a token. The storefront server's PCI scope
  shrinks dramatically because it never holds a PAN — only a token
  that means nothing outside Stripe's vault.
- **Implication for CritterMart**: CritterMart stubs payment in round
  one, so this is not a current concern. The architectural reminder
  is to keep the stubbed `PaymentReference` opaque and string-typed,
  so that a future real integration drops in by swapping the stub for
  Stripe Elements without altering the Order aggregate, the events,
  or any projections. Cardholder data must never be a field on a
  domain event — the only payment field on an event should be a
  reference (token, intent ID, capture ID) and a small set of safe
  flags (amount, currency, success/failure code).

#### Stripe Radar shows that fraud detection is per-transaction inference, not per-customer flagging

- **Source**: Stripe blog, "A primer on machine learning for fraud
  detection" (2016); Ryan Drapeau, "How we built it: Stripe Radar"
  (Stripe.dev, ongoing engineering reference). [link](https://stripe.com/blog/a-primer-on-machine-learning-for-fraud-detection).
- **Context**: Radar scores every card payment in real time against
  features derived from Stripe's network-wide payment history. Ground
  truth comes from banking partners' chargeback data, not from
  manually labeled examples. The architecture is a per-transaction
  decision point, not a per-customer trust score.
- **Implication for CritterMart**: Fraud detection is out of scope for
  round one, and CritterMart is single-seller anyway (no marketplace
  risk surface). The architectural seam to leave open: the Order
  aggregate's `PlaceOrder` command should be designed so that a
  future "evaluate risk" step could fit between cart submission and
  payment authorization. This is naturally a process-manager step
  with its own event and is straightforward to add later if it
  becomes relevant. Don't bake risk-score into the Order command
  signature now — keep it a side-channel that can be added without
  changing the existing slice.

---

### Order Lifecycle and Sagas

#### The order *is* the process manager — no separate saga state stream

- **Source**: Wolverine documentation, "Process Manager via Handlers"
  pattern (jasperfx.github.io/wolverine); Chris Richardson,
  *Microservices Patterns* (Manning, 2018) and microservices.io on the
  saga pattern; Mathias Verraes, "A Functional Foundation for CQRS/ES"
  (verraes.net, 2014). [link](https://microservices.io/patterns/data/saga.html),
  [link](https://verraes.net/2014/05/functional-foundation-for-cqrs-event-sourcing/).
- **Context**: Two common shapes for coordinating multi-step business
  flows: a saga with its own state stream (Wolverine's `Saga` base type
  is one example), or a process manager implemented as handlers on the
  aggregate that already owns the lifecycle. The PM-via-handlers
  approach has the property that there is no second source of truth for
  the order's progress — the order's event stream *is* the audit log,
  and state guards on the stream (e.g., "PaymentAuthorized requires
  StockReserved and PaymentPending") enforce idempotency without a
  separate state machine table. Verraes's functional framing
  generalizes: the aggregate is a function from history-plus-command
  to events, with no implicit hidden state.
- **Implication for CritterMart**: This is exactly the pattern the
  vision specifies for round one. The Order aggregate gates
  transitions on its own stream. The teaching point for the talk is
  that this is *one* defensible model — Wolverine's saga primitive
  is the alternative and is the right call when the coordination
  state has no obvious aggregate home (e.g., choreographed cross-BC
  flows where no single BC owns the lifecycle). Worth surfacing this
  trade-off explicitly in the talk's process-manager slide rather
  than presenting PM-via-handlers as the only option.

#### Translation-decision events make external decisions auditable

- **Source**: Marc Klefter, "Translation Decisions as First-Class Events
  in Event Modeling" (eventmodelers.de community essays); see also the
  CritterCab research note `agents-in-event-models.md` for the broader
  context. Klefter is a working domain modeler with sustained
  publishing in the Event Modeling community.
- **Context**: When an aggregate makes a decision that depends on input
  from outside (an external system's response, a human approval, a
  policy lookup), it is tempting to leave that decision implicit — to
  let the handler observe the input, change state, and emit a
  business-meaningful event. Klefter's argument: capture the input
  itself as a first-class event on the aggregate's stream. Then the
  audit trail tells you not only "the order was approved" but
  "PaymentAuthorizationReceived(success=true) caused
  OrderConfirmed." Replaying the stream reconstructs the decision
  context, not just the outcome.
- **Implication for CritterMart**: This is already a stated pattern in
  the CritterMart README. The lesson from outside the project is that
  this pattern is independently endorsed by multiple Event Modeling
  practitioners (Klefter, Bruun, Dymitruk's later writing), not just
  a CritterMart idiosyncrasy. Worth mentioning in the talk: the
  practice makes the Order stream a complete record of *why* the
  process manager made each decision, which is precisely the
  pedagogical point about event sourcing's audit-by-construction
  property.

#### Statecharts (Harel-style hierarchical state machines) are underused

- **Source**: Uber's 2021 Fulfillment Platform rewrite article (covered
  in `crittercab/docs/research/ride-sharing-lessons-learned.md` Lesson
  5). Cited here because the lesson generalizes from ride-sharing
  fulfillment to commerce order lifecycles.
- **Context**: Uber's fulfillment team explicitly chose hierarchical
  statecharts as the modeling primitive for their entity lifecycles
  in the 2021 rewrite. The motivation was that flat state machines
  flatten nested states (e.g., "in-trip" with sub-states for "driving,"
  "waiting," "completing") into a wide-and-shallow enum that loses
  structure. Statecharts preserve the nesting; transitions can be
  defined at the parent level and inherited by children.
- **Implication for CritterMart**: A formal statechart runtime is overkill
  for CritterMart at three aggregates. But the *mental model* is
  useful for the talk: the Order has nested states (cart, checking-out,
  payment-pending, fulfillment-pending, complete; with payment-pending
  having sub-states for authorized-not-captured, captured, declined,
  timed-out). Drawing this on a slide will land better than a flat
  enum of order statuses. Don't ship a statechart framework — but do
  draw the diagram in the talk.

#### Transactional outbox is the cheapest known answer to the dual-write problem

- **Source**: AWS Prescriptive Guidance, "Transactional outbox pattern"
  (canonical reference); Chris Richardson, microservices.io, "Pattern:
  Transactional Outbox" (chrisrichardson.net). Both are widely cited
  in the Critter Stack documentation and in Wolverine's outbox
  implementation.
  [link](https://docs.aws.amazon.com/prescriptive-guidance/latest/cloud-design-patterns/transactional-outbox.html).
- **Context**: A handler that wants to update local state *and* publish
  a message faces the dual-write problem: either operation can fail
  while the other succeeds, leaving the system inconsistent. The
  outbox pattern writes the outgoing message to a database table in
  the same transaction as the state change, then a relay process
  publishes from the table to the broker. The downstream is
  at-least-once, so consumers must be idempotent (see the idempotency
  lessons above). The trade-off is real: the outbox adds a small
  write per business operation, dispatch latency increases by the
  relay interval, and you have to manage outbox table growth.
- **Implication for CritterMart**: Wolverine's durable outbox is the
  Critter Stack's answer to this pattern. The vision already commits
  to it. The teaching value for the talk: the outbox is *the reason*
  the Place Order trace can show "command handled, events written,
  outgoing messages durably stored" as a single transaction. Without
  the outbox, the trace would have a gap between writing the event
  store and publishing to RabbitMQ, and that gap is where production
  bugs live. The slide is "the outbox is why the Place Order story
  is a single happy path even when RabbitMQ is briefly unreachable."

#### Background jobs must be staged transactionally, or they execute on data that does not yet exist

- **Source**: Brandur Leach, "Transactionally Staged Job Drains in Postgres"
  (brandur.org, 2017); same author, "Postgres Job Queues & Failure By
  MVCC" (2015). [link](https://brandur.org/job-drain),
  [link](https://brandur.org/postgres-queues).
- **Context**: A common bug pattern: the request handler creates a
  database row inside a transaction and enqueues a Sidekiq/Resque/etc.
  job that references the row's ID. The job runs on a worker, queries
  for the row, and finds nothing — the transaction hasn't committed
  yet. Or worse, the transaction rolls back and the job runs anyway,
  failing forever on a row that will never exist. Brandur's
  recommendation: write the job to a staging table inside the same
  transaction, then drain to the real job queue from a separate
  process after commit. The transaction-bound outbox is the same
  pattern at a different layer.
- **Implication for CritterMart**: The Wolverine outbox handles this
  automatically when commands and events are emitted from inside a
  handler. The relevant CritterMart-specific gotcha is around the
  async `CartAbandonmentReport` projection mentioned in the vision —
  if it scans the cart event stream and emits domain events, those
  events must go through the outbox, not be published inline. Worth a
  spot-check in the retrospective for that slice.

---

### Product Catalog and Search

#### CRUD is fine for product data — until it isn't

- **Source**: Mathias Verraes, "Practical Event Sourcing" (talk, multiple
  venues including SymfonyCon and PHPBenelux); Greg Young, "Why Event
  Sourced Systems Fail" (Highload fwdays, 2020).
  [link](https://www.youtube.com/watch?v=FKFu78ZEIi8).
- **Context**: Both authors are skeptical of event-sourcing-as-default.
  Young's talk explicitly enumerates failure modes including
  event-sourcing where it doesn't pay rent: aggregates that are
  mostly read-only, aggregates without invariants worth protecting,
  domains where the audit log is not a business requirement. Product
  catalog data — name, price, description, image — typically does
  not have invariants that benefit from a full event history. The
  cost of event-sourcing (versioning, projection rebuilds, harder
  ad-hoc queries) outweighs the benefit.
- **Implication for CritterMart**: The vision's choice to keep Catalog
  as a Marten document store and event-source Inventory and Orders is
  the right call, and Young's failure-modes talk is the cleanest
  defense of it. The talk slide is: "I event-source the things that
  benefit from event-sourcing. Product names don't." This is the
  *teaching* point — the talk's pedagogical value depends on showing
  the contrast, not on event-sourcing everything for consistency.

#### Search faceting is a property of the index, not a query rewrite

- **Source**: Algolia engineering blog, "Comparing Algolia and
  Elasticsearch For Consumer-Grade Search Part 2: Relevance Isn't Luck"
  (algolia.com/blog/engineering). Algolia's blog has sustained
  publishing on search engineering specifically; the cited post is
  signed and contains specific implementation discussion.
  [link](https://www.algolia.com/blog/engineering/comparing-algolia-and-elasticsearch-for-consumer-grade-search-part-2-relevance-isn-t-luck).
- **Context**: Facets ("price between $20-50", "color: red", "in stock
  only") are typically implemented as separately-aggregated counts
  alongside the result set, computed at query time. The relevance
  ranking and the facet aggregation are two passes over the same
  result set, not two separate queries. Algolia, Elasticsearch, Meili,
  and OpenSearch all use roughly the same model.
- **Implication for CritterMart**: Out of scope for round one (no
  faceted browse). The architectural note for future scope: if a
  faceted browse slice ever lands, the right shape is a separate
  read-model projection of the Catalog document store, not a query
  against the document store directly. Marten's projections are
  well-suited to this — the projection rebuilds when the schema
  changes, and the storefront's facet counts come from an indexed
  read model rather than full table scans of products.

---

### Performance and Conversion

#### A 100ms improvement is measurable money

- **Source**: Google web.dev, "Milliseconds Make Millions" (study by 55
  and Deloitte, 2019); referenced extensively across web-performance
  literature. [link](https://web.dev/case-studies/milliseconds-make-millions).
- **Context**: The Deloitte study tracked 37 European and American
  brand sites across more than 30 million sessions. A 0.1-second
  improvement across four metrics (First Meaningful Paint, Estimated
  Input Latency, Page Load Time, Time to First Byte) produced
  measurable progression-rate improvements at almost every step of
  the funnel for both retail and lead-generation sites. The headline
  number cited often elsewhere — a 1-second delay costing 7% of
  conversions — comes from the same study.
- **Implication for CritterMart**: CritterMart will not ship to real
  users, so conversion impact is theoretical. The lesson for the
  talk is that performance is not a separate engineering concern
  from commerce architecture — it is the architecture. The
  storefront-rendering rewrite that Shopify did (next lesson) is
  motivated by exactly this number.

#### Storefront reads should not share a database with checkout writes

- **Source**: Maxime Vaillancourt, "How Shopify Reduced Storefront
  Response Times with a Rewrite" (Shopify Engineering, 2020).
  [link](https://shopify.engineering/how-shopify-reduced-storefront-response-times-rewrite).
- **Context**: Shopify extracted the storefront-rendering code from
  the Rails monolith into a separate application and pointed it at
  dedicated read replicas. The result: average server-side response
  times dropped to about a quarter of the legacy implementation. The
  architectural property doing the work: separation of read load
  (storefront, catalog browsing) from write load (checkout, admin,
  API). The storefront does not share connection pools, query
  optimizers, or cache eviction patterns with the systems that have
  to be strongly consistent.
- **Implication for CritterMart**: At three services and a shared
  PostgreSQL, this is not where CritterMart lives. The lesson to
  carry forward: when the Long Road section eventually splits a
  separate BFF or storefront-renderer out of the current Catalog
  service, the right axis to cut on is read-vs-write load, not "BFF
  for mobile vs BFF for web." The Shopify storefront-rendering
  service is not a BFF — it is a read-side service with its own
  data store.

---

### Observability

#### Distributed checkout is debuggable only via traces

- **Source**: Charity Majors (CTO of Honeycomb), via Pragmatic Engineer
  interview "Observability: the present and future" (2025) and
  Majors / Fong-Jones / Miranda, *Observability Engineering* (O'Reilly,
  2022). Susan Fowler, *Production-Ready Microservices* (O'Reilly,
  2016) makes the same argument in a microservices framing.
- **Context**: Once a single business action (place order) crosses
  more than one service boundary, logs and metrics stop being enough.
  Logs are scattered across services; metrics aggregate away the
  identity of the request. A trace ties the spans together with a
  shared trace ID, so an engineer can see "Order received → stock
  reserved on Inventory in 12ms → payment authorized in 800ms →
  Order confirmed" as a single causal chain. Majors's framing in the
  Pragmatic Engineer interview is that "observability is about the unknown unknowns" — the unanticipated failure modes that don't have a pre-built dashboard.
- **Implication for CritterMart**: The vision already commits to
  OpenTelemetry tracing visible in the .NET Aspire dashboard. This
  is the single most important talk artifact — the Place Order trace
  spanning Orders → RabbitMQ → Inventory → Orders is the
  observability slide of the talk, and it is exactly what Majors
  argues is the only debugging tool that scales in a distributed
  system. Worth ensuring the trace shows the *outbox handoff* as a
  span, not just the broker hop, so the audience can see why the
  outbox earns its slot in the architecture.

#### High-cardinality wins over pre-aggregated metrics for "why did *this* fail?"

- **Source**: Charity Majors, *Observability Engineering* (O'Reilly,
  2022); same author's blog at `charity.wtf`.
- **Context**: A pre-aggregated metric ("checkout success rate, by
  region") tells you that something is wrong. A high-cardinality
  event stream ("for this trace, with this customer ID, this order
  ID, this payment provider, this device fingerprint, here are all
  the spans and their attributes") tells you what was wrong. Majors's
  argument is that the metrics layer is the wrong shape for
  debugging modern systems because it aggregates away the identity
  of the failing request.
- **Implication for CritterMart**: OpenTelemetry's span attributes
  give CritterMart this for free, but only if handlers add the
  identifiers (order ID, customer ID, cart ID) to their spans
  proactively. Worth a skill file or rule entry: every handler that
  enters a business flow should add the relevant identifiers to the
  active span, so that traces can be filtered by them in the Aspire
  dashboard or Jaeger or whatever surfaces them. This is also a
  good acceptance criterion for the talk: filtering the trace view
  by order ID should show only that order's spans, end to end.

---

### Resilience and Scaling

#### Game days and load tests are how you find out before BFCM

- **Source**: ByteByteGo, "How Shopify Prepares for Black Friday" (2025)
  summarizing Shopify's published preparation pipeline; Shopify
  Engineering, "Performance Testing At Scale — for BFCM and Beyond"
  (2023); same blog, "Preparing Shopify for Black Friday and Cyber
  Monday" (2018). [link](https://shopify.engineering/scale-performance-testing).
- **Context**: Shopify's BFCM-readiness program runs for roughly nine
  months and includes scheduled game days that intentionally inject
  failures into production, five major scale tests, capacity planning
  by service, and a feature/code freeze in the weeks before the
  weekend. The point is not the specific numbers (which scale beyond
  anything CritterMart will touch) but the discipline: critical
  journeys are tested *as if they would fail*, not certified by
  uptime statistics.
- **Implication for CritterMart**: CritterMart will never see a flash
  sale. The lesson is structural rather than operational: the Place
  Order critical journey deserves an explicit failure-mode test
  suite even at conference-demo scale. Specifically: what happens
  if RabbitMQ is briefly unreachable when the Order handler tries to
  publish? What happens if the Inventory BC's database is
  unreachable when the stock-reservation handler runs? Both should
  produce a recoverable retry, not a confused customer experience.
  The retrospective for the Place Order slice should explicitly
  check these.

#### Flash sales need priority queues with fairness, not raw rate limits

- **Source**: Shopify Engineering, "Surviving Flashes of High-Write
  Traffic Using Scriptable Load Balancers" Parts I and II (2017).
  [link](https://shopify.engineering/surviving-flashes-of-high-write-traffic-using-scriptable-load-balancers-part-i).
- **Context**: Shopify's first attempt at protecting the platform
  during flash sales was a simple rate limit at the load balancer
  returning 429s. The problem was that this prioritized whoever
  happened to hit the endpoint first, not whoever had been waiting
  longest. The redesign added a queue position (a signed cookie with
  a timestamp from when the user joined the queue) and gave higher
  priority to customers who had been waiting longer. The lesson
  generalizes: backpressure is the right shape, but pure rate
  limiting is the wrong implementation when fairness matters.
- **Implication for CritterMart**: CritterMart will not flash-sell.
  The architectural note: any system that needs to enforce admission
  control on a scarce resource (limited drops, ticket sales,
  giveaways) should be designed with fairness as a first-class
  property, not as a tweak on top of a rate limit. This is one of
  the harder problems in commerce engineering and warrants its own
  ADR if a "scarce drop" slice ever lands.

#### Leaky bucket is the right shape for self-imposed limits

- **Source**: Shopify Developer Docs, "REST Admin API rate limits" and
  "Shopify API rate limits" (canonical, well-cited).
  [link](https://shopify.dev/docs/api/admin-rest/usage/rate-limits).
- **Context**: Shopify exposes the same leaky-bucket model to API
  consumers that they use internally. The bucket has a fixed capacity
  (40 requests for standard REST, 400 for Plus) and a leak rate (2
  per second standard, 20 for Plus). The model accommodates bursts
  but enforces a long-run average. The fairness property comes from
  the per-tenant bucket isolation.
- **Implication for CritterMart**: Out of scope today. The note for
  future scope: when CritterMart eventually exposes a public API
  (admin tooling, partner integrations), the leaky-bucket model is
  the right starting point because it is well-understood by every
  e-commerce integrator on earth.

#### Pods isolate blast radius — at a scale CritterMart does not have

- **Source**: Shopify Engineering, "A Pods Architecture To Allow Shopify
  To Scale" (2018); "Shard Balancing: Moving Shops Confidently with
  Zero-Downtime at Terabyte-scale" (2021).
  [link](https://shopify.engineering/a-pods-architecture-to-allow-shopify-to-scale).
- **Context**: Shopify's pod architecture co-locates a subset of shops
  on an isolated set of databases (MySQL, Redis, Memcached) routed by
  a load-balancer component called Sorting Hat. The architectural
  benefit is blast-radius isolation: a database failure affects only
  one pod's shops, not the whole platform. This works because Shopify
  is multi-tenant; the shopId-based sharding key is natural.
- **Implication for CritterMart**: CritterMart is single-tenant
  (single seller). Pods are not applicable. The lesson is
  *negative*: when the talk's audience is a developer with a single
  brand storefront, the Shopify pods story is the wrong reference
  architecture to copy. It only earns its complexity when the
  tenancy axis (per-merchant) gives you a natural shard key and
  the failure-isolation benefit pays off.

---

### Architecture and Modularity

#### Monolith-first remains the right default

- **Source**: Martin Fowler, "MonolithFirst" (martinfowler.com, 2015).
  [link](https://martinfowler.com/bliki/MonolithFirst.html).
- **Context**: Fowler's argument, paraphrased: nearly every successful
  microservices system started as a monolith that grew into separate
  services, and nearly every system architected as microservices
  from scratch ended up in trouble. The reasoning is YAGNI plus the
  difficulty of drawing good bounded-context boundaries before the
  domain is stable. Stefan Tilkov published a counter-argument
  ("Don't start with a Monolith") on Fowler's own site within ten
  days, arguing that modularity is hard to enforce inside a monolith.
  The debate is unresolved, but the empirical pattern Fowler points
  to is stubborn.
- **Implication for CritterMart**: CritterMart starts with three
  services because the *teaching purpose* requires showing cross-BC
  messaging in the talk. This is the rare case where the
  "microservices-first" path is justified — but the justification is
  pedagogical, not architectural. Worth being explicit in the talk:
  CritterMart is split because the talk requires the split, not
  because three services is the right answer for a real
  single-seller storefront at this scale. The real answer for a real
  storefront is probably a modular monolith, à la Shopify.

#### Modular monolith is the Shopify-scale answer that didn't get the press

- **Source**: Kirsten Westeinde, "Deconstructing the Monolith: Designing
  Software that Maximizes Developer Productivity" (Shopify Engineering,
  2019); Philip Müller, "Under Deconstruction: The State of Shopify's
  Monolith" (Shopify Engineering, 2020). LeadDevNewYork 2019 talk by
  Westeinde with the same title.
  [link](https://shopify.engineering/deconstructing-monolith-designing-software-maximizes-developer-productivity).
- **Context**: Shopify's core codebase is one of the largest Ruby on
  Rails codebases in existence — over 2.8 million lines of Ruby,
  500,000 commits, decade-plus of investment. Faced with the
  productivity problems of a flat monolith, Shopify did *not*
  decompose into microservices. They chose a modular monolith:
  enforced component boundaries within a single deployable, with
  tooling (Packwerk) to enforce static constant references. The
  follow-up post 18 months later reports a mindset shift in
  developers toward modular design without the operational cost of
  splitting into services.
- **Implication for CritterMart**: This is the pattern most real
  single-seller storefronts should land on. CritterMart's
  three-service split is a teaching device. The honest narrative
  for the talk: "If you have one team and one storefront, you
  probably want a modular monolith. I'm building three services
  because I need to show the cross-BC messaging story; you may not
  need to." This is the kind of intellectual honesty that lands
  with senior audiences and avoids selling microservices as a
  default.

#### Backend-for-frontend exists because mobile and web want different APIs

- **Source**: Sam Newman, "Backends For Frontends" (samnewman.io, 2015,
  crediting Phil Calçado for the term at SoundCloud); Chris Richardson,
  *Microservices Patterns* (Manning, 2018) on the same topic.
  [link](https://samnewman.io/patterns/architectural/bff/).
- **Context**: A single backend serving every client surface (web,
  mobile native, mobile web, kiosk) tends to accumulate
  one-size-fits-no-one endpoints. The BFF pattern gives each client
  type its own backend that aggregates downstream services for that
  client's specific needs. The BFF is owned by the same team as
  the client, which collapses cross-team coordination into a single
  team's pull request.
- **Implication for CritterMart**: The vision defers a BFF to the
  Long Road, exposing Wolverine.Http surfaces from each service
  directly in round one. This is fine for a single-storefront demo,
  but is exactly the kind of decision that should be revisited once
  more than one client surface exists. The lesson is that the
  *team* boundary, not the technology, is what makes BFFs work —
  a BFF maintained by a backend team that doesn't ship the client
  is a worse API gateway, not a better one.

#### Sorbet at Stripe shows that strong typing pays for itself at scale

- **Source**: Jake Zimmerman, "Sorbet: Stripe's type checker for Ruby"
  (Stripe.dev blog, 2022); Gergely Orosz, "Inside Stripe's Engineering
  Culture" Part 1 (Pragmatic Engineer, 2023).
  [link](https://stripe.dev/blog/sorbet-stripes-type-checker-for-ruby).
- **Context**: Stripe built and open-sourced Sorbet specifically because
  the cost of dynamic typing on a 15-million-line Ruby codebase had
  become unbearable. The lesson generalizes: at sufficient scale,
  static type information is the cheapest form of API documentation
  and the cheapest form of refactoring safety.
- **Implication for CritterMart**: C# 14 / .NET 10 already pays this
  cost. The transferable lesson for the talk is that the contracts
  between BCs (Wolverine messages, Marten events) are *first-class
  type-checked artifacts* in the Critter Stack, and that is one of
  the reasons cross-BC integration is tractable. The slide is "every
  cross-service message is a type-checked record, every projection
  is a type-checked function from events to state." This is a
  significant advantage over the Ruby monolith path that Shopify
  and Stripe were stuck with.

---

### Trust, Safety, and Compliance

#### Tokenization minimizes PCI scope but does not eliminate it

- **Source**: Stripe Docs, "A guide to PCI compliance" (canonical);
  AWS Security Blog, "How to use tokenization to improve data
  security and reduce audit scope" (Apr 2023 update). Both are
  authoritative on PCI scoping.
- **Context**: Even with full tokenization via Stripe Elements,
  CritterMart-style storefronts would still need to fill in some PCI
  form (typically SAQ A or SAQ A-EP). Tokenization shrinks the
  *technical* PCI footprint, but the operational and policy
  obligations remain. The trap is assuming "no PAN on our servers =
  no PCI work."
- **Implication for CritterMart**: Not in scope for round one
  (payment is stubbed). The note for any future real integration:
  PCI scope decisions belong in an ADR before the integration
  ships, and the ADR should specify which SAQ applies, not just
  which tokenization SDK is used.

---

### Internationalization

#### Tax is a domain concern, not a platform plumbing concern

- **Source**: Stripe Tax product documentation and the Stripe newsroom
  announcement "Stripe launches Stripe Tax" (2021); the follow-up
  blog post "Stripe Tax: An all-in-one global tax compliance solution"
  (2025). [link](https://stripe.com/blog/stripe-tax-an-all-in-one-global-tax-compliance-solution).
- **Context**: Stripe's published rationale for building Stripe Tax is
  that tax calculation requires both correct rates per jurisdiction
  *and* awareness of where the business has economic nexus (and thus
  is obligated to collect). The product crosses 50+ countries and
  every US state. The architectural insight buried in the product
  announcement: tax cannot be a thin function `(amount, jurisdiction) → tax_amount`. It is a stateful service that knows about the
  seller's nexus, the buyer's location, the product classification,
  and the transaction history.
- **Implication for CritterMart**: Tax is not in round-one scope.
  The architectural note: if it ever lands, it lives in its own BC
  (or as a federated service like Stripe Tax / Avalara / TaxJar),
  *not* as a calculated field on the Order. The Order should carry
  a tax line that is the result of a lookup, not a computation.
  Bundling tax-rate logic into the Order aggregate would be a
  cross-context concern leaking inward, which is exactly the kind
  of decision DDD bounded-context rules are designed to prevent.

---

### Migration Stories

#### Most tools survive one order of magnitude of growth, then need replacement

- **Source**: Will Larson, *An Elegant Puzzle: Systems of Engineering
  Management* (Stripe Press, 2019).
- **Context**: Larson's framing in the book, paraphrased: most tools and processes support about one order of magnitude of growth before they need to be replaced. This is not a sign that the original tool was badly designed — it is a sign that the original tool was *appropriately* designed for its prior constraints. The conclusion is that migration is a permanent state of being, and the managerial / architectural skill that matters is making migrations cheap, not avoiding them.
- **Implication for CritterMart**: CritterMart will not grow ten-fold
  during the talk. The structural lesson: design the Place Order
  flow so that the components can be replaced without rewriting the
  flow. RabbitMQ to Azure Service Bus, stubbed payment to Stripe,
  shared Postgres to per-service Postgres, document Catalog to
  event-sourced Catalog — each of these is a swap, not a rewrite.
  This is also Erik's "Swapping the Bus" blog idea, and the talk
  can call out the lesson while staying single-broker.

#### Etsy's Vitess migration: a 16-year-old sharding scheme can be modernized incrementally

- **Source**: Etsy Code as Craft, "Migrating Etsy's database sharding to
  Vitess" (2026); InfoQ summary article (2026).
  [link](https://www.etsy.com/codeascraft/migrating-etsyas-database-sharding-to-vitess).
- **Context**: Etsy maintained a custom MySQL sharding architecture
  since around 2010 — approximately 1,000 shards, 425 TB, ~1.7M
  RPS, with proprietary sharding logic in an in-house ORM. Migrating
  this to Vitess was a multi-year effort. The architectural note in
  the Etsy post: the migration moved shard routing out of the ORM
  and into Vitess via vindexes, which then unlocked features like
  resharding and sharding previously-unsharded tables. The shape
  matters: the migration was *additive*, not a big-bang rewrite.
- **Implication for CritterMart**: CritterMart will not shard. The
  transferable lesson is about migration *shape*, not sharding.
  When the Long Road decisions arrive (promoting Identity to a
  real service, async projections for replay, BFF extraction), the
  Etsy pattern is the right template: introduce the new layer
  alongside the old, route a fraction of traffic, validate, then
  cut over. This is something the talk can briefly mention as a
  more general engineering discipline that the Spec-Driven
  Development pipeline supports — every slice is additive by
  construction.

#### Redis → MySQL: the database was the right answer all along, but only after the connection-pool fix

- **Source**: Shopify Engineering, "We replaced Redis with MySQL for
  inventory reservations — and it scaled" (2026).
  [link](https://shopify.engineering/scaling-inventory-reservations).
- **Context**: Shopify moved their oversell-protection reservation system
  off Redis and onto MySQL. The interesting part is not the
  destination (MySQL is sufficient for the workload) but the path:
  the team spent weeks optimizing queries and locks before
  discovering that the actual bottleneck was connection saturation
  in code paths the reservation system didn't even own. The
  reservation system had to behave well as a *neighbor* to cart
  updates, payment processing, and order creation, all of which
  share the database.
- **Implication for CritterMart**: This is the single most relevant
  migration story in the corpus for CritterMart's actual
  architecture. Shared PostgreSQL with schema-per-service has the
  same neighbor-isolation problem in miniature. The talk does not
  need to dwell on this, but the architectural note for any future
  retrospective on the Place Order slice is: when measuring
  performance, measure end-to-end *while other traffic is running*,
  because the failure mode is rarely in the system being measured.

---

## Cross-Cutting Themes

Reading across the lessons, a few patterns repeat across authors and
companies:

1. **Idempotency is the contract that makes retries safe, and retries
   are the contract that makes distributed commerce reliable.**
   Idempotency keys (Brandur / Stripe), at-least-once webhook semantics
   (Stripe), the transactional outbox (AWS, Chris Richardson), saga
   compensation (Verraes, Richardson), and Shopify's reservation-system
   design all point to the same property: every handler must be safe
   to invoke many times with the same logical input. CritterMart
   inherits this from Wolverine but the discipline has to extend to
   every handler the team writes.

2. **The interesting state is the transition history, not the current
   state.** Klefter's translation-decision events, Uber's statecharts,
   Verraes's event-sourcing-as-functional-folding, Greg Young's CQRS
   talks, and even Stripe's idempotency-key-as-state-machine all
   reach the same conclusion: storing the current value is cheap and
   easy and not what you want when you have to debug, audit, or
   evolve. CritterMart's choice to event-source Inventory and Orders
   while keeping Catalog as CRUD documents is the right shape because
   it makes the contrast a teaching point rather than a hidden
   trade-off.

3. **Separation by load type beats separation by service count.**
   Shopify's storefront-rendering split (reads from replicas, writes
   to primaries), Etsy's read-replica strategy, the BFF pattern for
   client-shape divergence, and the modular monolith argument all
   converge on a single principle: separate things that have
   different consistency, latency, or load requirements, not things
   that have different deployable names. CritterMart's
   three-service split is justified pedagogically, not by this
   principle. Future splits should be.

4. **Migration discipline is the second-order skill that matters.**
   Larson, Westeinde, the Etsy Vitess team, Shopify's reservation-system
   team, and even Stripe's Sorbet adoption all describe the *shape*
   of the migration more than the destination. The destinations are
   ordinary (MySQL, modular monolith, Vitess, typed Ruby). The
   shapes — additive, validated incrementally, kept reversible —
   are what made them succeed where similar efforts elsewhere fail.
   CritterMart's Spec-Driven Development pipeline is a per-slice
   embodiment of the same discipline.

5. **The hard problems are at the seams.** Cart-to-Order, Order-to-Inventory,
   Order-to-Payment, Order-to-Fulfillment, Webhook-to-Domain. Every
   author cited has a war story about the seam, not about the entity
   on either side of it. CritterMart's Event Modeling pipeline is
   specifically tuned to make seams visible (cross-BC events on the
   Order's stream, OutgoingMessages for translation decisions), and
   that is the strongest defensible justification for the pipeline
   in the talk.

---

## Open Questions for the Team

The research surfaced a small number of questions worth a discussion
or an ADR before the talk delivery:

1. **Should the talk explicitly contrast PM-via-Handlers with Wolverine's
   `Saga` base type?** Both are defensible. The vision picks PMvH for
   teaching reasons. Worth a slide that names the alternative,
   acknowledges the trade-off, and points to where Saga is the better
   call (no aggregate owns the lifecycle).

2. **Is the inline `StockLevelView` projection the right call, or should
   it be async with cache?** The reservation-as-MySQL Shopify story
   suggests that database-level reservation works at small to medium
   scale. CritterMart's choice (Marten projection, inline) is fine
   for a demo but worth a sentence in the talk explaining why the
   neighbor-isolation argument (Shopify) doesn't bite at three
   aggregates and a single PostgreSQL.

3. **How does the Place Order trace surface the outbox handoff?**
   Specifically, is there a span in the OpenTelemetry trace that
   shows "command handled, events written, outgoing messages
   durably staged for publish" as a single transaction? This is
   the architectural property that makes the rest of the story
   work, and it deserves to be visible in the demo.

4. **Where does the idempotency-key discipline get codified?** A
   skill file under `docs/skills/`? A rule in
   `docs/rules/structural-constraints.md`? The lesson is important
   enough that it should not depend on every prompt remembering it
   independently.

5. **Is there value in writing a future "Catalog as event-sourced"
   slice to contrast against the round-one document-store Catalog?**
   The teaching value of Greg Young's "event-sourcing where it
   doesn't pay rent" argument is highest when the audience has seen
   both shapes. Maybe a Long Road blog post, not a code slice.

6. **Should the talk explicitly disclaim that the three-service split
   is pedagogical, not architectural?** Strongly recommend yes.
   The honesty lands well with senior audiences and pre-empts the
   "why didn't you do a modular monolith?" question that *will*
   come up after the talk.

---

## Appendix A: Sources Considered and Rejected

Several sources looked promising but were excluded. Each is noted with
the reason, as an editorial audit trail.

- **Generic "Top 10 cart abandonment fixes" listicles**. Multiple
  Medium and SaaS-vendor posts in the search results matched the
  LLM-generated pattern: no concrete numbers, no war stories, no
  named author with verifiable other work. Excluded across the
  board.

- **"Event Sourcing Looked Perfect in the Book. Production Was a
  Nightmare." (Medium, Production Nightmares, 2026)**. The article
  is well-written and contains specific war stories, but the
  author is pseudonymous and has no other discoverable engineering
  footprint. The lessons it surfaces are real (event sourcing has
  failure modes), but they are covered more authoritatively by
  Greg Young's "Why Event Sourced Systems Fail" talk, which is
  cited instead.

- **Webhook-vendor blog posts (EventDock, HookListener, HookRay,
  WebhookWatch, Salable)**. These contain accurate technical
  content but are SaaS-vendor product marketing. Where they
  paraphrase Stripe's documented behavior they are useful for
  triangulation, but I cite Stripe's own docs and Brandur Leach's
  posts directly instead.

- **Pipiads "Scaling Shopify's Multi-Tenant Architecture: Secrets
  Unveiled"**. The content is accurate but is a secondary summary
  of Shopify's primary engineering posts, which are cited
  directly instead. Pipiads has no engineering footprint of its
  own that I could verify.

- **Algolia general-marketing blog posts on e-commerce search
  KPIs**. The benchmarks are useful for context but read as
  product marketing rather than engineering. The engineering
  side of the Algolia blog (cited inline) is a different and
  more substantive section.

- **AI agent and agentic search posts (Algolia 2026, Stripe Radar
  "AI-enabled fraud")**. Recent and possibly important, but
  outside the scope of a single-seller storefront's near-term
  decisions. Noted as future reading.

- **Stack Overflow answers and Shopify Community forum threads**.
  Cited only when they pointed to a primary source (e.g., the
  Shopify rate-limit forum thread that links to the Shopify
  engineering blog post).

- **LinkedIn posts by Gergely Orosz**. Used to corroborate
  Pragmatic Engineer newsletter content but not cited
  independently — the newsletter is the authoritative venue.

- **System-design-interview content**. Most "Scaling for Success:
  Shopify's System Design During Black Friday" style posts on
  LinkedIn and Medium are derivative summaries of the same
  Shopify engineering blog corpus, frequently with details
  added that do not appear in any primary source. Treated as
  potential signal for what to search for next; never cited
  directly.

- **Wikipedia articles on Gumroad, Sylius, etc.** Used for
  background context (who Sahil Lavingia is, when Sylius was
  founded). Not cited as evidence for engineering claims.

---

## Appendix B: Suggested Follow-Up Reading

Sources that didn't directly yield a numbered lesson but are worth
bookmarking for later rounds, in rough order of relevance:

- **Susan Fowler, *Production-Ready Microservices* (O'Reilly, 2016)**.
  Sixty-page production-readiness checklist drawn from standardizing
  1,000+ services at Uber. The "Stability and Reliability" and
  "Fault Tolerance and Catastrophe Preparedness" chapters are the
  most relevant if CritterMart ever extends to multiple
  deployments. The book is somewhat dated on specific tooling but
  the principles are durable.

- **Charity Majors, Liz Fong-Jones, George Miranda, *Observability
  Engineering* (O'Reilly, 2022)**. The book-length argument behind
  the "high-cardinality" lesson cited above. Worth reading before
  the talk so the observability slide can be informed by the
  underlying philosophy rather than just the OTel tooling.

- **Chris Richardson, *Microservices Patterns* (Manning, 2018)**.
  Canonical reference for saga, outbox, CQRS, and event-sourcing
  patterns. Useful for cross-checking the vocabulary in the talk
  with the dominant industry-standard naming.

- **Adam Tornhill, *Your Code as a Crime Scene* (Pragmatic
  Bookshelf, 2015)**. Less directly relevant to event sourcing but
  the canonical reference for thinking about codebases as
  evolutionary artifacts. The lesson "where the change happens" is
  the same insight that drives bounded-context placement decisions.

- **Vaughn Vernon, *Implementing Domain-Driven Design* (Addison-Wesley,
  2013)**. Still the most-cited reference for translating DDD strategic
  patterns (bounded context, context map) into code-level structure.
  Skip if you've read it; reread the aggregate and context-map
  chapters if you haven't recently.

- **Eric Evans, *Domain-Driven Design* (Addison-Wesley, 2003)**. The
  original. The "Context Maps" chapter is the foundation for every
  bounded-context decision in CritterMart, CritterBids, and CritterCab.

- **Greg Young, "Versioning in an Event Sourced System" (Leanpub
  e-book, ongoing updates)**. The canonical reference for event-schema
  evolution. Will become relevant the first time CritterMart needs
  to evolve a domain event without breaking projection rebuilds.
  Not needed for round one.

- **Stripe Press, *An Elegant Puzzle* (Will Larson, 2019)**. Cited
  above for the migration-as-way-of-life argument. The chapters on
  organizational structure and engineering-team sizing are
  tangential to CritterMart but useful for the speaking-kit and
  consulting contexts.

- **Shopify Engineering blog archive**. The pieces cited above are
  the highest-value subset. The full archive contains more than 600
  posts going back to 2010 and is the largest single corpus of
  e-commerce engineering writing on the public internet.

- **Etsy Code as Craft archive**. Worth periodic browsing. The
  search, experimentation, and frontend-engineering subsections are
  particularly strong.

- **Stripe.dev blog**. The newer Stripe blog (separate from
  `stripe.com/blog`) is more engineering-deep and less product-marketing.
  Subscribe to keep up with new posts.

---

## Document history

- **v0.1** (2026-05-26): Initial research pass. Primary sources:
  Shopify Engineering, Stripe blog, Brandur Leach, Etsy Code as
  Craft, Martin Fowler, Mathias Verraes, Greg Young, Charity
  Majors, Will Larson, Susan Fowler, Sam Newman, Chris Richardson,
  Gergely Orosz, Google web.dev. Curated into 30 lessons across
  12 categories with explicit CritterMart-applicability notes,
  cross-cutting themes, and open questions for the team.
  Intended to complement `docs/vision.md` and inform the
  Event Sourcing with Marten talk and the round-one slice
  retrospectives.
