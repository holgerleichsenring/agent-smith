using System.Globalization;
using System.Text;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207a: the beat's turn for the series master — the series as it stands and what needs its
/// judgement now. 2026-11-19-1b72a: a paused series says so first. 2026-11-25-433fb: the thread keeps the turn without the table
/// (<see cref="Kept"/>) — every beat reads the table afresh, and an old one is stale state. 2026-11-27-3532c: and without the
/// addresses it can read, which every beat lists afresh as well.</summary>
public static class SeriesMasterPrompt
{
    public static string Build(SeriesBeat beat, IReadOnlyList<SeriesSource>? readable = null) =>
        Compose(beat, withTable: true, readable ?? []);

    /// <summary>2026-11-25-433fb: the turn as the master's thread stores it — its head and its reasons, no table.</summary>
    public static string Kept(SeriesBeat beat) => Compose(beat, withTable: false, []);

    private static string Compose(SeriesBeat beat, bool withTable, IReadOnlyList<SeriesSource> readable)
    {
        ArgumentNullException.ThrowIfNull(beat);
        var record = beat.Record;
        var text = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"Series {record.Id} of project {record.Project}, ")
            .Append(CultureInfo.InvariantCulture, $"running since {record.StartedAt:u}; it is {beat.Now:u} now.\n\n")
            .Append(record.State == SeriesRunStates.Paused ? "The series is paused: none of its tickets starts until resume_series.\n\n" : string.Empty)
            .Append(withTable ? SeriesStatusText.Table(beat.Rows) + "\n\n" : string.Empty)
            .Append(Readable(readable)).Append("What needs your judgement now:\n");
        foreach (var reason in beat.Reasons) text.Append("- ").Append(reason).Append('\n');
        return text.ToString();
    }

    // 2026-11-27-3532c: what read_file, grep_in_tree, find_files and directory_tree read — a path starts with its address.
    private static string Readable(IReadOnlyList<SeriesSource> readable)
    {
        if (readable.Count == 0) return string.Empty;
        var text = new StringBuilder("What you can read, each path starting with its address:\n");
        foreach (var source in readable) text.Append("- `").Append(source.Address).Append("/` — ").Append(source.Shows).Append('\n');
        return text.Append('\n').ToString();
    }
}
