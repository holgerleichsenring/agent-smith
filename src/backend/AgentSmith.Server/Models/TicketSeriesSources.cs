namespace AgentSmith.Server.Models;

/// <summary>Where a ticket series was read from: a published cut, or the run record of attempts that predate it.</summary>
public static class TicketSeriesSources
{
    public const string Cut = "cut";
    public const string RunRecord = "run-record";
}
