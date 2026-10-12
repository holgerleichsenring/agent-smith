using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7b: joins the sub-series every run repository holds into the ticket's ONE set,
/// all or nothing. Any repository that read badly makes the set unreadable; nothing anywhere is
/// nothing at the path. The newest manifest names the members; a member not checked out or holding
/// nothing, or two bases among the members, is unreadable. A member on another revision time than
/// the newest is LAGGING: named, never joined — the set is the members on the newest revision.
/// 2026-10-12-f5f7f: a member outside the checkout is answered as such; a lagging join also carries the
/// executed prefix a re-cut demand cuts from.
/// </summary>
public sealed class SeriesJoin(SeriesNewestManifest newestManifest, SeriesJoinOrder order)
{
    public SeriesJoined Join(IReadOnlyList<SubSeriesRead> reads, string carryingRepo)
    {
        ArgumentNullException.ThrowIfNull(reads);
        if (reads.FirstOrDefault(r => r.State == SubSeriesState.Unreadable) is { } bad)
            return SeriesJoined.Unreadable(reads.Count > 1 ? $"{bad.Repo}: {bad.Why}" : bad.Why!);
        var found = reads.Where(r => r.State == SubSeriesState.Found).ToList();
        if (found.Count == 0) return NothingFound(reads, carryingRepo);

        var (newest, why) = newestManifest.Pick(found, carryingRepo);
        if (newest is null) return SeriesJoined.Unreadable(why!);
        var members = new List<SubSeriesRead>();
        foreach (var name in newest.Members)
        {
            var read = reads.FirstOrDefault(r => string.Equals(r.Repo, name, StringComparison.Ordinal));
            if (MissingMember(name, read) is { } missing) return missing;
            members.Add(read!);
        }
        return Joined(newest, members);
    }

    private SeriesJoined Joined(SubSeriesRead newest, IReadOnlyList<SubSeriesRead> members)
    {
        var bases = members.Select(m => m.Base).Distinct(StringComparer.Ordinal).ToList();
        if (bases.Count > 1)
            return SeriesJoined.Unreadable($"the series' members name different bases: {string.Join(", ", bases)}");
        var current = members.Where(m => m.Stamp == newest.Stamp).ToList();
        var (phases, executed, why) = order.Of(current);
        if (why is not null) return SeriesJoined.Unreadable(why);
        return new SeriesJoined(Otherwise: null)
        {
            Newest = newest, Phases = phases, Executed = executed,
            Shas = current.ToDictionary(m => m.Repo, m => m.Sha, StringComparer.Ordinal),
            Lagging = [.. members.Where(m => m.Stamp != newest.Stamp).Select(m => m.Repo)],
            OutOfStep = current.Count == members.Count ? null : order.ExecutedPrefix(members, current),
        };
    }

    // 2026-10-12-f5f7f: a member outside the checkout is its own answer — the run's scope is short.
    private static SeriesJoined? MissingMember(string name, SubSeriesRead? read) => read?.State switch
    {
        SubSeriesState.Found => null,
        SubSeriesState.Nothing => SeriesJoined.Unreadable($"series member '{name}' carries no sub-series of the ticket"),
        _ => new SeriesJoined(SpecSetOnBranch.NotCheckedOut($"series member '{name}' is not checked out in this run")),
    };

    // The carrying repository not checked out keeps the one-repository answer: its path was never
    // looked at, so "nothing is there" is a claim this run cannot make.
    private static SeriesJoined NothingFound(IReadOnlyList<SubSeriesRead> reads, string carryingRepo) =>
        reads.Any(r => r.Repo == carryingRepo && r.State == SubSeriesState.NotCheckedOut)
            ? SeriesJoined.Unreadable($"the repository '{carryingRepo}' carrying the set is not checked out in this run")
            : SeriesJoined.Nothing;
}
