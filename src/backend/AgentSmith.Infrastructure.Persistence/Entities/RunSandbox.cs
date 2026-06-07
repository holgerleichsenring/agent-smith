namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>A sandbox spawned for the run: its key, repo, toolchain image and status.</summary>
public sealed class RunSandbox : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? RepoName { get; set; }
    public string? ToolchainImage { get; set; }
    public string? Status { get; set; }
}
