using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207a: what a beat posts, in order, and what the record keeps of it.</summary>
public static class SeriesReport
{
    public static IReadOnlyList<string> Messages(SeriesBeat beat)
    {
        ArgumentNullException.ThrowIfNull(beat);
        var messages = new List<string>(beat.Said);
        if (beat.MasterLine is { Length: > 0 } line) messages.Add(line);
        return messages;
    }

    /// <summary>The record after the beat: the table it saw, and when it last spoke.</summary>
    public static SeriesRunRecord Seen(SeriesRunRecord record, SeriesBeat beat, bool spoke) => record with
    {
        LastTable = beat.Table,
        LastStatusAt = spoke ? beat.Now : record.LastStatusAt,
    };
}
