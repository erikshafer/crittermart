## Context

Slice 1.3 adds `ChangeProductPrice` to the existing Catalog service. The `Product` document, `ProductCatalogView`, and the per-product audit stream all exist (slices 1.1/1.2); this slice loads a product, updates its price, and appends a **second event kind** (`ProductPriceChanged`) to its stream. Cross-cutting decisions are inherited by reference: the document-store + audit-stream pattern and "document is source of truth" (slice 1.1 `design.md` Decisions 1–2), Wolverine.Http (ADR 006), and the stack/codegen posture (ADR 012). Authored in the consolidated one-PR slice mode (see the slice retrospective).

## Goals / Non-Goals

**Goals:** change a published product's price; record `ProductPriceChanged` (old + new) on the product's stream; update the document price; have the listing reflect the new price.

**Non-Goals:** no price-change notification to carts/Orders (round-one non-event); no scheduled or promotion-bound pricing (parked); no new document type; no change to publish or browse.

## Decisions

### 1. Load the `Product` by SKU; an unknown SKU yields a 404

The endpoint loads the existing `Product` by its SKU before mutating. A missing product returns a `ProblemDetails` `404`.

*Implementation note:* the idiomatic Critter Stack path is Wolverine's `[Entity]` declarative load (auto-404 on null, handler stays a pure function over the loaded entity). Use `[Entity]` **if** it binds cleanly to the string SKU from the route (verify via `ctx7`); otherwise fall back to an explicit `IDocumentSession.LoadAsync<Product>(sku)` + null → `ProblemDetails(404)`. Either way the not-found behavior is the same.

*Spec note:* Workshop 001 § 6.1 for slice 1.3 is **happy-path only**. The 404 is a **defensive engineering default** covered by a test, **not** a spec scenario — recorded in the retro rather than back-filled into the workshop (which would need its own workshop-tidy session).

### 2. `ProductPriceChanged` carries old + new price; appended to the existing stream

The handler reads the loaded product's current price (the **old** price — knowable only from the document), appends `ProductPriceChanged(sku, oldPrice, newPrice, changedBy, at)` to the product's SKU-keyed stream via `Events.Append` (**not** `StartStream` — the stream already exists from `ProductPublished`), and sets `Product.Price = newPrice`. The document update and the event append commit in **one transaction** (`AutoApplyTransactions`).

*Why:* the old price must come from the loaded document, hence load-then-append. `Append` (not `StartStream`) because the stream exists. The audit stream now reads `ProductPublished` → `ProductPriceChanged` — a real, growing history, which is the slice's pedagogical point.

### 3. Endpoint: `POST /products/{sku}/price`

A price change is scoped to a specific product resource. The SKU comes from the route; the new price from the body. Returns `200` on success. (Verify route-parameter binding via `ctx7`; if route binding is awkward, fall back to a body-carried command at `POST /products/change-price`.)

## Risks / Trade-offs

- **`[Entity]` / route binding for a string-keyed document is unverified** → mitigated by `ctx7` verification and the explicit-`LoadAsync` fallback (Decision 1).
- **Browse reflecting the new price depends on the document update, not the event** → correct: the document is the source of truth and `ProductCatalogView` queries it; the event is audit-only.
- **The not-found path is tested but unspecced** → intentional (Decision 1 spec note); flagged in the retro as a candidate for a future workshop-tidy that adds the failure GWT.
