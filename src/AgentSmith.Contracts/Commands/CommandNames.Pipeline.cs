namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Pipeline-execution command names: ticket fetch + checkout, project/context loading,
/// the Plan/Approve/Execute chain, test + commit + PR, phase-based triage (p0111c),
/// dialogue rounds (SkillRound, FilterRound, ConvergenceCheck), and bootstrap-init
/// concept-publishing handlers.
/// </summary>
public static partial class CommandNames
{
    public const string FetchTicket = "FetchTicketCommand";
    public const string CheckoutSource = "CheckoutSourceCommand";
    public const string TryCheckoutSource = "TryCheckoutSourceCommand";
    public const string BootstrapProject = "BootstrapProjectCommand";
    public const string LoadCodeMap = "LoadCodeMapCommand";
    public const string LoadCodingPrinciples = "LoadCodingPrinciplesCommand";
    public const string LoadContext = "LoadContextCommand";
    public const string LoadSkills = "LoadSkillsCommand";
    public const string AnalyzeCode = "AnalyzeCodeCommand";
    public const string GeneratePlan = "GeneratePlanCommand";

    /// <summary>p0140e: post-Plan gate that decides "skip cleanly" when plan has zero steps.
    /// Emits agent_smith_pipeline_skipped_as_irrelevant_total with reason='empty_plan' and
    /// sets ContextKeys.EmptyPlanSkipped; PipelineExecutor short-circuits on the flag.</summary>
    public const string EmptyPlanCheck = "EmptyPlanCheckCommand";

    public const string Approval = "ApprovalCommand";
    public const string AgenticExecute = "AgenticExecuteCommand";
    public const string Test = "TestCommand";
    public const string WriteRunResult = "WriteRunResultCommand";
    public const string CommitAndPR = "CommitAndPRCommand";
    public const string InitCommit = "InitCommitCommand";

    /// <summary>p0158c: PR cross-link pass-2. Runs after CommitAndPR / InitCommit when
    /// multiple PRs opened in the same run. Replaces the sibling-PRs marker in each
    /// opened PR's body with the actual sibling URL list via the source provider's
    /// UpdatePullRequestBodyAsync. No-op when fewer than two PRs opened.</summary>
    public const string PrCrossLink = "PrCrossLinkCommand";
    public const string GenerateTests = "GenerateTestsCommand";
    public const string GenerateDocs = "GenerateDocsCommand";
    public const string Triage = "TriageCommand";
    public const string SwitchSkill = "SwitchSkillCommand";
    public const string SkillRound = "SkillRoundCommand";
    public const string ConvergenceCheck = "ConvergenceCheckCommand";
    public const string CompileDiscussion = "CompileDiscussionCommand";
    public const string AcquireSource = "AcquireSourceCommand";
    public const string BootstrapDocument = "BootstrapDocumentCommand";
    public const string DeliverOutput = "DeliverOutputCommand";
    public const string SessionSetup = "SessionSetupCommand";
    public const string Ask = "AskCommand";

    public const string CompileKnowledge = "CompileKnowledgeCommand";
    public const string QueryKnowledge = "QueryKnowledgeCommand";
    public const string LoadRuns = "LoadRunsCommand";
    public const string WriteTickets = "WriteTicketsCommand";

    // p0111c: phase-based triage
    public const string FilterRound = "FilterRoundCommand";
    public const string RunReviewPhase = "RunReviewPhaseCommand";
    public const string RunFinalPhase = "RunFinalPhaseCommand";

    // p0112: branch persistence — pipeline-failure recovery commit + push
    public const string PersistWorkBranch = "PersistWorkBranchCommand";

    // p0125c: typed concept publication
    public const string PipelineNameInitializer = "PipelineNameInitializerCommand";
    public const string BootstrapCheck = "BootstrapCheckCommand";

    // p0128b: Plan open_questions round-trip
    public const string PlanOpenQuestions = "PlanOpenQuestionsCommand";

    // p0129a: Verify phase between Implementation and delivery
    public const string RunVerifyPhase = "RunVerifyPhaseCommand";

    // p0130a: bootstrap-files gate (aborts when context.yaml or coding-principles.md is missing)
    public const string BootstrapGate = "BootstrapGateCommand";

    // p0130c: project-language publication + bootstrap-skill dispatch (init-project)
    public const string PublishProjectLanguage = "PublishProjectLanguageCommand";
    public const string BootstrapDispatch = "BootstrapDispatchCommand";

    // p0130c-followup: producer-loop runtime for bootstrap skills (csharp/node/python/
    // generic-bootstrap). Distinct from SkillRound because the producer needs a
    // tool-bearing chat call (WriteFile to emit context.yaml + coding-principles.md),
    // not the observation-only discussion path SkillRoundHandlerBase ran it through.
    public const string BootstrapRound = "BootstrapRoundCommand";
}
