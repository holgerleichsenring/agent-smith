namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0327 durable-dialogue keys: the executor's live step cursor, the hybrid-wait
/// configuration, and the resume envelope. A run that parks on a DialogQuestion
/// serializes the cursor + context, ends its worker task, and re-enters here.
/// </summary>
public static partial class ContextKeys
{
    /// <summary>p0327: the executor's LIVE step cursor — the ordered
    /// IReadOnlyList&lt;PipelineCommand&gt; from the currently executing node to the
    /// end of the LinkedList (dynamically spliced follow-ups included). Published
    /// by PipelineExecutor before every batch so a checkpoint taken inside a
    /// handler serializes exactly the remaining work, starting with itself.</summary>
    public const string RemainingCommands = "RemainingCommands";

    /// <summary>p0327: the executor's running execution count, published alongside
    /// <see cref="RemainingCommands"/>. A resumed run continues its step indices
    /// from here so the dashboard renders one continuous run, not a restart.</summary>
    public const string PipelineExecutionCount = "PipelineExecutionCount";

    /// <summary>p0327: hot-wait threshold in seconds (config
    /// <c>dialogue.hot_wait_seconds</c>, default 600). Below it a DialogQuestion
    /// is awaited in-memory; past it an eligible run checkpoints and parks.</summary>
    public const string DialogueHotWaitSeconds = "DialogueHotWaitSeconds";

    /// <summary>p0327: approval-question timeout in seconds (config
    /// <c>dialogue.approval_timeout_seconds</c>, default 3 days). The persisted
    /// DefaultAnswer ("reject") applies headless when it elapses.</summary>
    public const string DialogueApprovalTimeoutSeconds = "DialogueApprovalTimeoutSeconds";

    /// <summary>p0327: bool marker set by the ask gate after a successful
    /// checkpoint. PipelineExecutorPolicy treats it as a clean park (like
    /// OpenQuestionsAwaitingAnswer) and ExecutePipelineUseCase publishes the
    /// terminal-less <c>waiting_for_input</c> status instead of success.</summary>
    public const string WaitingForInput = "WaitingForInput";

    /// <summary>p0327: request-context key carrying the serialized ResumePayload
    /// JSON (a plain string — it survives the Redis job queue's JsonElement
    /// round-trip). Present only on a resume launch enqueued by RunResumer.</summary>
    public const string ResumeCheckpoint = "ResumeCheckpoint";

    /// <summary>p0327: the operator's DialogAnswer delivered with the resume
    /// payload. The dialogue ask gate consumes it once, as the result of the
    /// checkpointed ask, instead of publishing + waiting again.</summary>
    public const string ResumedDialogueAnswer = "ResumedDialogueAnswer";

    /// <summary>p0327: resolved project name, seeded by ExecutePipelineUseCase so
    /// the checkpoint event can stamp the run's project without a DB read (the
    /// producer may be a spawned orchestrator with no DB access).</summary>
    public const string ProjectName = "ProjectName";

    /// <summary>p0327: tracker platform of a ticket run (e.g. "azuredevops"),
    /// seeded by ExecutePipelineUseCase; absent on non-ticket runs. Rides the
    /// checkpoint so the resume queue entry carries the same platform the
    /// original claim did.</summary>
    public const string TrackerPlatform = "TrackerPlatform";
}
