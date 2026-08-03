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

    /// <summary>p0331: understand the ticket BEFORE provisioning. Runs after FetchTicket
    /// and before CheckoutSource (the first sandbox-requiring step) in the ticket-driven
    /// code-changing presets. Builds the pre-checkout remote context inventory (one
    /// ResolveAllAsync per repo, cached at ContextKeys.RemoteContextInventory), then one
    /// cheap LLM call classifies ticket → affected repos and narrows ContextKeys.Repos to
    /// that subset — checkout, sandbox coordinator, CommitAndPR and PrCrossLink all re-read
    /// that key, so the whole run provisions only what the ticket needs. Conservative by
    /// construction: low confidence / parse failure / LLM error / unknown repo name keep
    /// all repos (today's behavior); the decision + rationale is recorded on the run.</summary>
    public const string ScopeRepos = "ScopeReposCommand";
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

    /// <summary>p0380: loads .agentsmith/memory/MEMORY.md (the experiential-memory
    /// index) into the pipeline context at plan time. Absent store = empty
    /// section, never an error.</summary>
    public const string LoadMemoryIndex = "LoadMemoryIndexCommand";
    public const string LoadContext = "LoadContextCommand";
    public const string LoadSkills = "LoadSkillsCommand";
    public const string AnalyzeCode = "AnalyzeCodeCommand";

    /// <summary>p0328: negotiate the WHAT before any code changes. Runs after
    /// AnalyzeCode (the draft must be grounded in analysis, not the raw ticket)
    /// and before EnsurePrerequisites/GeneratePlan. Drafts a schema-capped Soll
    /// block (observed / ≤5 verifiable assertions / ≤3 constraints / ≤1 A-or-B
    /// question), posts it to the ticket + dialogue transports and waits for
    /// ratification through the p0327 durable ask — approve verbatim, edited
    /// text (parsed back into the schema), or reject. The ratified expectation
    /// becomes the run's acceptance contract; headless runs auto-ratify with a
    /// visible 'unratified' stamp.</summary>
    public const string NegotiateExpectation = "NegotiateExpectationCommand";

    /// <summary>p0393a: turns any ticket into an ORDERED SET of phase specs on the ticket
    /// branch — yaml for what and done, markdown for the verbatim code templates — so the
    /// `code` pipeline runs on ordinary tickets and not only on ones an operator hand-wrote.
    /// Runs after AnalyzeCode because one of its two hand-backs ("the requirement
    /// contradicts the repository") is only findable once the code has been read. Source
    /// precedence is fixed: branch artifact, then a spec embedded in the ticket
    /// DESCRIPTION, then derivation — a ticket COMMENT is never a source. Every ticket
    /// segment is carried by a named phase or discarded with a reason; an accounting that
    /// cannot be produced does not split at all.</summary>
    public const string DeriveSpec = "DeriveSpecCommand";

    /// <summary>p0393a: routes the derivation's two hand-back cases. A requirement that
    /// contradicts the repository parks in needs_clarification_status and re-triggers on an
    /// answer; not-implementable is a VERDICT — it parks in its own
    /// not_implementable_status, does NOT auto-retry on a comment and restarts only on an
    /// explicit operator Retry. Two hand-backs with the same case code and no source commit
    /// between them end the loop. No-op when the derivation handed nothing back.</summary>
    public const string SpecHandback = "SpecHandbackCommand";

    /// <summary>p0393a: splices one plan → master → verify → record block per derived
    /// phase, in order. The sequence is where termination comes from: each phase ends at
    /// its own done-list and its own VerifyPhase, so stopping is structural instead of a
    /// judgement the model has to reach.</summary>
    public const string PhaseSequence = "PhaseSequenceCommand";

    /// <summary>p0393a: makes one phase of the derived sequence the current one. Everything
    /// downstream reads ContextKeys.PhaseSpec, so this is the only wiring the sequence
    /// needs and no other handler learns that sequences exist.</summary>
    public const string SelectPhase = "SelectPhaseCommand";

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
    /// ContextKeys.RepoCodeMaps WITHOUT a sandbox — structural design questions are
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

    /// <summary>p0393: runs the repository's own build and test commands after the master
    /// and fails the run before any PR when either is red. p0216 gave build+test to the
    /// coding master as a RESPONSIBILITY and left nothing that refuses a PR on a red build,
    /// so "green" was a model claim with no second opinion. The commands come from
    /// ProjectMap.Ci (populated by AnalyzeCode) — this step needs no discovery of its own.</summary>
    public const string VerifyPhase = "VerifyPhaseCommand";
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

    /// <summary>p0167a: fetches the PR diff via IPrDiffProvider and parses the
    /// unified diff into per-file hunks with line numbers. Publishes the
    /// structured result as ContextKeys.PrDiff (plus authoritative PrHead /
    /// PrBase shas) for the pr-review skill rounds to consume.</summary>
    public const string AnalyzePrDiff = "AnalyzePrDiffCommand";

    /// <summary>p0167b: skill round for the pr-review preset. Same
    /// discussion-round machinery as SkillRound, but the prompt strategy
    /// injects the PR under review (ContextKeys.PrDiff hunks with new-file
    /// line numbers) as the domain section so review skills anchor their
    /// observations with file + line_range.</summary>
    public const string PrReviewSkillRound = "PrReviewSkillRoundCommand";

    /// <summary>p0167a (preset slot) / p0167c (handler): groups pr-review skill
    /// observations by file + line range and sorts by severity into a
    /// PrReviewSummary. The step is part of the pr-review preset from p0167a;
    /// the handler ships in p0167c.</summary>
    public const string CompilePrReviewFindings = "CompilePrReviewFindingsCommand";

    /// <summary>p0167a (preset slot) / p0167c (handler): posts the compiled
    /// pr-review findings as PR comments via IPrCommentProvider, replacing
    /// marker-tagged comments from a previous review run (idempotent overwrite
    /// on PR-synchronize).</summary>
    public const string PostPrComments = "PostPrCommentsCommand";
}
