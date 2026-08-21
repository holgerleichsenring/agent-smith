namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0203: the label table itself — one operator-facing noun phrase per pipeline
/// command. Split from the resolution logic (p0428) the same way CommandModelUse
/// keeps its map beside it: adding a step is a row here, and the file that decides
/// how a name resolves does not grow with the catalogue.
/// </summary>
public static partial class CommandDisplayNames
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        [CommandNames.FetchTicket] = "Fetch ticket",
        [CommandNames.ScopeRepos] = "Scope repositories", // p0331
        [CommandNames.CheckoutSource] = "Check out source",
        [CommandNames.TryCheckoutSource] = "Resolve source",
        [CommandNames.RunPreflight] = "Check preconditions", // p0428
        [CommandNames.SetupRegistryAuth] = "Set up private-feed credentials",
        [CommandNames.EnsurePrerequisites] = "Prepare environment",
        [CommandNames.BootstrapProject] = "Bootstrap project context",
        [CommandNames.LoadCodeMap] = "Load code map",
        [CommandNames.LoadCodingPrinciples] = "Load coding principles",
        [CommandNames.LoadMemoryIndex] = "Load memory index", // p0380
        [CommandNames.LoadContext] = "Load project context",
        [CommandNames.LoadSkills] = "Load skills",
        [CommandNames.AnalyzeCode] = "Analyze codebase",
        [CommandNames.NegotiateExpectation] = "Negotiate expectation", // p0328
        [CommandNames.DeriveSpec] = "Derive the phase specs",
        [CommandNames.SpecHandback] = "Hand the ticket back",
        [CommandNames.PhaseSequence] = "Plan the phase sequence",
        [CommandNames.SelectPhase] = "Start the phase",
        [CommandNames.EmptyPlanCheck] = "Check plan is non-empty",
        // p0394a: retired steps, kept by literal name so run records persisted
        // before the retirement still render their trail labels.
        ["GeneratePlanCommand"] = "Generate plan",
        ["PlanOpenQuestionsCommand"] = "Post Plan open questions",
        [CommandNames.Approval] = "Await approval",
        [CommandNames.AgenticExecute] = "Execute plan",
        [CommandNames.AgenticMaster] = "Run master skill",
        [CommandNames.LoadCachedCodeMap] = "Load cached code map",
        [CommandNames.CollectSpecDialogReply] = "Collect design reply",
        [CommandNames.PhaseSpecGate] = "Validate phase spec",
        [CommandNames.MasterOpenQuestions] = "Post master open questions",
        [CommandNames.WritePhaseRecord] = "Write phase record",
        [CommandNames.WriteRunResult] = "Write run result",
        [CommandNames.CommitAndPR] = "Create pull request",
        [CommandNames.InitCommit] = "Commit init files",
        [CommandNames.PrCrossLink] = "Cross-link sibling pull requests",
        [CommandNames.InitComplete] = "Complete the init pull requests",
        [CommandNames.GenerateTests] = "Generate tests",
        [CommandNames.GenerateDocs] = "Generate docs",
        [CommandNames.Triage] = "Triage ticket",
        [CommandNames.SwitchSkill] = "Switch skill",
        [CommandNames.SkillRound] = "Skill round",
        [CommandNames.ConvergenceCheck] = "Check convergence",
        [CommandNames.CompileDiscussion] = "Compile discussion",
        [CommandNames.AcquireSource] = "Acquire source document",
        [CommandNames.BootstrapDocument] = "Bootstrap document",
        [CommandNames.DeliverOutput] = "Deliver output",
        [CommandNames.SessionSetup] = "Authenticate API personas",
        [CommandNames.Ask] = "Ask human",
        [CommandNames.CompileKnowledge] = "Compile knowledge base",
        [CommandNames.QueryKnowledge] = "Query knowledge base",
        [CommandNames.LoadRuns] = "Load run history",
        [CommandNames.WriteTickets] = "Write tickets",
        [CommandNames.FilterRound] = "Filter round",
        [CommandNames.RunReviewPhase] = "Run review phase",
        [CommandNames.RunFinalPhase] = "Run final phase",
        [CommandNames.PersistWorkBranch] = "Persist work branch",
        [CommandNames.LoadCatalog] = "Load catalog",
        [CommandNames.PipelineNameInitializer] = "Publish pipeline name",
        [CommandNames.BootstrapCheck] = "Check bootstrap files",
        [CommandNames.RunVerifyPhase] = "Run verify phase",
        [CommandNames.CommitPhaseWork] = "Commit the phase's work",
        [CommandNames.VerifyPhase] = "Build and test", // p0393
        [CommandNames.BootstrapGate] = "Verify bootstrap files",
        [CommandNames.PublishProjectLanguage] = "Publish project language",
        [CommandNames.BootstrapDispatch] = "Dispatch bootstrap skill",
        [CommandNames.BootstrapDiscover] = "Discover project components",
        [CommandNames.BootstrapRound] = "Produce bootstrap files",
        [CommandNames.LoadSwagger] = "Load Swagger spec",
        [CommandNames.SpawnNuclei] = "Run Nuclei scan",
        [CommandNames.SpawnSpectral] = "Run Spectral lint",
        [CommandNames.SpawnZap] = "Run ZAP scan",
        [CommandNames.ApiSecuritySkillRound] = "API security skill round",
        [CommandNames.CompileFindings] = "Compile findings",
        [CommandNames.CollectMasterFindings] = "Collect master findings",
        [CommandNames.DeliverFindings] = "Deliver findings",
        [CommandNames.CompressApiScanFindings] = "Compress API scan findings",
        [CommandNames.SecuritySkillRound] = "Security skill round",
        [CommandNames.StaticPatternScan] = "Scan for security patterns",
        [CommandNames.GitHistoryScan] = "Scan git history for secrets",
        [CommandNames.DependencyAudit] = "Audit dependencies",
        [CommandNames.CompressSecurityFindings] = "Compress security findings",
        [CommandNames.MergeMasterFindings] = "Merge master findings",
        [CommandNames.SecurityTrend] = "Analyze security trends",
        [CommandNames.SecuritySnapshotWrite] = "Write security snapshot",
        [CommandNames.SpawnFix] = "Generate security fix requests",
        [CommandNames.AnalyzePrDiff] = "Analyze PR diff",
        [CommandNames.PrReviewSkillRound] = "PR review skill round",
        [CommandNames.CompilePrReviewFindings] = "Compile PR review findings",
        [CommandNames.PostPrComments] = "Post PR review comments",
        [CommandNames.RatifyScanContract] = "State what the scan looks for",
        [CommandNames.SubstantiateFindings] = "Substantiate findings",
        [CommandNames.AccountScanCoverage] = "Account for scan coverage",
    };
}
