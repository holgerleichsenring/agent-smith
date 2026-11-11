namespace AgentSmith.Contracts.Turns;

/// <summary>
/// 2026-11-10-d854c: told the run id of the design turn it was set for, as soon as the turn's pipeline has one, so the
/// conversation names the running turn's run even if the server dies before the turn ends.
/// <para>An implementation must not throw: the turn's grounding awaits it.</para>
/// </summary>
public interface ITurnRunRecorder
{
    Task RecordAsync(string runId, CancellationToken cancellationToken);
}
