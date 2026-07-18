# Prompt: Docs 017 — Content-Tidy: write the real `customer-registry` `## Purpose` over the archive-stamped TBD

**Kind**: maintenance / docs surface (a *voluntary* content-tidy — author the `customer-registry` capability's real `## Purpose` over the archive-stamped TBD placeholder; **not** a cadence-owed design-return)
**Files touched**: `openspec/specs/customer-registry/spec.md` (edit — replace the TBD `## Purpose` with a real one); `docs/prompts/README.md` + `docs/retrospectives/README.md` (edit — `docs/` count 16→17 + population note); `docs/prompts/docs/017-content-tidy-customer-registry-purpose.md` (this prompt, new); `docs/retrospectives/docs/017-content-tidy-customer-registry-purpose.md` (new)
**Mode**: solo synthesis — a single spec-content edit (the Purpose), authored from the shipped 8-requirement set; mechanical index reconciliation. No CLI archive (unlike docs/016 — `customer-registry` has no active change; the capability is already in the main specs), no code, no tests.
**Commit subject**: `tidy: docs — write customer-registry OpenSpec Purpose over archive-stamped TBD`

## Framing

This is the **second instance** of the recurring debt the `openspec archive` CLI creates and that retro docs/016 named explicitly: when a capability's very first main spec is born by archiving a change, the CLI seeds a placeholder `## Purpose` reading *"TBD - created by archiving change …"*. That TBD is invisible to `--strict` validation (the spec passes), so it lingers until a human writes it. Docs/016 drained the first instance (`coupon-promotion`) as it archived that capability's storefront-completing slice; `openspec/specs/customer-registry/spec.md:4` is the next instance awaiting the same treatment — it still reads *"TBD - created by archiving change customer-registry. Update Purpose after archive."*

The key difference from docs/016: that PR was a **cadence-owed** design-return — it discharged the interleave debt from the two Promotions implementation PRs (#144/#146). This one is **not owed**. Per the [post-Promotions handoff](../../handoffs/post-promotions-next-direction.md), the design-return counter is already **reset** (PR #147 was the owed interleave) and the OpenSpec workspace is at **0 active changes**. This session is therefore a **voluntary content-tidy** the owner chose from an open next-direction fork — the ceremony level is set not by cadence but by the **tidy-ceremony rule**: a tidy that authors spec content (a spec `## Purpose`) carries the full prompt/retro pair. This one authors a Purpose, so it does.

Now that the `customer-registry` requirement set is complete and stable (eight requirements spanning register → uniqueness → OHS resolve → the email-change saga's open/confirm/timeout → register-with-credentials → login/JWT → offline token verify → logout), this is the moment to write a real Purpose of the same caliber as its siblings (`order-lifecycle`, `coupon-promotion`, `stock-management`).

## Goal

After this session, the `customer-registry` capability reads coherently to a fresh session-runner, and the canonical spec no longer carries an archive placeholder:

1. **`customer-registry` `## Purpose`** is a real, sibling-caliber paragraph — naming the Identity BC's one-non-event-sourced shape (EF Core + Npgsql; the `Customer` row *is* the read model; `CustomerRegistered` is an outbox notification, not an event stream), the register/resolve endpoints and normalized-email double-guard, the **Open-Host Service** read + **Published Language** event (Orders' `LocalCustomerView`, ADR 001 no-sync-HTTP), the `EmailChange` **EF-Core-backed convention saga** (open/confirm/timeout, deadline-scheduled-once), and the **ADR 023** auth-issuer story (ASP.NET Core Identity, self-signed JWT, offline verification, `sub` as the sole trust boundary with `X-Customer-Id` fully retired, client-side-discard logout).
2. **The capability still validates `--strict`** — the Purpose edit is prose only; no requirement or scenario changes.
3. **Index READMEs accurate** — `docs/` count 16→17 in both `docs/prompts/README.md` and `docs/retrospectives/README.md`, population note extended with this content-tidy.

## Spec delta

The `customer-registry` capability's **`## Purpose`** is authored from the archive-stamped TBD to a real prose statement — a spec-content edit, the anchor that makes this a full-pair tidy. **No requirement is added, removed, or modified**; no scenario changes; the requirement count is unchanged (the capability is already storefront-complete). No workshop slice, no narrative, no code, no tests; the index-count bump is mechanical. This reconciles the canonical spec's prose with the shipped, stable capability — it does not alter the modeled behavior.

## Orientation

Read in this order:

1. **CLAUDE.md** — § *Tidy ceremony rule* (the "authors a spec `## Purpose`" clause is the reason for the pair), § *Design-return cadence* (to confirm this is *not* a cadence-owed return — the counter is reset), § *Spec-delta closure loop*.
2. **`docs/handoffs/post-promotions-next-direction.md`** — this session's brief; names the `customer-registry` TBD as fork option 2 (a voluntary content-tidy).
3. **`openspec/specs/customer-registry/spec.md`** — the eight pre-existing requirements + the archive-stamped TBD Purpose (the sibling Purposes in `order-lifecycle`/`coupon-promotion` are the caliber to match).
4. **`docs/context-map/README.md`** — the Identity integration rows (Open-Host Service + Published Language; the auth relationship as OHS + PL with Conformist consumers) for precise DDD vocabulary.
5. **`docs/decisions/023-real-authentication-for-identity.md`** — ADR 023, the auth-issuer decision the Purpose's auth clause records.
6. **`docs/prompts/docs/016-design-return-archive-slice-6-2-coupon-promotion-purpose.md`** + its retro — the immediate precedent for the Purpose-write shape (the caliber and the "archive-stamped TBD" methodology note this session cashes in).

## Working pattern

Read the eight shipped requirements to ground the Purpose in the real endpoints, events, saga, and auth contract (not a modeling-time proposal). **Write the real `## Purpose`** over the TBD line. Verify `openspec validate customer-registry --specs --strict` still passes (prose-only edit — the requirement set is untouched). Then **reconcile the index counts** (16→17). Then the **retro**. One branch (`tidy/customer-registry-purpose`), one PR, containing this prompt, the Purpose edit, the index edits, and the retro. Nothing else.

## Deliverable plan

1. **`customer-registry` `## Purpose`** — replace the TBD with a real sibling-caliber paragraph authored from the shipped 8-requirement set.
2. **Index READMEs** — `docs/prompts/README.md` + `docs/retrospectives/README.md`: `docs/` 16→17, population note extended with this content-tidy.
3. **Retro** (`docs/retrospectives/docs/017-content-tidy-customer-registry-purpose.md`) — six-section format; the spec-delta line forward-confirms the Purpose landed and `--strict` still passes.

## Out of scope

- **No code, no tests, no new OpenSpec change, no CLI archive.** `customer-registry` has no active change to archive (unlike docs/016); the capability is already synced into `openspec/specs/`. The edit is a direct prose write to the main spec's `## Purpose`, exactly as the sibling Purposes carry real prose.
- **No requirement or scenario edits.** The eight requirements are storefront-complete and locked; the Purpose *summarizes* them, it does not change them.
- **Do not touch the context-map README.** Its § *Round-one stubs* auth-cutover line still narrates the layered cutover as in-progress ("until the auth slices ship, the `X-Customer-Id` seam still carries…"), which reads as pre-hard-cutover. That is a *separate* latent doc-staleness surfaced here — recorded in the retro's outstanding items as a candidate, not an opportunistic edit in this PR.
- **Do not re-open resolved decisions.** The non-event-sourced Identity shape (ADR 009/023), the offline-JWT-verification design (ADR 023), the hard `X-Customer-Id` cutover, and deferred server-side revocation (ADR 023 Q15) are locked; the Purpose *records* them, it does not re-litigate them.
- **No live boot.** A docs/spec tidy — verification is `openspec validate --strict` + a re-read of the Purpose, not an Aspire stack.
