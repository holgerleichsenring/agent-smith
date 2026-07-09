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

    /// <summary>p0198: pre-stage private-feed credentials in each sandbox so
    /// downstream build/test steps don't hit NU1301 / EAUTH / 401. Reads
    /// the repo's nuget.config / .npmrc files, matches declared source URLs
    /// against the operator's <c>registries:</c> block, writes user-level
    /// credential files (<c>~/.nuget/NuGet/NuGet.Config</c> and
    /// <c>~/.npmrc</c>) inside each sandbox. Runs after CheckoutSource +
    /// before any handler that builds, tests, or restores packages.
    /// Operator-deterministic — no LLM, no recovery, no master fallback
    /// needed; the master's get_artifact_credentials tool (p0191) is now
    /// the fallback for hosts the operator hasn't pre-configured.</summary>
    public const string SetupRegistryAuth = "SetupRegistryAuthCommand";

    /// <summary>p0202: language-agnostic dependency-install step. Runs the
    /// per-context <c>prerequisites</c> (e.g. <c>npm ci</c>,
    /// <c>pip install -r requirements.txt</c>) in each sandbox after
    /// SetupRegistryAuth has staged private-feed credentials and before
    /// BootstrapCheck. Non-dotnet test runners (jest, pytest, mvn, cargo, go)
    /// assume their install/restore ran first; <c>dotnet test</c> restores
    /// implicitly so dotnet repos can leave the command empty. Empty/missing
    /// command → step skips cleanly. Inserted only into the three
    /// code-touching presets (fix-bug, fix-no-test, add-feature).</summary>
    public const string EnsurePrerequisites = "EnsurePrerequisitesCommand";
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

    /// <summary>p0179b: master-driven coding step. Loads a master skill body
    /// (e.g. coding-agent-master) via the p0179a IPromptCatalog adapter and
    /// runs it in one agentic loop — plan + execute + verify happen
    /// internally. Replaces the
    /// Triage→GeneratePlan→PlanOpenQuestions→EmptyPlanCheck→AgenticExecute→
    /// RunReviewPhase→RunFinalPhase→RunVerifyPhase chain on coding pipelines.
    /// AgenticExecute stays alive for non-coding presets until those migrate.</summary>
    public const string AgenticMaster = "AgenticMasterCommand";

    /// <summary>p0315b: tier-1 spec-dialog grounding. Loads the CACHED ProjectMap
    /// (IProjectMapStore, latest entry per scoped repo, staleness accepted) into
    /// ContextKeys.CodeMap WITHOUT a sandbox — structural design questions are
    /// answered from this alone; only content questions later materialise the
    /// lazy read-only source sandbox inside the master loop.</summary>
    public const string LoadCachedCodeMap = "LoadCachedCodeMapCommand";

    /// <summary>p0315b: copies the master's final reply (ContextKeys.MasterAnswer)
    /// into the server-seeded SpecDialogReplySlot so the turn runner can deliver
    /// it to the chat thread after the run.</summary>
    public const string CollectSpecDialogReply = "CollectSpecDialogReplyCommand";

    /// <summary>p0315d: extracts the fenced yaml spec out of the phase ticket
    /// (inverse of the p0315c renderer), schema-validates it and publishes it as
    /// ContextKeys.PhaseSpec plus the approved plan the master executes. Fails
    /// loud when a phase-labelled ticket carries no valid spec — before any
    /// master tokens are spent.</summary>
    public const string PhaseSpecGate = "PhaseSpecGateCommand";

    /// <summary>p0315d: posts a question the master captured mid-run (ask_human
    /// on a ticket-triggered run) as a p0318 open-questions ticket comment and
    /// parks the ticket in needs_clarification_status; sets the awaiting-answer
    /// flag so the executor short-circuits the rest of the run. No-op when the
    /// master asked nothing.</summary>
    public const string MasterOpenQuestions = "MasterOpenQuestionsCommand";

    /// <summary>p0315d: dogfoods the methodology — writes the executed phase
    /// spec to the target repo's .agentsmith/phases/done/ inside the sandbox
    /// working tree so CommitAndPR ships it with the change set.</summary>
    public const string WritePhaseRecord = "WritePhaseRecordCommand";
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

    // p0205: visible skill-catalog binding — first step of every preset
    public const string LoadCatalog = "LoadCatalogCommand";

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

    /// <summary>p0161d: read-only first pass of cold-init. One round per repo;
    /// the project-discovery skill produces the component list with evidence;
    /// BootstrapDispatch then fans out one BootstrapRound per (repo, component).
    /// Re-init (existing <c>.agentsmith/contexts/&lt;name&gt;/</c> on remote)
    /// short-circuits this round.</summary>
    public const string BootstrapDiscover = "BootstrapDiscoverCommand";

    // p0130c-followup: producer-loop runtime for bootstrap skills (csharp/node/python/
    // generic-bootstrap). Distinct from SkillRound because the producer needs a
    // tool-bearing chat call (WriteFile to emit context.yaml + coding-principles.md),
    // not the observation-only discussion path SkillRoundHandlerBase ran it through.
    public const string BootstrapRound = "BootstrapRoundCommand";
}
