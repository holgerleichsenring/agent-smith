namespace AgentSmith.Server.Api.Models;

/// <summary>API response shape for GET /api/jobs and GET /api/jobs/{id}.</summary>
public sealed record RunMetaResponse(
    string RunId,
    string PipelineName,
    string Status,
    DateTimeOffset? StartedAt,
    int DurationSeconds,
    string RepoMode,
    int SandboxCount,
    IReadOnlyList<string> Repos,
    string? Ticket,
    string? Type);

public sealed record JobListResponse(
    IReadOnlyList<RunMetaResponse> Jobs,
    int Total,
    int Page,
    int PageSize);

public sealed record JobDetailResponse(
    RunMetaResponse Meta,
    IReadOnlyList<RunArtefactResponse> Artefacts);
