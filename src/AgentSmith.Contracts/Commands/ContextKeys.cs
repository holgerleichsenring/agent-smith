namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Well-known keys for the PipelineContext dictionary. Split into per-subdomain
/// partial files (Pipeline, Security, Api, Bootstrap) for readability; every
/// caller references ContextKeys.X regardless of which partial defines the constant.
/// This file holds the top-level "core" keys touched by every pipeline run.
/// </summary>
public static partial class ContextKeys
{
    public const string AgentConfig = "AgentConfig";
    public const string TicketId = "TicketId";
    public const string Ticket = "Ticket";
    public const string Repository = "Repository";

    /// <summary>p0140d: the RepoConnection for THIS run. Resolved at the top of ExecutePipelineUseCase
    /// from PipelineRequest.RepoName + project.Repos. Single source of truth for "which repo is
    /// this run for" — every consumer that previously read project.Repo now reads CurrentRepo.</summary>
    public const string CurrentRepo = "CurrentRepo";

    public const string ProjectMap = "ProjectMap";
    public const string DomainRules = "DomainRules";
    public const string CodingPrinciples = DomainRules;
    public const string CodeMap = "CodeMap";
    public const string ProjectContext = "ProjectContext";
    public const string Headless = "Headless";
    public const string ConfigDir = "ConfigDir";

    public const string SourceType = "SourceType";
    public const string SourcePath = "SourcePath";
    public const string SourceUrl = "SourceUrl";
    public const string SourceAuth = "SourceAuth";

    public const string SwaggerSpecFull = "SwaggerSpecFull";
    public const string CheckoutBranch = "CheckoutBranch";
    public const string ResolvedPipeline = "ResolvedPipeline";

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

    /// <summary>Dictionary&lt;string, string&gt; mapping summoned-skill → summoner-skill recorded
    /// every time BlockingFollowUpDetector (p0147d) inserts a SwitchSkill follow-up.
    /// Used to break immediate A→B→A ping-pong cycles in O(1) without an explicit cap.</summary>
    public const string SwitchSkillLastSummoner = "SwitchSkillLastSummoner";

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

    // p0128c: name of the currently-executing pipeline step. PipelineExecutor sets
    // this before each step and clears it after; the gated context wrapper reads it
    // to decide whether a Get<T>/TryGet<T> is permitted under the active IPhaseDataFlow.
    public const string ActivePhaseStep = "ActivePhaseStep";

    // p0129a: Verify phase between Implementation and delivery.
    // VerifyRoundCount counts re-implementation rounds (1 = first run, 2 = after one re-loop).
    // VerifyNotes is the human-readable note string fed back into AgenticExecute on re-loop.
    // VerifyObservations carries the raw observations from each verify-phase invocation
    // (kept separate from SkillObservations so the delivery layer doesn't render them).
    public const string VerifyRoundCount = "VerifyRoundCount";
    public const string VerifyNotes = "VerifyNotes";
    public const string VerifyObservations = "VerifyObservations";
    public const string Decisions = "Decisions";
    public const string Attachments = "Attachments";
    public const string SourceFilePath = "SourceFilePath";
    public const string DocumentMarkdown = "DocumentMarkdown";
    public const string ContractType = "ContractType";
}
