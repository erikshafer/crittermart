---
retrospective: 003
kind: chore
prompt: docs/prompts/chore/003-pre-frontend-hardening.md
deliverable: src/CritterMart.ServiceDefaults/Extensions.cs, src/CritterMart.{Catalog,Inventory,Orders}/Program.cs, .github/workflows/dotnet.yml, .github/dependabot.yml, docs/research/pre-frontend-endpoint-audit.md
date: 2026-06-13
mode: solo infra; collaborative decisions (scope + design fork chosen by the user)
session-runner: Claude (Opus 4.8)
---

# Retrospective — Chore 003: Pre-frontend hardening (CORS + endpoint audit + CI)

## Outcome summary

A decks-clearing session before round-two frontend work, run as the project's "anything left on the
backend/DevOps/docs side?" checkpoint. Shipped:

- **CORS** — `AddFrontendCors` in `ServiceDefaults/Extensions.cs` (default policy, origins from
  `Cors:AllowedOrigins`, Development fallback `http://localhost:5173`, no credentials per the stubbed
  identity), folded into `AddServiceDefaults()` so all three services register it; `app.UseCors()`
  added to all three pipelines. This realizes the backend half of the ADR 006/015 direct-call SPA shape.
- **Endpoint audit** — `docs/research/pre-frontend-endpoint-audit.md`, checking every customer-facing
  screen in Narratives 002/004 against its read model and current endpoint. One **blocking** gap found
  (open-cart-by-customer read), two minor ones (product detail, order history). No endpoint code written.
- **CI hardening** — a `Format` job (`dotnet format --verify-no-changes`) added to `dotnet.yml`, and a
  new `.github/dependabot.yml` (nuget grouped by stack family: critter-stack / aspire / opentelemetry /
  microsoft-extensions / test-stack, plus github-actions).

Build clean (0 warnings, 0 errors); full suite green (87 tests: Catalog 7, Inventory 14, Orders 63,
CrossBc 3); `dotnet format --verify-no-changes` passed on the existing codebase before the gate was added.

## What worked

- **Grounding the audit in real handler code, not the slice table.** Reading `AddToCart.cs`,
  `BrowseProducts.cs`, and `PlaceOrder.cs` directly is what surfaced the cart read/write keying
  asymmetry — the workshop slice table lists `CartView` as the read model and looks complete; only the
  endpoint signatures reveal that the SPA can't reach it on a cold load. The blocking gap was invisible
  at the spec layer.
- **Folding CORS into `AddServiceDefaults`** kept the per-`Program.cs` change to a single `app.UseCors()`
  line in each — minimal, symmetric, and the same shape across all three services.
- **The AskUserQuestion fork** (audit & defer vs. implement now) kept the cart-gap decision with the
  owner instead of silently expanding scope into endpoint code mid-chore.
- **The gate immediately earned its keep.** On the first CI run it caught pre-existing style drift the
  project had accumulated with no gate to stop it (the maintainer's "we let it slide") — alias `using`
  directives sorted first instead of last (`IMPORTS`) across ~17 earlier-slice files, and a missing
  `public` on `IPaymentProvider.AuthorizeAsync` (`IDE0040`). Both fixed in-PR (the gate-unblock
  exception), `.gitattributes` `* text=auto eol=lf` added for cross-platform parity. A gate that finds
  real debt on day one is the gate working, not the gate failing.

## What was harder than expected

- **Deciding where the cart-by-customer endpoint belongs** was the real judgment call. It is a hard
  blocker with the index already in place, so "just add it" was tempting — but ADR 016's guardrail is
  explicit that a read of a domain fact is a *view slice* to be modeled before it's built. Implementing
  it here would have front-run the very pipeline ADR 016 established. Defer-to-modeling was the
  pipeline-honest answer, confirmed by the user.
- **CORS origins without a frontend to point at.** The real frontend origin doesn't exist yet (the Vite
  app isn't built), so the config is forward-looking: a Dev fallback now, with the AppHost expected to
  inject the real origin via `Cors__AllowedOrigins__*` once `AddViteApp` lands. Resisting the urge to
  hardcode a guessed production origin kept this from becoming churn the frontend session must undo.
- **The format gate passed locally but failed on CI.** The pre-merge check ran
  `dotnet format --verify-no-changes`; CI ran `... --verify-no-changes --no-restore` after an explicit
  `dotnet restore`. The near-equivalent command masked the failures. Reproducing the *byte-identical* CI
  command locally surfaced them at once. Separately, `.editorconfig`'s `end_of_line = lf` plus Windows
  `core.autocrlf=true` produced local-only `ENDOFLINE` noise that CI (Linux/LF) never sees — resolved by
  the `.gitattributes` `* text=auto eol=lf` normalization so the gate is deterministic on both.

## Methodology refinements that emerged

- **An endpoint/read-model audit is a cheap, high-value pre-frontend step** and is worth folding into
  the standard "entering frontend mode" ritual for any event-sourced backend: walk each screen → read
  model → endpoint *against the authored narratives* before modeling UI. It turns "we'll discover gaps
  as we build" into a named list the first modeling session walks in holding.
- **`chore:` sessions that author research content carry the prompt/retro pair** (this one authored an
  audit doc), consistent with the tidy-ceremony rule's "what the session writes, not how long it takes"
  test — even though `chore` is not `tidy`. The two purely-mechanical index bumps here rode along and
  did not, on their own, warrant ceremony.
- **Verify a new CI gate with the byte-identical command it will run, not a near-equivalent.** When
  introducing a `dotnet format` (or any) gate, run the exact CI invocation locally — flags and all
  (`--no-restore` after an explicit `restore`) — before trusting it. And when a gate's correctness
  depends on file bytes (line endings), pin them in `.gitattributes` so Windows and Linux agree;
  otherwise the gate is non-deterministic across the contributors who run it.

## Outstanding items / next-session inputs

- **The frontend-mode entry is now unblocked.** Step one is the ADR 016 workshop amendment (add the
  `Wireframe` column + model the net-new view slices) and a customer-journey narrative. The audit doc is
  its #1 input — **model Gap #1 (open-cart-by-customer, provisional slice 3.5) first**; it is the only
  blocking gap and the unique computed index on `CartView.CustomerId` already exists.
- **CORS needs the real origin wired** when the Vite app lands: the AppHost should inject the frontend
  origin into each service (`Cors:AllowedOrigins`), and an `npm` ecosystem block should be added to
  `.github/dependabot.yml` at the same time.
- **Round-one-completion marker** was offered and not taken this session; available as a small standalone
  session if wanted.
- **Stale memory** (`next-pickup`) should be refreshed — it predates round-one completion.

## Spec-delta — landed?

**Named none; forward-confirmed none.** The prompt named no slice/capability/narrative/workshop delta —
this is backend infra (realizing existing ADR 006/015) plus research evidence plus repo-meta. No
canonical spec changed, and none should have. The audit doc is decision-evidence that *feeds* a future
ADR 016 amendment; it does not itself amend a spec.
