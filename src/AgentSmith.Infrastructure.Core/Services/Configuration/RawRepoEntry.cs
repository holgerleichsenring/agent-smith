using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Raw YAML shape for one entry inside the top-level `repos:` catalog.
/// The resolver converts this to a <see cref="AgentSmith.Contracts.Models.Configuration.RepoConnection"/>.
/// </summary>
public sealed class RawRepoEntry
{
    public RepoType Type { get; set; }
    public string? Url { get; set; }
    public string? Path { get; set; }
    public string? Organization { get; set; }
    public string? Project { get; set; }
    public string Auth { get; set; } = string.Empty;
    public string? DefaultBranch { get; set; }
}
