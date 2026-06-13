# Prompt: Docs 011 — Round-One Close + Doc Reconciliation

**Kind**: maintenance / docs surface (round-one close — reconciling canonical docs with shipped reality before frontend mode)
**Files touched**: `docs/decisions/017-critterwatch-integrated.md` (new — ADR); `docs/decisions/013-critterwatch-deferred-to-messaging-slices.md` (edit — forward-pointer only, append-only); `docs/decisions/README.md` (edit — index row); `docs/workshops/001-crittermart-event-model.md` (edit — frontmatter sync + v1.7 Document History row); `docs/skills/wolverine-cross-bc-cascading/SKILL.md` (new — local skill); `docs/skills/README.md` (edit — current-skills list); `docs/retrospectives/docs/011-round-one-close-reconciliation.md` (new)
**Mode**: solo synthesis — reconcile each stale canonical claim against shipped reality, mint the two new artifacts the reconciliation implies (ADR 017, cascading skill), and smoke-check the live topology. Collaborative on scope: the maintainer chose A + C + D (declined B, the `global.json` SDK pin) via AskUserQuestion at session start.
**Commit subject**: `docs: round-one close — reconcile CritterWatch ADR, sync workshop, capture cross-BC cascading skill`

## Framing

Round-one backend is complete — all modeled slices shipped (§ 5 slice-table total: 18; the original 17 plus the later-added slice 2.4, which landed 2026-06-13 in PR #45). PR #46 (`6b7903b`) shipped the pre-frontend hardening (CORS, CI `Format` gate, grouped Dependabot, `.gitattributes`, the endpoint audit). The frontend is decided (ADR 015/016) but unstarted.

This is the **design-return interleave** the cadence rule calls for before frontend implementation begins, and it closes a *reverse* spec delta: normally the canonical spec lags behind no code, but here three canonical docs have drifted *behind shipped reality* and now contradict the code:

1. **Workshop 001 frontmatter is stale.** `status: Draft`, `date: 2026-05-26` (the v1.0 date), despite round-one being complete and the Document History having been amended through v1.6 (slice 2.4, 2026-06-13). `version: v1.6` already matches the latest history row; `status` and `date` are the drifted fields.
2. **ADR 013 still reads "deferred."** It says CritterWatch is deferred to the 4.x messaging slices and carries an unresolved Open Question (tier / feed / license-key). But CritterWatch is now fully integrated: a `CritterMart.CritterWatch` console project, per-service `AddCritterWatchMonitoring` wiring (Catalog/Inventory/Orders + the AppHost), a dedicated `critterwatch` Postgres database, telemetry over the existing RabbitMQ broker. ADR 013 *itself predicted a successor*: "a successor ADR will record the actual integration when slice 4.2 makes it earn its place." That successor was never authored.
3. **A hard-won Wolverine routing lesson is buried** in an archived slice's `design.md` (`2026-06-13-slice-2-4-commit-stock/design.md`, Decision 2): a handler returning `object?` breaks Wolverine's *conventional* RabbitMQ routing, because conventional routing provisions outbound exchanges/queues from the message types it discovers at code-gen time — and `object?` hides those types. CritterMart's answer is a typed cascading return (the `(CommitStock?, ReleaseStock?)` tuple). This is a recurring CritterMart convention (the release path in slice 4.6, the commit path in slice 2.4) currently discoverable only by spelunking an archived change.

The session is a reconciliation, not a redesign: every edit corresponds to a fact already shipped. The two *new* artifacts (ADR 017, the cascading skill) are not new decisions — they durably record decisions the code already embodies.

## Goal

After this session, a fresh session-runner reading the canonical docs sees reality:

1. **Workshop 001** carries an honest completion status and a v1.7 Document History row marking the round-one close (all modeled slices shipped; round-two frontend amendments per ADR 016 explicitly pending — the workshop is not frozen, it will gain the `Wireframe` column next).
2. **ADR 017** records the actual CritterWatch integration and resolves ADR 013's Open Question (Trial tier, license key in user-secrets, the Production-environment gotcha, the dedicated DB, the single-node/non-clustered choice, the Aspire resource-name collision). **ADR 013** keeps its status `Accepted` (the deferral was genuinely accepted and was *honored*, not reversed) and gains a one-line forward-pointer to 017 — append-only, no body rewrite. The README index lists 017.
3. **A local skill** (`docs/skills/wolverine-cross-bc-cascading`) captures CritterMart's typed-cascading-return convention with the `object?`-breaks-conventional-routing gotcha as its central "common mistake," deferring Wolverine mechanics to the upstream skills and recording only the CritterMart-specific convention. The skills README lists it.
4. **The live topology boots clean** with the new CORS (Postgres + RabbitMQ + 3 services + CritterWatch) — verified, result recorded in the retro.

## Spec delta

Workshop 001 gains a v1.7 Document History row recording the round-one close and the frontmatter sync (status → round-one complete, date → 2026-06-13). ADR 017 is minted as the integration record ADR 013 predicted; ADR 013 gains a forward-pointer and the README index gains a row. A new local skill records the cross-BC typed-cascading convention. No workshop *slice* changes and no OpenSpec capability changes — this reconciles existing canonical artifacts with shipped code; it does not alter the modeled scenario set.

## Orientation

Read in this order:

1. **This session's handoff** (`crittermart-handoff-pre-frontend.md`) and **CLAUDE.md** — the routing layer, the design-return cadence, the tidy-ceremony rule (a tidy/docs session that authors spec content carries the full prompt/retro pair — this one does), and the append-only ADR discipline.
2. **`docs/workshops/001-crittermart-event-model.md`** — frontmatter (lines 1–19) and § 9 Document History (the v1.0–v1.6 rows; v1.7 is appended here).
3. **`docs/decisions/013-critterwatch-deferred-to-messaging-slices.md`** — the deferral and its Open Question / predicted successor. **`docs/decisions/README.md`** — the index table + the append-only / supersede convention. **`docs/decisions/016-...md`** — the most recent ADR, for house format.
4. **The live CritterWatch wiring** — `src/CritterMart.CritterWatch/Program.cs`, `src/CritterMart.AppHost/Program.cs`, and the per-service `AddCritterWatchMonitoring` calls — so ADR 017 records the integration accurately.
5. **`openspec/changes/archive/2026-06-13-slice-2-4-commit-stock/design.md`** — Decision 2, the `object?` routing gotcha being lifted into the skill.
6. **`docs/skills/README.md`**, **`docs/skills/marten-projection-conventions/SKILL.md`**, **`docs/skills/event-modeling/SKILL.md`** — the local-skill convention (frontmatter shape, "defer to upstream, write only what diverges").

## Out of scope

- **Do not re-add or rewrite the PR #46 hardening.** CORS, the CI `Format` gate, Dependabot, `.gitattributes`, the endpoint audit are all shipped — do not touch them.
- **Do not pin the SDK / add `global.json`** (Option B). The maintainer declined it this session.
- **Do not rewrite ADR 013's body.** ADRs are append-only; 013's deferral reasoning is historically true and stays. Only a forward-pointer line is added.
- **Do not begin frontend mode.** No `Wireframe` column, no customer-journey narrative, no slice 3.5 modeling, no Vite app. The workshop's v1.7 row *names* those as pending; it does not do them.
- **Do not duplicate upstream Wolverine docs** in the new skill. Defer mechanics to the upstream `wolverine-handlers-fundamentals` / `wolverine-messaging-message-routing`; write only the CritterMart convention and the gotcha.
- **Do not commit the CritterWatch license key** or any secret. ADR 017 describes *where* the key lives (user-secrets), not the key.
- **Do not opportunistically fix anything the smoke-check surfaces.** If the topology fails to boot, stop and report — a fix is a separate session, not a rider here.
- **Do not edit frozen historical files** (prior prompts/retros, the archived slice change).

## Deliverable plan

1. **ADR 017** (`docs/decisions/017-critterwatch-integrated.md`) — house format (`# ADR 017: …`, `**Status**: Accepted`, `## Context` / `## Decision` / `## Consequences`). Records: the two-package + console-project shape, per-service `AddCritterWatchMonitoring` + AppHost orchestration, dedicated `critterwatch` DB on the shared Postgres, telemetry over the existing RabbitMQ, the **Trial** tier with the key in user-secrets, the **Production-environment gotcha** (Development silently substitutes a "Development" tier and never reads the key; the console explicitly loads user-secrets so a local Production run validates the real key), the **single-node / `enableClusterPartitioning: false`** choice (clustered mode needs a sharded queue topology), and the **Aspire resource-name collision** (`critterwatch` DB vs `critterwatch-console` project share one case-insensitive namespace). Closes ADR 013's Open Question. Cross-references 013 (predicted-successor), 002 (schema-per-service — and why third-party tooling sits outside it), 003/004/005.
2. **ADR 013 forward-pointer** — one line near the top (under Status) pointing to ADR 017 as the realizing integration record. Status stays `Accepted`.
3. **README index** — add the 017 row; confirm 013 row unchanged.
4. **Workshop 001** — frontmatter `status: Draft` → round-one-complete phrasing, `date` → `2026-06-13`, `version` → `v1.7`; append a v1.7 Document History row.
5. **Cascading skill** — `docs/skills/wolverine-cross-bc-cascading/SKILL.md` with local-skill frontmatter; body covers when-to-apply, the typed-tuple-return convention, the `object?` common mistake with the conventional-routing rationale, in-repo precedents (slice 4.6 release, slice 2.4 commit), and a See-also to upstream + ADR 014. Update `docs/skills/README.md` current-skills list.
6. **Aspire smoke-check** — boot the AppHost, confirm clean startup of the full topology with CORS, capture the outcome for the retro, then shut down.
7. **Retro** (`docs/retrospectives/docs/011-...`) — seven-section format; the spec-delta line forward-confirms the named delta landed.

## Working pattern

Reconcile in dependency order (ADR 017 → 013 pointer → README → workshop → skill → skill README), then run the smoke-check, then author the retro. One branch (`docs/round-one-close-reconciliation`), one PR, containing this prompt, the six edited/new docs, and the retro. Nothing else.
