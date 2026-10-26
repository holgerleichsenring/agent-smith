namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>Per-repo outcome of a run: the PR opened (or why not).</summary>
public sealed class RunRepo : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public string RepoName { get; set; } = string.Empty;
    public string? PrUrl { get; set; }
    public string? PrStatus { get; set; }
    public string? Reason { get; set; }
    /// <summary>2026-10-26-cb25f: never written and no longer read; the column leaves both schemas in
    /// 2026-10-26-0a9e, over two releases so a rolling deploy's old pods keep inserting.</summary>
    public int ChangeCount { get; set; }
}
