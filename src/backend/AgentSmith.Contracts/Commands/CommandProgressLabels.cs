namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Display labels for each pipeline command. Used by Slack/Teams/CLI progress
/// rendering to show "Generating plan" instead of "GeneratePlanCommand". Lookup
/// goes through <see cref="CommandNames.GetLabel"/>; parameterized command names
/// (e.g. "SkillRoundCommand:architect:1") fall back to the base-command label.
/// </summary>
public static partial class CommandNames
{
    public static string GetLabel(string commandName)
    {
        if (Labels.TryGetValue(commandName, out var label))
            return label;

        // Handle parameterized commands (e.g. "SkillRoundCommand:architect:1")
        var baseCommand = commandName.Contains(':')
            ? commandName[..commandName.IndexOf(':')]
            : commandName;

        return Labels.GetValueOrDefault(baseCommand, commandName);
    }

    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        [FetchTicket] = "Fetching ticket",
        [CheckoutSource] = "Checking out source",
        [BootstrapProject] = "Bootstrapping project context",
        [LoadCodeMap] = "Loading code map",
        [LoadCodingPrinciples] = "Loading coding principles",
        [LoadContext] = "Loading project context",
        [AnalyzeCode] = "Analyzing codebase",
        [GeneratePlan] = "Generating plan",
        [Approval] = "Awaiting approval",
        [AgenticExecute] = "Executing plan",
        [AgenticMaster] = "Running master skill",
        [WriteRunResult] = "Writing run result",
        [CommitAndPR] = "Creating pull request",
        [InitCommit] = "Committing init files",
        [GenerateTests] = "Generating tests",
        [GenerateDocs] = "Generating docs",
        [Triage] = "Triaging ticket",
        [SwitchSkill] = "Switching skill",
        [SkillRound] = "Skill round",
        [ConvergenceCheck] = "Checking convergence",
        [CompileDiscussion] = "Compiling discussion",
        [AcquireSource] = "Acquiring source document",
        [BootstrapDocument] = "Bootstrapping document",
        [DeliverOutput] = "Delivering output",
        [SecuritySkillRound] = "Security skill round",
        [LoadSwagger] = "Loading swagger spec",
        [SpawnNuclei] = "Running Nuclei scan",
        [SpawnSpectral] = "Running Spectral lint",
        [SpawnZap] = "Running ZAP scan",
        [ApiSecuritySkillRound] = "API security skill round",
        [CompileFindings] = "Compiling findings",
        [LoadSkills] = "Loading skills",
        [DeliverFindings] = "Delivering findings",
        [StaticPatternScan] = "Scanning for security patterns",
        [GitHistoryScan] = "Scanning git history for secrets",
        [DependencyAudit] = "Auditing dependencies",
        [CompressSecurityFindings] = "Compressing security findings",
        [CompressApiScanFindings] = "Compressing API scan findings",
        [SecurityTrend] = "Analyzing security trends",
        [SecuritySnapshotWrite] = "Writing security snapshot",
        [Ask] = "Asking human",
        [SpawnFix] = "Generating security fix requests",
        [CompileKnowledge] = "Compiling knowledge base",
        [QueryKnowledge] = "Querying knowledge base",
        [LoadRuns] = "Loading run history",
        [WriteTickets] = "Writing tickets",
        [SessionSetup] = "Authenticating API personas",
        [TryCheckoutSource] = "Resolving source",
        [FilterRound] = "Filter round",
        [RunReviewPhase] = "Running review phase",
        [RunFinalPhase] = "Running final phase",
        [PersistWorkBranch] = "Persisting work branch",
        [LoadCatalog] = "Loading skill catalog",
        [PipelineNameInitializer] = "Publishing pipeline name",
        [BootstrapCheck] = "Checking bootstrap files",
        [PlanOpenQuestions] = "Posting Plan open questions",
        [RunVerifyPhase] = "Running verify phase",
        [BootstrapGate] = "Verifying bootstrap files",
        [PublishProjectLanguage] = "Publishing project language",
        [BootstrapDispatch] = "Dispatching bootstrap skill",
        [BootstrapRound] = "Producing bootstrap files",
    };
}
