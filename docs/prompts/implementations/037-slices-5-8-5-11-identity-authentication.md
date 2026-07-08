# Prompt: Implementations 037 — Slices 5.8–5.11 Identity Authentication (ASP.NET Core Identity + self-validated JWT)

**Kind**: per-slice implementation (Identity as auth issuer + Orders as resource server + frontend cutover), consolidated into one PR per [[feedback-consolidate-slice-prs]]
**Files touched**: `openspec/changes/slices-5-8-5-11-identity-authentication/{proposal.md,specs/customer-registry/spec.md,design.md,tasks.md}` (new, this session, `openspec validate --strict` green); `docs/narratives/010-customer-authentication.md` (new) + `docs/narratives/README.md` (count 9→10); `docs/prompts/implementations/037-…md` (this file); `Directory.Packages.props` (+3 packages); `src/CritterMart.Identity/*` (rename `IdentityDbContext`→`CustomerDbContext`; new `Auth/*`, `Features/{RegisterWithCredentials,LogIn,LogOut}.cs`; `Program.cs`, `.csproj`); `src/CritterMart.Orders/*` (`Program.cs`, `Auth/*`, customer-keyed endpoints, `.csproj`); `src/CritterMart.AppHost/Program.cs` (key/issuer config to Orders); `client/src/{identity,api}/*` + login/register pages; tests (Identity + Orders); CLAUDE.md non-negotiables line; `docs/skills/DEBT.md` (fallback-header removal row); `docs/retrospectives/implementations/037-…md` (at close)
**Mode**: solo implementation, fully consolidated — this session runs OpenSpec proposal/specs/design/tasks → narrative → this prompt → code → tests → live-verify → retro
**Commit subject**: `feat: add real authentication for Identity (ASP.NET Core Identity + self-validated JWT) — slices 5.8–5.11`

## Framing

ADR 023 already decided the architecture — **do not re-litigate it.** Identity becomes the system's sole **auth issuer** (ASP.NET Core Identity user store + a JWT it signs with an asymmetric private key only it holds); Catalog/Inventory/Orders are **resource servers** that verify the token **offline** against Identity's config-distributed public key. The `sub` claim retires `X-Customer-Id` as the trust boundary. Three session-start forks were resolved via AskUserQuestion (design.md decisions 1/5/6): **entity shape** = two id-linked tables, one `CustomerDbContext : IdentityUserContext<IdentityUser>` (this also resolves the `IdentityDbContext` name collision the handoff flagged); **cutover** = layered (Bearer is the trust boundary, `X-Customer-Id` kept as a dev-only fallback so the seeder/existing tests/demo-traffic stay green — DEBT-tracked for removal); **gate** = browse Catalog anonymously, checkout requires login.

**The load-bearing demonstration** is slice 5.10: a resource server validates a token with **zero HTTP into Identity**, per request or at startup (the public key is config, not a fetched JWKS). That is the auth analogue of Workshop 002's OHS/PL split and the proof ADR 001 survives real auth. Keep it literal in the code (no `Authority`/`MetadataAddress` on `AddJwtBearer`).

**Still non-event-sourced.** Auth is relational user-store CRUD — zero stream events, no saga, no projection. It extends the boring-CRUD foil (ADR 009 amendment / ADR 022), it does not reverse it.

## Goal

- Identity mints: `POST /register` (`RegisterWithCredentials` — user + `Customer` row + `CustomerRegistered`, one transaction; `409` duplicate, `400` weak password) and `POST /login` (`LogIn` — `SignInManager` check → signed JWT with `sub`=customer id; `401` no-enumeration on bad creds). `POST /logout` is informational (stateless client discard).
- Orders verifies: `AddJwtBearer` (public key from config, `MapInboundClaims = false`, offline); customer-keyed endpoints source the id from `sub` ?? `X-Customer-Id` fallback; invalid/expired → `401`.
- Frontend cuts to Bearer: login/register/logout UI, session token store, `useCurrentCustomer` from `sub`, `client.ts` sends `Authorization: Bearer`, cart/checkout gated, Catalog anonymous.
- `dotnet build` (whole repo) zero errors; `dotnet test` green incl. **all** pre-existing tests (layered cutover = no regressions); frontend Vitest green. Live-verified end-to-end against the real Aspire stack.

## Spec delta

This session satisfies the **four ADDED requirements** in the `customer-registry` capability — *register with credentials* (5.8), *log in and issue a JWT* (5.9), *verify a token at a resource server* (5.10), *log out* (5.11) — in `openspec/changes/slices-5-8-5-11-identity-authentication/specs/customer-registry/spec.md`, authored this session. Workshop 002 v1.3 § 6 carries the GWT scenarios; Narrative 010 is the human companion. The retro confirms closure.

## Orientation files

