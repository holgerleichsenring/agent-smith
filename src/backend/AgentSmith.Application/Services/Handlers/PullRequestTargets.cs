using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-13-a284: the base each repository's pull request must open against, written
/// once at checkout and read by every site that opens one.
/// <para>
/// PER REPOSITORY, because the rung is. The openers loop repositories with a SINGLE
/// branch value — <c>context.Repository.CurrentBranch</c> — so one recorded target would
/// send a two-repository run's second clone to a base its feature branch may not even
/// have. 2026-09-13-5cdf resolves one base per repository; this carries that answer
/// unchanged to the provider.
/// </para>
/// <para>
/// ONLY A RUNG IS RECORDED. A ladder that fell through named the clone's own base, and
/// passing that as an explicit target would freeze a lookup the provider already does
/// better — <c>DefaultBranchResolver</c> honours the configured
/// <c>RepoConnection.DefaultBranch</c>. An absent entry therefore means exactly today's
/// behaviour, which is what makes a run with no rung unchanged by construction.
/// </para>
/// </summary>
public static class PullRequestTargets
{
    /// <summary>
    /// Records what this repository's branch was cut from. A null or blank rung is the
    /// fall-through and is not recorded, so the map holds only bases somebody chose.
    /// </summary>
    public static void Record(PipelineContext pipeline, string repoName, string? rung)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (string.IsNullOrWhiteSpace(rung)) return;
        var targets = pipeline.TryGet<IReadOnlyDictionary<string, string>>(
            ContextKeys.PullRequestTargets, out var existing) && existing is not null
            ? new Dictionary<string, string>(existing, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
        targets[repoName] = rung!;
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.PullRequestTargets, targets);
    }

    /// <summary>
    /// Whether an already-open pull request sitting on <paramref name="current"/> has to be
    /// moved onto <paramref name="target"/>.
    /// <para>
    /// A base nobody could READ is not a wrong one — a provider with no pull request server,
    /// or one that could not answer, reports null, and moving on that would retarget pull
    /// requests that are already right. Null target likewise means this run resolved no
    /// rung, so there is nothing to move it to.
    /// </para>
    /// </summary>
    public static bool NeedsMove(string? current, BranchName? target) =>
        target is not null
        && !string.IsNullOrWhiteSpace(current)
        && !string.Equals(current, target.Value, StringComparison.Ordinal);

    /// <summary>
    /// The base this repository's pull request belongs against, or null when the run has
    /// no rung for it and the provider's own default-branch resolution is the answer.
    /// </summary>
    public static BranchName? For(PipelineContext pipeline, string repoName)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<IReadOnlyDictionary<string, string>>(
                ContextKeys.PullRequestTargets, out var targets) || targets is null)
            return null;
        return targets.TryGetValue(repoName, out var rung) && !string.IsNullOrWhiteSpace(rung)
            ? new BranchName(rung)
            : null;
    }
}
