using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Models;

/// <summary>
/// Explicit request to execute a pipeline. No regex parsing, no guessing.
/// Built by CLI commands, Slack intent parser, or TicketClaimService.
/// </summary>
public sealed record PipelineRequest(
    string ProjectName,
    string PipelineName,
    TicketId? TicketId = null,
    bool IsInit = false,
    bool Headless = false,
    Dictionary<string, object>? Context = null,
    Dictionary<string, string>? PlanAnswers = null,
    // p0281d: a CLI scan (api-scan / security-scan) sets this from `--agent` to run
    // WITHOUT a project entry — an ephemeral project is built from this agent + the
    // --source-path. When set it takes precedence over ProjectName. Null = the legacy
    // path (resolve ProjectName against the projects: catalog).
    string? AgentName = null);
