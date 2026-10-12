using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-10-12-f5f7d: where one revision of a series is written. <see cref="Set"/> is the set with
/// every spec naming exactly the repository it is placed in and <c>series_repos</c> stamped;
/// <see cref="Members"/> are the repositories that each get their own sub-series, in series order;
/// <see cref="Carrier"/> is the carrying repository (the earliest spec's, or the one a set
/// without specs keeps), null when there is none. A placement that cannot be made says why.
/// </summary>
public sealed record SeriesPlacement(
    SpecSet Set,
    IReadOnlyList<RepoConnection> Members,
    RepoConnection? Carrier,
    string? Refusal = null)
{
    public IReadOnlyList<string> Names => [.. Members.Select(m => m.Name ?? string.Empty)];

    public static SeriesPlacement Refused(SpecSet set, string why) => new(set, [], null, why);
}
