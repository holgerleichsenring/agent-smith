namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-25-9992h: a series act said in the conversation under the machine identity the series acts as, with the
/// reason the master gave; the tool's answer goes back unchanged. No reason, nothing said. 2026-11-25-9992e: what it did is
/// kept on the beat either way, the text the act log records. 2026-11-26-f511c: the act is kept on the beat's acts, not in
/// what it says — the report puts the beat's acts under the master's sentence as one line, so an act and its narration are
/// one entry (<see cref="SeriesReport"/>).</summary>
public static class SeriesActSay
{
    public const string Why = "One sentence: why you take this act. It is said in the conversation with the act.";

    public static string Acted(SeriesBeat beat, string did, string? reason, string answer)
    {
        ArgumentNullException.ThrowIfNull(beat);
        beat.ActDid = did;
        beat.Acts.Add(new SeriesActLine(did, string.IsNullOrWhiteSpace(reason) ? null : $"{beat.Actor} {did}: {reason.Trim()}"));
        return answer;
    }

    /// <summary>2026-11-26-5f6e: what a start_ticket did, in the words of its outcome.</summary>
    public static string Started(string key, Contracts.Series.SeriesStart start)
    {
        ArgumentNullException.ThrowIfNull(start);
        return start.Kind switch
        {
            Contracts.Series.SeriesStartKind.Started => $"started {key}{(start.RunId is { } run ? $" (run {run})" : string.Empty)}",
            Contracts.Series.SeriesStartKind.Queued => $"queued {key}",
            _ => $"could not start {key}",
        };
    }

    /// <summary>2026-11-25-96aeb: an act whose words carry its own why (a merge over a refusal, with its override) is said
    /// without a reason.</summary>
    public static string Told(SeriesBeat beat, string did, string answer)
    {
        ArgumentNullException.ThrowIfNull(beat);
        beat.ActDid = did;
        beat.Acts.Add(new SeriesActLine(did, $"{beat.Actor} {did}"));
        return answer;
    }
}
