namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Well-known keys for the PipelineContext dictionary. Split into per-subdomain
/// partial files (Pipeline, Security, Api, Bootstrap) for readability; every
/// caller references ContextKeys.X regardless of which partial defines the constant.
/// This file holds the top-level "core" keys touched by every pipeline run.
/// </summary>
public static partial class ContextKeys
{
    public const string AgentConfig = "AgentConfig";
    public const string TicketId = "TicketId";
    public const string Ticket = "Ticket";
    public const string Repository = "Repository";

    /// <summary>p0140d: the RepoConnection for THIS run. Resolved at the top of ExecutePipelineUseCase
    /// from PipelineRequest.RepoName + project.Repos. Single source of truth for "which repo is
    /// this run for" — every consumer that previously read project.Repo now reads CurrentRepo.</summary>
    public const string CurrentRepo = "CurrentRepo";

    public const string ProjectMap = "ProjectMap";
    public const string DomainRules = "DomainRules";
    public const string CodingPrinciples = DomainRules;
    public const string CodeMap = "CodeMap";
    public const string ProjectContext = "ProjectContext";
    public const string Headless = "Headless";
    public const string ConfigDir = "ConfigDir";

    public const string SourceType = "SourceType";
    public const string SourcePath = "SourcePath";
    public const string SourceUrl = "SourceUrl";
    public const string SourceAuth = "SourceAuth";

    public const string SwaggerSpecFull = "SwaggerSpecFull";
    public const string CheckoutBranch = "CheckoutBranch";
    public const string ResolvedPipeline = "ResolvedPipeline";

    public const string Decisions = "Decisions";
    public const string Attachments = "Attachments";
    public const string SourceFilePath = "SourceFilePath";
    public const string DocumentMarkdown = "DocumentMarkdown";
    public const string ContractType = "ContractType";
}
