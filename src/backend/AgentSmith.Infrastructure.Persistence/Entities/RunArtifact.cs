namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// A run-produced document (result_md | plan_md | analyze_md | plan_json |
/// diff_json | bootstrap). Non-final artifacts + Content blobs are pruned by the
/// retention policy (p0246c); the final result_md/plan_md are kept.
/// </summary>
public sealed class RunArtifact : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? Content { get; set; }
}
