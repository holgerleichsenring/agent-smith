namespace AgentSmith.Server.Models;

/// <summary>
/// p0169a: in-memory representation of the result.md YAML frontmatter for
/// a completed run. Source of the dashboard's Job-Viewer payload.
///
/// All fields are nullable / sentinel-aware because pre-p0169a runs only
/// carry a subset (tokens + cost + ticket). New runs carry everything.
/// </summary>
public sealed record RunMetaFrontmatter(
    string RunId,
    string? PipelineName,
    string? Status,
    DateTimeOffset? StartedAt,
    int DurationSeconds,
    string? RepoMode,
    int SandboxCount,
    IReadOnlyList<string> Repos,
    string? Ticket,
    string? Type);
