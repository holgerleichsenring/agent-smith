namespace AgentSmith.Server.Models;

/// <summary>Where a ticket series was read from: a published cut, or the run record of attempts that predate it.</summary>
public static class TicketSeriesSources
{
    public const string Cut = "cut";
    public const string RunRecord = "run-record";

    /// <summary>2026-11-17-a0b6b: the ticket branch's set, before any attempt published a cut.</summary>
    public const string Branch = "branch";
}
