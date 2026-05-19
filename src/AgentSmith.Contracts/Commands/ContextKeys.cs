namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Well-known keys for the PipelineContext dictionary.
/// </summary>
public static class ContextKeys
{
    public const string AgentConfig = "AgentConfig";
    public const string TicketId = "TicketId";
    public const string Ticket = "Ticket";
    public const string Repository = "Repository";
    /// <summary>p0140d: the RepoConnection for THIS run. Resolved at the top of ExecutePipelineUseCase
    /// from PipelineRequest.RepoName + project.Repos. Single source of truth for "which repo is
    /// this run for" — every consumer that previously read project.Repo now reads CurrentRepo.</summary>
    public const string CurrentRepo = "CurrentRepo";
    public const string Plan = "Plan";
    public const string CodeChanges = "CodeChanges";
    public const string ProjectMap = "ProjectMap";
    public const string DomainRules = "DomainRules";
    public const string CodingPrinciples = DomainRules;
    public const string ActiveSkill = "ActiveSkill";
    public const string AvailableRoles = "AvailableRoles";
    public const string ProjectSkills = "ProjectSkills";
    public const string ExecutionTrail = "ExecutionTrail";
    public const string DiscussionLog = "DiscussionLog";
    public const string ConsolidatedPlan = "ConsolidatedPlan";
    public const string ConsolidatedDiscussion = "ConsolidatedDiscussion";
    public const string Approved = "Approved";
    public const string TestResults = "TestResults";
    public const string PullRequestUrl = "PullRequestUrl";
    public const string Headless = "Headless";
    public const string CodeMap = "CodeMap";
    public const string ProjectContext = "ProjectContext";
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
    public const string InitMode = "InitMode";
    public const string SourceFilePath = "SourceFilePath";
    public const string DocumentMarkdown = "DocumentMarkdown";
    public const string ContractType = "ContractType";
    public const string Attachments = "Attachments";
    public const string Decisions = "Decisions";
    public const string SourceType = "SourceType";
    public const string SourcePath = "SourcePath";
    public const string SourceUrl = "SourceUrl";
    public const string SourceAuth = "SourceAuth";

    public const string ScanPrIdentifier = "ScanPrIdentifier";
    public const string ScanBranch = "ScanBranch";
    public const string OutputFormat = "OutputFormat";
    public const string OutputDir = "OutputDir";
    public const string SwaggerSpec = "SwaggerSpec";

    /// <summary>p0147c: original (uncompressed) swagger spec. LoadSwaggerHandler runs the
    /// fetched spec through ISwaggerSpecCompressor: the (possibly shrunk) result lands in
    /// <see cref="SwaggerSpec"/>, the verbatim original lands here. Skills that need full
    /// schema detail (response-analyst, payload-fuzz scanners) read from this key;
    /// every other consumer reads the default <see cref="SwaggerSpec"/>.</summary>
    public const string SwaggerSpecFull = "SwaggerSpecFull";
    public const string NucleiResult = "NucleiResult";
    public const string ZapResult = "ZapResult";
    public const string SpectralResult = "SpectralResult";
    public const string ApiTarget = "ApiTarget";
    public const string ZapFailed = "ZapFailed";
    public const string ApiScanFindingsSummary = "ApiScanFindingsSummary";
    public const string ApiScanFindingsByCategory = "ApiScanFindingsByCategory";
    public const string SwaggerPath = "SwaggerPath";
    public const string CheckoutBranch = "CheckoutBranch";
    public const string ResolvedPipeline = "ResolvedPipeline";
    public const string StaticScanResult = "StaticScanResult";
    public const string GitHistoryScanResult = "GitHistoryScanResult";
    public const string DependencyAuditResult = "DependencyAuditResult";
    public const string SecurityFindingsSummary = "SecurityFindingsSummary";
    public const string SecurityFindingsByCategory = "SecurityFindingsByCategory";
    public const string SecurityTrend = "SecurityTrend";
    public const string DialogueAnswer = "DialogueAnswer";
    public const string DialogueQuestion = "DialogueQuestion";
    public const string SecurityFixRequests = "SecurityFixRequests";
    public const string SkillCandidates = "SkillCandidates";
    public const string SkillEvaluations = "SkillEvaluations";
    public const string SkillInstallPath = "SkillInstallPath";
    public const string ApprovedSkills = "ApprovedSkills";
    public const string WikiUpdates = "WikiUpdates";
    public const string QueryAnswer = "QueryAnswer";
    public const string RunHistory = "RunHistory";
    public const string AutonomousFindings = "AutonomousFindings";
    public const string WrittenTickets = "WrittenTickets";
    public const string PipelineTypeName = "PipelineType";
    /// <summary>p0145: pipeline preset name (e.g. "fix-bug", "security-scan"). Set by
    /// ExecutePipelineUseCase alongside PipelineTypeName. Distinct from the
    /// "pipeline_name" concept (Activation-system enum) — this key is the
    /// ToolKit pipeline-allow-list lookup key.</summary>
    public const string PipelineName = "PipelineName";
    public const string SkillOutputs = "SkillOutputs";
    public const string ConfigDir = "ConfigDir";
    public const string DoneStatus = "DoneStatus";
    public const string SkillObservations = "SkillObservations";
    public const string ConvergenceResult = "ConvergenceResult";
    public const string Personas = "Personas";
    public const string ActiveMode = "ActiveMode";
    public const string HttpProbeResults = "HttpProbeResults";
    public const string DeferredBuffers = "DeferredBuffers";

    // p0111c: phase-based triage
    public const string TriageOutput = "TriageOutput";
    public const string CurrentPhase = "CurrentPhase";
    public const string PlanArtifact = "PlanArtifact";
    public const string ConceptVocabulary = "ConceptVocabulary";

    /// <summary>
    /// Storage slot for the typed concept values published during a pipeline run
    /// (Dictionary&lt;string, object&gt;). Managed by IRunStateConcepts; do not write directly.
    /// </summary>
    public const string ConceptValues = "ConceptValues";

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
    /// every time SkillRoundHandlerBase.DetectBlockingFollowUp inserts a SwitchSkill follow-up.
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
}
