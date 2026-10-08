namespace AgentSmith.Server.Services.Rework;

/// <summary>2026-10-08-e8b9b: the entry's answer, with the run it names when there is one.</summary>
public sealed record ReworkOutcome(ReworkOutcomeKind Kind, string? Reason = null, string? RunId = null, bool Busy = false)
{
    public static ReworkOutcome Started(string? runId) => new(ReworkOutcomeKind.Started, RunId: runId);
    public static readonly ReworkOutcome NotARework = new(ReworkOutcomeKind.NotARework);
    public static readonly ReworkOutcome AlreadyServed = new(ReworkOutcomeKind.AlreadyServed);
    public static ReworkOutcome Refused(string reason, string? runId = null) => new(ReworkOutcomeKind.Refused, reason, runId);

    /// <summary>2026-10-08-e8b9c: refused because a run is working on the ticket right now.</summary>
    public static ReworkOutcome WorkedOn(string? runId) =>
        new(ReworkOutcomeKind.Refused, $"run {runId ?? "(starting)"} is working on this ticket", runId, Busy: true);
}
