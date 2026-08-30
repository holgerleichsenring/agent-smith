using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Services.Output;

/// <summary>
/// 2026-08-30-03e1: renders what each station examined into the delivered report.
/// <para>
/// The entry map says where each station LIVES; this says whether the scan examined it and
/// what it cited there. A run that examined no station says so in its first line — the one
/// thing the retired answered-of-total header could not do, because a denominator nobody
/// answered still reads as a number. Empty when nothing was mapped and nothing was cited,
/// so a report from a run without it is the report it was before.
/// </para>
/// </summary>
public static class ExaminationSection
{
    public const string Heading = "## Requirements — what each station examined";

    /// <summary>The section, or the empty string when this run has nothing to say.</summary>
    public static string Markdown(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<ScanExaminationAccount>(
                ContextKeys.ScanExaminationAccount, out var account)
            || account is null || account.IsEmpty)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine(Heading).AppendLine().AppendLine(Opening(account)).AppendLine();
        foreach (var group in account.Groups) AppendGroup(sb, group);
        sb.AppendLine(account.Attribution).AppendLine();
        return sb.ToString();
    }

    /// <summary>What the run can claim overall — and, when it examined nothing, that
    /// rather than a coverage figure.</summary>
    private static string Opening(ScanExaminationAccount account) =>
        account.ExaminedCount == 0
            ? "NO STATION WAS EXAMINED. Nothing below rests on a station this run both "
                + "located and read around, so this report covers none of them."
            : $"{account.ExaminedCount} of {account.Stations.Count} stations examined across "
                + $"{account.Groups.Count(g => g.Attempted)} entry group(s), citing "
                + $"{account.Located.Count} finding(s) against {account.CatalogueVersion}.";

    private static void AppendGroup(StringBuilder sb, EntryGroupExamination group)
    {
        sb.AppendLine($"### {group.Group}").AppendLine();
        if (!group.Attempted)
        {
            sb.AppendLine("NOT ATTEMPTED — beyond the entry groups this run accounts for. "
                + "Nothing here was examined.").AppendLine();
            return;
        }

        sb.AppendLine("| Station | Examined | Cited |").AppendLine("| --- | --- | --- |");
        foreach (var station in group.Stations)
            sb.AppendLine($"| {station.Station} | {Examined(station)} | {Cited(station)} |");
        sb.AppendLine();
        AppendNamedRows(sb, group);
    }

    private static string Examined(StationExamination station) =>
        station.Examined ? "yes" : $"no — {station.Note}";

    private static string Cited(StationExamination station) =>
        station.Located.Count == 0
            ? "—"
            : string.Join(", ", station.Located.Select(row => row.Reference));

    /// <summary>The rows a reader must be able to look up: what each station yielded, and
    /// every claim the scan made that its own read set could not carry.</summary>
    private static void AppendNamedRows(StringBuilder sb, EntryGroupExamination group)
    {
        foreach (var station in group.Stations)
        {
            foreach (var row in station.Located)
                sb.AppendLine($"- {row.Reference} {station.Station} — `{row.Citation}`: {row.Detail}");
            foreach (var row in station.Unlocated)
                sb.AppendLine($"- NOT DELIVERED: {row.Reference} {station.Station} — {row.Note}");
        }
        sb.AppendLine();
    }
}
