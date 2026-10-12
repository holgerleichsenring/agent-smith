using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7e: the spec pull requests of a run, one per series repository —
/// <see cref="ContextKeys.SpecPullRequestUrls"/>. A run parked before the map existed carries the one
/// URL under <see cref="ContextKeys.SpecPullRequestUrl"/>; it reads as <c>{SpecRepo: url}</c>, so the
/// resumed run still finds its spec pull request.
/// </summary>
public static class SpecPullRequests
{
    /// <summary>Repository → spec pull request URL; empty when the run opened none.</summary>
    public static IReadOnlyDictionary<string, string> Of(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (pipeline.TryGet<Dictionary<string, string>>(ContextKeys.SpecPullRequestUrls, out var map) && map is not null)
            return map;
        if (!pipeline.TryGet<string>(ContextKeys.SpecPullRequestUrl, out var url) || string.IsNullOrWhiteSpace(url))
            return new Dictionary<string, string>(StringComparer.Ordinal);
        var repo = pipeline.TryGet<string>(ContextKeys.SpecRepo, out var name) ? name ?? string.Empty : string.Empty;
        return new Dictionary<string, string>(StringComparer.Ordinal) { [repo] = url! };
    }

    /// <summary>Records <paramref name="url"/> as <paramref name="repo"/>'s spec pull request, keeping
    /// every other repository's.</summary>
    public static void Record(PipelineContext pipeline, string repo, string url)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var map = new Dictionary<string, string>(Of(pipeline), StringComparer.Ordinal) { [repo] = url };
        pipeline.Set(ContextKeys.SpecPullRequestUrls, map);
    }
}
