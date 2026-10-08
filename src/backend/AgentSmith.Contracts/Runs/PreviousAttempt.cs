namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-10-08-7c0e: the ticket's newest code attempt before the current run — the Run row, not a
/// guess. <see cref="StartedAt"/> is the server's clock at launch; a resumed row carries its resume
/// time, so "since the previous attempt" means since it last ran. <see cref="Finished"/> is true once a
/// terminal status landed (success, shortfall, failed, cancelled).
/// </summary>
public sealed record PreviousAttempt(string RunId, string Status, DateTimeOffset StartedAt, bool Finished)
{
    /// <summary>The skew allowed between a tracker's clock and the server's: a comment written within
    /// this margin of the attempt's start is read as belonging to that attempt.</summary>
    public static readonly TimeSpan SkewMargin = TimeSpan.FromMinutes(1);

    /// <summary>True when <paramref name="at"/> is clearly after this attempt started.</summary>
    public bool Precedes(DateTimeOffset at) => at > StartedAt + SkewMargin;
}
