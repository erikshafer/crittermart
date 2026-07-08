# ADR 023: Real Authentication for Identity via ASP.NET Core Identity + Self-Validated JWT

**Status**: Accepted

## Context

[ADR 009](009-polecat-deferred-for-round-one.md) deferred real authentication for round one: identity arrives ambiently as a hardcoded `X-Customer-Id` header, sourced behind the frontend's `useCurrentCustomer` seam. Two amendments since then promoted the Identity *registry* to a deployed EF-Core service ([Workshop 002](../workshops/002-identity-event-model.md)) and framed it — via [ADR 022](022-convention-sagas-additive-to-pmvh.md) — as CritterMart's deliberately **boring, non-event-sourced CRUD foil** to the three Marten-backed services. Throughout, *authentication itself* stayed stubbed: the registry is a data store, not an auth provider.

This ADR closes that stub. It is round-one's "real authentication for Identity" long-road candidate (context map § Long road, [vision](../vision.md) § Long road), now chosen as the next direction. The direction (real auth) and the primary mechanism (**ASP.NET Core Identity** — `UserManager`/`SignInManager`, EF-Core-backed on the existing `identity` schema) were settled by owner decision; this ADR records them and settles the two architecturally load-bearing sub-questions that mechanism leaves open:

1. **What credential does the SPA present, and in what format** — cookie or bearer token?
2. **How do the other three services (Catalog, Inventory, Orders) trust that identity without a synchronous call back to Identity** — given the [no-synchronous-service-to-service-HTTP non-negotiable](001-separate-services-topology.md) (ADR 001/003)?

These two are one question. The SPA calls all four services directly (no BFF — [ADR 006](006-wolverine-http-per-service-no-bff.md); cross-origin — [ADR 018](018-frontend-three-services-cors-posture.md)). RabbitMQ cannot authenticate an inbound *browser* request, so the credential must travel *with* each HTTP call, and each service must be able to validate it *offline*. A decisive constraint on the mechanism: ASP.NET Core Identity's built-in login (`MapIdentityApi` / `AddBearerToken`) issues **custom, non-JWT tokens validated via Identity's own Data-Protection key ring** (confirmed against current ASP.NET Core documentation) — so neither its cookie nor its opaque bearer can be validated by a *sibling* service without sharing Identity's keys. Cross-service, offline trust requires a **standard JWT**.

## Decision

