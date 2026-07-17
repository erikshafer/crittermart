# CritterMart — Handoff: Post-Promotions next direction (design-return closed)

> **Durable handoff** (version-controlled at `docs/handoffs/`), authored 2026-07-17.
> Direct successor to [`promotions-design-return.md`](promotions-design-return.md), now **consumed**:
> the design-return it named shipped as **PR #147** (`main` @ `7ab1f77`) — the slice-6.2 OpenSpec change
> archived (`coupon-promotion` 4→5) and the capability's real `## Purpose` written over the #145 TBD.
> Read the artifacts, not this doc, for detail:
> [retro docs/016](../retrospectives/docs/016-design-return-archive-slice-6-2-coupon-promotion-purpose.md),
> [prompt docs/016](../prompts/docs/016-design-return-archive-slice-6-2-coupon-promotion-purpose.md), and
> [`openspec/specs/coupon-promotion/spec.md`](../../openspec/specs/coupon-promotion/spec.md) (5 requirements + Purpose).

## Where things stand

- **Promotions increment: storefront-complete AND design-reconciled.** Slices 6.1/6.3/6.4 (DCB core, PR #144)
  + 6.2 (advisory preview, PR #146) shipped the coupon journey end-to-end (define → preview → redeem-under-cap
  → release-on-cancel); PR #147 (this session) archived the last change and gave `coupon-promotion` its real
  Purpose. The capability carries **five requirements**; the OpenSpec workspace is at **0 active changes**.
- **Design-return cadence is RESET.** PR #147 was the interleave owed after #144/#146. The counter is clear —
  the next PR may be an implementation.
- **`main` is synced + verified** at `7ab1f77`; the tidy feature branch deleted; tree clean; only `main` local.

## Immediate next job — an OPEN next-direction fork (owner call)

The round-two Promotions increment is **complete**, and every remaining Promotions slice is explicitly
**long road** (Workshop 003 §8). So the genuine question at session start is *what direction comes next* — this
is an owner-level fork; present it, don't assume. Candidate directions (present 2–4 as options with previews per
[[feedback-options-with-previews]]):

1. **A second DCB — `one-redemption-per-customer` (recommended if a build is wanted).** Workshop 003 §8 item 6 /
   ADR 024's named "more dynamic boundary": a composite `(coupon × customer)` tag so a coupon is redeemable once
   *per customer*. It is the standout teaching follow-on — a **second** DCB that introduces the composite-tag
   pattern on top of the count-cap the first one taught, a clean single-slice increment. (Siblings: item 7
   shared-discount-budget — a summed value across streams; item 9 coupon lifecycle — expiry/disable, more
   configuration-as-events.) Would open a new OpenSpec change against `coupon-promotion`.
2. **A content-tidy interleave: write `customer-registry`'s real `## Purpose`.** A **second** archive-stamped TBD
   surfaced in retro docs/016 (`openspec/specs/customer-registry/spec.md` still reads "TBD - created by
   archiving…"). Small, self-contained, the same move this session did for `coupon-promotion` — a natural light
   design-return or a rider on the next one.
3. **Close deferred verification: the slice-6.2 visual W2/W3 browser-verify.** #146's live-verify was API-level
   only (Chrome extension unconnected). See *Deferred* below.
4. **A new bounded context / increment entirely** — would require a context-map update + a workshop pass first
   (CLAUDE.md § *No new bounded contexts without a context map update and workshop pass*).

## Deferred from the Promotions run (non-blocking)

- **Visual W2/W3 browser-verify.** Slice 6.2's live-verify was **API-level only** — all three validate states
  (`valid`/`invalid`/`exhausted`), the cap flip, and the W3 order fields were confirmed on the Aspire stack, but
  the **Claude-in-Chrome extension was not connected**, so the visual SPA drive (apply → preview → place → W3
  discount line; bogus + exhausted inline errors) + GIF is unproven in a real browser. UI rendering is covered by
  the 123 client Vitest tests. To close: connect the extension (claude.ai/chrome, likely a Chrome restart), boot
  the stack ([demo-runbook](../demo-runbook.md) Step 1 + Step 5d), drive `http://localhost:5273`.
- **`customer-registry` TBD Purpose** — see fork option 2 above.

## Carry-forwards (unchanged, non-blocking)

- Two **remote** branches await delete/keep (`origin/feat/cart-identity-harmonization`,
  `origin/research/cw-telemetry-spike`); dependabot #132–139 re-triage; `UseDurableLocalQueues()` saga-timeout +
  `ReplenishTimeout` verification gaps in research docs; refresh/revocation (ADR 023 Q15) + authZ/roles (Q16)
  deferred. **POST-TALK:** delete the AppHost demo knobs (payment decline/delay, order timeout, replenish
  timeout — `FLASH20`'s cap 3 is real domain data, not a knob).

## Locked / standing (do NOT re-litigate)

- **Wolverine stays 6.19.0** (CritterWatch beta.4 deliberate 1-minor lead — [[critterwatch-wolverine-version-coupling]]);
  do not bump past it. Marten resolved **9.15.1**. CritterWatch trial **expired 2026-07-10** (console blocked).
- **Advisory is advisory** — the 6.2 validate query never guards checkout; the DCB append is the only authority
  (design intent, not a bug). **Frontend units** run with `--exclude "**/e2e/**"`.
- **PR hygiene:** post the full PR URL as plain visible text ([[feedback-always-post-pr-url]]); no Claude commit
  trailer ([[feedback-no-claude-commit-trailer]]).
- **OpenSpec authoring note:** name active changes **without** a date prefix — the `openspec archive` CLI stamps
  today's date on archive, so a date-named active dir double-prefixes (retro docs/016's methodology note).

## Suggested skills for the next session

- If drawing the **second-DCB path** (fork option 1): `marten-advanced-dynamic-consistency-boundary` (Polecat-framed
  — CritterMart uses the **Marten 9.15.1** path: `RegisterTagType`/`WithTag`/`FetchForWritingByTags`), plus
  `openspec-propose` and the local `event-modeling` skill for the Workshop 003 slice + GWT authoring.
- If drawing the **content-tidy path** (fork option 2): `openspec-explore` to confirm the `customer-registry`
  requirement set before writing its Purpose.
