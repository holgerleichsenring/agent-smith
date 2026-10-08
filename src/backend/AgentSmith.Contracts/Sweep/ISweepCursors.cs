namespace AgentSmith.Contracts.Sweep;

/// <summary>2026-10-08-9e6e: the persisted cursors of the change sweep; a write only moves forward.</summary>
public interface ISweepCursors
{
    Task<SweepPosition?> GetAsync(string source, CancellationToken cancellationToken);

    Task AdvanceAsync(string source, SweepPosition position, CancellationToken cancellationToken);
}
