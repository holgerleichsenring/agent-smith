namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Pipeline-execution PipelineContext keys: run metadata + lifecycle markers
/// (RunId — canonical UTC timestamp + suffix per pipeline run, RunStartedAt,
/// ActivePhaseStep, FailedStepName, Sandbox), and the preset selectors
/// (PipelineTypeName, PipelineName). Plan/verify/skill keys live in dedicated
/// partial files (Plan, Verify, Skill).
/// </summary>
public static partial class ContextKeys
{
    public const string ExecutionTrail = "ExecutionTrail";
    public const string DiscussionLog = "DiscussionLog";
    public const string PullRequestUrl = "PullRequestUrl";
    public const string RunCostSummary = "RunCostSummary";
    public const string RunDurationSeconds = "RunDurationSeconds";

    /// <summary>
    /// <see cref="DateTimeOffset"/> stamped at pipeline start. WriteRunResultHandler
    /// reads this to compute the run's wall-clock duration when no handler
    /// explicitly wrote <see cref="RunDurationSeconds"/> (e.g. init-project, which
    /// has no AgenticExecute step).
    /// </summary>
    public const string RunStartedAt = "RunStartedAt";

    public const string PipelineTypeName = "PipelineType";

    /// <summary>p0145: pipeline preset name (e.g. "code", "security-scan"). Set by
    /// ExecutePipelineUseCase alongside PipelineTypeName. Distinct from the
    /// "pipeline_name" concept (Activation-system enum) — this key is the
    /// ToolKit pipeline-allow-list lookup key.</summary>
    public const string PipelineName = "PipelineName";

    public const string DoneStatus = "DoneStatus";
    /// <summary>p0261: native ticket status a FAILED run terminalizes to (failure
    /// counterpart of DoneStatus). Seeded by SpawnPipelineRunsUsecase from the
    /// trigger's failed_status, falling back to done_status. Read by the failure
    /// path (PipelineErrorHandler) to move the ticket out of its trigger status.</summary>
    public const string FailedStatus = "FailedStatus";
    /// <summary>p0318: native ticket status the clarification gates park the ticket in
    /// when the run needs user input. Seeded by SpawnPipelineRunsUseCase from the
    /// trigger's needs_clarification_status (null when unset — then the gate posts +
    /// halts but does not park). Read via ClarificationParkStatusResolver
    /// (MasterOpenQuestions, SpecHandback).</summary>
    public const string NeedsClarificationStatus = "NeedsClarificationStatus";

    /// <summary>p0390: the native status a NOT-IMPLEMENTABLE verdict parks in. Separate
    /// from the clarification park because a verdict is not a question — a comment must
    /// not restart it, only an explicit operator Retry.</summary>
    public const string NotImplementableStatus = "NotImplementableStatus";
    public const string Personas = "Personas";
    public const string ActiveMode = "ActiveMode";
    public const string DeferredBuffers = "DeferredBuffers";

    public const string DialogueAnswer = "DialogueAnswer";
    public const string DialogueQuestion = "DialogueQuestion";
    
    /// <summary>Canonical run identifier: UTC ISO-8601 timestamp + 4-hex suffix
    /// (e.g. <c>2026-05-20T22-27-43-8a3f</c>). Generated once at pipeline start
    /// by ExecutePipelineUseCase; reused as log-scope tag, WIP commit trailer,
    /// run-directory prefix, and context.yaml <c>runs:</c> entry key.</summary>
    public const string RunId = "RunId";

    /// <summary>Display label of the pipeline step that failed (set by PipelineExecutor before
    /// the failure-recovery wrapper invokes PersistWorkBranchHandler). Used in WIP commit trailer.</summary>
    public const string FailedStepName = "FailedStepName";

    /// <summary>p0237: the human-readable reason a run failed/was cancelled (the failed step's
    /// CommandResult.Message). Set by PipelineExecutor on the failure path so the finalizer tail
    /// (WriteRunResult → result.md, the ticket comment) can state WHY, not just "failed".</summary>
    public const string FailureReason = "FailureReason";

    /// <summary>Typed PersistFailureKind set by PersistWorkBranchHandler before returning Fail.
    /// Read by PipelineExecutor's wrapper for log-level routing and counter escalation.</summary>
    public const string PersistFailureKind = "PersistFailureKind";

    /// <summary>p0360: Dictionary&lt;string,bool&gt; of repo name → "a checkpoint commit with
    /// REAL code (non-run-record paths) was pushed". Written by RunWorkCheckpointer after each
    /// mid-run checkpoint push; read by CommitAndPRHandler so a repo whose work was already
    /// committed by checkpoints (clean tree at PR time) still counts as changed and gets its
    /// PR instead of a false "nothing to commit" skip.</summary>
    public const string CheckpointedRepos = "CheckpointedRepos";

    /// <summary>Active ISandbox for the pipeline run (created by PipelineExecutor when the
    /// pipeline contains CheckoutSource / AgenticExecute / GenerateTests / GenerateDocs).
    /// Discussion-only pipelines leave this unset.</summary>
    public const string Sandbox = "Sandbox";

    // p0128c: name of the currently-executing pipeline step. PipelineExecutor sets
    // this before each step and clears it after; the gated context wrapper reads it
    // to decide whether a Get<T>/TryGet<T> is permitted under the active IPhaseDataFlow.
    public const string ActivePhaseStep = "ActivePhaseStep";

    /// <summary>2026-09-13-a284: Dictionary&lt;string,string&gt; of repo name → the base
    /// branch that repository's work branch was actually cut from — 2026-09-13-5cdf's
    /// resolved rung, recorded at checkout and read by every site that opens a pull
    /// request. A repository whose ladder fell through to the clone's own base has NO
    /// entry: the provider's default-branch lookup is then the right answer, which is
    /// what every run did before rungs existed. Keyed per repository because the rung is
    /// — a two-repo run may carry the feature branch in one clone and not the other.</summary>
    public const string PullRequestTargets = "PullRequestTargets";

    /// <summary>2026-09-17-0e79e: <see cref="Pipeline.StepBudget"/> — how many command
    /// executions this run's segment may spend before the executor calls it an insertion
    /// loop. PhaseSequence publishes it for the tail it splices and nothing else publishes
    /// one; with the key absent the executor uses the budget every preset always had.</summary>
    public const string StepBudget = "StepBudget";

    /// <summary>2026-08-31-7097: dictionary keyed by sandbox key, holding the toolchain
    /// image the backend ACTUALLY pulled for that sandbox. An entry exists only when the
    /// sandbox named one, so the in-process backend — which runs on the host and pulls
    /// nothing — is an absent key rather than an image nobody started.</summary>
    public const string SandboxImages = "SandboxImages";
}
