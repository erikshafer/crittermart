# CritterMart — Handoff: Identity Real-Auth Direction — design session next

> **Durable handoff** (version-controlled at `docs/handoffs/`), authored 2026-07-07.
> Closes out the fork-resolution session (parent: [`post-saga2-next-direction.md`](post-saga2-next-direction.md))
> and opens the design session for the chosen direction. This was **deliberately a fork-resolution session,
> not a design or implementation session** — the next session should do the actual ADR/workshop reasoning,
> ideally with a reasoning-heavy model (Opus/Fable).

## Mission for the next session

Author the pre-code design artifacts for **giving Identity real authentication via ASP.NET Core Identity**,
starting from the ADR. This is round-one's Long Road candidate 1, now correctly scoped (see "What changed"
below) and with its tech choice already made.

**Do not re-litigate the direction or the tech choice** — both were decided this session via `AskUserQuestion`
with previews, per [[feedback-collaborate-on-decisions]] / [[feedback-options-with-previews]]. Start from the
ADR.

## What's true right now (2026-07-07, verified this session — don't re-derive)

- `main` @ `2f5cda4`, clean. Commit `2f5cda4` is a `tidy:` correcting a factual error (below); no design work
  has landed yet for this direction.
- **Direction chosen**: real authentication for Identity (Long Road candidate 1), *not* Polecat, *not*
  Promotions+DCB, *not* a Returns slice. The other Long Road candidates remain open/unchosen for a future
  fork.
- **Tech chosen**: **ASP.NET Core Identity**, layered onto Identity's existing EF Core + Npgsql stack
  (`IdentityDbContext`, `UserManager`/`SignInManager`; cookie auth for the SPA or bearer tokens for the API —
  that split is an open sub-question for the ADR to settle, not decided yet). Rejected alternatives:
  OpenIddict (real OIDC provider — bigger lift, judged to compete with the event-sourcing narrative rather
  than complement it) and hand-rolled JWT (smallest lift, but reads as "toy auth," weaker fit for closing the
  stub credibly).
- **What changed and why (important context for the ADR):** ADR 009 previously claimed "Polecat is the
  JasperFx identity stack; CritterCab uses it." That's factually wrong — verified this session against the
  actual `C:\Code\CritterCab\README.md`: Polecat is JasperFx's SQL Server-backed document/event store (the
  SQL Server counterpart to Marten), with zero auth/credential/session/claims functionality. CritterCab uses
  it purely as a persistence choice. The error had propagated into `vision.md`'s Long Road and
  `docs/context-map/README.md`'s Long Road as "Polecat-backed real authentication." All three are corrected
  (commit `2f5cda4`) to frame the candidate as "real authentication for Identity," mechanism now settled as
  ASP.NET Core Identity via this session's fork.
- Identity's current shape (from Saga #2, shipped and archived): EF-Core-backed customer registry
  (`IdentityDbContext`, `AddDbContextWithWolverineIntegration`), a shipped `EmailChange` convention saga
  (`Wolverine.Saga`, EF-Core-backed), framed by ADR 022 as CritterMart's deliberately **boring, non-event-
  sourced CRUD foil** to the other three Marten-backed services. ASP.NET Core Identity extends that framing
  rather than reversing it — still boring CRUD, just real boring CRUD instead of a stub.
- Frontend currently sources the customer id via a `useCurrentCustomer` seam (a single React hook/context
  provider, added per ADR 009's 2026-06-04 amendment specifically so this promotion would be a one-file
  change, not a call-site sweep) — check that seam still holds before assuming a bigger frontend rewrite is
  needed.

## First design step (per CLAUDE.md's pre-code design phase)

1. **New ADR** (next number is 023) recording the ASP.NET Core Identity decision — supersedes/amends ADR 009's
   "auth deferred" stance now that a direction and mechanism are chosen. Needs to settle: cookie vs. bearer
   token issuance for the SPA↔Identity relationship; whether other services (Catalog/Inventory/Orders) need to
   validate tokens directly or continue trusting a header Identity/frontend populates (this determines whether
   the "no synchronous service-to-service HTTP" non-negotiable is in tension with token validation, and if so
   how — e.g. shared signing key/JWKS vs. claims forwarded through existing async messaging).
2. **Context-map update** (`docs/context-map/README.md`) — name the relationship pattern once the ADR settles
   how other BCs relate to Identity's new auth responsibility (still likely Open-Host Service + Published
   Language, but confirm rather than assume — auth may need a different pattern than the registry's).
3. **Event Modeling workshop pass** (likely an amendment to
   [Workshop 002](../workshops/002-identity-event-model.md), same as the `EmailChange` saga's precedent) —
   register, login, session/token issuance, logout as new slices.
4. From there, the per-slice loop as usual: OpenSpec proposal + narrative → prompt → implement → retro.

## Smaller carry-forward items (inherited from the parent handoff, still open)

- **CritterWatch trial expired/expires 2026-07-10** — check whether Erik's renewal conversation (Babu/Jeremy)
  resolved before attempting any live CritterWatch-console verification work.
- **Marten-sibling verification gap** — whether `Replenishment`'s `ReplenishTimeout` is also absent from
  `inventory.wolverine_incoming_envelopes` is still *inferred*, not verified (see
  `docs/research/critterwatch-saga-visibility-beta1.md`, "Promotion path" table). Doesn't need the CritterWatch
  console — a direct Postgres check would close it.
- **`UseDurableLocalQueues()` decision for saga timeouts** — still an unresolved observation, not yet an
  ADR-tracked decision either way. Crosses the ADR threshold once someone actually decides it.
- **Five AppHost demo knobs** — still post-talk-only, do not delete yet.
- **Do NOT bump Wolverine past 6.16.0** (CritterWatch coupling) or transitive JasperFx dependencies (suppressed
  MessagePack CVE) — both still standing.

## Orientation files for the next session (read first, in order)

1. This handoff.
2. [`docs/decisions/009-polecat-deferred-for-round-one.md`](../decisions/009-polecat-deferred-for-round-one.md)
   — read the 2026-07-07 correction amendment; the ADR you author next likely supersedes this one's auth
   stance (not its "boring CRUD foil" framing, which ADR 022 already carried forward).
3. [`docs/decisions/022-convention-sagas-additive-to-pmvh.md`](../decisions/022-convention-sagas-additive-to-pmvh.md)
   — the "boring CRUD foil" framing this direction extends.
4. `docs/context-map/README.md` § Long road and § Identity's current relationships.
5. [Workshop 002](../workshops/002-identity-event-model.md) — the existing Identity event model this session's
   workshop pass will amend.
6. `CLAUDE.md` — pipeline order (ADR → context-map → workshop → per-slice loop); this direction starts at the
   ADR, not mid-pipeline.

## Definition of done

- [ ] ADR 023 authored (ASP.NET Core Identity decision, cookie-vs-bearer and cross-BC trust questions settled)
- [ ] Context-map amended if the auth relationship pattern differs from the registry's Open-Host
      Service + Published Language
- [ ] Workshop 002 amended with the new auth slices (register, login, session/token issuance, logout)
- [ ] `/post-merge` → `/handoff` → `/blurb` close-out ritual run once this session's PR(s) land

## Suggested skills

- `domain-modeling` — for the ADR and context-map work.
- `event-modeling` — for the Workshop 002 amendment.
- Consider running this session on Opus or Fable given the architectural reasoning load (cross-BC trust
  boundary for token validation is a genuinely non-obvious tradeoff).
