namespace AgentSmith.Server.Services.Tickets.Runway;

/// <summary>2026-10-13-8762v: what the runway answers — the next attempt starts, holds (one of eleven codes) or
/// the series is complete.</summary>
public static class SeriesHeldReasons
{
    public const string Start = "start";
    public const string Hold = "hold";
    public const string Complete = "complete";

    public const string Off = "off";
    public const string Misconfigured = "misconfigured";
    public const string HandedBack = "handed-back";
    public const string Cancelled = "cancelled";
    public const string Incomplete = "incomplete";
    public const string NoProgress = "no-progress";
    public const string BudgetUsd = "budget-usd";
    public const string WallClock = "wall-clock";
    public const string DefaultPending = "default-pending";
    public const string Refused = "refused";
    public const string Closed = "closed";

    /// <summary>The holds the runway itself causes, said once on the ticket. A hand-back, a failure and a cancel are
    /// told by the attempt's own end; off is the operator's own choice; refused and closed are marks of their own.</summary>
    public static readonly IReadOnlySet<string> Spoken =
        new HashSet<string>(StringComparer.Ordinal) { NoProgress, BudgetUsd, WallClock, DefaultPending, Misconfigured };
}
