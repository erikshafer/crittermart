# Prompt: Narrative 001 — The Seller Manages the Catalog

**Kind**: pre-code design (narrative)
**Files touched**: `docs/prompts/narratives/001-seller-manage-catalog.md` (new); `docs/narratives/001-seller-manage-catalog.md` (new); `docs/narratives/README.md` (population line); `docs/prompts/README.md` (population line); `docs/retrospectives/narratives/001-seller-manage-catalog.md` (forthcoming, authored at session close)
**Mode**: solo authoring — single session writes the first CritterMart narrative against Workshop 001 slice 1.1; surfaces the conventions later narratives will inherit.
**Commit subject**: `docs: add seller-catalog narrative covering slice 1.1 publish-product`

## Framing

This is the first narrative authored under the per-slice implementation triangle (CLAUDE.md § 4–5) and the first session in the slice 1.1 triangle (narrative → OpenSpec proposal → implementation prompt). It precedes the slice 1.1 OpenSpec proposal (forthcoming) and the slice 1.1 implementation prompt (forthcoming).

The triangle's authoring order — **narrative first, OpenSpec proposal second, implementation prompt third** — was chosen in conversation. Workshop 001 § 6.1 already supplies precise GWT scenarios for slice 1.1, which makes the OpenSpec proposal's SHALL translation a tight mechanical lift. Drafting the narrative first lets the actor framing (Seller voice, single-operator persona, audit-trail emphasis) settle before SHALL precision constrains the prose. CLAUDE.md is deliberately non-prescriptive on this order, calling narrative and OpenSpec proposal "siblings produced from the event model"; neither is derived from the other. The retrospective will record whether this ordering should harden into a `tidy: encode-narrative-first` convention or stay case-by-case for now. One data point is not enough to encode the rule; the choice rides on convention discovery from observed practice across slices 1.x and 4.x.

This is also the bootstrap session for the `docs/prompts/narratives/` and (eventually) `docs/retrospectives/narratives/` subdirectories, both of which are flagged *forthcoming* in their parent READMEs.

## Goal

Produce `docs/narratives/001-seller-manage-catalog.md` covering the Seller's catalog-management journey, scoped initially to Workshop 001 slice 1.1 (Publish a product — happy + duplicate-SKU failure). The journey is framed wide enough to grow into slice 1.3 (Change a product's price) without restructuring, but the v1.0 narrative authors only the Moments slice 1.1 needs.

The narrative must follow the format documented in `docs/narratives/README.md`:

- YAML frontmatter with `status`, `version`, `slices` fields.
- A sequence of **Moments**, each with `Context`, `Interaction`, and `System response`.
- A `Document History` table at end.

The voice is the Seller's — a single-operator small business owner of a critter-themed merchandise storefront. No enterprise abstractions, no team workflow, no approval gates. The Seller is also the back-office operator, the buyer, and the inventory manager rolled into one person.

## Spec delta

`docs/narratives/001-seller-manage-catalog.md` is created. `docs/narratives/README.md`'s *Current population* line moves from "Forthcoming" to "One narrative: 001 (Seller's catalog management, covering slice 1.1)." `docs/prompts/README.md`'s *Current population* line adds `narratives/` to the populated-kinds list. The forthcoming slice 1.1 OpenSpec proposal and slice 1.1 implementation prompt gain their human-readable companion spec, completing the first edge of the per-slice triangle.

## Orientation

Read these in this order:

1. **`CLAUDE.md` § 4b** — narrative routing-layer treatment: what a narrative is, why it exists, how it sits next to the OpenSpec proposal.
2. **`docs/narratives/README.md`** — narrative format conventions: frontmatter shape, Moments structure, versioning, file naming. The first narrative may surface subtleties that earn a small README update; that's an acceptable in-bounds edit per "same-file edits adjacent to the primary change are in-bounds" — but only if a subtlety actually surfaces.
3. **`docs/workshops/001-crittermart-event-model.md`** — specifically § 2 (Catalog BC summary, document-store framing, `ProductPublished` as a lifecycle moment not a state-reconstruction event), § 4 (Catalog event vocabulary), § 5 (slice table row 1.1), § 6.1 (the two GWT scenarios: happy path + duplicate-SKU failure). The narrative must agree with these; if a contradiction surfaces, the workshop is the source of truth and the narrative-authoring session stops to raise it.
4. **`docs/vision.md`** — single-seller framing, audience for the talk, long-road list (so the narrative knows what is deliberately out of scope).
5. **`docs/context-map/README.md`** — the Catalog BC has *no* BC-level outbound integration in round one. The narrative reflects this: storefront customers see the product (via `ProductCatalogView`), but no cross-BC events fire on publish. Pricing changes are not yet broadcast to Orders; the cart's snapshot of price at add-to-cart time is authoritative until checkout.

## Working pattern

1. Author this prompt artifact first, capturing session intent at start.
2. Draft the narrative's frontmatter and a short journey-scope intro: who the Seller is, what the journey covers in round one (slice 1.1), what it will grow to include (slice 1.3), what is deliberately not in scope here (slice 1.2 belongs to the Customer narrative).
3. Draft **Moment 1** (happy path — publish first product) using Workshop 001 § 6.1's first GWT. Lift the example SKU and price (`crit-001`, "Cosmic Critter Plush", `$24.99`) directly so the narrative and workshop stay quote-identical at the data level. The narrative renders the *why* and *experience* around the GWT's *what*.
4. Draft **Moment 2** (duplicate-SKU failure) using § 6.1's second GWT. Frame the *why* a duplicate would occur in human terms — the kind of context a future contributor can't recover from the GWT alone. The system response remains identical to the workshop's `ProductAlreadyPublished` rejection.
5. Note **forthcoming Moments** for slice 1.3 (`ChangeProductPrice`) without authoring them.
6. Stamp `Document History` with `v1.0` and today's date.
7. Bridge the Workshop's "operator" with the narrative's "Seller" at first mention so the terminology divergence is explicit rather than silent. Capture this as a *first surfaced terminology subtlety* in the retrospective.
8. Update the two README population lines downstream of the new artifacts.
9. Author the retrospective at session close, naming spec-delta — landed?, and record:
   - Whether the narrative-first ordering felt earned or forced for this triangle.
   - The Seller/operator terminology bridge as a surfaced refinement.
   - Whether any prose-shaped subtlety earned a `docs/narratives/README.md` clarification.

## Out of scope

- Do not author the slice 1.1 OpenSpec proposal. That is the next session in the triangle.
- Do not author the slice 1.1 implementation prompt. That is the third session in the triangle.
- Do not author Moments for slice 1.3 (`ChangeProductPrice`) beyond the forward-looking note. Slice 1.3 will get its own narrative-update session that bumps the version and appends its Moments.
- Do not author a Customer narrative for slice 1.2 (Browse and view products). That is a separate narrative with a separate actor; conflating it here would muddy the journey.
- Do not edit Workshop 001 or `docs/skills/event-modeling/SKILL.md`. If a contradiction with the workshop surfaces while drafting, stop and raise it — the workshop is amended by an explicit workshop-tidy session, not silently by a narrative.
- Do not commit any code. This is design.
- Do not encode "narrative-first" as a pipeline convention in CLAUDE.md. The retrospective records the choice; if the convention earns its keep across 2–3 narratives, a future `tidy: encode-narrative-first` session lifts it into CLAUDE.md § 4.
- Do not pre-decide UI surfaces or API shapes that aren't already implied by Workshop 001 § 5 (e.g., the back-office Catalog page is implied by the Seller actor and the `PublishProduct` command; the exact form, fields, and routing are the implementation prompt's call).
