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

    /// <summary>p0145: pipeline preset name (e.g. "fix-bug", "security-scan"). Set by
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

    /// <summary>Active ISandbox for the pipeline run (created by PipelineExecutor when the
    /// pipeline contains CheckoutSource / AgenticExecute / GenerateTests / GenerateDocs).
    /// Discussion-only pipelines leave this unset.</summary>
    public const string Sandbox = "Sandbox";

    // p0128c: name of the currently-executing pipeline step. PipelineExecutor sets
    // this before each step and clears it after; the gated context wrapper reads it
    // to decide whether a Get<T>/TryGet<T> is permitted under the active IPhaseDataFlow.
    public const string ActivePhaseStep = "ActivePhaseStep";

}
