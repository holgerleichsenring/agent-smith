using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Services.Output;

/// <summary>
/// 2026-08-30-18e3: renders the checked entry map into the delivered report.
/// <para>
/// The findings say what the scan found; this says what the scan LOOKED AT, per entry group
/// and per station, so a reader can tell a system with no scope station from a scan that
/// never looked for one. Empty when no map was stated, so every report a run without the
/// map produces is exactly the report it produced before this existed.
/// </para>
/// </summary>
public static class EntryStationSection
{
    public const string Heading = "## Entry map — the stations of a request";

    /// <summary>The section, or the empty string when this run stated no map.</summary>
    public static string Markdown(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<RequestStationMap>(ContextKeys.RequestStationMap, out var map)
            || map is null || map.IsEmpty)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine(Heading).AppendLine();
        var total = map.Groups.Count * Enum.GetValues<VerificationStation>().Length;
        sb.AppendLine($"{total - map.Unlocated.Count} of {total} stations located "
            + $"across {map.Groups.Count} entry group(s).").AppendLine();
        foreach (var group in map.Groups) AppendGroup(sb, group);
        return sb.ToString();
    }

    private static void AppendGroup(StringBuilder sb, EntryGroupStations group)
    {
        sb.AppendLine($"### {group.Group}").AppendLine();
        sb.AppendLine("| Station | Location |").AppendLine("| --- | --- |");
        foreach (var station in group.Stations)
            sb.AppendLine($"| {station.Station} | {Cell(station)} |");
        sb.AppendLine();
    }

    private static string Cell(StationLocation station) =>
        station.Located ? $"`{station.Display}`" : $"NOT LOCATED — {station.Note}";
}
