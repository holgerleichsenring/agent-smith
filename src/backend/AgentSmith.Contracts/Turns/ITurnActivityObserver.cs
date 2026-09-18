namespace AgentSmith.Contracts.Turns;

/// <summary>
/// Told what the turn that set it is doing, step by step, while it runs.
/// <para>
/// An implementation must not throw: the report is awaited on the path that serves the work
/// it announces, and a progress line that failed would cost the answer.
/// </para>
/// </summary>
public interface ITurnActivityObserver
{
    Task ReportAsync(TurnActivity activity, CancellationToken cancellationToken);
}
