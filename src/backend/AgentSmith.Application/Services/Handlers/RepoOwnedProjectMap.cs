using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-23-bb73: the analysis map a repository OWNS.
/// <see cref="ContextKeys.RepoProjectMaps"/> is published under the composed SANDBOX KEY, so a
/// lookup by repository name hits only where key and repository name coincide — one run shape of
/// four. Both bootstrap rounds looked it up by name and bounded the miss with a COUNT, which
/// answers a different question: a repository with several toolchain groups keys on the context
/// name or on repo-plus-context, the name misses, the count refuses the fallback, and the round
/// fails with no map at all.
/// <para>
/// So the keys are read through <see cref="SandboxTargets.KeyBelongsToRepo"/> — the one key→repo
/// ownership test, the same one 2026-09-23-6698 resolved the sandboxes with. A single-repo run
/// still resolves its sole map, because every key belongs to the one repository; a multi-repo run
/// can no longer borrow another repository's, because no key of it belongs.
/// </para>
/// </summary>
internal static class RepoOwnedProjectMap
{
    /// <summary>
    /// The first map published under a key this repository owns; null when it owns none, which
    /// the caller reports rather than borrowing. A repository with several toolchain groups owns
    /// several — the per-context map is what tells those apart, and this is the repo-level answer
    /// its callers ask for.
    /// </summary>
    public static ProjectMap? In(
        PipelineContext pipeline, string repoName, SandboxTargets sandboxTargets)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(sandboxTargets);
        if (!pipeline.TryGet<IReadOnlyDictionary<string, ProjectMap>>(
                ContextKeys.RepoProjectMaps, out var maps) || maps is null)
            return null;
        pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, out var owners);
        var multiRepo = pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos)
                        && repos is not null && repos.Count > 1;
        foreach (var (key, map) in maps)
            if (sandboxTargets.KeyBelongsToRepo(key, repoName ?? string.Empty, multiRepo, owners))
                return map;
        return null;
    }
}
