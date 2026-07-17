# CritterMart — Handoff: Promotions design-return (post slice 6.2)

> **Durable handoff** (version-controlled at `docs/handoffs/`), authored 2026-07-17.
> Direct successor to [`promotions-slice-6-2.md`](promotions-slice-6-2.md), now **consumed**: slice 6.2
> (advisory cart-review coupon validate + W2/W3 UI + comment tidy) shipped as **PR #146**
> (`main` @ `2326d38`). Read the artifacts, not this doc, for detail:
> [retro 040](../retrospectives/implementations/040-slice-6-2-advisory-coupon-validate.md),
> [Workshop 003 v1.2 §6.2](../workshops/003-promotions-event-model.md),
> [Narrative 011 v1.1](../narratives/011-customer-redeems-coupon.md), and the OpenSpec change
> `openspec/changes/2026-07-16-slice-6-2-advisory-coupon-validate/` (**not yet archived** — see below).

## Where things stand

- **Promotions increment: storefront-complete.** Slices 6.1/6.3/6.4 (DCB core, PR #144) + 6.2 (advisory
  preview, PR #146) are all shipped. The coupon journey runs end-to-end: define → preview → redeem-under-cap
  → release-on-cancel. Narrative 011 (v1.1) is the human companion; `coupon-promotion` capability carries the
  four requirements.
- **`main` is synced + verified** at `2326d38`; all four stale local branches deleted this session; tree clean.

## Immediate next job — the design-return (cadence is DUE)

The Promotions run now has **2 consecutive implementation PRs** (#144, #146). Per CLAUDE.md §Design-return
cadence, the **next PR must be a design-return** (a new narrative, the next BC's workshop, or a tidy) — a third
consecutive Promotions implementation would signal drift. The natural, already-owed candidate:

1. **Archive the 6.2 OpenSpec change (tidy — satisfies the design-return).** `openspec archive
   2026-07-16-slice-6-2-advisory-coupon-validate -y` syncs the ADDED requirement into `openspec/specs/coupon-promotion/`
   and moves the change under `changes/archive/`. This is the post-merge tidy the retro named (the coupon-dcb /
   customer-data precedent — archive rides a *separate* post-merge PR, not the implementation PR). **This handoff
   doc + the archive can ride the same `tidy:` PR** (the promotions-slice-6-2 handoff rode the #145 archive tidy).
   Note: `openspec/specs/coupon-promotion/spec.md` still has a `## Purpose` reading "TBD - created by archiving…"
   from the #145 archive — a good moment to write a real Purpose while archiving.

Alternatives if you'd rather the design-return be substantive rather than mechanical:

2. **Skill/DEBT drain (the two upstream DCB findings).** Both are the same "DCB is newer than its tooling" theme:
   DEBT row 3 (the `marten-advanced-dynamic-consistency-boundary` skills present DCB as **Polecat-only** with
   Polecat symbols; the Marten 9.15.1 path is verified — `RegisterTagType`/`WithTag`/`FetchForWritingByTags`),
   and retro 039's finding that `DeleteAllDocumentsAsync` throws on an id-less `[BoundaryAggregate]` (worked
   around with a SQL-truncate reset). Candidates for the ai-skills/Marten upstream pass.
3. **The next Promotions long-road slice or the next BC** — but only *after* a design interleave.

## Deferred from this session (non-blocking)

- **Visual W2/W3 browser-verify.** Slice 6.2's live-verify was **API-level only** — all three validate states
  (`valid`/`invalid`/`exhausted`), the cap flip, and the W3 order fields were confirmed on the Aspire stack, but
  the **Claude-in-Chrome extension was not connected**, so the visual SPA drive (apply → preview → place → W3
  discount line; bogus + exhausted inline errors) + GIF is unproven in a real browser. UI rendering is covered by
  the 123 client Vitest tests. To close it: connect the extension (claude.ai/chrome, likely a Chrome restart),
  boot the stack ([demo-runbook](../demo-runbook.md) Step 1 + Step 5d), and drive `http://localhost:5273`.

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

## Suggested skills for the next session

- **`openspec-archive-change`** (or `opsx:archive`) — archive `2026-07-16-slice-6-2-advisory-coupon-validate`
  (tool-backed, [[feedback-prefer-tool-backed-over-freeform]]); write the real `coupon-promotion` `## Purpose` while there.
- **`/blurb`** — to emit the kickstart pointer at the end (this handoff's path).
- If drawing the DEBT-drain path: read `docs/skills/DEBT.md` row 3 + retro 039 first.
