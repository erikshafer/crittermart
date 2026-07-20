---
retrospective: 006
kind: chore
prompt: docs/prompts/chore/006-ci-pipeline-parallelism-and-client-gate.md
deliverable: .github/workflows/ci.yml (renamed from dotnet.yml), client/vite.config.ts
date: 2026-07-20
mode: solo infra; scope confirmed by the user (single PR, client gate not split out)
session-runner: Claude (Opus 4.8)
---

# Retrospective — Chore 006: CI pipeline parallelism + the client gate

## Outcome summary

A measurement-first pass over a CI shape untouched since chore/003, prompted by an open question:
*can this be faster, and is caching too early in the project's life?* Shipped:

- **Flattened job graph** — `needs: build` removed from both test jobs. `.github/workflows/ci.yml`
  carries a block comment explaining why, so the gate isn't "restored" later as a fix.
- **NuGet caching** — `actions/cache` on `~/.nuget/packages`, keyed on the hash of the single
  `Directory.Packages.props`, added to all four .NET jobs.
- **A `Client` job** — `npm ci` → `npm run build` (`tsc --noEmit && vite build`) → `npm test`
  (Vitest), with `setup-node`'s built-in npm cache.
- **Vitest scoping fix** — `client/vite.config.ts` gains `include: ["src/**/*.{test,spec}.{ts,tsx}"]`.
- **Workflow renamed** `dotnet.yml` → `ci.yml`, `name: .NET` → `name: CI`, now that it is not
  .NET-only. Verified no branch protection referenced the old name.

Measured baseline (run `29762921031`): **2m34s** wall clock — Build 39s, Format 53s, Unit 42s,
Integration 106s; restore 11.5s per job. Expected after: **~1m42s**, a ~35% cut, from removing ~44s of
serialization and ~8s of restore on the critical path. The `Client` job (~40s) runs inside the
integration-test shadow and should not extend wall clock.

Client suite verified locally: 17 files, 125 tests green in 7.7s; `npm run build` clean.

## What worked

- **Measuring before proposing.** The intuitive answer to "make CI faster" is caching, and caching was
  the *smaller* win — 8s against the 44s the job graph was giving away. Pulling per-job and per-step
  timings with `gh run view` took two minutes and completely reordered the recommendations. Had the
  session optimized by intuition it would have shipped the cache, felt productive, and left the actual
  problem in place.
- **Asking what `needs: build` was buying.** The gate looks like textbook fail-fast, and reads as
  correct until you check whether anything downstream consumes the build. Nothing did — no artifact
  upload, so each test job compiled the solution from scratch anyway. The gate only changed *which*
  check went red while serializing the slowest job behind the fastest one on every single run.
- **Honouring chore/003's own lesson.** That retro says: verify a new CI gate with the byte-identical
  command it will run. Doing so is the only reason this session didn't merge a red pipeline (below).
- **Declining the sharding.** Matrix-sharding the integration tests was the flashiest available change
  and would have cut 106s to ~50s — at 5x the Testcontainers churn, for a job that is not yet a
  problem. Recorded in the workflow comments with a concrete trigger (~4 minutes) instead.

## What was harder than expected

- **The client gate could not simply be wired; it had to be repaired first.** Running `npm test`
  locally — as chore/003's lesson demands — showed it **failing today**: Vitest's default `include`
  (`**/*.{test,spec}.*`) collected `client/e2e/seeder.spec.ts`, a Playwright spec, which throws
  *"Playwright Test did not expect test() to be called here."* 125 tests passed and the run still
  exited non-zero. Wiring the job without looking would have merged a permanently-red check and
  invited exactly the wrong fix (weakening the gate to make it green).
- **The failure had been latent for the entire life of the e2e suite.** Nobody hit it because the only
  way to hit it is to run `npm test` — and no CI job did. This is the self-proving case for the gate:
  the first thing the client check did was find a broken client check.
- **Per-job path filtering had no clean answer.** Both routes are worse than doing nothing: a
  third-party filter action re-serializes the graph this session just flattened, and splitting into
  two path-filtered workflows makes checks go *absent* rather than green, which reads as "pending"
  to branch protection. Left alone; the client job's ~40s on .NET-only PRs is cheaper than either.

## Methodology refinements that emerged

- **A CI job that has never run a command is not a gate — it is a guess.** Generalizing chore/003's
  byte-identical-command lesson: the risk isn't only flag mismatch, it's that the command may never
  have been run *at all* in the configuration CI will run it. `npm test` existed in `package.json`
  for the entire round-two frontend effort and was broken. Any script wired into CI should be
  executed locally, from the working directory CI will use, before the job is authored.
- **When a test runner and an e2e runner share a repo, scope them by directory explicitly.** Default
  globs assume they own the tree. Vitest owns `src/`, Playwright owns `e2e/`, and saying so in config
  is one line that prevents a confusing cross-runner error.
- **Job dependencies need a stated payload.** `needs:` is only justified when the downstream job
  *consumes* something (an artifact, a published package) or when the upstream job is meaningfully
  cheaper than the work it gates. "Build first" as a sequencing instinct costs real wall clock and
  buys nothing when every job builds anyway. Worth checking the other direction too: the follow-on
  here is artifact passing, which would make `needs:` genuinely earn its place.
- **Caching is not "too early" when the cache key is structurally sound.** The instinct to defer
  caching in a young project is about key correctness — stale or over-broad keys cause worse problems
  than slow restores. Central Package Management removes that risk: one file pins every version, so
  its hash fully determines the restore graph. The maturity question was the wrong question; the key
  question was the right one.

## Outstanding items / next-session inputs

- **Build-artifact passing** is the real fix for compiling the solution three times per run
  (`upload-artifact` → `dotnet test --no-build`). It would restore a *justified* `needs: build` and
  cut more than this session did. Recorded in the `needs:` note in `ci.yml`.
- **Integration-test matrix sharding** when that job crosses ~4 minutes. Trigger is in the workflow.
- **Playwright e2e in CI** — needs the full Aspire stack; wants its own workflow. `client/e2e` is
  currently run by hand only, and the Vitest scoping fix means nothing collects it accidentally now.
- **Confirm the projected timing on the first post-merge run.** The ~1m42s figure is a projection from
  the measured baseline, not an observed number; the merge run on `main` is the check.
- **`docs/prompts/chore/005`** (demo-traffic & observability review) has a prompt but no retro. Either
  that session ran light and the pair should be closed out, or it is still owed. Untouched here.

## Spec-delta — landed?

**Named none; forward-confirmed none.** The prompt named no slice, capability, narrative, workshop, or
ADR delta — this is repo-meta CI configuration plus the one-line Vitest scoping fix the client gate
required to be green. No canonical spec changed, and none should have. The `client/vite.config.ts`
edit is test-runner configuration, not application behavior: no component, query, schema, or endpoint
changed, and the 125 tests that passed before pass after.
