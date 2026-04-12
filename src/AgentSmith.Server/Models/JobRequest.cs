namespace AgentSmith.Server.Models;

/// <summary>
/// Generic job request that abstracts over the intent type.
/// Both FixTicket and InitProject create a JobRequest for the spawner.
/// </summary>
public sealed record JobRequest
{
    /// <summary>CLI input for the Host, e.g. "fix #123 in my-project" or "init my-project".</summary>
    public required string InputCommand { get; init; }

    /// <summary>Project identifier matching agentsmith.yml config.</summary>
    public required string Project { get; init; }

    /// <summary>Slack/Teams channel ID for progress reporting.</summary>
    public required string ChannelId { get; init; }

    /// <summary>User who triggered the command.</summary>
    public required string UserId { get; init; }

    /// <summary>Platform: "slack", "teams", etc.</summary>
    public required string Platform { get; init; }

    /// <summary>Optional pipeline override, e.g. "init-project".</summary>
    public string? PipelineOverride { get; init; }
}
