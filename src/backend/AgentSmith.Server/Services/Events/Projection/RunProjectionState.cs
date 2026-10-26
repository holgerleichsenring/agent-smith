namespace AgentSmith.Server.Services.Events.Projection;

/// <summary>
/// 2026-10-26-0f19c: one run's projection on this replica — the gate that serialises it, the tenure
/// it was last caught up in, and the highest stream position it has written in that tenure.
/// </summary>
public sealed class RunProjectionState
{
    public SemaphoreSlim Gate { get; } = new(1, 1);

    public long Tenure { get; set; }

    public long Through { get; set; } = long.MinValue;
}
