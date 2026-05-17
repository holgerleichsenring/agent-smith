namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Raw YAML shape for one entry inside the top-level `trackers:` catalog.
/// The resolver converts this to a <see cref="AgentSmith.Contracts.Models.Configuration.TrackerConnection"/>.
/// </summary>
public sealed class RawTrackerEntry
{
    public string Type { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Organization { get; set; }
    public string? Project { get; set; }
    public string Auth { get; set; } = string.Empty;
    public List<string> OpenStates { get; set; } = [];
    public string? DoneStatus { get; set; }
    public string? CloseTransitionName { get; set; }
    public List<string> ExtraFields { get; set; } = [];
}
