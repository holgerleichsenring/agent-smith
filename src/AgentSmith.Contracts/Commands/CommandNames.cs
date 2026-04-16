namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Single source of truth for all pipeline command names and their display labels.
/// </summary>
public static class CommandNames
{
    public const string FetchTicket = "FetchTicketCommand";
    public const string CheckoutSource = "CheckoutSourceCommand";
    public const string BootstrapProject = "BootstrapProjectCommand";
    public const string LoadCodeMap = "LoadCodeMapCommand";
    public const string LoadDomainRules = "LoadDomainRulesCommand";
    public const string LoadCodingPrinciples = "LoadCodingPrinciplesCommand";
    public const string LoadContext = "LoadContextCommand";
    public const string AnalyzeCode = "AnalyzeCodeCommand";
    public const string GeneratePlan = "GeneratePlanCommand";
    public const string Approval = "ApprovalCommand";
    public const string AgenticExecute = "AgenticExecuteCommand";
    public const string Test = "TestCommand";
    public const string WriteRunResult = "WriteRunResultCommand";
    public const string CommitAndPR = "CommitAndPRCommand";
    public const string InitCommit = "InitCommitCommand";
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
    public const string SecurityTriage = "SecurityTriageCommand";
    public const string SecuritySkillRound = "SecuritySkillRoundCommand";
    public const string LoadSwagger = "LoadSwaggerCommand";
    public const string SpawnNuclei = "SpawnNucleiCommand";
    public const string SpawnSpectral = "SpawnSpectralCommand";
    public const string SpawnZap = "SpawnZapCommand";
    public const string ApiSecurityTriage = "ApiSecurityTriageCommand";
    public const string ApiSecuritySkillRound = "ApiSecuritySkillRoundCommand";
    public const string CompileFindings = "CompileFindingsCommand";
    public const string LoadSkills = "LoadSkillsCommand";
    public const string DeliverFindings = "DeliverFindingsCommand";
    public const string StaticPatternScan = "StaticPatternScanCommand";
    public const string GitHistoryScan = "GitHistoryScanCommand";
    public const string DependencyAudit = "DependencyAuditCommand";
    public const string CompressSecurityFindings = "CompressSecurityFindingsCommand";
    public const string CompressApiScanFindings = "CompressApiScanFindingsCommand";
    public const string ExtractFindings = "ExtractFindingsCommand";
    public const string SecurityTrend = "SecurityTrendCommand";
    public const string SecuritySnapshotWrite = "SecuritySnapshotWriteCommand";
    public const string Ask = "AskCommand";
    public const string SpawnFix = "SpawnFixCommand";
    public const string DiscoverSkills = "DiscoverSkillsCommand";
    public const string EvaluateSkills = "EvaluateSkillsCommand";
    public const string DraftSkillFiles = "DraftSkillFilesCommand";
    public const string ApproveSkills = "ApproveSkillsCommand";
    public const string InstallSkills = "InstallSkillsCommand";
    public const string CompileKnowledge = "CompileKnowledgeCommand";
    public const string QueryKnowledge = "QueryKnowledgeCommand";
    public const string LoadVision = "LoadVisionCommand";
    public const string LoadRuns = "LoadRunsCommand";
    public const string WriteTickets = "WriteTicketsCommand";
    public const string SessionSetup = "SessionSetupCommand";

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
        [LoadDomainRules] = "Loading domain rules",
        [LoadCodingPrinciples] = "Loading domain rules",
        [LoadContext] = "Loading project context",
        [AnalyzeCode] = "Analyzing codebase",
        [GeneratePlan] = "Generating plan",
        [Approval] = "Awaiting approval",
        [AgenticExecute] = "Executing plan",
        [Test] = "Running tests",
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
        [SecurityTriage] = "Triaging security scan",
        [SecuritySkillRound] = "Security skill round",
        [LoadSwagger] = "Loading swagger spec",
        [SpawnNuclei] = "Running Nuclei scan",
        [SpawnSpectral] = "Running Spectral lint",
        [SpawnZap] = "Running ZAP scan",
        [ApiSecurityTriage] = "Triaging API security scan",
        [ApiSecuritySkillRound] = "API security skill round",
        [CompileFindings] = "Compiling findings",
        [LoadSkills] = "Loading skills",
        [DeliverFindings] = "Delivering findings",
        [StaticPatternScan] = "Scanning for security patterns",
        [GitHistoryScan] = "Scanning git history for secrets",
        [DependencyAudit] = "Auditing dependencies",
        [CompressSecurityFindings] = "Compressing security findings",
        [CompressApiScanFindings] = "Compressing API scan findings",
        [ExtractFindings] = "Extracting findings for output",
        [SecurityTrend] = "Analyzing security trends",
        [SecuritySnapshotWrite] = "Writing security snapshot",
        [Ask] = "Asking human",
        [SpawnFix] = "Generating security fix requests",
        [DiscoverSkills] = "Discovering skill candidates",
        [EvaluateSkills] = "Evaluating skill candidates",
        [DraftSkillFiles] = "Drafting skill files",
        [ApproveSkills] = "Awaiting skill approval",
        [InstallSkills] = "Installing approved skills",
        [CompileKnowledge] = "Compiling knowledge base",
        [QueryKnowledge] = "Querying knowledge base",
        [LoadVision] = "Loading project vision",
        [LoadRuns] = "Loading run history",
        [WriteTickets] = "Writing tickets",
        [SessionSetup] = "Authenticating API personas",
    };
}
