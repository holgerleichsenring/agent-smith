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

    public static string GetLabel(string commandName) =>
        Labels.GetValueOrDefault(commandName, commandName);

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
        [Test] = "Running tests",
        [WriteRunResult] = "Writing run result",
        [CommitAndPR] = "Creating pull request",
        [InitCommit] = "Committing init files",
        [GenerateTests] = "Generating tests",
        [GenerateDocs] = "Generating docs",
    };
}
