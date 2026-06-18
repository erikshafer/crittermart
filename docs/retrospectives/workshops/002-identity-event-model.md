---
retrospective: 002
kind: workshop
prompt: docs/prompts/workshops/002-identity-event-model.md
deliverable: docs/workshops/002-identity-event-model.md
date: 2026-06-18
mode: solo multi-persona
session-runner: Claude (Opus 4.8)
---

# Retrospective — Workshop 002: CritterMart Identity Bounded Context (Spike Promotion)

## Outcome summary

Authored `docs/workshops/002-identity-event-model.md` (v1.0) and amended `docs/context-map/README.md` to promote Identity from a stubbed **Conformist** relationship to a deployed **Open-Host Service + Published Language** BC. The workshop models 4 slices: **5.1 Register a customer** and **5.2 Resolve a customer** (both realized by the EF-Core spike on `spike/efcore-identity` @ `0ffe42e`), and **5.3 Resolve identity from `X-Customer-Id`** and **5.4 Consume `CustomerRegistered`** (the future OHS/PL integration roadmap, modeled-not-built). Identity stays a DATA STORE, not auth (ADR 009). This session is design only — the spike code re-lands on `main` later via a per-slice OpenSpec proposal → narrative → implementation prompt. The session also discharges the design-return interleave the cadence rule had flagged as due.

## What worked

- **The spike as a reference implementation, not a fait accompli.** Modeling the BC *after* a working spike meant slices 5.1/5.2 describe behavior already proven live (register/read/404, schema-per-service, the EF outbox publishing `CustomerRegistered` unconsumed, captured in the spike's live verification). The workshop documented observed shapes rather than guessing — while still owning the design decisions the spike deferred (the duplicate-email guard; the OHS/PL split).
- **The OHS-for-frontend / PL-for-backends split.** The Architect voice's load-bearing insight: `GET /customers/{id}` (Open-Host Service) is for external/storefront callers; `CustomerRegistered` (Published Language) is for cross-BC consumers, because ADR 001 forbids sync service-to-service HTTP. This reconciles the owner's OHS+PL choice with the no-sync-HTTP rule and gives slices 5.3/5.4 a correct shape (resolve via a local read model fed by the PL event, never a sync call into Identity).
- **Naming the non-event-sourced contrast.** Identity is the only BC where the event is NOT the source of truth. Framing `CustomerRegistered` as an outbound notification (published via the EF outbox) rather than a stream event is the teaching payoff — and the "no projection; the row IS the read model" section is the counterpart to Catalog's "CRUD is fine" thread.

## What was harder than expected

- **Where slices 5.3/5.4 belong.** They are Identity's integration slices, but their *code* lands in CONSUMING BCs (a local read model + header resolution in, e.g., Orders). Modeling them under the Identity workshop — as the BC that PUBLISHES the contract — while noting the consumer-side ownership took care; the slice-table BC column reads `Identity → consumer` to capture it.
- **Keeping the Polecat/auth concern separate.** The context-map Long-road note conflated "a real Identity service" with "via Polecat / Customer-Supplier." Promoting an EF-Core *registry* required splitting those: this promotion is OHS/PL and explicitly NOT auth (ADR 009); Polecat-backed authentication remains a distinct long-road item. The amendment had to supersede the old note without erasing the auth concern.
- **Honest deployment status in the context map.** The code is on a spike branch, not `main`. The map had to describe the promotion (decided, modeled, spiked) while being honest that the OHS/PL traffic is *declared, not yet trafficked* — the spike publishes `CustomerRegistered` unconsumed. Mirrored how the old map drew the Conformist edges (dashed, "no active integration").
- **Slice numbering.** Identity is the 5th BC bucket (Catalog=1, Inventory=2, Cart=3, Order=4), so its slices are 5.x — chosen to extend the global slice space coherently rather than restarting at 1.x inside a second workshop file.

## Methodology refinements that emerged

1. **A spike can legitimately precede its workshop.** The pipeline's two-phase shape tolerates a throwaway inverting the order, PROVIDED the promotion authors the design layer before the code lands on `main`. This is the first time CritterMart ran model-after-spike; it worked because the spike was explicitly throwaway and the workshop re-establishes the trace. Worth keeping as a sanctioned path for genuinely exploratory work.
2. **A second workshop for a new BC (vs. amending 001) is the right call** when the BC is genuinely new and self-contained — 001 stays the round-one rolled-up record; 002 owns Identity. The slice-number space stays global (5.x) so cross-references remain unambiguous.

## Outstanding items / next-session inputs

These flow downstream into the per-slice chain that re-lands the spike code on `main`.

1. **First slice to author: 5.1 (`RegisterCustomer`).** Author its OpenSpec proposal + a narrative + an implementation prompt that re-land the spike code on `main`, resolving the duplicate-email guard the spike skipped. The spike on `spike/efcore-identity` is the reference; cherry-pick or re-apply its code under the proposal.
2. **Capability name.** Per CLAUDE.md's one-capability-per-aggregate rule, Identity carries one capability — propose `customer-registry` (or `customer-identity`). Settle in the proposal session.
3. **5.3/5.4 are future increments** — they land when a consumer genuinely needs customer data (e.g., Orders enriching `OrderStatusView` with a display name). Not this chain.
4. **A possible new ADR.** "Identity as an EF-Core data store (not Polecat, not auth)" may earn an ADR by the CLAUDE.md threshold (it spans the context map; the next contributor would re-derive the data-store-not-auth boundary). Flagged, not authored — the context-map amendment + ADR 009 carry it for now; promote to an ADR if the proposal session finds it re-litigated.
5. **No `docs/skills/` entry triggered.** The spike defers to the upstream `critterstack-arch-new-project-wolverine-efcore` skill; the one CritterMart-specific wrinkle (the Weasel-vs-EF column-casing reconciliation) is captured in the spike commit, not a local skill — promote only if a second EF-Core service surfaces the same convention.

## Spec-delta — landed?

**Yes.** The prompt named four things:

1. `docs/workshops/002-identity-event-model.md` created — **landed** at v1.0 with all sections (slices 5.1–5.4, the `CustomerRegistered` vocabulary, the read-model-is-the-row section).
2. `docs/context-map/README.md` promotes Identity to a deployed OHS + Published Language BC — **landed** (the Identity bullet, the topology diagram, the relationships-table row Conformist → OHS/PL, the round-one-stubs line, and the Long-road note split from the Polecat/auth concern).
3. The Identity BC gains an authoritative event model the downstream OpenSpec/narrative/implementation chain will reference — **landed** (slices 5.1–5.4; the `CustomerRegistered` naming authority).
4. `docs/workshops/` gains its second workshop and the design-return counter resets — **landed** (README current-population bumped 1 → 2).

No spec-delta items were dropped or downscoped.

## Process notes

- One prompt, one session, one PR — the PR contains the workshop, the context-map amendment, the workshops-README population bump, and this retro: the design-return's named deliverables, nothing else. The spike branch is untouched (the code lands via its own later chain). No opportunistic edits; Workshop 001 was read but not modified.
- No code committed. This is design.
- Workshop Document History stamped v1.0 per CLAUDE.md § 4b. The context map carries no `version` field — it is amended in place per its own convention.
