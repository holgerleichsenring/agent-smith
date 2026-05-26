using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Merges CLI-provided source overrides into the run's Repos. The CLI may also set
/// ContextKeys.SourceOverrideRepo to scope the overrides to a single repo by name;
/// in that case the overrides apply only to the named repo, sibling repos are
/// preserved. When SourceOverrideRepo is absent (queue-driven K8s/Compose runs),
/// no source override is expected.
/// </summary>
public sealed class SourceConfigOverrider(ILogger<SourceConfigOverrider> logger) : ISourceConfigOverrider
{
    public ResolvedProject Apply(ResolvedProject project, PipelineContext pipeline)
    {
        var repos = pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos);
        var targetName = pipeline.TryGet<string>(ContextKeys.SourceOverrideRepo, out var t) ? t : null;
        var updated = BuildUpdatedRepos(repos, pipeline, targetName);
        if (ReferenceEquals(updated, repos)) return project;

        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, updated);
        return project with { Repos = updated };
    }

    private IReadOnlyList<RepoConnection> BuildUpdatedRepos(
        IReadOnlyList<RepoConnection> repos, PipelineContext pipeline, string? targetName)
    {
        var arr = new RepoConnection[repos.Count];
        var anyChanged = false;
        for (var i = 0; i < repos.Count; i++)
        {
            arr[i] = repos[i];
            if (targetName is not null
                && !string.Equals(repos[i].Name, targetName, StringComparison.OrdinalIgnoreCase))
                continue;
            arr[i] = ApplyOverrides(repos[i], pipeline);
            if (!ReferenceEquals(arr[i], repos[i])) anyChanged = true;
        }
        return anyChanged ? arr : repos;
    }

    private RepoConnection ApplyOverrides(RepoConnection repo, PipelineContext pipeline)
    {
        var updated = ApplyTypeOverride(repo, pipeline);
        updated = ApplyPathOverride(updated, pipeline);
        updated = ApplyUrlOverride(updated, pipeline);
        updated = ApplyAuthOverride(updated, pipeline);
        return updated;
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
