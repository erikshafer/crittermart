# CritterMart — AI Development Guidelines (AGENTS.md)

> **For Anthropic Claude:** the primary instruction file is `CLAUDE.md` in this directory.
> **For all other AI agents (Codex, Grok, Gemini, etc.):** the canonical guidelines live in `CLAUDE.md`. Read that file in its entirety before taking any action in this repository. Everything in it applies to you.

## Why CLAUDE.md is the source of truth

This project maintains a single, version-controlled AI instruction file (`CLAUDE.md`) to avoid drift between agent-specific copies. `AGENTS.md` is intentionally thin — it exists only so that tools that look for the cross-vendor `AGENTS.md` convention find a valid pointer.

## What you will find in CLAUDE.md

- Project vision and purpose
- The two-phase design-and-build pipeline (pre-code design → per-slice implementation loop)
- Directory layout and artifact layer map
- Tech stack and architectural non-negotiables
- Operating disciplines (one-prompt-one-PR, spec-delta closure, no opportunistic edits, design-return cadence)
- Routing table to all canonical artifact directories (`docs/`, `openspec/`, etc.)

## Quick orientation

| File | Purpose |
|---|---|
| [`CLAUDE.md`](CLAUDE.md) | **Read this.** Full AI development guidelines. |
| [`docs/vision.md`](docs/vision.md) | What CritterMart is and why it exists. Read second. |
| [`docs/rules/structural-constraints.md`](docs/rules/structural-constraints.md) | Terse, imperative architectural constraints. |
| [`docs/decisions/`](docs/decisions/README.md) | ADRs — significant architectural decisions. |

---

*If your tool supports reading `CLAUDE.md` natively, prefer it over this file — it is always more up to date.*
