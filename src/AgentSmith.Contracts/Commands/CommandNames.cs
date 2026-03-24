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
    public const string ApiSecurityTriage = "ApiSecurityTriageCommand";
    public const string ApiSecuritySkillRound = "ApiSecuritySkillRoundCommand";
    public const string CompileFindings = "CompileFindingsCommand";
    public const string LoadSkills = "LoadSkillsCommand";

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
        [ApiSecurityTriage] = "Triaging API security scan",
        [ApiSecuritySkillRound] = "API security skill round",
        [CompileFindings] = "Compiling findings",
        [LoadSkills] = "Loading skills",
    };
}