1. **`docs/handoffs/identity-auth-implementation.md`** — the mission + the "forward questions" this layer resolves.
2. **`docs/decisions/023-real-authentication-for-identity.md`** — the fixed architecture; build to it.
3. **`docs/workshops/002-identity-event-model.md` §§ 2/3/4/5/6 (auth subsections) + § 8 items 13–17** — the model + open questions this session resolves.
4. **`openspec/changes/slices-5-8-5-11-identity-authentication/{proposal.md,design.md}`** — the build map + the implementation decisions (with ctx7 grounding), authored this session.
5. **`src/CritterMart.Identity/{Program.cs,Customers/IdentityDbContext.cs,Features/RegisterCustomer.cs}`** — the wiring the auth layer extends: the `AddDbContextWithWolverineIntegration` one-transaction outbox, the lowercase-column discipline, the `RegisterCustomer.ValidateAsync` duplicate-email railway idiom + email normalization to reuse.
6. **`src/CritterMart.Orders/{Program.cs,Features/AddToCart.cs}`** — the resource server; `AddToCart.cs:73` is the `[FromHeader("X-Customer-Id")]` binding the cutover helper replaces (prefer `sub`, fall back to header).
7. **`client/src/api/client.ts` + `client/src/identity/useCurrentCustomer.tsx`** — the one-seam header→Bearer swap point ADR 009 engineered; `docs/skills/frontend/SKILL.md` for the SPA conventions.

## Working pattern

1. **Branch** `feat/identity-auth-slices-5-8-5-11` (already created).
2. **ctx7 first for any API you touch** (ASP.NET Core Identity `AddIdentityCore`/`SignInManager`, `JsonWebTokenHandler`, `AddJwtBearer` — .NET 10; already fetched once this session, re-fetch if a signature surprises you).
3. **Identity issuer** — rename the context; add `Auth/{JwtSettings,JwtSigningKeys,JwtTokenIssuer}.cs`; wire `Program.cs`; `RegisterWithCredentials`, `LogIn`, `LogOut`. Unit/integration-test as you go. **Verify Weasel creates the Identity tables** (design.md decision 1's flagged risk) early — boot a test host and check `register`+`login` round-trips before building further.
4. **Orders resource server** — `AddJwtBearer` + `AddAuthorization` + `UseAuthentication/UseAuthorization`; the `sub ?? header` resolution helper; update the customer-keyed endpoints; AppHost config. Add `TokenAuthTests` (valid → sub sourced, invalid/expired → 401, header fallback still works). **Confirm existing Orders tests stay green** (they use the header path).
5. **Frontend** — auth store, seam, `client.ts` Bearer, login/register/logout pages + route gating; Vitest.
6. **Whole-repo build + test green**; then **live-verify** against the real Aspire stack (demo-runbook): register → login → authenticated Orders call (200, sub sourced) → drop token → 401 → confirm no HTTP into Identity; then drive it from the SPA.
7. **Retro** at close — spec-delta closure (4 ADDED requirements), the Weasel-index + fallback-header findings, the CLAUDE.md line update, the DEBT row.

## Deliverable plan (in order)

| File | Status |
|---|---|
| `openspec/changes/slices-5-8-5-11-identity-authentication/*` | new (this session, landed, `--strict` green) |
| `docs/narratives/010-customer-authentication.md` + README count 9→10 | new (this session, landed) |
| `docs/prompts/implementations/037-…md` | new (this file) |
| `Directory.Packages.props` | +`Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.IdentityModel.JsonWebTokens`, `Microsoft.AspNetCore.Authentication.JwtBearer` |
| `src/CritterMart.Identity/Customers/IdentityDbContext.cs` → `CustomerDbContext.cs` | rename + derive `IdentityUserContext<IdentityUser>` |
| `src/CritterMart.Identity/Auth/{JwtSettings,JwtSigningKeys,JwtTokenIssuer}.cs` | new |
| `src/CritterMart.Identity/Features/{RegisterWithCredentials,LogIn,LogOut}.cs` | new |
| `src/CritterMart.Identity/{Program.cs,CritterMart.Identity.csproj}` | modify |
| `src/CritterMart.Orders/Auth/*` + `Program.cs` + customer-keyed endpoints + `.csproj` | modify |
| `src/CritterMart.AppHost/Program.cs` | modify (Jwt config to Orders) |
| `client/src/identity/*`, `client/src/api/client.ts`, login/register pages, routing | modify/new |
| tests (Identity `RegisterWithCredentialsTests`/`LogInTests`; Orders `TokenAuthTests`; Vitest) | new/modify |
| CLAUDE.md non-negotiables line; `docs/skills/DEBT.md` row | modify |
| `docs/retrospectives/implementations/037-…md` | new (at close) |

## Out of scope

- **Authorization** (roles/policies/`[Authorize(Policy=…)]`) — open Q16; the token authenticates, never authorizes.
- **Refresh tokens + server-side revocation/denylist** — open Q15; logout is client-side discard.
- **A JWKS endpoint** — ADR 023 chose config-distributed public key; a JWKS move stays open (open Q17), not taken.
- **Catalog/Inventory browser-facing auth** — no customer-keyed browser endpoints there; Inventory's customer id rides RabbitMQ payload, off the trust path.
- **Removing the `X-Customer-Id` fallback header / migrating seeder+tests+demo-traffic to JWTs** — the hard-cutover follow-up; DEBT-tracked, not this PR.
- **Real secret-store key management** — dev keypair in config; prod override + rotation cadence is open Q17.
- **Any new `CritterMart.Contracts` type, cross-BC message, stream event, saga, or projection** — auth adds none by design; if implementation finds a reason to, stop and raise it.
