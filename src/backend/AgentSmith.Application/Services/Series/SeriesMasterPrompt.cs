using System.Globalization;
using System.Text;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207a: the beat's turn for the series master — the series as it stands and what needs its
/// judgement now. 2026-11-19-1b72a: a paused series says so first. 2026-11-25-433fb: the thread keeps the turn without the table
/// (<see cref="Kept"/>) — every beat reads the table afresh, and an old one is stale state.</summary>
public static class SeriesMasterPrompt
{
    public static string Build(SeriesBeat beat) => Compose(beat, withTable: true);

    /// <summary>2026-11-25-433fb: the turn as the master's thread stores it — its head and its reasons, no table.</summary>
    public static string Kept(SeriesBeat beat) => Compose(beat, withTable: false);

    private static string Compose(SeriesBeat beat, bool withTable)
    {
        ArgumentNullException.ThrowIfNull(beat);
        var record = beat.Record;
        var text = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"Series {record.Id} of project {record.Project}, ")
            .Append(CultureInfo.InvariantCulture, $"running since {record.StartedAt:u}; it is {beat.Now:u} now.\n\n")
            .Append(record.State == SeriesRunStates.Paused ? "The series is paused: none of its tickets starts until resume_series.\n\n" : string.Empty)
            .Append(withTable ? SeriesStatusText.Table(beat.Rows) + "\n\n" : string.Empty).Append("What needs your judgement now:\n");
        foreach (var reason in beat.Reasons) text.Append("- ").Append(reason).Append('\n');
        return text.ToString();
    }
}
