namespace AgentSmith.Contracts.Runs;

/// <summary>
/// The run statuses that mean "stopped without ending". A run in one of these keeps
/// FinishedAt null, keeps its capacity reservation and relaunches onto its own row, so
/// every consumer of a terminal RunFinished has to ask which kind it is rather than
/// switching on the event type alone.
/// </summary>
public static class RunStatuses
{
    public const string Queued = "queued";
    public const string WaitingForInput = "waiting_for_input";

    /// <summary>True when the status is a pause, not an ending.</summary>
    public static bool IsWaiting(string? status) => status is Queued or WaitingForInput;

    /// <summary>True when a run's terminal event is a pause rather than an ending — the
    /// question every consumer of RunFinished has to ask before it releases anything.</summary>
    public static bool IsPause(Contracts.Events.RunEvent runEvent) =>
        runEvent is Contracts.Events.RunFinishedEvent finished && IsWaiting(finished.Status);
}
