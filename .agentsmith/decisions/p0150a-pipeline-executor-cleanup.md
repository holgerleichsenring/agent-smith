# p0150a — PipelineExecutor cleanup

## Decision: delete `PipelineExecutorLegacy` entirely

The parallel-class window from p0147e has carried one release cycle of
stability on the new composed shape. No operator reports of divergence.
Defensive preservation has its own cost — drifted flag plumbing, doubled
test packs that rot, and the persistent cognitive load of "which executor
is running in prod". The window was always meant to be cut after enough
production exposure; that bar is met.

Removed in this phase:

- `src/AgentSmith.Application/Services/PipelineExecutorLegacy.cs`
- `PIPELINE_EXECUTOR_USE_LEGACY` env-flag plumbing
- `UseLegacyExecutor()` helper + the conditional `IPipelineExecutor`
  registration in `PipelineExecutionExtensions`
- `tests/AgentSmith.Tests/Services/PipelineExecutorTestHarness.cs`

DI now resolves `IPipelineExecutor` directly to the live `PipelineExecutor`.

## Decision: collapse `[Theory]/[MemberData]` shape parametrisation

The parametrisation existed exclusively to assert observable parity between
the new composed executor and the pre-p0147e monolith. With the monolith
deleted, the `Shape` enum + `ExecutorShapes()` member-data are dead weight.
Each test now runs as a plain `[Fact]` against the live executor.

The expected test-count drop: every test in the four `PipelineExecutor*Tests`
files that ran twice (once per shape) now runs once. The substantive coverage
is unchanged — same assertions, same mocks, half the case rows.

## Decision: thin `PipelineExecutorTestBuilder` rather than per-class boilerplate

The phase YAML allowed either "DI-resolve directly in each `[Fact]`" or
"a thin local test helper". The four test files share the same mock graph
(executor, factory, ticket factory, lifecycle coordinator, sandbox factory,
progress reporter, language resolver). Inlining that into four separate
classes would duplicate ~30 lines of setup four times. A single internal
`PipelineExecutorTestBuilder` in `tests/Services/` exposes the same mock
properties without the discriminated-`Shape` enum — straight construction
of the live executor and its three composed services.

## Decision: extract `PipelineExecutorPolicy` to land the executor at ≤ 80 lines

`PipelineExecutor.cs` had two pure helpers — `ResolveMaxConcurrent` (an
`AgentConfig.Parallelism.MaxConcurrentSkillRounds` lookup that prefers
`ResolvedPipelineConfig` over `ResolvedProject.Agent`) and
`TryGetParkedReason` (looks for `OpenQuestionsAwaitingAnswer` /
`EmptyPlanSkipped` in `PipelineContext`). Both are static / pure inspection
of context; neither touches mutable instance state. Moving them into a
sibling `internal static class PipelineExecutorPolicy` keeps the orchestrator
at its 80-line budget without amputating logic that genuinely belongs at the
pipeline level.

Test-side: the existing batching tests called `PipelineExecutor.PeelBatch`
directly (a static shim from p0147e). Repointed to
`PipelineStepRunner.PeelBatchInternal` so the shim could also go.

## Decision: phase-history comments stripped from touched files only

`// p0117b:`, `// p0128b:`, `// p0135:`, `// p0140d:`, `// p0140e:`,
`// p0112:` etc. removed from the executor + its three composed services.
The underlying reasoning kept in each comment block — only the phase prefix
is dropped. A repo-wide sweep is deferred; doing it in this phase would
inflate the diff and make review harder.
