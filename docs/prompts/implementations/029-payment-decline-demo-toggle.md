# Prompt: Implementations 029 — Config-gated payment-decline toggle (live-demo affordance)

**Kind**: small **feat** — a **demo affordance**, not new domain behavior. Makes the already-built, already-tested slice-4.6 payment-**decline** path (`OrderCancelled{payment_declined}` + compensating `ReleaseStock`) triggerable in a **live** demo via a config threshold, so the talk can show a decline → cancel → stock-released-back beat without swapping providers or restarting. **No new event, command, handler, projection, aggregate, or saga change. No spec delta** (the behavior is already `order-lifecycle`'s "Cancel an order when payment is declined").
**Source**: the demo smoke-test (this session) surfaced that `StubPaymentProvider` always approves, so the decline path — though fully implemented and covered by `PaymentAuthorizationTests.a_declined_payment_cancels_the_order_and_releases_the_reserved_stock` — could only be seen in tests. The owner asked to make it live-demo-able and picked **Option B** (a `DeclineOverAmount` threshold) over Option A (`DeclineAll`) and the rejected Option C (magic payload value).
**Files touched**: this prompt; `src/CritterMart.Orders/Ordering/PaymentProvider.cs` (new `PaymentDeclinePolicy(decimal? DeclineOverAmount)` record + `StubPaymentProvider` takes it and declines when `Amount > threshold`; default null → approve-all); `src/CritterMart.Orders/Program.cs` (register `PaymentDeclinePolicy` from `Payment:DeclineOverAmount`, mirroring the `PaymentDeadline`/`CartActivityDeadline` config-singleton pattern; default unset); `src/CritterMart.AppHost/Program.cs` (inject `Payment__DeclineOverAmount=100` into Orders, in a loud "DEMO AFFORDANCE — remove after the talk" comment block); `tests/CritterMart.Orders.Tests/StubPaymentProviderTests.cs` (new — threshold unit tests); `docs/demo-runbook.md` (Step 5b decline drive + the affordance box + Known gaps); `docs/{prompts,retrospectives}/README.md` (implementations 28 → 29); `docs/retrospectives/implementations/029-payment-decline-demo-toggle.md` (forthcoming).
**Mode**: solo. **No genuine fork** — the owner chose Option B up front; the implementation shape (a config singleton read from `builder.Configuration`, injected into the provider) is settled by the in-repo `PaymentDeadline` precedent, not re-decided.
**Commit subject**: `feat: config-gated payment-decline toggle for the live demo (Payment:DeclineOverAmount)`

## Framing

The smoke test proved the happy path and the insufficient-stock cancel run clean live, but the
**payment-decline** route — the richer compensation story, because stock is reserved and then released
back — was not reachable in a running stack: `StubPaymentProvider.AuthorizeAsync` hardcodes
`Approved: true`, and the decline branch is exercised only in tests by swapping `DecliningPaymentProvider`.

The decline *flow* is entirely built: `PaymentAuthFailed`, `OrderCancelled(payment_declined)`, the
`PaymentDecisionHandler` decline branch returning a compensating `ReleaseStock`, Inventory's idempotent
release handler, the `OrderStatusView.cancelReason` fold, the `order-lifecycle` spec requirement, and a
passing integration test all exist (slice 4.6). The only missing piece is a **runtime trigger**. This
slice adds exactly that — a config-gated threshold on the stub — and nothing else.

## Goal

With `Payment:DeclineOverAmount` set (the AppHost sets `100` for the demo), the stubbed provider declines
any order whose total exceeds the threshold, so a large order placed live walks
`awaiting_confirmation → stock_reserved → cancelled (payment_declined)` and its reserved stock is
released back to Inventory — visible in the trace / CritterWatch. With the key **unset** (the default,
and in every test), the stub approves everything exactly as before. The knob is loudly documented (code
comments + the runbook) and trivially removable (one AppHost line). Full backend suite green; format clean.

## Spec delta

**None.** This is a demo affordance over already-shipped, already-spec'd behavior — the
`order-lifecycle` requirement *"Cancel an order when payment is declined"* (slice 4.6) is unchanged; the
config toggle only decides *when the stub says no*, which is an implementation/runtime detail, not a
domain SHALL. No OpenSpec change, no workshop or narrative change. The **runbook** (`docs/demo-runbook.md`)
is the durable record of the knob. The retro forward-confirms the named-none.

## Orientation

1. **CLAUDE.md** — one-prompt-one-PR; `{type}/{slug}` branch (`feat/payment-decline-demo-toggle`).
2. **`src/CritterMart.Orders/Ordering/PaymentHandlers.cs`** — the decline branch (`PaymentDecisionHandler`, lines ~57-63) that already appends `PaymentAuthFailed` + `OrderCancelled` and returns `ReleaseStock`. Confirms nothing downstream needs changing.
3. **`tests/CritterMart.Orders.Tests/PaymentAuthorizationTests.cs`** — `DecliningPaymentProvider` + the passing decline test: the proof the chain works, and the "no magic values in the payload, swap the provider" policy this slice keeps (config-gated, not payload-gated).
4. **`src/CritterMart.Orders/Program.cs`** lines ~22-36 — the `PaymentDeadline`/`CartActivityDeadline` config-singleton pattern to mirror for `PaymentDeclinePolicy`.
5. **`src/CritterMart.AppHost/Program.cs`** — the orders resource block where the demo env var is injected.

## Working pattern

`PaymentProvider.cs`: add `PaymentDeclinePolicy`; convert `StubPaymentProvider` to a primary-constructor class taking the policy; decline when `policy.DeclineOverAmount is { } t && command.Amount > t`, else approve. → `Program.cs`: read `Payment:DeclineOverAmount` (nullable decimal), register `new PaymentDeclinePolicy(...)`; default unset → approve-all. → AppHost: `.WithEnvironment("Payment__DeclineOverAmount","100")` on Orders, in a loud removable comment block. → `StubPaymentProviderTests.cs`: unset approves; at/under approves; over declines. → build (incl. AppHost) + the payment/stub tests, then full suite + `dotnet format`. → runbook Step 5b + the affordance box + Known gaps. → README counts + retro. One PR; **owner merges**.

## Deliverable plan

- **`PaymentProvider.cs`** — `PaymentDeclinePolicy(decimal? DeclineOverAmount)` + threshold-aware `StubPaymentProvider(PaymentDeclinePolicy policy)`, with DEMO-AFFORDANCE comments.
- **`Program.cs` (Orders)** — register the policy from config; default unset = approve-all.
- **`Program.cs` (AppHost)** — inject `Payment__DeclineOverAmount=100` for the demo, loudly commented + removable.
- **`StubPaymentProviderTests.cs`** — unset/at-or-under/over cases.
- **`docs/demo-runbook.md`** — Step 5b (the decline drive + the released-stock contrast), the affordance box (on by default, how to change/remove), Known-gaps update.
- **Docs** — README counts 28 → 29; retro 029.

## Out of scope

- **No new domain behavior** — no event, command, handler, projection, aggregate, or saga change. The decline chain already exists (slice 4.6).
- **No spec / workshop / narrative change** — the behavior is already modeled; this is a config-gated stub affordance.
- **No payload magic value** — the decision is config-gated (the codebase's chosen policy), not triggered by a special amount/header in the command.
- **No change to the payment-timeout route** — still config-only (`Orders:PaymentTimeout`); named in the runbook, not touched here.
- **No seed automation** — separate deferred follow-up.
- **No live boot in this session** — the threshold decision is unit-tested and the full chain is already integration-tested; a one-time live re-confirm of the decline→release is noted in the runbook for whoever next boots the stack.
