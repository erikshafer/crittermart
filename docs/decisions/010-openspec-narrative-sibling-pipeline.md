# ADR 010: OpenSpec + Sibling Narrative for the SDD Pipeline

**Status**: Accepted

## Context

CritterMart is a sandbox for a more disciplined Spec-Driven Development pipeline than CritterSupply used. The question was whether to use OpenSpec alone (machine-readable), narratives alone (human-readable), or both as siblings.

## Decision

Per slice, both an OpenSpec proposal and a narrative are authored before the implementation prompt. Same source (the event-modeling slice), same scope (one slice), two artifact shapes (machine-readable SHALL spec and human-readable journey prose), two audiences. Both are reference points for the implementation prompt; both must agree.

## Consequences

Small additional authoring cost per slice. Repaid when the talk's pipeline-reveal section shows narrative → spec → code, when contributors read the narrative first and the spec second during onboarding, and when implementation prompts have an unambiguous spec to satisfy. The spec-delta closure loop in `CLAUDE.md` keeps both in sync with the code.
