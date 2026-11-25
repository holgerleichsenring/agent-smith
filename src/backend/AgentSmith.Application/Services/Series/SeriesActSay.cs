namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-25-9992h: a series act said in the conversation under the machine identity the series acts as, with the
/// reason the master gave; the tool's answer goes back unchanged. No reason, nothing said. 2026-11-25-9992e: what it did is
/// kept on the beat either way, the text the act log records.</summary>
public static class SeriesActSay
{
    public const string Why = "One sentence: why you take this act. It is said in the conversation with the act.";

    public static string Acted(SeriesBeat beat, string did, string? reason, string answer)
    {
        ArgumentNullException.ThrowIfNull(beat);
        beat.ActDid = did;
        if (!string.IsNullOrWhiteSpace(reason)) beat.Said.Add($"{beat.Actor} {did}: {reason.Trim()}");
        return answer;
    }
}
