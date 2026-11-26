namespace AgentSmith.Contracts.Series;

/// <summary>2026-11-19-1b72a: what a series' start of one member ticket came to, and the sentence of what happened.
/// 2026-11-26-5f6e: as one of three outcomes — started (with its run), queued (the text says why), or refused (the text says
/// why and what lifts it) — so the act that asked for it is logged as what it did.</summary>
public sealed record SeriesStart(SeriesStartKind Kind, string Text, string? RunId = null)
{
    public bool Started => Kind == SeriesStartKind.Started;

    public static SeriesStart Refused(string text) => new(SeriesStartKind.Refused, text);
}

/// <summary>2026-11-26-5f6e: what a start came to.</summary>
public enum SeriesStartKind
{
    /// <summary>A run was claimed for the ticket.</summary>
    Started,

    /// <summary>A run waits in the capacity queue for room.</summary>
    Queued,

    /// <summary>Nothing runs for the ticket.</summary>
    Refused,
}
