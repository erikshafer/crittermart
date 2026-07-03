---
narrative: 009
title: The Customer Changes Their Email
actor: Customer
status: draft
version: v1.0
slices: [5.5, 5.6, 5.7]
references:
  - docs/workshops/002-identity-event-model.md (§ 4 saga state and saga messages, § 5 slices 5.5–5.7, § 6 GWT scenarios, § 8 items 7–10)
  - openspec/changes/slices-5-5-5-7-email-change-saga/ (sibling OpenSpec proposal + customer-registry SHALL delta)
  - docs/narratives/006-customer-registration.md (the same Customer; the registered `Customer` row this journey mutates)
  - docs/narratives/008-operator-replenish-backorders.md (the sibling saga journey — Saga #1, Marten-backed)
  - docs/decisions/009-polecat-deferred-for-round-one.md (Identity's second amendment — boring CRUD holds even with a saga)
  - docs/decisions/022-convention-sagas-additive-to-pmvh.md (the binding guard this saga designs against)
  - docs/research/wolverine-saga-feasibility.md (§ Candidate #2, the feasibility spike)
---

# Narrative 009 — The Customer Changes Their Email

The Customer from Narrative 006 registered once, with one email, and nothing in that journey let it change. Emails do change — a job, a household, a typo caught too late — and CritterMart's registry has had no way to follow along. This narrative is that gap closing: a customer asks to change their email, has a window to confirm they meant it, and the change either lands or quietly expires.

That window is the whole reason this journey needs a saga rather than a plain mutating command. Narrative 006's registration is a single step: ask, and it happens. This is two steps separated by *time* — ask, then (maybe) confirm — and something has to remember the ask until the confirm arrives or the clock runs out. That something is an **`EmailChange` saga**: CritterMart's *second* convention `Wolverine.Saga`, and the direct EF-Core-backed counterpart to Narrative 008's Marten-backed `Replenishment`. Two sagas, two different stores, exercising the identical Wolverine programming model — that contrast is deliberate, and it is this journey's teaching payoff, the same way Narrative 006 carried Identity's "row instead of a stream" contrast.

## Journey scope

The Customer's email-change journey threads three Workshop 002 slices, consolidated into one PR:

- **Slice 5.5 — Request.** Moment 1 below: asking to change an email, and the two ways that ask can be refused before any saga opens.
- **Slice 5.6 — Confirm.** Moment 2 below: confirming within the window, and the conflict where someone else claimed the pending email first.
- **Slice 5.7 — Timeout.** Moment 3 below: letting the window lapse without confirming.

## Moment 1 — Asking to change an email

**Context.** Ada Lovelace, registered in Narrative 006 with `ada@example.com`, has a new address and wants her account to reflect it: `ada.new@example.com`.

**Interaction.** The storefront issues `RequestEmailChange { customerId: "c-1", newEmail: "ada.new@example.com" }` to `POST /customers/c-1/email-change`.

**System response.** Two guards run before anything opens. If `c-1` isn't a registered customer at all, the request is refused (`CustomerNotFound`) — there is nothing to change an email *for*. If `ada.new@example.com` already belongs to someone else, the request is refused (`EmailAlreadyRegistered`) — the same duplicate-email guard Narrative 006 uses for registration, reused here rather than reinvented. Past both guards, an `EmailChange` saga opens, keyed by `c-1`, holding `PendingEmail: "ada.new@example.com"`. Opening it schedules an `EmailChangeTimeout` — the deadline Ada now has to come back and confirm. Nothing about her `Customer` row changes yet; `ada@example.com` is still what Identity will tell anyone who asks, until Moment 2 resolves one way or the other.

If Ada changes her mind mid-window and asks again for a different address, the *existing* saga updates — `PendingEmail` moves to the newest request — but the clock does not restart. This is a corrected design, not an oversight: an earlier draft of this saga let a re-request reschedule the deadline, and a workshop review caught that Wolverine cannot cancel an already-scheduled timeout, so the *original* one would still fire and drop the "reset" window early. The fix mirrors `Replenishment`'s own re-open rule (no second timeout on a re-detected shortfall) for the identical reason. Practically: a customer who changes their mind twice gets less time on the third attempt, not more — a small, honest trade-off named in Workshop 002 § 8 rather than hidden.

## Moment 2 — Confirming within the window

**Context.** The `EmailChange` saga for `c-1` is open, `PendingEmail: "ada.new@example.com"`, and the deadline hasn't passed.

**Interaction.** Ada confirms: `ConfirmEmailChange { customerId: "c-1" }` to `POST /customers/c-1/confirm-email-change`.

**System response.** Identity checks whether `ada.new@example.com` is still hers to claim — nothing else in the storefront froze it while she was deciding, so it's possible (if unlikely at demo scale) that someone else registered or changed into that exact address during the window. If it's still free, `Customer.Email` for `c-1` becomes `ada.new@example.com` and the saga completes — its state, having done its job, is deleted. If it was claimed out from under her, the confirmation is refused (`EmailChangeConflict`) and, notably, **the saga stays open** rather than dying on the spot: Ada isn't left with a dead end, she can still let the window lapse (Moment 3) or ask for a different address (which re-arms the saga per Moment 1's re-request path). A confirmation that arrives after the window already closed — the saga already gone, either from a prior confirm or a prior timeout — is a silent no-op; Identity has nothing left to say yes or no *to*.

## Moment 3 — Letting the window lapse

**Context.** The `EmailChange` saga for `c-1` is open, and Ada never confirms — perhaps she never saw whatever prompted the confirmation, perhaps she reconsidered without saying so.

**Interaction.** None from Ada — like Narrative 008's Moment 3, this Moment is driven by time, not a command. The scheduled `EmailChangeTimeout` fires.

**System response.** The saga is still open, so the pending change is dropped — `Customer.Email` for `c-1` stays exactly `ada@example.com` — and the saga completes and is deleted. There is no alert, no escalation to anyone; unlike `Replenishment`'s timeout (which pages an operator because an unreplenished SKU is *somebody's* problem to chase), an uncompleted email change is nobody's problem but Ada's, and she is free to simply ask again. If Ada *had* confirmed first, the saga would already be gone by the time this scheduled message arrives, and — exactly as `Replenishment`'s timeout does when a restock beats the clock — it finds nothing and does nothing, because Wolverine offers no way to reach back and cancel a message once scheduled.

## The second saga, a different store

Narrative 008 named the "third way to wait" — a Bruun temporal projection, Process Manager via Handlers, and a convention saga, side by side. This journey doesn't add a fourth way; it proves the third way travels. `Replenishment` keeps its state in **Marten** saga storage, deleted on `MarkCompleted()`. `EmailChange` keeps the identical shape of state — open, pending, gone-when-resolved — in a **plain EF-Core row** on `IdentityDbContext`, riding the same transactional wiring Narrative 006's registration already uses. Nothing about the `Wolverine.Saga` programming model — `StartOrHandle`, `Handle`, `MarkCompleted()`, the mandatory `NotFound` statics for a message that arrives too late — changes between the two. That is [ADR 022](../decisions/022-convention-sagas-additive-to-pmvh.md)'s guard in practice: a saga does relational or document things *the Wolverine way*, and which store sits underneath is a plumbing choice, not a modeling one.

## What the Customer does *not* yet see

- **No proof Ada controls the new inbox.** Confirming is "yes, I meant it," not "I received a code at the new address and typed it back." A real confirmation link or code is out of scope here — Identity performs no authentication ([ADR 009](../decisions/009-polecat-deferred-for-round-one.md)), and inventing one would be building an auth feature under cover of a saga demo.
- **No notification that an email changed.** Registration publishes `CustomerRegistered` so a future consumer can keep a local copy of a customer's details current; a successful email change publishes nothing analogous yet. Until some consumer actually needs to know, adding the event would be a shared contract nobody uses — the same restraint Narrative 006 named for `CustomerRegistered`'s early, consumer-less days.
- **No re-armed deadline on a re-request.** Named plainly in Moment 1: the window is anchored to the first ask, not the latest.

## Document History

| Version | Date       | Notes |
| ------- | ---------- | ----- |
| v1.0    | 2026-07-02 | Initial commit. Covers Workshop 002 slices 5.5 (Request — with the unknown-customer and duplicate-email guards), 5.6 (Confirm — with the stays-open-on-conflict rule), and 5.7 (Timeout — drop, no escalation). Threads CritterMart's **second convention `Wolverine.Saga`**, drawing the direct contrast with Narrative 008's Marten-backed `Replenishment`: identical saga programming model, different backing store, proving the store is swappable per [ADR 022](../decisions/022-convention-sagas-additive-to-pmvh.md). Names the re-request-doesn't-reset-the-deadline correction surfaced during workshop review. Authored as the human-readable sibling of the `slices-5-5-5-7-email-change-saga` OpenSpec change, consolidating all three slices plus implementation into one PR. |
