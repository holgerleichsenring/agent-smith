using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: one commit per revision, staged path-scoped so the set lands on its own, then pushed
/// immediately — a reviewer reading the cut while the run is still working needs it on the remote.
/// Each member's commit is <see cref="SeriesMemberCommit"/>'s (2026-10-06-03c7d).
/// <para>
/// 2026-10-12-f5f7d: each member of the placement gets its OWN sub-series — its specs, a manifest
/// listing only them, the series-wide fields and the member list — from one set, so every member
/// carries the same revision time. A member that is not checked out refuses the whole write before
/// anything is written.
/// </para>
/// <para>
/// 2026-10-12-f5f7f: a write into several repositories lands in all of them or in none. Every member
/// is pushed plainly; when one fails, <see cref="SeriesWriteRollback"/> reverts the members this write
/// pushed and puts every tree back, and the result says so. A one-repository write is unchanged.
/// </para>
/// </summary>
public sealed class SeriesWriter(
    SeriesMemberCommit member,
    SeriesWriteRollback rollback,
    SandboxGitOperations gitOps,
    SandboxTargets sandboxTargets,
    ILogger<SeriesWriter> logger) : ISpecSetWriter
{
    public async Task<SpecSetWriteResult> WriteAsync(
        PipelineContext pipeline, SeriesPlacement placement, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(placement);
        var targets = new List<(RepoConnection Repo, ISandbox Sandbox)>(placement.Members.Count);
        foreach (var repo in placement.Members)
        {
            var matches = sandboxTargets.SandboxesForRepo(pipeline, repo);
            if (matches.Count == 0)
                return SpecSetWriteResult.Refused(
                    $"the series names repository '{repo.Name}', which is not checked out in this run");
            targets.Add((repo, matches[0].Value));
        }
        // The branch CheckoutSource actually landed on — the same value CommitAndPR pushes to.
        var branch = pipeline.TryGet<Domain.Entities.Repository>(ContextKeys.Repository, out var r)
            ? r?.CurrentBranch.Value : null;
        if (string.IsNullOrWhiteSpace(branch))
            return SpecSetWriteResult.Failed("the run has no ticket branch to carry the spec set");
        return await WriteMembersAsync(targets, placement, branch!, cancellationToken);
    }

    private async Task<SpecSetWriteResult> WriteMembersAsync(
        IReadOnlyList<(RepoConnection Repo, ISandbox Sandbox)> targets, SeriesPlacement placement,
        string branch, CancellationToken ct)
    {
        var several = targets.Count > 1;
        var progress = new SeriesWriteProgress(several ? await HeadsAsync(targets, ct) : new Dictionary<string, string>(),
            new Dictionary<string, string>(StringComparer.Ordinal), new Dictionary<string, bool>(StringComparer.Ordinal));
        foreach (var (repo, sandbox) in targets)
        {
            try
            {
                (progress.Shas[repo.Name], progress.Pushed[repo.Name]) = await member.CommitAsync(
                    sandbox, repo, branch, placement.Set.OfRepository(repo.Name), plain: several, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Spec-set commit failed for {Repo}", repo.Name);
                var failure = $"{repo.Name}: {ex.Message.Split('\n', 2)[0].Trim()}";
                return several
                    ? await rollback.UndoAsync(targets, placement.Set, branch, progress, failure, ct)
                    : SpecSetWriteResult.Failed(failure);
            }
        }
        var carrier = placement.Carrier?.Name;
        return SpecSetWriteResult.Ok(progress.Shas, carrier is not null && progress.Shas.TryGetValue(carrier, out var own)
            ? own : progress.Shas.Values.FirstOrDefault() ?? string.Empty) with { Pushed = progress.Pushed };
    }

    private async Task<IReadOnlyDictionary<string, string>> HeadsAsync(
        IReadOnlyList<(RepoConnection Repo, ISandbox Sandbox)> targets, CancellationToken ct)
    {
        var heads = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (repo, sandbox) in targets)
            heads[repo.Name] = await gitOps.GetHeadCommitAsync(sandbox, ct);
        return heads;
    }

    /// <summary>The commit message of a series revision.</summary>
    public static string MessageFor(SpecSet set) =>
        $"spec: {set.Series} ({set.Key}) revision {set.Current.Number} ({set.Current.Cause}), "
        + $"{set.Phases.Count} spec(s)";
}
