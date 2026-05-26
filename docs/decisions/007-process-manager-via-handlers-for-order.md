# ADR 007: Process Manager via Handlers for the Order Aggregate

**Status**: Accepted

## Context

The Place Order flow coordinates work across Inventory (stock reservation), Payments (stubbed for round one), and Orders itself. Candidate patterns included Wolverine Saga, a separate process-manager state stream alongside Order, or making the Order aggregate itself the process manager using the Process Manager via Handlers (PMvH) pattern.

## Decision

The Order aggregate itself serves as the process manager, using PMvH as documented in Wolverine PR #2579 and demonstrated in the `ProcessManagerViaHandlers` sample. State flags (`StockReserved`, `PaymentAuthorized`) track progress; terminal events (`OrderConfirmed`, `OrderCancelled`) close the stream; a scheduled `OrderPaymentTimeout` self-message is idempotent by construction via state guards.

## Consequences

One aggregate, one stream, full lifecycle event-sourced, with a clean pedagogical narrative. Pure-function handlers that unit-test without Wolverine or Marten. The talk cites the PMvH documentation as the recipe. Tradeoff: forgoes the `Wolverine.Saga` base class; intentional — the PMvH pattern is the point.
