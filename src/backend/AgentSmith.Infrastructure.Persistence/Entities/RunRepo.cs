namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>Per-repo outcome of a run: the PR opened (or why not) and the change count.</summary>
public sealed class RunRepo : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
    public string? PrUrl { get; set; }
    public string? PrStatus { get; set; }
    public string? Reason { get; set; }
    public int ChangeCount { get; set; }
}
