namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Well-known keys for the PipelineContext dictionary.
/// </summary>
public static class ContextKeys
{
    public const string TicketId = "TicketId";
    public const string Ticket = "Ticket";
    public const string Repository = "Repository";
    public const string Plan = "Plan";
    public const string CodeChanges = "CodeChanges";
    public const string CodeAnalysis = "CodeAnalysis";
    public const string DomainRules = "DomainRules";
    public const string CodingPrinciples = DomainRules;
    public const string ActiveSkill = "ActiveSkill";
    public const string AvailableRoles = "AvailableRoles";
    public const string ProjectSkills = "ProjectSkills";
    public const string ExecutionTrail = "ExecutionTrail";
    public const string DiscussionLog = "DiscussionLog";
    public const string ConsolidatedPlan = "ConsolidatedPlan";
    public const string Approved = "Approved";
    public const string TestResults = "TestResults";
    public const string PullRequestUrl = "PullRequestUrl";
    public const string Headless = "Headless";
    public const string DetectedProject = "DetectedProject";
    public const string RepoSnapshot = "RepoSnapshot";
    public const string CodeMap = "CodeMap";
    public const string ProjectContext = "ProjectContext";
    public const string RunNumber = "RunNumber";
    public const string RunCostSummary = "RunCostSummary";
    public const string RunDurationSeconds = "RunDurationSeconds";
    public const string InitMode = "InitMode";
    public const string SourceFilePath = "SourceFilePath";
    public const string DocumentMarkdown = "DocumentMarkdown";
    public const string ContractType = "ContractType";
    public const string Attachments = "Attachments";
    public const string Decisions = "Decisions";
    public const string ScanRepoPath = "ScanRepoPath";
    public const string ScanPrIdentifier = "ScanPrIdentifier";
    public const string ScanBranch = "ScanBranch";
    public const string OutputFormat = "OutputFormat";
    public const string OutputDir = "OutputDir";
    public const string SwaggerSpec = "SwaggerSpec";
    public const string NucleiResult = "NucleiResult";
    public const string ZapResult = "ZapResult";
    public const string SpectralResult = "SpectralResult";
    public const string ApiTarget = "ApiTarget";
    public const string SwaggerPath = "SwaggerPath";
    public const string CheckoutBranch = "CheckoutBranch";
    public const string SkillsPathOverride = "SkillsPathOverride";
    public const string StaticScanResult = "StaticScanResult";
    public const string GitHistoryScanResult = "GitHistoryScanResult";
    public const string DependencyAuditResult = "DependencyAuditResult";
    public const string SecurityFindingsSummary = "SecurityFindingsSummary";
    public const string SecurityFindingsByCategory = "SecurityFindingsByCategory";
    public const string ExtractedFindings = "ExtractedFindings";
    public const string SecurityTrend = "SecurityTrend";
    public const string DialogueAnswer = "DialogueAnswer";
    public const string DialogueQuestion = "DialogueQuestion";
    public const string SecurityFixRequests = "SecurityFixRequests";
}
