# CritterMart — Handoff: Identity Auth Implementation — per-slice loop next

> **Durable handoff** (version-controlled at `docs/handoffs/`), authored 2026-07-07.
> Closes out the ADR-023 design session (parent: [`identity-real-auth-direction.md`](identity-real-auth-direction.md))
> and opens the **implementation** phase for the auth increment. This session authored the design layer
> (ADR + workshop + context-map); the next session builds the code, per CLAUDE.md's per-slice loop.

## Mission for the next session

Implement the **ASP.NET Core Identity authentication increment** — Workshop 002 slices **5.8–5.11** — via the
per-slice loop: **OpenSpec proposal + narrative → prompt → implement → retro**, consolidated into one PR per
Erik's slice-PR preference ([[feedback-consolidate-slice-prs]]). The architecture is already decided by
[ADR 023](../decisions/023-real-authentication-for-identity.md) — **do not re-litigate it.** Build against it.

## What's true right now (2026-07-07, verified this session — don't re-derive)

- `main` @ `502f5fd`, clean. That commit is the squash-merge of **PR #129** (ADR 023 design layer). The
  post-merge sync reconciled a divergence (two prior local-only commits — the handoff and the ADR-009
  correction — were folded into #129's squash; content preserved, local `main` reset to `origin/main`).
- **Stale branch to delete:** `design/adr-023-real-auth-identity` (squashed into `502f5fd`). Deletion needs
  `git branch -D`, which the git-guardrails hook blocks — run `! git branch -D design/adr-023-real-auth-identity`
  in-session when convenient.
- **The decided architecture (ADR 023 — build to this, don't reopen):**
  - **ASP.NET Core Identity** for the user store + credential handling (`UserManager.CreateAsync`,
    `SignInManager.CheckPasswordSignInAsync`), EF-Core-backed on the existing shared-Postgres `identity` schema.
  - On login, Identity **mints a standard JWT** (customer id in the `sub` claim), signed with an **asymmetric
    private key it alone holds**.
  - The SPA sends the JWT as `Authorization: Bearer …` to **all four** services. Catalog/Inventory/Orders
    configure `AddJwtBearer` with Identity's **public key distributed as config** and validate **offline** —
    **no HTTP into Identity**, per-request or at startup (config, not JWKS). This is what keeps the
    no-sync-HTTP non-negotiable (ADR 001) intact.
  - The `X-Customer-Id` seam **retires as the trust boundary**; the `sub` claim replaces it. The frontend's
    `useCurrentCustomer` seam sources the id from the authenticated session — the one-file change ADR 009's
    amendment engineered.
  - Auth stays **non-event-sourced** — ASP.NET Core Identity is relational user-store CRUD; **zero stream
    events, no saga** in this increment. It extends the boring-CRUD foil (ADR 009 amendment / ADR 022).
- **Slice breakdown** (Workshop 002 § 5–6 carry the GWT scenarios — read them):
  - **5.8 Register with credentials** (`RegisterWithCredentials`) — creates the Identity user + the registry
    `Customer` row in one flow, same id; `CustomerRegistered` still publishes via the outbox. Failure paths:
    duplicate email, weak password.
  - **5.9 Log in — issue a JWT** (`LogIn`) — password check → mint the signed JWT. Failure: bad credentials
    (no user enumeration).
  - **5.10 Verify a token at a resource server** *(system, code lands in Catalog/Inventory/Orders)* — the
    "Identity → consumer" shape, like slices 5.3/5.4. Offline validation; `401` on invalid/expired, locally.
  - **5.11 Log out** — stateless JWT → client-side token discard; server-side revocation deferred.

## Forward questions ADR 023 named for THIS layer (resolve in the OpenSpec proposal / slice design — these are
## open *implementation* questions, NOT re-openings of the architecture)

1. **Entity shape + a naming collision (load-bearing).** Decide whether the ASP.NET Core Identity user and the
   registry `Customer` row are one entity or two id-linked tables (`Customer.Id` is already a string;
   `IdentityUser.Id` is a string by default — they can align). **Gotcha:** CritterMart's existing
   `IdentityDbContext` (`src/CritterMart.Identity/Customers/IdentityDbContext.cs`) shares its name with ASP.NET
   Core Identity's `Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityDbContext<TUser>` base class —
   either rename the local context or derive it from the ASP.NET base. Don't let a fresh session trip on this.
2. **Registration: replace or layer?** Does `RegisterWithCredentials` supersede the spike's open
   `RegisterCustomer` outright, or coexist (admin-provisioned customers)?
3. **Token lifetime / refresh / logout.** Round one: short access token + client-side-discard logout, no
   refresh, no server-side revocation. Consider a demo-paced TTL config knob mirroring `EmailChangeDeadline`.
4. **Authorization is deferred.** ADR 023 settles authN only; roles/policies wait for a second actor.
5. **Signing-key management.** Where the keypair lives (user-secrets/config dev, secret store prod) and
   rotation cadence (config-redeploy accepted for round one).

## First steps (per CLAUDE.md's per-slice loop)

1. **OpenSpec proposal** — extend the `customer-registry` capability (one-capability-per-aggregate; the
   `EmailChange` saga already sits here) with the auth SHALL deltas for 5.8–5.11. Author + validate with the
   openspec CLI (prefer the tool over freeform — [[feedback-prefer-tool-backed-over-freeform]]).
2. **Narrative** — a customer-auth journey (register → log in → shop authenticated → log out), NDD-informed,
   at `docs/narratives/`. Sibling to the proposal; both must agree.
3. **Prompt** at `docs/prompts/implementations/`, then **implement**, then **retro**. Live-verify the auth
   flow end-to-end against the real stack before the PR ([[feedback-live-verify-after-changes]]) — boot via
   the demo-runbook, drive a register/login/authenticated-request/logout cycle ([[feedback-drive-demo-flows]]).

## Carry-forward items (unchanged, non-blocking)

- **Do NOT bump Wolverine past 6.16.0** (CritterWatch 1.0.0-beta.1 coupling) or transitive JasperFx deps
  (suppressed MessagePack CVE). Both standing.
- **CritterWatch trial expires 2026-07-10** — check the renewal conversation before any live-console work.
- **Five AppHost demo knobs** — still post-talk-only, do not delete yet.
- **CLAUDE.md's non-negotiables line** still reads "frontend sends a hardcoded `X-Customer-Id`" — accurate
  until the auth slices ship; update it in the same PR as slice 5.10.

## Orientation files (read first, in order)

1. This handoff.
2. [ADR 023](../decisions/023-real-authentication-for-identity.md) — the decision; the "Forward questions"
   section is the slice layer's input.
3. [Workshop 002](../workshops/002-identity-event-model.md) §§ 5.8–5.11 (slice table + GWT), § 3's auth
   architect note, § 4's auth vocabulary subsection.
4. `docs/context-map/README.md` — the auth relationship row (OHS+PL extended, Conformist, offline / no edge).
5. `src/CritterMart.Identity/` — the current shape (`Program.cs`, `IdentityDbContext.cs`, `Customer.cs`,
   the `EmailChange` saga) the auth increment layers onto.
6. `docs/decisions/023-...md`'s ctx7-verified fact: ASP.NET Core Identity's built-in tokens are NOT JWTs —
   the standard-JWT layer is deliberate. Fetch current ASP.NET Core Identity docs via `ctx7` when wiring
   `UserManager`/`SignInManager`/`AddJwtBearer` (per the global context7 rule — .NET 10 APIs may be recent).

## Definition of done (next session)

- [ ] OpenSpec proposal (`customer-registry` auth deltas, 5.8–5.11) authored + validated
- [ ] Customer-auth narrative authored (sibling; agrees with the proposal)
- [ ] Implementation prompt authored; slices 5.8–5.11 implemented (incl. the `IdentityDbContext` collision
      resolved); `X-Customer-Id` → `sub` cutover; frontend `useCurrentCustomer` sourced from the session
- [ ] Live-verified end-to-end (register/login/authenticated request/logout) against the real stack
- [ ] Retro authored; one consolidated PR; `/post-merge → /handoff → /blurb` on merge

## Suggested skills

- `wolverine-handlers-efcore`, `wolverine-http-fundamentals`, `wolverine-testing-alba` (Critter Stack impl +
  integration tests).
- `ctx7` for ASP.NET Core Identity + JWT bearer API specifics (`UserManager`, `SignInManager`, `AddJwtBearer`,
  `JsonWebTokenHandler`, asymmetric signing) — .NET 10, verify against current docs.
- `frontend` skill for the `useCurrentCustomer` seam cutover and the login/logout UI.
