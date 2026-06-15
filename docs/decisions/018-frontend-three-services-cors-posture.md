# ADR 018: Frontend-to-Three-Services Dev-Server + CORS Posture

**Status**: Accepted

## Context

[ADR 006](006-wolverine-http-per-service-no-bff.md) puts no BFF between the SPA and the backend, and [ADR 015](015-vite-react-frontend-stack.md) makes the storefront a client-side-rendered SPA. Together they mean one browser app calls **three** Wolverine.Http services (Catalog, Inventory, Orders) directly, over a real HTTP boundary. That boundary is not incidental: the **OpenTelemetry trace spanning the network is a hard success criterion** (`vision.md` § Success criteria, restated in ADR 015) — the cross-network hop from SPA to three services *is* the demo beat.

Three services means three origins from the browser's perspective, so **production CORS is committed and unavoidable**. The CORS allowlist already shipped in the `chore: pre-frontend hardening` pass but was never given its own ADR; this ADR records that posture and resolves the one open question it left: **how the SPA reaches the three services in development.**

The sibling precedent inverts here. CritterBids ([CritterBids ADR 025], a modular monolith with a *single* API host) chose a Vite dev-server proxy precisely *to avoid* adding CORS to that one host — same-origin in dev, no CORS anywhere. CritterMart cannot reuse that benefit: with three origins and no BFF, CORS exists in production regardless, so a dev proxy would only *hide* a boundary that is real in the demo.

This clears the ADR bar: the choice is structural (it shapes every service-calling surface and each service's bootstrap), non-obvious (it deliberately inverts the CritterBids precedent), and a later contributor would otherwise re-derive it.

## Decision

The SPA calls the three services **cross-origin against the CORS allowlist in both development and production**. There is **no Vite dev-server proxy.**

- Each service's bootstrap configures CORS to permit the SPA's origins — the Vite dev-server origin in development and the Aspire-served demo origin — for the verbs and headers the SPA uses. The production allowlist already in place is extended with the dev origin.
- The SPA reads each service's base URL from build-time / Aspire-injected configuration; [ADR 004](004-dotnet-aspire-orchestrator.md)'s `AddViteApp` integration injects the three service URLs into the SPA resource as environment variables.
- **Dev mirrors prod.** Because there is no proxy, the SPA always issues genuine cross-origin requests to the three services, so the cross-network OpenTelemetry boundary is exercised in development exactly as it is in the demo.

## Consequences

The trace beat is real in every environment, not just the demo — a misconfigured allowlist surfaces as a browser CORS error during dev, which is the earliest and cheapest place to catch it. CORS configuration becomes a permanent part of each service's bootstrap rather than a demo-only afterthought, and this ADR gives the already-shipped allowlist a home in the decision log.

The cost is that every new service-calling surface is a real CORS-permitted request — there is no same-origin escape hatch — and the allowlist must list each SPA origin the project runs from. This is accepted: it is the honest shape of a no-BFF, three-service frontend, and exercising it continuously is on-message for a project whose thesis is the traceable cross-network path.

Rejected alternatives. **A Vite dev proxy fanning to the three service targets** (the CritterBids shape) was rejected: its single benefit — avoiding CORS — is moot when production CORS is already committed, it makes dev diverge from the prod cross-origin path the demo depends on, and it adds three proxy entries to keep in sync with the service URLs. **A hybrid (proxy in dev, CORS in prod)** carries the most configuration surface and an intentional dev/prod divergence for no gain here. Both remain available if a future requirement (e.g., cookie-based auth needing same-origin semantics) makes same-origin dev genuinely valuable — that would be its own decision, not a silent reversal.

This ADR depends on [ADR 006](006-wolverine-http-per-service-no-bff.md) (no BFF), [ADR 004](004-dotnet-aspire-orchestrator.md) (Aspire injects the service URLs), and [ADR 015](015-vite-react-frontend-stack.md) (the CSR SPA that calls the three services). The decision evidence is [Research: frontend-cross-repo-comparison](../research/frontend-cross-repo-comparison.md).
