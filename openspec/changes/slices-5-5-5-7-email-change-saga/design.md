## Context

CritterMart's second convention `Wolverine.Saga`. An `EmailChange` saga, keyed by `CustomerId`, opens when a
customer requests an email change, applies it on a timely confirmation, and drops it on timeout. Unlike
`Replenishment` (Saga #1), every trigger here is customer-facing HTTP, not a system-internal message — Saga
#1 has no HTTP-driven saga precedent in this codebase, so the HTTP-wiring decisions below are new ground,
verified against current Wolverine docs (ctx7 `/jasperfx/wolverine`) rather than assumed from Saga #1's
shape. See the proposal for the Why/What and the `customer-registry` spec delta for the SHALLs; this
document records the implementation decisions.

## Goals / Non-Goals

**Goals:**
- Ship the `EmailChange` saga EF-Core-backed, riding the existing `AddDbContextWithWolverineIntegration`
  wiring — no new infrastructure.
- Keep every saga message Identity-local, exactly mirroring `Replenishment`'s "no new `Contracts` type"
  posture.
- Make the confirm-or-expire window demoable at speaking pace (short deadline default), mirroring
  `ReplenishDeadline`.

**Non-Goals:**
- No proof the customer controls the new email's inbox (no confirmation token/link) — ADR 009's no-auth
  stance is unchanged; this is out of scope per the workshop's "don't invent a workflow" framing.
- No new `CustomerEmailChanged` Published-Language event — no consumer needs one yet.
- No re-armed deadline on re-request — see Decision 4.

## Decisions

1. **REVISED after an empirical failure — `[WolverinePost]` does NOT go on the saga class itself.** The
   original plan (below, struck through in spirit not text, kept for the record) put
   `[WolverinePost("/customers/{customerId}/email-change")]` directly on
   `EmailChange.StartOrHandle(RequestEmailChange, EmailChangeDeadline)`, reasoning from two confirmed docs
   patterns: a handler method can double as an HTTP endpoint (`guide/http/validation.md`'s
   `NumberMessageHandler.Handle`), and starting a saga from HTTP is established (`guide/http/sagas.md`'s
   `Reservation` example). **This failed empirically** the moment a *second* `[WolverinePost]` method
   (`ConfirmEmailChange`'s) was added to the *same* saga class: Wolverine's HTTP chain builder threw
   `JasperFx.CodeGeneration.UnResolvableVariableException` while building the `/email-change` chain, because
   it pulled in a dependency (`ConfirmEmailChange`) belonging to the *other* chain — Wolverine's HTTP codegen
   does not partition a class's `[WolverinePost]`-annotated methods by message-type as cleanly as its
   message-bus dispatch does. **Fix, verified by re-running the integration tests:** the HTTP surface moved
   to two separate static endpoint classes, `RequestEmailChangeEndpoint` and `ConfirmEmailChangeEndpoint`
   (in `RequestEmailChange.cs` / `ConfirmEmailChange.cs`), each `[WolverinePost]`-annotated on its own `Post`
   method, which dispatches into the (now HTTP-attribute-free) `EmailChange` saga via
   `IMessageBus.InvokeAsync(command)` — confirmed synchronous, in-process (ctx7
   `guide/messaging/message-bus.md`). The saga class itself is now a **pure** message handler, exactly like
   `Replenishment` — no `[WolverinePost]` anywhere on it. **One `StartOrHandle` method, not separate static
   `Start` + instance `Handle`, still holds** — that part of the original reasoning was correct and unaffected
   by the fix (repeating that split for a message that both starts and continues a saga is the exact mistake
   the Saga #1 handoff calls out).

2. **Guards run as a `ValidateAsync(RequestEmailChange, IdentityDbContext)` static method on
   `RequestEmailChangeEndpoint`, returning `ProblemDetails`** — the same railway idiom
   `RegisterCustomer.ValidateAsync` already establishes in Identity, confirmed composable with any
   `[WolverinePost]` method by `guide/http/validation.md`'s `Validate`/`ProblemDetails` pattern (this part of
   the plan was unaffected by Decision 1's revision — the guard just moved to the new endpoint class
   alongside its `Post`). Two checks, in order: `CustomerNotFound` (404) if no `Customer` row exists for
   `customerId`; `EmailAlreadyRegistered` (409) if the normalized `newEmail` already belongs to a *different*
   customer. Both checks run **before** dispatch, so a rejected request opens no saga and schedules no
   timeout — no row-level cleanup needed on the reject path.

3. **`ConfirmEmailChangeEndpoint`'s `ValidateAsync` loads the `EmailChange` row directly via EF Core**
   (`db.EmailChanges.FindAsync(customerId)`), rather than reading a saga instance Wolverine loaded for it —
   a direct consequence of Decision 1's revision (the endpoint is no longer instance-scoped on the saga).
   If found, checks whether its `PendingEmail` is already registered to a different customer —
   `EmailChangeConflict` (409) if so; the saga stays open (`Handle(ConfirmEmailChange)` never runs, so
   `MarkCompleted()` never fires). If not found (window expired, already confirmed), `ValidateAsync` returns
   `WolverineContinue.NoProblems` and lets dispatch reach `EmailChange.NotFound(ConfirmEmailChange)` — the
   mandatory silent no-op (Wolverine throws otherwise on a non-start message for a missing saga; the Saga #1
   finding, unchanged).
   **The design's flagged open question is now answered empirically, not assumed:** does `NotFound` surface
   as HTTP `404` through `IMessageBus.InvokeAsync`? **No.** A void-returning `Post(command, IMessageBus bus)
   => bus.InvokeAsync(command)` returns Wolverine.Http's default success status for an action with no
   response value — **`204 No Content`**, confirmed by the integration tests — regardless of whether the
   saga was found. A confirm against an absent saga and a confirm that actually applied the change are
   **indistinguishable at the HTTP layer** (both `204`). This is judged the *more* faithful reading of the
   spec's "silent no-op" than an explicit 404 would have been (the caller is told nothing went wrong), so the
   originally-planned existence-pre-check-for-a-controllable-404 fallback was **not** added — it would have
   manufactured a distinction the spec never asked for.

4. **Re-request updates `PendingEmail`, never reschedules `EmailChangeTimeout`.** `StartOrHandle` detects
   "already open" the same way `Replenishment` does — a blank instance's default state (`PendingEmail ==
   null`/empty) means new; a non-empty `PendingEmail` means an open saga, so only assign `PendingEmail` and
   return no cascaded `EmailChangeTimeout`. Rescheduling would leave the *original* timeout still armed
   (Wolverine has no scheduled-message cancellation), which would fire and drop the "reset" window early — a
   defect caught during workshop review (Workshop 002 v1.1 § 8, item 7) and designed against here rather than
   reintroduced.

5. **`EmailChangeTimeout` is purely message-bus/system-triggered — no HTTP surface.** `Handle(EmailChangeTimeout)`
   drops the pending change (no `Customer` row write) and calls `MarkCompleted()`; `NotFound(EmailChangeTimeout)`
   is the silent no-op for a timeout delivered after the saga already resolved (identical shape to
   `Replenishment.NotFound(ReplenishTimeout)`).

6. **The deadline is config-driven via an `EmailChangeDeadline` singleton**, mirroring `ReplenishDeadline`
   exactly: `record EmailChangeDeadline(TimeSpan Duration)` with a short `static readonly Default` (demoable
   at speaking pace — Workshop 002/the research frame the production duration as 24h; the shipped default is
   a demo-paced stand-in, exactly as `ReplenishDeadline.Default` is 2 minutes, not `ReplenishDeadline`'s
   implied production hours), bound in `Program.cs` from `Identity:EmailChangeTimeout` via
   `GetValue<TimeSpan?>(...) ?? Default` + `AddSingleton`, injected into `StartOrHandle`.

7. **The confirm-time conflict check is application-level and racy on its own** — `db.Customers.AnyAsync(c
   => c.Email == pendingEmail && c.Id != customerId)` — with the existing `ux_customers_email` unique index
   as the true backstop against a same-window race (identical posture to `RegisterCustomer`). A
   unique-violation surfacing at commit despite the app-level guard passing is a narrow, documented residual
   risk, not specially caught in this pass — consistent with `RegisterCustomer` not catching one either.

8. **`EmailChange` is a `DbSet`-mapped entity on `IdentityDbContext`**, same lowercase-column discipline as
   `Customer` (`OnModelCreating`), string-keyed (`Id = CustomerId`), riding the existing
   `UseEntityFrameworkCoreWolverineManagedMigrations` path — no extra DDL, no secondary index (keyed by PK).
   **A second, undocumented casing gap found empirically:** the base `Wolverine.Saga` class unconditionally
   declares `public int Version { get; set; }` (reflection-confirmed — it backs `IRevisioned`-style
   optimistic concurrency even for sagas, like `EmailChange`, that don't implement `IRevisioned`). EF Core's
   default convention maps it regardless, and Wolverine's own generated saga-loading query references it —
   omitting an explicit mapping produced a live `"column e.Version does not exist"` failure (Postgres has
   `version`, unquoted-folded by Weasel; EF queried `"Version"`, quoted) — the exact same class of bug
   `Customer`'s columns already guard against, just for an inherited property easy to miss because it isn't
   declared in `EmailChange`'s own source. Fixed by mapping it explicitly (`HasColumnName("version")`)
   alongside `Id`/`PendingEmail`, rather than `Ignore`-ing it, since Wolverine's own persistence machinery
   depends on the column existing.

9. **Every saga message is Identity-local.** `RequestEmailChange`, `ConfirmEmailChange`, and
   `EmailChangeTimeout` are routed in-process; no new `CritterMart.Contracts` type, no new broker
   exchange/queue. `[SagaIdentity]` annotates the `CustomerId` property of all three (none is named
   `Id`/`SagaId`).

## Risks / Trade-offs

Both risks originally listed here were resolved empirically during implementation (kept below with their
outcomes, not deleted, per the project's own "if implementation proves X, amend this decision" convention):

- **[Resolved] `[WolverinePost]` directly on multiple saga instance methods in one class.** → Failed with
  `UnResolvableVariableException`; fixed by moving the HTTP surface to separate endpoint classes dispatching
  via `IMessageBus.InvokeAsync` (Decision 1).
- **[Resolved] `NotFound` → HTTP status code translation.** → Confirmed **not** automatic; a void `Post`
  returns `204` regardless of whether the saga was found, which reads as a *more* faithful "silent no-op"
  than a manufactured 404 (Decision 3).
- **[Resolved] The base `Saga` class's inherited `Version` property needed explicit EF mapping** — an
  undocumented casing gap, not anticipated by this design pass; found only by running the integration tests
  against the real host (Decision 8).
- **[Trade-off, accepted] The re-request window is anchored to the first request, not the latest
  (Decision 4).** A customer who re-requests gets less than a full fresh window. Accepted: the alternative
  (a generation marker so stale timeouts can no-op) adds saga-state complexity for a demo-scale edge case;
  revisit only if a real product reason surfaces (Workshop 002 § 8, item 7).

**Methodology note for future sagas:** this design pass's ctx7-sourced HTTP-wiring plan (Decision 1's
original form) was reasoned soundly from real documentation and still failed in practice — the codegen
conflict and the `Version` column gap were both only findable by actually running the host. Neither ctx7 nor
Saga #1's precedent (which has no HTTP-driven saga trigger to compare against) could have surfaced them.
Treat "verify empirically" as load-bearing advice for any future saga design, not a formality.
