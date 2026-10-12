using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7b: one run repository's sub-series of a ticket, read with today's one-repository
/// reader — its manifest, its specs in manifest order, which of them lie in done/, the last commit
/// on its series paths and its current revision time (null when unparsed).
/// </summary>
public sealed record SubSeriesRead(string Repo, SubSeriesState State, string? Why = null)
{
    public string Base { get; init; } = string.Empty;
    public SeriesManifestDocument? Document { get; init; }
    public IReadOnlyList<SpecPhase> Specs { get; init; } = [];
    public IReadOnlyList<string> Executed { get; init; } = [];
    public string Sha { get; init; } = string.Empty;
    public DateTimeOffset? Stamp { get; init; }

    /// <summary>A manifest without <c>series_repos</c> predates them: a one-repository series.</summary>
    public bool IsLegacy => Document?.SeriesRepos is not { Count: > 0 };

    /// <summary>The repositories the manifest names as the series' members; itself when legacy.</summary>
    public IReadOnlyList<string> Members =>
        IsLegacy ? [Repo] : [.. Document!.SeriesRepos!.Distinct(StringComparer.Ordinal)];

    public static SubSeriesRead Failed(string repo, string why) => new(repo, SubSeriesState.Unreadable, why);
}
