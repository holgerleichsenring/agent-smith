# Parked Architecture Items

Cross-slice problems collected during the b → c → a → d ship-order of
p0169j. Each entry is a real architectural mismatch found while
implementing a slice; the slice itself proceeds with the documented
workaround so the ship-order doesn't stall. Resolution happens in a
dedicated cleanup pass at the end of p0169j.

Format: one entry per problem.

- **Found-in** — the slice where it surfaced
- **Symptom** — what breaks if you take the spec literally
- **Workaround in slice** — what we did anyway so the slice ships
- **Real fix** — the architectural change needed in the cleanup pass

---

## 1. Server can't read `.agentsmith/runs/{runId}/result.md`

**Found-in:** p0169j-c (Result tab)

**Symptom:** Spec says `JobsHub.GetResultMarkdown(runId)` reads
`.agentsmith/runs/{runId}/result.md` from disk. The actual write happens
via `ISandboxFileReader` inside the sandbox (Docker volume or operator's
host repo); the Server-in-Docker has no path to it. `AGENTSMITH_RUNS_ROOT`
env-var was deleted in p0169e as having no consumer.

**Workaround in slice:** WriteRunResultHandler additionally writes the
rendered result.md into `IRunArtifactStore` as a new 4th slot
(`WriteResultMarkdownAsync` / `ReadResultMarkdownAsync`) — mirrors the
existing plan/diff/bootstrap pattern. ResultMarkdownReader reads from
Redis instead of disk. TTL 24h. Empty-state in the dashboard links to
the PR (durable surface) when Redis entry is gone.

**Real fix:** None needed — the PR is the long-term durable surface for
result.md (it's in `git add -A` as part of CommitAndPR). Workaround
becomes the permanent design. Move from PARKED to "decision logged"
once the cleanup pass confirms operator UX is acceptable.

## 2. Same gap for events.jsonl frozen-trail persistence

**Found-in:** p0169j-a (anticipated)

**Symptom:** Spec says FrozenTrailWriter dumps to
`.agentsmith/runs/{id}/events.jsonl`. Same disk-not-reachable problem
as item 1.

**Workaround planned:** XRANGE the run-stream into a Redis LIST (or
extend the IRunArtifactStore with a frozen-events slot) on RunFinished.
Lifecycle tied to job-cleanup, not a fixed TTL. Optional: bump Redis
maxmemory to 1 GB now that we have AOF persistence on disk.

**Real fix:** Same as item 1 — Redis is enough; the PR carries the
durable artefact for any operator who needs to refer back beyond the
in-memory retention window.

## 3. Hardcoded `"api-scan"` pipeline name in DeliverFindingsHandler ✅ CLOSED

**Found-in:** observed while reading code for p0169j-c

**Symptom:** `DeliverFindingsHandler.cs:28` constructed OutputContext
with the literal string `"api-scan"` for ProjectName regardless of
which pipeline was running. mad-discussion / fix-bug / etc. would all
label themselves "api-scan" in any consumer that read
`OutputContext.ProjectName`.

**Fixed:** Reads `context.Pipeline.TryGet<string>(ContextKeys.PipelineName, ...)`
with a `"unknown-pipeline"` fallback when not set (CLI / test paths
without a resolved pipeline). 4-line change in the p0169j cleanup pass.

## 4. CI Console.SetOut race (6 tests red on main) ✅ CLOSED

**Found-in:** CI run 26524374968 on the merged PR #214

**Symptom:** `Console.SetOut(stringWriter)` in ChannelSplitTests +
ConsoleOutputStructuredFallbackTests + SummaryEvidenceBreakdownTests
raced against parallel ConsoleOutputStrategyTests + MultiOutputTests
via xUnit's default parallelism. Failure:
`ObjectDisposedException: Cannot write to a closed TextWriter`.
Release 0.65.0 went out with this red.

**Fixed:** Annotated the five output-test classes with
`[Collection("ConsoleOut")]` so xUnit's collection-scheduler runs
them serially. No new fixture needed — xUnit's default behaviour
is "no parallelism within a collection."
