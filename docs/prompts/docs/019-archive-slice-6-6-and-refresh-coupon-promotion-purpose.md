# Prompt: Docs 019 — Archive slice 6.6 + refresh the `coupon-promotion` Purpose

**Kind**: tidy — post-merge OpenSpec archive **plus** spec content (the capability `## Purpose`)
**Files touched**: `docs/prompts/docs/019-archive-slice-6-6-and-refresh-coupon-promotion-purpose.md` (new, this file); `openspec/changes/slice-6-6-per-customer-preview-and-copy/` → `openspec/changes/archive/2026-07-20-…/` (CLI move); `openspec/specs/coupon-promotion/spec.md` (deltas folded by the CLI + the `## Purpose` rewritten by hand); `docs/prompts/README.md` + `docs/retrospectives/README.md` (index counts); `docs/retrospectives/docs/019-…` (at close)
**Mode**: solo tidy. **Full prompt/retro pair** — see the ceremony call below.
**Commit subject**: `tidy: openspec — archive slice-6.6 (coupon-promotion 6→7) + refresh the capability Purpose`

## Framing

Slice 6.6 merged as [PR #161](https://github.com/erikshafer/crittermart/pull/161). Per the established convention the OpenSpec archive does **not** ride the implementation PR, so it falls to this session: run `openspec archive`, let the CLI fold the change's deltas into the main `coupon-promotion` spec, and return the workspace to zero active changes.

**Why this tidy is not purely mechanical.** CLAUDE.md's tidy ceremony rule turns on what the session *writes*: a tidy that authors **spec content** — and it names "a spec `## Purpose`" explicitly — carries the full prompt/retro pair; a purely mechanical one may run light. Inspection at session start found `coupon-promotion`'s `## Purpose` **two slices stale**: it still describes `CouponDefined { code, discountPercent, cap }` without `oneRedemptionPerCustomer`, still calls the checkout DCB read "the sole authority" in the singular when there are now two boundaries, and closes its slice inventory at 6.2. The 6.5 archive folded that slice's requirement deltas but left the Purpose untouched, so the drift has been compounding — precisely the failure mode the spec-delta closure loop exists to catch. Refreshing it is spec content. **Full pair, decided with Erik at session start (`AskUserQuestion`).**

**The Purpose is the capability's front door.** It is the paragraph a reader hits before any SHALL statement, and the one an AI session-runner reads to orient. A stale front door is worse than a terse one: it actively misinforms about how many consistency boundaries the capability has.

## Goal

- `openspec archive slice-6-6-per-customer-preview-and-copy` — deltas fold into `openspec/specs/coupon-promotion/spec.md` (**+1 ADDED, ~2 MODIFIED**, per the proposal), the change moves under `openspec/changes/archive/` with the CLI's date stamp.
- **Verify the fold, don't assume it**: 0 active changes; `openspec validate --all --strict` green across all six specs; requirement count 6→7; the reworded `409` copy present and the old mechanical sentence gone.
- **Rewrite `## Purpose`** to cover the capability as it now stands: two DCBs and the count-vs-existence contrast between them; the composite single-scalar tag; advisory-vs-authoritative at *both* scopes with each view's never-persisted boundary twin; the optionally-authenticated validate query and its forward-only under-warn asymmetry; the slice inventory extended through 6.5 and 6.6.
- Index counts in both READMEs.

## Spec delta

The `coupon-promotion` capability gains its slice-6.6 requirements by CLI fold (**ADDED** *Track advisory per-customer coupon usage*; **MODIFIED** *Validate and price a coupon at cart review* and *Enforce one redemption per customer …*), taking it 6→7 requirements. Separately and by hand, the capability's `## Purpose` is rewritten from a slices-6.1–6.4 description to one that covers the composite second DCB (6.5) and the per-customer preview + refusal copy (6.6). **No requirement text is authored or edited by hand** — the SHALL statements are the CLI's fold of an already-reviewed change; only the Purpose prose is session-authored.

## Orientation files

1. **`openspec/changes/slice-6-6-per-customer-preview-and-copy/proposal.md`** — the declared delta shape (+1 ADDED, ~2 MODIFIED) to check the CLI's fold against.
2. **`openspec/specs/coupon-promotion/spec.md`** — the stale Purpose, and the seven requirements it must now describe.
3. **`docs/retrospectives/docs/016-…`** + **`017-content-tidy-customer-registry-purpose.md`** — the two precedents for writing a capability Purpose as tidy work.
4. **`CLAUDE.md` § Tidy ceremony rule** — the light-vs-full call this session is an instance of.
5. **`docs/decisions/024-dcb-coupon-redemption-in-orders.md` §38** — the composite-boundary vocabulary the Purpose must get right.

## Working pattern

1. `main`, pull the merge, branch `tidy/archive-slice-6-6-and-refresh-coupon-purpose`.
2. Archive; verify the fold against the proposal's declared counts before touching anything by hand.
3. Rewrite the Purpose; re-validate `--strict`.
4. README counts; retro; PR.

## Out of scope

- **No requirement edits by hand.** If the CLI's fold looks wrong, that is a finding for the retro, not a silent hand-fix.
- **No encoding of the archive-tidy checklist.** Decided with Erik: the recurring shape (run the CLI → check the Purpose against shipped slices → let that answer decide light-vs-full) earns its own `tidy: encode-` session. It is logged as a next-session input, not written here.
- No other capability's Purpose, however stale — one capability per session.
- No code, no tests, no workshop/narrative edits (slice 6.6's spec delta already landed and was confirmed in retro implementations/042).
- The standing carry-forwards stay carried: the context-map auth-cutover doc-tidy, slice-6.2 browser-verify, live-verify on the Aspire stack, the two stale remote branches, dependabot #132–139.
