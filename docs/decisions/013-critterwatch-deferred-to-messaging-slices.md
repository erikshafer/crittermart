# ADR 013: CritterWatch Deferred to the 4.x Messaging Slices

**Status**: Accepted

> **Realized by [ADR 017](017-critterwatch-integrated.md).** The deferral held and was honored — CritterWatch was integrated out of band once slices 4.2–4.7 produced cross-BC traffic worth monitoring. ADR 017 is the successor this ADR predicted; it records the actual integration and resolves the Open Question below (Trial tier, key in user-secrets, nuget.org-sourced so no private-feed CI break). This ADR's deferral reasoning stands as the historical record; it is not reversed.

## Context

CritterWatch (JasperFx) is a monitoring console for Wolverine services — live node/agent/endpoint health, messaging topology, and operational state. Integration is two packages (`Wolverine.CritterWatch` in each monitored service, `CritterWatch` in a server project), the server needs its own PostgreSQL database (`Database=critterwatch`), and wiring is `opts.AddCritterWatchMonitoring(...)` in each service plus `builder.AddCritterWatch(...)` + `app.UseCritterWatch()` in the server, with .NET Aspire orchestration recommended for local dev. The installed `wolverine-integrations-critterwatch-setup` skill is the eventual playbook.

We want CritterWatch in CritterMart, at capabilities the maintainer understands to be a **paid/commercial tier sourced from the private `packages.jasperfx.net` feed**. (The public quickstart reads as MIT-licensed and implies nuget.org packages; it does not cover a paid tier or the private feed. The exact tier, feed, and any license-key requirement are **unconfirmed** as of this ADR — see Open Question.)

Three facts make now the wrong time to integrate:

1. **Nothing to monitor yet.** CritterMart has no live Wolverine messaging. Orders is not stood up, and the first cross-service RabbitMQ traffic is slice 4.2 (Orders → `ReserveStock` → Inventory → `StockReserved` back). Until then the console would render empty; CritterWatch's value curve is flat before 4.2.
2. **Private-feed CI cost.** The `packages.jasperfx.net` feed is auth-gated and returns 401 on the public CI runner. PR #23 just removed it as a repo restore source (the `nuget.config` is now nuget.org-only). A required CritterWatch `PackageReference` from that feed would re-break CI and gate the public, teaching-oriented build behind paid feed access.
3. **Round-one observability is already settled.** OpenTelemetry traces into the Aspire dashboard (ADR 004, ADR 005). CritterWatch is a *second, complementary* layer — Wolverine operational state, not request traces — not a replacement. The talk's trace beat does not need it.

## Decision

CritterWatch integration is **deferred to the 4.x messaging slices**. The earliest sensible window is with or just after **slice 4.2** (the first real cross-BC RabbitMQ traffic), when there is Wolverine messaging worth monitoring and the Aspire AppHost already orchestrates the PostgreSQL and RabbitMQ that CritterWatch needs.

Until then: do **not** add the `packages.jasperfx.net` feed or any CritterWatch `PackageReference` to the repo. The nuget.org-only `nuget.config` (PR #23) stands.

When integrated, the private-feed dependency must be isolated so the public build and CI stay green without paid feed access — e.g., CritterWatch confined to an opt-in project excluded from the default CI restore, or authenticated NuGet on CI via a secret. A successor ADR records that integration decision (feed/CI design and the chosen tier) when slice 4.2 lands.

## Open Question

Which CritterWatch tier/capabilities are targeted, does that tier ship from `packages.jasperfx.net`, and does it require a license key? This determines the CI/feed design and blocks the *integration* ADR — not this *deferral*, which holds regardless.

## Consequences

Intent is durably recorded: the eventual integration session references this ADR rather than re-deriving the timing and the CI/feed tradeoff. No build or CI change now; the nuget.org-only posture from PR #23 is preserved. Tradeoff: no operational messaging console in the interim — acceptable, since OTel/Aspire covers the round-one trace story and there is no messaging to watch before 4.2.

Rejected now: integrating CritterWatch immediately (empty console plus a re-broken CI), and re-adding the `jasperfx` feed to the repo config (the exact 401 PR #23 removed). This ADR is the deferral-and-direction record; a successor ADR will record the actual integration when slice 4.2 makes it earn its place. Mirrors the deferral shape of [ADR 009](009-polecat-deferred-for-round-one.md).
