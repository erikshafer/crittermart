# ADR 008: Inline Snapshot Projections, One Async Teaser, No Daemon for Round One

**Status**: Accepted

## Context

Marten supports inline (synchronous, in-transaction) and async (background daemon) projections. Round one favors simplicity but also wants to teach that projections can be rebuilt asynchronously when scale or read-model complexity demands it.

## Decision

All event-sourced aggregates (Cart, Order, Stock) use `SnapshotLifecycle.Inline`. One async projection lives somewhere in the codebase as a teaser for the "and you can also rebuild asynchronously" beat of the talk. No async daemon is driven in the demo path for round one.

## Consequences

Synchronous projections are simpler to reason about, easier to debug, and produce consistent reads immediately after writes. The async teaser plants a seed for round two and follow-up content. Tradeoff: real systems extract heavier projections to async, and the talk acknowledges this.
