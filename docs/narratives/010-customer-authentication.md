---
narrative: 010
title: The Customer Signs In
actor: Customer
status: draft
version: v1.0
slices: [5.8, 5.9, 5.10, 5.11]
references:
  - docs/workshops/002-identity-event-model.md (§ 2 auth subsection, § 3 auth architect note, § 4 auth commands + the issued token, § 5 slices 5.8–5.11, § 6 GWT scenarios, § 8 items 13–17)
  - openspec/changes/slices-5-8-5-11-identity-authentication/ (sibling OpenSpec proposal + customer-registry SHALL delta)
  - docs/decisions/023-real-authentication-for-identity.md (the fixed architecture — issuer/resource-server, self-validated JWT)
  - docs/decisions/001-separate-services-topology.md (the no-sync-HTTP non-negotiable this journey honors)
  - docs/decisions/009-polecat-deferred-for-round-one.md (the X-Customer-Id seam this journey retires as the trust boundary)
  - docs/narratives/006-customer-registration.md (the registry row this journey adds credentials to)
  - docs/narratives/005-customer-storefront.md (the storefront screens this journey now gates behind login)
---

# Narrative 010 — The Customer Signs In

Every journey before this one took the Customer's identity on faith. The storefront announced "I am `customer-demo`" in a header, and Catalog, Inventory, and Orders believed it — because in round one there was nobody else to be, and no password to prove otherwise ([ADR 009](../decisions/009-polecat-deferred-for-round-one.md)). Narrative 006 registered a customer as a *row*; it never gave them a way to *log in*. This narrative closes that gap: the Customer sets a password, proves who they are, carries that proof to every service, and lets it go when they leave.

The mechanism is fixed by [ADR 023](../decisions/023-real-authentication-for-identity.md), and its shape is the teaching payoff. Identity becomes the system's sole **auth issuer**: it, and only it, holds a private signing key, so it, and only it, can *mint* a token. Catalog, Inventory, and Orders become **resource servers**: they hold the matching *public* key as configuration, so they can *verify* a token — but never forge one. The proof the Customer carries is a standard **JWT** whose `sub` claim is their customer id, and the load-bearing property is that a resource server checks it **entirely on its own** — no phone call back to Identity, per request or ever. That is the same no-synchronous-HTTP rule ([ADR 001](../decisions/001-separate-services-topology.md)) that keeps customer *data* off sync calls, now keeping customer *trust* off them too.

Two things this journey deliberately is **not**. It is not event sourcing — ASP.NET Core Identity is a relational user store, so credentials are more of Identity's "boring CRUD" foil, not a new stream. And it is not authorization — the token says *who* the Customer is, never *what they may do*; roles and policies wait for a second actor who isn't just "the shopper" ([ADR 023](../decisions/023-real-authentication-for-identity.md); Workshop 002 § 8 item 16).

## Journey scope

The Customer's sign-in journey threads four Workshop 002 slices, consolidated into one PR:

- **Slice 5.8 — Register with credentials.** Moment 1: setting a password, and the two ways that can be refused.
- **Slice 5.9 — Log in.** Moment 2: proving the password and receiving the token.
- **Slice 5.10 — Shop authenticated.** Moment 3: carrying the token to a resource server that trusts it offline — and what happens to a bad one.
- **Slice 5.11 — Log out.** Moment 4: letting the token go.

## Moment 1 — Setting a password

**Context.** A newcomer wants a CritterMart account. Unlike Narrative 006's registration — which minted a row from an email and a display name alone — they will now also choose a password, so they can come back and prove it is them.

**Interaction.** The storefront's Register screen posts `RegisterWithCredentials { email: "ada@example.com", displayName: "Ada Lovelace", password: … }` to `POST /register`.

**System response.** This is Narrative 006's registration with credentials layered on top, not in place of it — the old passwordless `POST /customers` still exists for the seeder and admin-provisioned customers, but a self-registering shopper comes through `/register`. Identity does two writes as one: `UserManager.CreateAsync` creates an ASP.NET Core Identity user with the password **hashed** (Identity never stores the plaintext), and a registry `Customer` row is written with the **same** id — so the user and the row are two tables describing one person, joined by a shared key. In the same transaction, `CustomerRegistered` is enrolled in the EF-Core outbox exactly as before (Orders' local customer read model, Narrative 007, still learns about her the same way). It is all-or-nothing: both writes land, or neither does, and the response is `201 Created`.

