using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// 2026-08-30-18e3: turns what the master SAID about its entry groups into what the run can
/// SHOW about them.
/// <para>
/// A location that does not resolve is not a location. Free text would make this a form the
/// model fills plausibly; a location that must land in the evidence the scan actually holds
/// makes it a method. The evidence is the run's own read set, and the rule is
/// <see cref="ReadPathNormalizer.WasRead"/> — the same one that decides whether a finding
/// may call itself analyzed-from-source, so a located station and a delivered finding are
/// resolved by one rule and cannot disagree about what "cited" means.
/// </para>
/// </summary>
public sealed class StationMapResolver
{
    public RequestStationMap Resolve(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var claims = StationClaimLog.In(pipeline);
        if (claims.Count == 0) return RequestStationMap.Empty;

        var read = pipeline.TryGet<List<string>>(ContextKeys.MasterReadPaths, out var paths)
            ? paths : null;
        return new RequestStationMap(
        [
            .. claims
                .GroupBy(c => c.Group, StringComparer.OrdinalIgnoreCase)
                .Select(g => new EntryGroupStations(g.Key, Stations(g.ToList(), read)))
        ]);
    }

    /// <summary>Every station of the enum, in its order — a group states six rows whether or
    /// not the master remembered six, because the row nobody filled is the whole point.</summary>
    private static IReadOnlyList<StationLocation> Stations(
        IReadOnlyList<StationClaim> claimed, IReadOnlyCollection<string>? read) =>
    [
        .. Enum.GetValues<VerificationStation>().Select(station =>
            Check(station, claimed.LastOrDefault(c => c.Station == station), read))
    ];

    private static StationLocation Check(
        VerificationStation station, StationClaim? claim, IReadOnlyCollection<string>? read)
    {
        if (claim is null)
            return new StationLocation(station, null, 0, false, "the map never states this station");
        if (claim.File is null || claim.StartLine <= 0)
            return new StationLocation(
                station, null, 0, false, claim.NotLocatedReason ?? "no reason stated");
        if (!ReadPathNormalizer.WasRead(read, claim.File))
            return new StationLocation(
                station, claim.File, claim.StartLine, false,
                $"cites {claim.File}, which this scan never read");
        return new StationLocation(station, claim.File, claim.StartLine, true, string.Empty);
    }
}
