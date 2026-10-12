using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7b: the run repositories' sub-series joined into one set — the newest manifest,
/// the joined specs in order, which of them ran, the last commit per joined repository and the
/// members whose sub-series lags the newest revision. Or the branch answer standing in its place:
/// nothing anywhere, or something no run can read as one set.
/// </summary>
public sealed record SeriesJoined(SpecSetOnBranch? Otherwise)
{
    public SubSeriesRead? Newest { get; init; }
    public IReadOnlyList<SpecPhase> Phases { get; init; } = [];
    public IReadOnlyList<string> Executed { get; init; } = [];
    public IReadOnlyDictionary<string, string> Shas { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<string> Lagging { get; init; } = [];

    public static SeriesJoined Unreadable(string why) => new(SpecSetOnBranch.Unreadable(why));

    public static readonly SeriesJoined Nothing = new(SpecSetOnBranch.Nothing);
}