Two refusals guard the door, both before any write commits. If `ada@example.com` is already registered, the request is rejected (`CustomerAlreadyRegistered`) — the same duplicate-email guard Narrative 006 uses, now backing a user as well as a row. And if the password is too weak for the configured policy, Identity refuses it (`400`, with the policy's own complaints) and creates *nothing* — no orphaned user, no orphaned row. A shopper cannot get halfway into existence.

## Moment 2 — Proving it

**Context.** Ada is registered with a password. On a later visit — a new browser, no session — she needs to prove she is Ada before the storefront will treat a cart as hers.

**Interaction.** The Login screen posts `LogIn { email: "ada@example.com", password: … }` to `POST /login`.

**System response.** Identity runs `SignInManager.CheckPasswordSignInAsync` against the hashed credential. On success, it **mints a JWT**: a short, signed token whose `sub` claim is Ada's customer id, stamped with an issuer, an audience, and an expiry, and signed with Identity's **private** key. Ada's browser receives that token and holds onto it; nothing is written to any row, and the token itself is never persisted server-side — it lives only in transit and in the browser. It is a bearer of proof, not a session record.

On failure, Identity says as little as possible: a wrong password and an email that was never registered both return the **same** `401`, with no hint which was which. Telling Ada "that email isn't registered" would tell an attacker the same thing — so the storefront learns only "those credentials don't work," never "that account exists" (Workshop 002 § 6, no user enumeration).

## Moment 3 — Shopping as yourself

**Context.** Ada holds a valid token. She browses CritterMart's shelves — which needs no login at all, exactly as Narrative 005 always let her — and then adds something to her cart, which now does.

**Interaction.** Browsing Catalog carries no token and needs none: product listings are public, so the shelves load for anyone. The moment Ada adds to her cart, the storefront attaches her token — `Authorization: Bearer <jwt>` — to the request into Orders, and to every customer-keyed request after it.

**System response.** Orders was handed Identity's **public** key as configuration at startup, so when Ada's token arrives it checks the whole thing **itself**: the signature against that public key (proving Identity minted it and nobody tampered), the issuer and audience (proving it was minted for *this* system), and the expiry (proving it is still fresh). All of that happens inside Orders — **no call into Identity**, not per request, not once at startup, because the public key is config, not something fetched. If the token is good, Orders reads Ada's id from the `sub` claim and treats the request as hers: the customer id that used to arrive on faith in a header now arrives *proven* in a claim. That claim is the trust boundary now; the old `X-Customer-Id` header is retired from that role (a dev-only fallback lingers behind the scenes so the seeder and existing tests keep working, tracked for removal — but the storefront no longer sends it, and it is not what Orders trusts).

A bad token fails the same way it was checked — locally. A tampered signature fails the public-key check; an expired token fails the lifetime check; a token minted for some other audience fails that check — and every one of them is a `401` decided by Orders alone, again with **no call into Identity**. This is the whole architecture's proof in one Moment: real authentication, and still not one synchronous hop between services. Catalog and Inventory need no auth changes at all — Catalog's shelves are public, and Inventory only ever hears a customer id as ordinary payload on a RabbitMQ message from Orders (a `ReserveStock`), never as a browser's token, so it sits off this trust path entirely.

## Moment 4 — Letting it go

**Context.** Ada is done shopping and logs out — or simply closes the tab.

**Interaction.** The storefront logs out.

**System response.** Because the token is stateless — Identity kept no session, no server-side record — logging out is simply **discarding the token**. The browser drops it, the `useCurrentCustomer` seam returns to unauthenticated, and the next customer-keyed request carries no bearer and is met with `401`: Ada is a browsing stranger again until she signs back in. There is one honest asterisk, named rather than hidden: a token already handed out stays cryptographically valid until it *expires*, because nothing revokes it. A stolen token isn't un-stolen by clicking log out — which is exactly why the token's lifetime is kept short. A real revocation story — a denylist, or short access tokens refreshed against a longer-lived credential — is a modeled-but-deferred future increment (Workshop 002 § 8 item 15), not part of this pass.

## The issuer and the resource servers

Narrative 006 made Identity a place customers are *stored*; this narrative makes it the place customers are *trusted from*. The split is the point: one service mints because one service holds the private key, and three services verify because three services hold only the public one. Nothing about that requires an event store — it is the same "when CRUD is fine" thread Catalog runs on, applied to credentials, and it adds no new arrow to the context map, because verification never leaves the resource server. The strongest reading of "no synchronous service-to-service HTTP" doesn't just survive real authentication here; real authentication is built *to demonstrate* it.

## What the Customer does *not* yet see

- **No "what may I do?" — only "who am I?"** The token authenticates; it does not authorize. Every logged-in shopper has the same single role. Roles, policies, and an admin/backoffice actor are deferred until a second kind of actor exists ([ADR 023](../decisions/023-real-authentication-for-identity.md); Workshop 002 § 8 item 16).
- **No "stay logged in."** There is no refresh token and no server-side logout; a session lasts exactly as long as the access token's lifetime, then Ada logs in again. Refresh + revocation is the named next increment (item 15).
- **No password reset, no email verification of the login itself.** Setting a password proves nothing about controlling the inbox (as Narrative 009 also noted for email changes); a real "forgot password" or "verify your email" flow is out of scope for this pass.
- **No token on the bus.** Where Ada's identity crosses a service boundary between backends (Orders → Inventory), it rides as ordinary message payload over RabbitMQ, never as a JWT — the broker already established that trust. The token is a browser-to-service concern only.

## Document History

| Version | Date       | Notes |
| ------- | ---------- | ----- |
| v1.0    | 2026-07-07 | Initial commit. Covers Workshop 002 slices 5.8 (register with credentials — happy path plus duplicate-email and weak-password refusals), 5.9 (log in — mint a signed JWT; bad-credentials `401` with no user enumeration), 5.10 (shop authenticated — offline token verification at Orders with no HTTP into Identity, `sub` sourced as the trust boundary, invalid/expired rejected locally), and 5.11 (log out — client-side token discard, deferred revocation named). Threads CritterMart's **issuer / resource-server** split per [ADR 023](../decisions/023-real-authentication-for-identity.md): Identity mints with an asymmetric private key, the resource servers verify offline against a config-distributed public key, retiring `X-Customer-Id` as the trust boundary in favor of the `sub` claim. Load-bearing framings held: auth is still non-event-sourced (relational user store extending the boring-CRUD foil) and the no-sync-HTTP non-negotiable ([ADR 001](../decisions/001-separate-services-topology.md)) survives real auth. Sibling of the `slices-5-8-5-11-identity-authentication` OpenSpec change; consolidated with the implementation into one PR. |
