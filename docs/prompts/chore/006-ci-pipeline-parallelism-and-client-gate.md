# Prompt: Chore 006 — CI pipeline parallelism + the client gate

**Kind**: chore (repo-meta CI/DevOps; no domain, no capability, no slice)
**Files touched**: this prompt; `.github/workflows/dotnet.yml` → `.github/workflows/ci.yml` (renamed + rewritten); `client/vite.config.ts` (Vitest `include` scope); `docs/retrospectives/chore/006-ci-pipeline-parallelism-and-client-gate.md` (forthcoming); `docs/prompts/README.md` + `docs/retrospectives/README.md` (population counts)
**Mode**: solo infra; scope confirmed by the user via AskUserQuestion (single PR, not split into a separate client-gate session)
**Commit subject**: `chore: ci — flatten the job graph, cache NuGet, gate the storefront SPA`
**Branch**: `chore/ci-parallelism-and-client-gate`

---

## Framing

The pipeline has been untouched since [chore/003](003-pre-frontend-hardening.md) added the `Format`
gate and Dependabot. Everything since — the round-two SPA, four bounded contexts' worth of test
projects, two sagas, two DCBs — landed against a CI shape authored when the repo was backend-only and
the unit suite was empty. This session is the periodic look-back at that shape.

The trigger was a plain question — *can this be faster, and is caching too early?* — so the session
begins with measurement rather than assumption. The most recent run (`29762921031`) took **2m34s**:
Build 39s, Format 53s, Unit tests 42s, Integration tests 106s, with `Build (39s) → Integration (106s)`
as the critical path and restore measured at 11.5s per job.

That measurement reframed the question. The pipeline is not slow because any single step is slow; it
is slow because the job graph serializes the longest job behind a job whose output it never consumes.
Caching is the smaller, secondary win. And the measurement surfaced a third thing nobody asked about:
the `client/` SPA — 17 Vitest files, 125 tests, a typecheck, and a production bundle — has **no CI
job at all** and can be broken by a green PR.

This is a `chore`, not a slice. The user chose to keep it as one PR rather than splitting the client
gate into its own session.

## Goal

1. **Flatten the job graph.** Remove `needs: build` from both test jobs so every job starts at t=0,
   taking the critical path from `Build → Integration` down to `Integration` alone.
2. **Cache NuGet restore** with a Central-Package-Management-keyed cache.
3. **Gate the client.** A `Client` job running the typecheck, the production build, and the Vitest
   suite — closing the hole where a PR can break the SPA and still go green.
4. Leave the pipeline's *semantics* unchanged: same checks, same filters, same commands, no test
   skipped or reordered into passing.

## Spec delta

**None.** No slice behavior, no capability or OpenSpec change, no narrative or workshop amendment, no
ADR. This is repo-meta CI configuration plus the one-line test-runner scoping fix that the client gate
requires in order to be green. No canonical spec changes and none should.

## Orientation

1. **`.github/workflows/dotnet.yml`** — the surface being rewritten; its comments carry the reasoning
   for the `paths-ignore` denylist, the `Format` gate, and the Testcontainers-not-`services:` choice,
   all of which must survive the rewrite.
2. **Measured job + step timings** from the most recent run via `gh run view` — the session's evidence
   base. Do not optimize by intuition; the `needs:`-removal win is only visible in the numbers.
3. **`Directory.Packages.props`** — CPM; the single file whose hash is a sound NuGet cache key, and
   the reason `setup-dotnet`'s built-in `cache: true` (which wants `packages.lock.json`) is not usable.
4. **`client/package.json` + `client/vite.config.ts`** — the scripts the client job will run and the
   Vitest config that decides which files it collects.
5. **[chore/003 retro](../../retrospectives/chore/003-pre-frontend-hardening.md)** — specifically its
   "verify a new CI gate with the byte-identical command it will run" lesson, which applies directly.

## Working pattern

1. **Measure first.** Pull per-job and per-step timings for a recent run; establish the critical path
   and the restore cost before proposing anything.
2. **Flatten + cache.** Drop `needs:` from the test jobs, keeping `Build` as a standalone check. Add
   the `actions/cache` block to all four .NET jobs.
3. **Verify the client commands locally before wiring them** — run `npm test` and `npm run build`
   exactly as CI will. Fix whatever that surfaces; a gate authored against an un-run command is a
   red pipeline on merge.
4. **Add the `Client` job**, then rename the workflow to `ci.yml` / `name: CI` now that it is no
   longer .NET-only. (Confirm no branch protection references the old name first.)
5. Validate the YAML parses; write the retro.

## Out of scope

- **Integration-test matrix sharding.** Viable, but at 106s the 5x Testcontainers churn is not earned.
  Named in the workflow comments as the move if it crosses ~4 minutes.
- **Build-artifact passing** (`upload-artifact` → `dotnet test --no-build`). The real fix for building
  the solution three times, but a larger change than this session's savings justify; the `needs:` note
  in the workflow records it as the follow-on.
- **Per-job path filtering** (.NET jobs skipping `client/**`-only PRs and vice versa). Requires either
  a third-party filter action that re-serializes the graph or a workflow split that makes required
  checks go absent rather than green. Deliberately not taken.
- **Playwright e2e in CI.** `client/e2e` needs the full Aspire stack running; it wants its own
  workflow, not a step in this one.
- **Trigger narrowing** (dropping the push-to-main run). Offered and explicitly not chosen.
- No `.editorconfig`, Dependabot, ADR, openspec, or structural-constraints edit.
