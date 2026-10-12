using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7d: the repositories a series is written into are the ones its specs NAME, in series
/// order — never one picked by position. A spec naming none (legacy, executed or not) belongs to the
/// scope's legacy repository; a spec naming several, none with no legacy repository, or one the run
/// does not hold is refused with its id. A set without specs goes to the members the run read, else
/// every repository of the run, and keeps the carrier it had.
/// </summary>
public sealed class SeriesRepos
{
    public SeriesPlacement Place(SpecSet set, SeriesScope scope)
    {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(scope);
        if (set.Phases.Count == 0) return WithoutSpecs(set, scope);
        var phases = new List<SpecPhase>(set.Phases.Count);
        var members = new List<RepoConnection>();
        foreach (var phase in set.Phases)
        {
            var (repo, why) = RepoOf(phase, scope);
            if (repo is null) return SeriesPlacement.Refused(set, why!);
            phases.Add(phase with { Draft = phase.Draft with { Repos = [repo.Name] } });
            if (!members.Any(m => Same(m.Name, repo.Name))) members.Add(repo);
        }
        var placed = set with { Phases = phases, SeriesRepos = [.. members.Select(m => m.Name)] };
        return new SeriesPlacement(placed, members, members[0]);
    }

    /// <summary>
    /// The carrying repository an approval records (2026-10-12-f5f7e: no longer the one pull request):
    /// the earliest draft's repository, else the approval's only repository, else none.
    /// </summary>
    public string CarrierOf(IReadOnlyList<PhaseDraft> drafts, IReadOnlyList<string> repositories)
    {
        ArgumentNullException.ThrowIfNull(drafts);
        return drafts.Select(d => d.Repos).FirstOrDefault(r => r.Count == 1)?[0]
               ?? (repositories is [var only] ? only : string.Empty);
    }

    private static (RepoConnection? Repo, string? Why) RepoOf(SpecPhase phase, SeriesScope scope)
    {
        var named = phase.Draft.Repos.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (named.Count > 1)
            return (null, $"spec {phase.PhaseId} names several repositories ({string.Join(", ", named)}); a spec is written into exactly one");
        var name = named.Count == 1 ? named[0] : scope.LegacyRepo;
        if (string.IsNullOrWhiteSpace(name))
            return (null, $"spec {phase.PhaseId} names no repository and the series records none it belongs to");
        var repo = scope.RunRepos.FirstOrDefault(r => Same(r.Name, name));
        return repo is not null
            ? (repo, null)
            : (null, $"spec {phase.PhaseId} names repository '{name}', which is not checked out in this run");
    }

    private static SeriesPlacement WithoutSpecs(SpecSet set, SeriesScope scope)
    {
        IReadOnlyList<RepoConnection> members = scope.MembersRead.Count > 0
            ? [.. scope.MembersRead.Select(name => scope.RunRepos.FirstOrDefault(r => Same(r.Name, name))
                ?? new RepoConnection { Name = name })]
            : scope.RunRepos;
        var carrier = string.IsNullOrWhiteSpace(scope.PreviousCarrier) ? null
            : scope.RunRepos.FirstOrDefault(r => Same(r.Name, scope.PreviousCarrier))
              ?? new RepoConnection { Name = scope.PreviousCarrier };
        return new SeriesPlacement(set with { SeriesRepos = [.. members.Select(m => m.Name)] }, members, carrier);
    }

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
