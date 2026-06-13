# Prompt: Chore 003 — Pre-frontend hardening (CORS + endpoint audit + CI)

**Kind**: chore (cross-cutting backend + repo-meta; clears the decks before round-two frontend work)
**Files touched**: this prompt; `src/CritterMart.ServiceDefaults/Extensions.cs` (`AddFrontendCors`); `src/CritterMart.{Catalog,Inventory,Orders}/Program.cs` (`app.UseCors()`); `.github/workflows/dotnet.yml` (Format job); `.github/dependabot.yml` (new); `docs/research/pre-frontend-endpoint-audit.md` (new); `docs/retrospectives/chore/003-pre-frontend-hardening.md` (forthcoming); `docs/retrospectives/README.md` (population count)
**Mode**: solo infra; collaborative decisions (per the lifted act-on-leans autonomy) — scope and the one design fork chosen by the user via AskUserQuestion
**Commit subject(s)**: `chore: pre-frontend hardening — CORS, endpoint audit, CI format gate + Dependabot` + `docs: pre-frontend-hardening prompt + retro`

## Framing

Round-one backend is complete (all 17 modeled slices shipped; slice 2.4 landed 2026-06-13). The
next effort is the round-two frontend, decided in [ADR 015](../../decisions/015-vite-react-frontend-stack.md)
(Vite + React SPA) and [ADR 016](../../decisions/016-frontend-full-pipeline-ui-first-class.md)
(UI modeled through the full pipeline). Before switching into "frontend mode," this session clears
the backend/CI items that are cheaper to do now than to context-switch back for. It is a `chore`,
not a slice — the one-PR-per-slice rule doesn't apply, but it's naturally one PR.

Scope (chosen by the user): **CORS + read-model endpoint audit** and **CI/DevOps hardening**.
Deliberately *not* in scope this session: a round-one-completion marker, and the frontend-mode
entry itself (the ADR 016 workshop amendment + customer-journey narrative).

## Goal

1. The three Wolverine.Http services accept cross-origin calls from the round-two SPA (ADR 006/015 shape: SPA → three services, no BFF). Origins config-driven; Development falls back to the Vite dev origin.
2. A version-controlled audit of the read-model query endpoints the customer journeys need vs. what exists, feeding the first frontend modeling session.
3. CI gains a `dotnet format` style gate and automated, grouped dependency updates.

## Spec delta

No slice behavior; no capability/spec change; no narrative or workshop amendment. Realizes the
backend half of ADR 006/015 (CORS for the direct-call SPA shape) and produces decision-evidence
(`docs/research/pre-frontend-endpoint-audit.md`) for the forthcoming ADR 016 view-slice modeling.
No new ADR (CORS realizes existing decisions; the audit is research, not a decision).

## Design fork (resolved)

The audit surfaced one **blocking** gap: the cart's write side is customer-keyed (server resolves
the open cart) but the only read is `GET /carts/{cartId}`, which the SPA can't satisfy on a cold
load. **Decision (user): audit & defer** — document it as the #1 input to the ADR 016 workshop
amendment (a `CartView → GET /carts/mine` view slice; the unique computed index on
`CartView.CustomerId` already exists), and write **no endpoint code** this session. This keeps the
SDD pipeline honest: reads-of-a-domain-fact are modeled as view slices before they're built.

## Orientation

1. **ADR 006 / 015 / 016** — the no-BFF direct-call shape, the stack, and the UI-in-the-pipeline grain.
2. **`src/CritterMart.ServiceDefaults/Extensions.cs`** — `AddServiceDefaults()` is where the shared CORS registration plugs in; each `Program.cs` ends with `app.MapWolverineEndpoints()` (where `app.UseCors()` slots in just before).
3. **Narratives 002 + 004** — the authored customer journeys the endpoint audit checks against.
4. **`.github/workflows/dotnet.yml`** + `Directory.Packages.props` (root CPM) — the CI surface and the dependency manifest Dependabot reads.

## Working pattern

1. CORS: `AddFrontendCors` in ServiceDefaults (default policy, origins from `Cors:AllowedOrigins`, Dev fallback `http://localhost:5173`, no credentials per stubbed identity); `app.UseCors()` in all three pipelines. Build + confirm tests stay green (Origin-less Alba requests pass through CORS untouched).
2. Endpoint audit: walk each journey screen → read model → endpoint; record gaps and recommended view slices in `docs/research/`.
3. CI: a `Format` job running `dotnet format --verify-no-changes` (confirmed the codebase passes clean first); `.github/dependabot.yml` (nuget grouped by stack family + github-actions; npm deferred until the Vite app lands).

## Out of scope

- **Any read-model endpoint code** (Gaps #1–#3) — deferred to the ADR 016 view-slice modeling, per the resolved fork.
- **Round-one-completion marker / closing retro** — a separate session if wanted.
- **The frontend-mode entry** (ADR 016 workshop amendment + customer-journey narrative; Vite skeleton bootstrap) — this is step one of frontend mode, not pre-work.
- **npm Dependabot ecosystem** — added when the Vite app exists.
- No structural-constraints edit, no openspec change, no ADR.
