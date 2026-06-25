# docs/research/

Exploratory work — spikes, technology comparisons, investigations, and *this-isn't-a-decision-yet* drafts. See [CLAUDE.md § Research](../../CLAUDE.md) for the routing-layer treatment of where research sits in the pipeline.

Research keeps exploratory work **out of** the canonical document layers (workshops, narratives, OpenSpec proposals, ADRs) while still version-controlling it. A research doc has no authority — it informs decisions but does not *make* them. When a research doc's findings warrant a binding choice, that choice is promoted to an **ADR** in [`../decisions/`](../decisions/); when it warrants build work, it becomes a **prompt** in [`../prompts/`](../prompts/). The research doc stays put as the durable record of *how the question was investigated*.

## File naming

`{slug}.md` — a short kebab-case identifier describing the investigation (e.g., [`otel-trace-walkthrough.md`](otel-trace-walkthrough.md), [`mmoreconnect-overlap-audit.md`](mmoreconnect-overlap-audit.md)).

Unlike workshops, narratives, prompts, retrospectives, and ADRs, research docs are **not** numbered — there is no meaningful sequence to a spike, and a slug stays discoverable when investigations are revisited out of order. (Some research *sessions* do carry numbered prompt/retro pairs under [`../prompts/research/`](../prompts/research/) and [`../retrospectives/research/`](../retrospectives/research/); the research doc itself is still slug-named.)

## Format

Research docs are looser than the canonical layers, but the in-repo precedents share a shape worth keeping:

- **Frontmatter** — `version`, `status` (`Active` | `Superseded` | `Archived`), `date`, and a `references:` list of the artifacts the investigation touched. (Older docs predate this convention; add it when you next edit one.)
- **A leading blockquote** stating *what this is* and, crucially, *what it is not* — explicitly disclaiming decision/build-order authority so a future reader doesn't mistake a spike for a commitment.
- **A "Bottom line"** up top — the finding, before the supporting detail.

## Promotion path

A research doc is a waypoint, not a terminus. Its findings travel:

- **→ ADR** when a binding, cross-cutting decision falls out of it (e.g. [`frontend-cross-repo-comparison.md`](frontend-cross-repo-comparison.md) fed ADRs 015/016/018).
- **→ prompt** when it scopes build work for a session.
- **status: Superseded** when a later doc or decision overtakes it — cross-reference the successor rather than deleting the original.

## Index

| Doc | What it investigates | Status |
| --- | --- | --- |
| [ecommerce-engineering-lessons](ecommerce-engineering-lessons.md) | Engineering lessons from production e-commerce systems, mined for CritterMart's reference architecture | Active |
| [ecommerce-frontend-stack](ecommerce-frontend-stack.md) | Survey of candidate frontend stacks for the round-two storefront | Active |
| [frontend-cross-repo-comparison](frontend-cross-repo-comparison.md) | Frontend approach compared across sibling repos; plan refinement (fed ADRs 015/016/018) | Active |
| [pre-frontend-endpoint-audit](pre-frontend-endpoint-audit.md) | Read-model endpoint audit taken before frontend work began | Active |
| [otel-trace-walkthrough](otel-trace-walkthrough.md) | Teaching walkthrough of the cross-service purchase trace in the Aspire dashboard | Active |
| [mmoreconnect-overlap-audit](mmoreconnect-overlap-audit.md) | Fixed-surface (ports/containers) collision audit vs. the sibling MmoReconnect app | Active |
| [e2e-reqnroll-aspire-testing](e2e-reqnroll-aspire-testing.md) | Current test coverage, gaps, and the aspiration for Reqnroll + Aspire.Hosting.Testing E2E | Active |
| [e2e-strategy-conceptual-plan](e2e-strategy-conceptual-plan.md) | Conceptual plan for the Reqnroll + Aspire E2E layer — pyramid placement, suite shapes, CI job, pipeline path (companion to the aspiration note; feeds ADR 022) | Active |
| [cw-telemetry-fodder](cw-telemetry-fodder.md) | CritterWatch telemetry spike — async daemon + 4 projection/messaging shapes (gated on `Cw:Telemetry`) to light the monitoring console for a UI/UX review | Active |
| [cw-feedback-jasperfx](cw-feedback-jasperfx.md) | Distilled, screenshot-anchored UI/UX feedback packet for JasperFx (Jeremy/Babu) on CritterWatch 1.0.0-alpha.3, with [`cw-screenshots/`](cw-screenshots/README.md) evidence | Active |
| [cw-alpha4-and-deep-round-plan](cw-alpha4-and-deep-round-plan.md) | Plan for the alpha.4 upgrade assessment + the deep DX/UX feedback round (handoff to the higher-effort session) | Active |
| [cw-feedback-jasperfx-deep](cw-feedback-jasperfx-deep.md) | Deep round — adds a DX lens (install/version-coupling/licensing/MCP/docs accuracy) + interaction-level UI/UX (axe-core a11y, narrow-viewport, dark mode, deep-linking) on alpha.3, with round-one closure and the alpha.4 recommendation; evidence in [`cw-screenshots/deep/`](cw-screenshots/deep/) | Active |

Keep this table in sync when a research doc is added or its status changes — it is the discoverability payload of this README.

## Cross-references

- [CLAUDE.md § Research](../../CLAUDE.md) — research routing-layer treatment.
- [`../decisions/`](../decisions/) — where research findings are promoted to binding decisions.
- [`../prompts/research/`](../prompts/research/) and [`../retrospectives/research/`](../retrospectives/research/) — per-research-session intent and outcome records, where a research session ran the prompt/retro pipeline.
