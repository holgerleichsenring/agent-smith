using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0281a: raw YAML shape for one entry inside the top-level <c>connections:</c> catalog.
/// The resolver converts this to a <see cref="ResolvedConnection"/>.
/// </summary>
public sealed class RawConnectionEntry
{
    public RepoType Type { get; set; }
    public string? Organization { get; set; }
    public string? Project { get; set; }
    public string? Owner { get; set; }
    public string? Group { get; set; }
    public string? Host { get; set; }
    public string Auth { get; set; } = string.Empty;
    public string? DefaultBranch { get; set; }
}
