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
    Dictionary<string, object>? Context = null);
