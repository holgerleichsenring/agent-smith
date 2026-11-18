using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207a: what a beat posts, in order, and what the record keeps of it. 2026-11-17-0207b: the
/// opening on the series' first beat; the table when it changed or the cadence is due.</summary>
public static class SeriesReport
{
    public static IReadOnlyList<string> Messages(SeriesBeat beat, ResolvedProject project, DialogueGlobalConfig dialogue)
    {
        ArgumentNullException.ThrowIfNull(beat);
        var messages = new List<string>();
        if (beat.Record.LastStatusAt is null && beat.Record.IsActive) messages.Add(SeriesOpening.Text(beat, project, dialogue));
        messages.AddRange(beat.Said);
        if (beat.MasterLine is { Length: > 0 } line) messages.Add(line);
        if (beat.Record.IsActive && TableDue(beat, dialogue)) messages.Add(Status(beat));
        return messages;
    }

    /// <summary>The record after the beat: the table it saw, when it last spoke, the messages it read.</summary>
    public static SeriesRunRecord Seen(SeriesRunRecord record, SeriesBeat beat, bool spoke) => record with
    {
        LastTable = beat.Table,
        LastStatusAt = spoke ? beat.Now : record.LastStatusAt,
        MessagesThrough = Math.Max(record.MessagesThrough, beat.MessagesThrough),
    };

    private static bool TableDue(SeriesBeat beat, DialogueGlobalConfig dialogue) =>
        beat.Table != beat.Record.LastTable || SeriesBeatDue.Cadence(beat.Record, beat.Now, dialogue.SeriesStatusMinutes);

    private static string Status(SeriesBeat beat) =>
        $"Status at {beat.Now.UtcDateTime:HH:mm} UTC.\n\n{SeriesStatusText.Table(beat.Rows)}\n\n"
        + SeriesStatusText.Counts(beat.Rows, beat.Facts, beat.Record.MasterCostUsd);
}
