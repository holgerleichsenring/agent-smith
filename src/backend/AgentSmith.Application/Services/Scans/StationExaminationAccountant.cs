using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// 2026-08-30-03e1: turns what the scan LOOKED AT and what it CITED into what the run can
/// show for each station of each entry group.
/// <para>
/// The denominator stays external and stays the station map's: six stations per group,
/// stated by the master and settled against the read set, so nothing here lets a run
/// examine the easy three and read as thorough. What the catalogue contributes is no longer
/// a count of entries owed an answer — it is the clause a finding names.
/// </para>
/// </summary>
public sealed class StationExaminationAccountant(IVerificationLens lens)
{
    public ScanExaminationAccount Settle(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var cited = CitedFindingLog.In(pipeline);
        var map = pipeline.TryGet<RequestStationMap>(ContextKeys.RequestStationMap, out var stated)
            && stated is not null ? stated : RequestStationMap.Empty;
        if (map.IsEmpty && cited.Count == 0) return ScanExaminationAccount.Empty;

        var read = pipeline.TryGet<List<string>>(ContextKeys.MasterReadPaths, out var paths)
            ? paths : null;
        var catalogue = lens.For(pipeline, VerificationStation.Admission);
        return new ScanExaminationAccount(catalogue.CatalogueVersion, catalogue.Attribution,
        [
            .. Groups(map, cited).Select((group, index) => Settle(
                group, index < CitedFindingLog.MaxEntryGroups, map, cited, read))
        ]);
    }

    /// <summary>The groups the scan stated, in the order it stated them — the entry map
    /// first, since a group nobody mapped is a group nobody located.</summary>
    private static IReadOnlyList<string> Groups(
        RequestStationMap map, IReadOnlyList<CitedFinding> cited) =>
        [.. map.Groups.Select(group => group.Group)
            .Concat(cited.Select(finding => finding.Group))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    private static EntryGroupExamination Settle(
        string group, bool attempted, RequestStationMap map, IReadOnlyList<CitedFinding> cited,
        IReadOnlyCollection<string>? read)
    {
        if (!attempted) return new EntryGroupExamination(group, Attempted: false, []);
        var mapped = map.Groups.FirstOrDefault(
            g => string.Equals(g.Group, group, StringComparison.OrdinalIgnoreCase));
        var mine = cited
            .Where(finding => string.Equals(finding.Group, group, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return new EntryGroupExamination(group, Attempted: true,
        [
            .. Enum.GetValues<VerificationStation>().Select(station => Settle(
                station, mapped?.Stations.FirstOrDefault(s => s.Station == station), mine, read))
        ]);
    }

    private static StationExamination Settle(
        VerificationStation station, StationLocation? location,
        IReadOnlyList<CitedFinding> mine, IReadOnlyCollection<string>? read)
    {
        var (examined, note) = Examined(location, read);
        return new StationExamination(station, examined, note,
        [
            .. mine.Where(finding => finding.Station == station)
                .Select(finding => RequirementCitation.Settle(finding, read))
        ]);
    }

    /// <summary>Examined is the located citation standing AND the read set holding files
    /// beneath it — two facts the run already has, never a third assertion by the model.</summary>
    private static (bool Examined, string Note) Examined(
        StationLocation? location, IReadOnlyCollection<string>? read)
    {
        if (location is null) return (false, "this group states no entry map");
        if (!location.Located) return (false, location.Note);
        return ReadNeighbourhood.HoldsFilesBeneath(read, location.File)
            ? (true, string.Empty)
            : (false, $"located at {location.Display}, but the scan read nothing else beneath it");
    }
}