Identity gains real authentication built on **ASP.NET Core Identity** for the user store and credential handling (registration, password hashing, `SignInManager` password checks), backed by EF Core on the existing shared Postgres `identity` schema — the same store the customer registry and the `EmailChange` saga already use. This **extends** the boring-CRUD-foil framing (ADR 009's amendment / ADR 022); it does not reverse it. ASP.NET Core Identity is a relational user store, not an event stream: no event sourcing, no Polecat, no OIDC authorization-server.

Cross-service trust uses a **self-validated, standard JWT bearer token**:

- On a successful `SignInManager` password check, Identity **mints a standard JWT** carrying the customer id as the `sub` claim, **signed with an asymmetric key** (RSA/ECDSA) whose **private half only Identity holds**.
- The SPA presents that JWT as `Authorization: Bearer …` on every request to **all four** services.
- Catalog, Inventory, and Orders each configure `AddJwtBearer` with Identity's **public key, distributed as configuration**. They validate the token **fully offline** — signature, issuer, audience, lifetime — and read the customer id from `sub`. There is **no HTTP call into Identity**, per-request or at startup: the public key is config, not a fetched JWKS document.
- The round-one `X-Customer-Id` header is **retired as the trust boundary**: the authenticated `sub` claim replaces it. The frontend's `useCurrentCustomer` seam now sources the id from the authenticated session's token rather than a hardcoded constant — the one-file change ADR 009's amendment engineered the seam to make.

This is a clean **issuer / resource-server** split: Identity is the sole issuer (only it holds the private key and can *mint*); the other three are resource servers that can only *verify*.

## Consequences

**The no-synchronous-service-to-service-HTTP non-negotiable is honored, not bent.** Offline signature validation against a config-distributed public key involves zero service-to-service traffic. This is the auth analogue of Workshop 002's existing "OHS-for-the-frontend / PL-for-the-backends" reconciliation — the same rule that keeps customer *data* off sync HTTP now keeps identity *trust* off it. Identity crossing a service boundary over RabbitMQ (e.g. `ReserveStock`, Orders→Inventory) is unaffected: that trust is already established by the broker and the customer id rides as ordinary message payload — **no token travels on the bus.**

The relationship pattern is confirmed, not merely assumed (context map amended alongside): auth **extends** Identity's existing **Open-Host Service + Published Language** posture — Identity publishes a second contract, the JWT claim shape + public key, beside `CustomerRegistered` — and the resource servers stay **Conformist** to it, verifying and accepting `sub` without translation, exactly as they already accept the `X-Customer-Id` shape (`structural-constraints.md`). The novelty versus the registry: this contract is validated *offline against a config-distributed public key*, so it adds **no runtime integration edge at all** — the purest possible honoring of the no-sync-HTTP rule.

**Why bearer, not cookie.** Cookie auth falls out of the no-BFF, cross-origin SPA shape: it would need a shared Data-Protection key ring across four services, or a BFF to front them — the coupling ADR 006 deliberately avoided. **Why asymmetric, not symmetric.** A shared HMAC secret would let any of the four *mint* Identity's tokens, not just verify them — a shared-kernel secret across bounded contexts, dissolving the issuer/resource-server boundary. **Why a config public key, not JWKS.** A JWKS endpoint would be a (cached, startup) HTTP call *into* Identity — defensible as metadata rather than per-request business traffic, but a soft asterisk on the non-negotiable this ADR chooses not to take on. The tradeoff accepted: rotating the keypair means redeploying config to the validators — acceptable at round-one cadence, and a JWKS move stays open if rotation frequency ever justifies it.

**Rejected mechanisms, folded in.** *OpenIddict / full OIDC* — a real authorization-server lift that competes with, rather than complements, the event-sourcing material the talk is built on. *Hand-rolled JWT with no user store* — reads as toy auth, no credential lifecycle, a weaker close on the stub. *ASP.NET Core Identity's cookie or `MapIdentityApi` opaque bearer* — not offline-validatable by a sibling service without sharing Identity's Data-Protection keys (the decisive fact above). *Symmetric HMAC* and *JWKS* — as folded above.

**Forward questions this ADR names but leaves to the workshop and per-slice loop** (not re-litigated here):

- **Entity shape.** Whether the ASP.NET Core Identity user and the registry `Customer` row are one entity or two tables linked by id. `Customer.Id` is already a string chosen to line up with the identity seam ([`Customer.cs`](../../src/CritterMart.Identity/Customers/Customer.cs)); `IdentityUser.Id` is a string by default — they can align. A naming collision to resolve alongside: CritterMart's existing `IdentityDbContext` shares a name with ASP.NET Core Identity's `IdentityDbContext<TUser>` base class.
- **Logout / token lifetime.** JWTs are stateless, so round-one logout is client-side token discard; server-side revocation (a denylist, or short access tokens + a refresh flow) is a future increment — modeled, not built.
- **Registration flow.** Whether the spike's open `RegisterCustomer` becomes credentialed sign-up or is layered with a separate credential-establishment step (Workshop 002 slice 5.8).

**Relationship to prior ADRs.** This ADR **supersedes ADR 009's authentication-deferral stance only** — ADR 009's Polecat deferral and the boring-CRUD-foil framing (carried forward by ADR 022) both still hold, so ADR 009 is amended with a cross-reference rather than flipped to `Superseded`. The paired [`structural-constraints.md`](../rules/structural-constraints.md) Identity section is updated in this same PR (its own header rule requires an ADR that changes a constraint to pair with a rule-file update); the context map and Workshop 002 are amended alongside.
