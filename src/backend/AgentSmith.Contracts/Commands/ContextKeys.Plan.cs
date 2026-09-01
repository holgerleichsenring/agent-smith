namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Plan + diff + approval PipelineContext keys. Covers the legacy Plan entity
/// (p0394a retired its GeneratePlan producer; the key survives for the
/// empty-plan gate and old run records), the open-questions round-trip, and the
/// wire-format JSON/markdown payloads consumed by WriteRunResultHandler + the
/// Redis pipeline-storage layer.
/// </summary>
public static partial class ContextKeys
{
    public const string Plan = "Plan";
    public const string CodeChanges = "CodeChanges";

    /// <summary>p0452: the commands the agent ran in this phase, as evidence for the
    /// delivery account. Until it could see them, three runs were refused for searches
    /// the agent had actually run.</summary>
    public const string PhaseCommands = "PhaseCommands";

    /// <summary>p0341: the coding master's durable <c>ProgressLedger</c> — seeded
    /// from the ratified phase spec's steps (p0394a), kept current via
    /// update_progress, source-of-truth copy. Read by the nudge builders
    /// (re-drives resume the checklist) and by WriteRunResultHandler (the
    /// done-status honesty diagnostic).</summary>
    public const string ProgressLedger = "ProgressLedger";

    /// <summary>p0241: the coding master's parsed structured verification verdict
    /// (a <c>MasterVerification</c>). Set by AgenticMasterHandler, read by the
    /// keystone in CommitAndPRHandler to refuse success on an unverified/red run.</summary>
    public const string MasterVerification = "MasterVerification";

    /// <summary>p0267: the master loop's final answer text. Set by AgenticMasterHandler,
    /// read by CollectMasterFindingsHandler on the api-security path to scrape the
    /// master's triaged observation-array into SkillObservations.</summary>
    public const string MasterAnswer = "MasterAnswer";

    /// <summary>p0267: the master skill name that ran (e.g. <c>api-security-master</c>).
    /// Set by AgenticMasterHandler, read by CollectMasterFindingsHandler to resolve the
    /// master's declared output_schema and gate the findings scrape.</summary>
    public const string MasterSkillName = "MasterSkillName";

    /// <summary>p0279: the distinct source paths the scan master read this run (raw,
    /// agent-addressed). Set by AgenticMasterHandler, read by CollectMasterFindings /
    /// MergeMasterFindings to downgrade an analyzed_from_source claim on a file the master
    /// never read, and used by the coverage re-drive gauge.</summary>
    public const string MasterReadPaths = "MasterReadPaths";

    /// <summary>2026-09-01-3653: the size, in characters, of the system prompt the master
    /// pass was given. Written down because every number in the argument about prompt size
    /// had been a guess, twice wrong in the same discussion, from the same absence.</summary>
    public const string MasterSystemPromptChars = "MasterSystemPromptChars";

    /// <summary>2026-09-01-3653: how many turns the master's last pass used, counted from its
    /// own assistant messages. NEAR-EXACT and biased high — a provider may split one turn
    /// across messages — and everything that renders it says so.</summary>
    public const string MasterTurnsUsed = "MasterTurnsUsed";
    public const string ConsolidatedPlan = "ConsolidatedPlan";
    public const string ConsolidatedDiscussion = "ConsolidatedDiscussion";
    public const string Approved = "Approved";
    public const string PlanArtifact = "PlanArtifact";

    // p0128a: wire-format JSON/markdown payloads alongside the typed Plan/CodeChanges
    // entries. Existing Plan and CodeChanges keep their typed-entity semantics; the new
    // keys carry the persisted shape consumed by WriteRunResultHandler and the Redis
    // pipeline-storage layer.
    public const string PlanJson = "PlanJson";
    public const string DiffJson = "DiffJson";
    public const string BootstrapMarkdown = "BootstrapMarkdown";

    // p0128b: Plan open_questions round-trip. OpenQuestionsAwaitingAnswer halts the
    // pipeline cleanly when the Plan emits questions; PlanAnswers carries operator
    // answers from the webhook re-trigger into the next Plan-skill run.
    public const string OpenQuestionsAwaitingAnswer = "OpenQuestionsAwaitingAnswer";
    public const string PlanAnswers = "PlanAnswers";

    /// <summary>p0140e: empty-plan gate flag. Set by EmptyPlanCheckHandler when the Plan has zero
    /// actionable steps (and no open questions). PipelineExecutor short-circuits the same way as
    /// OpenQuestionsAwaitingAnswer — run completes Ok without running downstream handlers.</summary>
    public const string EmptyPlanSkipped = "EmptyPlanSkipped";
}
