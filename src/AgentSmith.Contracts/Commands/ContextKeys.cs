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

    /// <summary>The list of RepoConnections this run operates on
    /// (IReadOnlyList&lt;RepoConnection&gt;). Published by ExecutePipelineUseCase from the project's
    /// configured Repos, optionally filtered to a single entry when SourceOverrideRepo is set.
    /// Single-repo projects expose a one-element list; multi-repo projects expose all repos.</summary>
    public const string Repos = "Repos";

    /// <summary>Optional repo name (string) that scopes the run to a single configured repo.
    /// Set by the CLI when `--repo NAME` is provided; absent in queue-driven (K8s/Compose) runs.</summary>
    public const string SourceOverrideRepo = "SourceOverrideRepo";

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
