using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Merges CLI-provided source overrides into the resolved project's repo.
/// Returns a new ResolvedProject when any override applies; the original
/// instance otherwise. p0139 single-repo assumption: overrides target the
/// project's sole repo (project.Repo). Multi-repo override is out of scope
/// until p0140 establishes per-repo addressing.
/// </summary>
public sealed class SourceConfigOverrider(ILogger<SourceConfigOverrider> logger) : ISourceConfigOverrider
{
    public ResolvedProject Apply(ResolvedProject project, PipelineContext pipeline)
    {
        var repo = project.Repo;
        var updated = ApplyTypeOverride(repo, pipeline);
        updated = ApplyPathOverride(updated, pipeline);
        updated = ApplyUrlOverride(updated, pipeline);
        updated = ApplyAuthOverride(updated, pipeline);

        return ReferenceEquals(repo, updated)
            ? project
            : project with { Repos = new[] { updated } };
    }

    private RepoConnection ApplyTypeOverride(RepoConnection repo, PipelineContext pipeline)
    {
        if (!pipeline.TryGet<string>(ContextKeys.SourceType, out var type) || type is null) return repo;
        if (!Enum.TryParse<RepoType>(type, ignoreCase: true, out var parsed)) return repo;
        logger.LogDebug("Overriding source type: {Type}", parsed);
        return repo with { Type = parsed };
    }

    private RepoConnection ApplyPathOverride(RepoConnection repo, PipelineContext pipeline)
    {
        if (!pipeline.TryGet<string>(ContextKeys.SourcePath, out var path)) return repo;
        logger.LogDebug("Overriding source path: {Path}", path);
        return repo with { Path = path };
    }

    private RepoConnection ApplyUrlOverride(RepoConnection repo, PipelineContext pipeline)
    {
        if (!pipeline.TryGet<string>(ContextKeys.SourceUrl, out var url)) return repo;
        logger.LogDebug("Overriding source url: {Url}", url);
        return repo with { Url = url };
    }

    private RepoConnection ApplyAuthOverride(RepoConnection repo, PipelineContext pipeline)
    {
        if (!pipeline.TryGet<string>(ContextKeys.SourceAuth, out var auth) || auth is null) return repo;
        logger.LogDebug("Overriding source auth: {Auth}", auth);
        return repo with { Auth = auth };
    }
}
