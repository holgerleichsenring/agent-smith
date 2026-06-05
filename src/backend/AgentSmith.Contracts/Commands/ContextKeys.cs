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
    // p0230: resolved default run_command timeout (seconds) for this run —
    // per-project override ?? global sandbox.run_command_timeout_seconds. Read by
    // the agentic handlers when building the filesystem tool host.
    public const string RunCommandTimeoutSeconds = "RunCommandTimeoutSeconds";
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

    /// <summary>p0158e + p0161a: dictionary keyed by composite sandbox key holding
    /// one ISandbox per discovered context (each with its own toolchain image).
    /// Published by PipelineSandboxCoordinator on first sandbox-requiring command.
    /// Key shape (p0161a): "default" for single-repo single-default-context, the
    /// bare context name for single-repo monorepos, the bare repo name for
    /// multi-repo single-context, "&lt;repo&gt;/&lt;ctx&gt;" for multi-repo
    /// monorepos. Legacy ContextKeys.Sandbox (singular) is populated from the
    /// primary repo's first sandbox for back-compat callers.</summary>
    public const string Sandboxes = "Sandboxes";

    /// <summary>p0161a: dictionary keyed by the same sandbox key as
    /// ContextKeys.Sandboxes, holding the RemoteContextDiscovery (ContextName,
    /// Workdir, Language) that produced each sandbox. Handlers iterate this
    /// dict to look up per-sandbox Workdir (operative working directory for
    /// per-context commands like ci.test_command) and Language (toolchain
    /// annotation for PromptPrefix). p0180: when multiple same-toolchain
    /// contexts share one sandbox, this dict carries the FIRST discovery
    /// in the group as the representative; the full per-sandbox context
    /// list lives at ContextKeys.SandboxContexts.</summary>
    public const string SandboxDiscoveries = "SandboxDiscoveries";

    /// <summary>p0180: dictionary keyed by sandbox key, holding the FULL list
    /// of RemoteContextDiscovery entries inside each sandbox. With the
    /// per-toolchain dedup, one sandbox can contain multiple contexts that
    /// share its image (e.g. 5 csharp sub-projects in one repo). Handlers
    /// that need to walk every context inside the sandbox (BootstrapCheck
    /// probes context.yaml + coding-principles.md per context) read this
    /// key. Handlers that only need a representative discovery per sandbox
    /// can stay on ContextKeys.SandboxDiscoveries.</summary>
    public const string SandboxContexts = "SandboxContexts";

    /// <summary>p0158f: dictionary keyed by repo name with each repo's analyzed
    /// ProjectMap. Populated by AnalyzeProjectHandler iterating per-repo sandboxes.
    /// Legacy ContextKeys.ProjectMap stays as the primary repo's map for back-compat.</summary>
    public const string RepoProjectMaps = "RepoProjectMaps";

    /// <summary>p0158f: dictionary keyed by repo name with each repo's loaded
    /// `.agentsmith/context.yaml` content. Legacy ContextKeys.ProjectContext stays
    /// as the primary repo's YAML for back-compat.</summary>
    public const string RepoContextYamls = "RepoContextYamls";

    /// <summary>p0158f: dictionary keyed by repo name with each repo's loaded
    /// `.agentsmith/coding-principles.md` content. Legacy ContextKeys.CodingPrinciples
    /// stays as a single aggregated string (per-repo headers concatenated) for
    /// AgenticExecute back-compat.</summary>
    public const string RepoCodingPrinciples = "RepoCodingPrinciples";

    /// <summary>p0158f: comma-separated list of repo names whose bootstrap files are
    /// missing (context.yaml or coding-principles.md). Populated by BootstrapCheckHandler;
    /// read by BootstrapGateHandler to render a clear error message.</summary>
    public const string MissingBootstrapRepos = "MissingBootstrapRepos";

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

    /// <summary>The list of OpenedPullRequest entries produced by CommitAndPRHandler
    /// or InitCommitHandler during multi-repo runs. Holds one entry per configured
    /// repo with status Opened/SkippedNoChanges/Failed. Read by p0158c's PATCH
    /// pass to render the sibling URL list back into each opened PR's body.</summary>
    public const string OpenedPullRequests = "OpenedPullRequests";

    /// <summary>Dictionary&lt;repoName, body-string-used-at-open-time&gt; published by
    /// CommitAndPRHandler / InitCommitHandler alongside OpenedPullRequests. p0158c's
    /// PrCrossLinkHandler reads this to do in-memory marker-replace per PR without
    /// re-fetching bodies from the platform.</summary>
    public const string OpenedPullRequestBodies = "OpenedPullRequestBodies";

    public const string Decisions = "Decisions";
    public const string Attachments = "Attachments";
    public const string SourceFilePath = "SourceFilePath";
    public const string DocumentMarkdown = "DocumentMarkdown";
    public const string ContractType = "ContractType";
}
