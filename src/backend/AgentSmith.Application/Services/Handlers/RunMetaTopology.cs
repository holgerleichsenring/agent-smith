namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0169a: topology fields harvested from PipelineContext at run-end so the
/// dashboard's Job-Viewer can render repos / sandbox-count / pipeline-name
/// badges without re-deriving them from execution-trail breadcrumbs.
///
/// All fields optional; absent values render as "unknown" placeholders in
/// the dashboard for pre-p0169a runs.
/// </summary>
public sealed record RunMetaTopology(
    string? RunId = null,
    string? PipelineName = null,
    string? Status = null,
    DateTimeOffset? StartedAt = null,
    string? RepoMode = null,
    int SandboxCount = 0,
    IReadOnlyList<string>? Repos = null);
