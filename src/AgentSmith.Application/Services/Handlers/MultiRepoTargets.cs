using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0158f back-compat helper. Multi-repo handlers prefer ContextKeys.Sandboxes
/// + ContextKeys.Repos (post-p0158e). Legacy single-sandbox callers / tests
/// that only seed ContextKeys.Sandbox get a one-entry dict + synthetic
/// single-element Repos list keyed by an empty repo name, so per-repo
/// iteration runs once against the legacy sandbox.
/// </summary>
internal static class MultiRepoTargets
{
    public static (IReadOnlyDictionary<string, ISandbox>? Sandboxes, IReadOnlyList<RepoConnection>? Repos)
        Resolve(PipelineContext pipeline)
    {
        var hasDict = pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, out var dict) && dict is not null;
        var hasRepos = pipeline.TryGet<IReadOnlyList<RepoConnection>>(
            ContextKeys.Repos, out var repos) && repos is not null;
        if (hasDict && hasRepos) return (dict, repos);

        if (!pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var legacy) || legacy is null)
            return (null, null);
        var oneSandbox = new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [string.Empty] = legacy };
        var oneRepo = new[] { new RepoConnection() };
        return (oneSandbox, oneRepo);
    }
}
