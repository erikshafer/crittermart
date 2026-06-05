# ADR 016: Frontend Modeled Through the Full Pipeline — UI First-Class in the Event Model

**Status**: Accepted

## Context

Round one shipped seventeen modeled slices across Catalog, Inventory, and Orders, but no frontend. The Event Model carries the UI only as storyboard "moments" — workshop § 3 says "two screens flank the timeline" without drawing them, and the § 6 slice table has a `View` column (naming the read model a screen would bind to) but no `Wireframe` dimension. Round two requires a real storefront ([ADR 015](015-vite-react-frontend-stack.md)). The question this ADR settles is *at what grain the frontend becomes traceable work*: modeled through the full pipeline like every backend slice, built as a thin client outside the modeled boundary, or something between.

The tension is real and was worked through deliberately. Event Modeling (Dymitruk-style) treats the **wireframe as a first-class row**, not an afterthought: command slices are `wireframe → command → event`, view slices are `event → read model → wireframe`. By that lens, UI *belongs* in the model — round one simply under-ran the view half because there was no frontend to demand it. The opposing pull is that minting domain events for **pure presentation state** (a modal opening, a theme toggling, a pagination cursor) would be the first genuine corruption of an otherwise clean event model.

The decision clears the ADR bar: it is a methodology choice touching the whole SDD pipeline, the grain is non-obvious (it sits between two tempting extremes), and a later contributor would otherwise re-derive it.

## Decision

The frontend runs the **full SDD pipeline** — workshop → narrative → OpenSpec proposal → prompt → implementation → retrospective — the same loop the backend slices used. The UI is first-class in the Event Model, realized two ways:

1. **Existing customer-facing slices gain a `Wireframe` dimension.** A `Wireframe` column is added to the workshop § 6 slice table, and the storyboard "moments" become actual sketches tied to their commands and views.
2. **Under-modeled view/query slices are modeled.** The screens round one never needed without a frontend — catalog browse-and-detail, cart review, order tracking — are modeled as legitimate `event → projection → wireframe` view slices. They mostly add no new events; modeling them confirms (or exposes gaps in) the read models the UI binds to (`ProductCatalogView`, `CartView`, `OrderStatusView`).

The decision is bounded by a **presentation-state guardrail**, applied per interaction:

| The interaction… | …is modeled as | Where it lives |
| --- | --- | --- |
| **reads** a domain fact (browse, cart review, order status) | a **view / query slice** + wireframe | workshop |
| **produces** a domain fact (add to cart, place order) | already modeled — **attach a wireframe** to the existing command slice | workshop |
| pure presentation state (modal open, pagination, theme) | **not an event** | frontend code + narrative only |

The realizing artifacts (forthcoming, in their own PRs) are a **proportional** workshop amendment — the `Wireframe` column plus sketches of the net-new view slices, *not* a full per-slice wireframe re-draw — and one or more customer-journey narratives threading the wireframe-bearing slices into a coherent experience (browse → cart → checkout → track).

## Consequences

The frontend gains a durable, version-controlled artifact trail — workshop slice, narrative, OpenSpec proposal, prompt, retro — so the spec-delta closure loop applies to it exactly as to backend work. That traceability from modeled scenario to code is the thing CritterMart exists to demonstrate; a frontend with no trail would have been off-brand for the project's thesis. The Event Model finally exercises its view half, which makes the storefront a teachable artifact rather than glue. The choice honors Event Modeling's treatment of the wireframe as first-class.

The guardrail keeps the weave honest: presentation-only state never reaches the event stream. This is compatible with the context map's line that "product information flows through the frontend… presentation-layer composition, not a bounded-context integration" — that statement is about the cross-BC *integration* axis (the frontend joining Catalog data into Cart commands), whereas per-slice wireframe modeling is the *modeling* axis. The two do not conflict.

The accepted cost is more ceremony than a pure thin client. It is deliberate and proportional: the workshop amendment is a column plus a handful of sketches, not a workshop rewrite.

Rejected alternatives. **Full modeling that mints UI "slices" with new commands and events for screens** would push presentation into a layer built for domain behavior — a category blur of presentation versus domain, and the first real corruption of the event model. **A pure thin client outside the pipeline** would be fastest to a demo but forfeits the traceability that is the project's whole point; the frontend would have a stack but no spec. The chosen weave takes the modeling rigor of the first and the journey-coherence of the second without either's failure mode.

This ADR pairs with [ADR 015](015-vite-react-frontend-stack.md) (the stack the frontend is built on) and is realized by a forthcoming workshop amendment and customer-journey narrative(s). It does not change the per-slice loop defined in `CLAUDE.md`; it confirms that the frontend is inside that loop, not beside it.
