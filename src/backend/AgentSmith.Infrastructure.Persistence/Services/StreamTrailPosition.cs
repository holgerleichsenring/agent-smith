namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-26-0f19a: a run event's trail position, read off the id Redis gave its stream entry. Redis
/// mints "&lt;milliseconds&gt;-&lt;sequence&gt;", strictly increasing per stream and the same for every
/// reader, so every replica and every restart records one event under one position — which is what the
/// (RunId, EventSeq) identity of every projected row needs. A counted position re-minted the numbers a
/// lost buffer had used, and a replica starting mid-run numbered new events with older events' positions.
/// <para>
/// milliseconds × 1000 + sequence stays below 2^53, the dashboard's integer ceiling, until the year
/// 2255. It is strictly increasing while a stream averages fewer than a thousand entries per
/// millisecond across any backward step of the Redis host's clock — Redis then holds the millisecond and
/// counts the sequence up. Nothing is refused: a refused entry would be retried by the drain forever.
/// </para>
/// </summary>
public static class StreamTrailPosition
{
    private const long PerMillisecond = 1000;

    /// <summary>
    /// The lowest position an entry minted after 2001 can have. A stored position below it was counted
    /// by a process before positions came from the stream.
    /// </summary>
    public const long Floor = 1_000_000_000_000_000;

    public static long Of(string entryId)
    {
        var dash = entryId.IndexOf('-', StringComparison.Ordinal);
        if (dash <= 0) throw new FormatException($"'{entryId}' is not a stream entry id");
        return long.Parse(entryId[..dash], System.Globalization.CultureInfo.InvariantCulture) * PerMillisecond
               + long.Parse(entryId[(dash + 1)..], System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The entry id a derived position came from, for reading the stream after it.</summary>
    public static string ToEntryId(long position) =>
        $"{position / PerMillisecond}-{position % PerMillisecond}";
}
