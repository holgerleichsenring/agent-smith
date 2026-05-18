using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Merges CLI-provided source overrides into the run's CurrentRepo. p0140d: reads
/// CurrentRepo from the pipeline context (set by ExecutePipelineUseCase from
/// PipelineRequest.RepoName) instead of project.Repo. For multi-repo projects only
/// the run's repo is overridden; sibling repos in project.Repos are preserved.
/// </summary>
public sealed class SourceConfigOverrider(ILogger<SourceConfigOverrider> logger) : ISourceConfigOverrider
{
    public ResolvedProject Apply(ResolvedProject project, PipelineContext pipeline)
    {
        var current = pipeline.Get<RepoConnection>(ContextKeys.CurrentRepo);
        var updated = ApplyTypeOverride(current, pipeline);
        updated = ApplyPathOverride(updated, pipeline);
        updated = ApplyUrlOverride(updated, pipeline);
        updated = ApplyAuthOverride(updated, pipeline);

        if (ReferenceEquals(current, updated)) return project;

        pipeline.Set(ContextKeys.CurrentRepo, updated);
        return project with { Repos = ReplaceInList(project.Repos, current, updated) };
    }

    private static IReadOnlyList<RepoConnection> ReplaceInList(
        IReadOnlyList<RepoConnection> repos, RepoConnection oldRepo, RepoConnection newRepo)
    {
        var arr = new RepoConnection[repos.Count];
        for (var i = 0; i < repos.Count; i++)
            arr[i] = ReferenceEquals(repos[i], oldRepo) ? newRepo : repos[i];
        return arr;
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
