namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Pipeline-execution PipelineContext keys: run metadata + lifecycle markers
/// (RunId, RunStartedAt, ActivePhaseStep, FailedStepName, Sandbox), and the
/// preset selectors (PipelineTypeName, PipelineName). Plan/verify/skill keys
/// live in dedicated partial files (Plan, Verify, Skill).
/// </summary>
public static partial class ContextKeys
{
    public const string ExecutionTrail = "ExecutionTrail";
    public const string DiscussionLog = "DiscussionLog";
    public const string TestResults = "TestResults";
    public const string PullRequestUrl = "PullRequestUrl";
    public const string RunNumber = "RunNumber";
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
    public const string Personas = "Personas";
    public const string ActiveMode = "ActiveMode";
    public const string DeferredBuffers = "DeferredBuffers";

    public const string DialogueAnswer = "DialogueAnswer";
    public const string DialogueQuestion = "DialogueQuestion";

    /// <summary>Short correlation id (8 hex chars) generated per pipeline run, attached as
    /// log scope so concurrent runs are filterable in shared log streams.</summary>
    public const string RunId = "RunId";

    /// <summary>Display label of the pipeline step that failed (set by PipelineExecutor before
    /// the failure-recovery wrapper invokes PersistWorkBranchHandler). Used in WIP commit trailer.</summary>
    public const string FailedStepName = "FailedStepName";

    /// <summary>Typed PersistFailureKind set by PersistWorkBranchHandler before returning Fail.
    /// Read by PipelineExecutor's wrapper for log-level routing and counter escalation.</summary>
    public const string PersistFailureKind = "PersistFailureKind";

    /// <summary>Active ISandbox for the pipeline run (created by PipelineExecutor when the
    /// pipeline contains CheckoutSource / AgenticExecute / Test / GenerateTests / GenerateDocs).
    /// Discussion-only pipelines leave this unset.</summary>
    public const string Sandbox = "Sandbox";

    // p0128c: name of the currently-executing pipeline step. PipelineExecutor sets
    // this before each step and clears it after; the gated context wrapper reads it
    // to decide whether a Get<T>/TryGet<T> is permitted under the active IPhaseDataFlow.
    public const string ActivePhaseStep = "ActivePhaseStep";
}
