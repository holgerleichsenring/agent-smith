namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0177: per-run, thread-safe counter that caps the total number of
/// sub-agents spawned across all <c>spawn_agents</c> calls.
/// <see cref="TryReserve"/> returns how many of <paramref name="requested"/>
/// the budget can accommodate — may be fewer than asked, may be zero,
/// never throws. Surplus tasks are turned into Failed results without
/// an LLM call by SpawnAgentToolHost; the run continues.
///
/// <para>Scoped per run via the DI container (registered as Scoped).
/// Tests instantiate directly and exercise the lock-based reservation
/// path in isolation.</para>
/// </summary>
public sealed class SubAgentBudget
{
    private readonly object _lock = new();
    private int _used;

    public const string RunKey = "limits.max_sub_agents_per_run";
    public const string DialogTurnKey = "limits.max_sub_agents_per_dialog_turn";

    public int MaxPerRun { get; }
    public int Used { get { lock (_lock) return _used; } }
    public int Remaining => Math.Max(0, MaxPerRun - Used);

    /// <summary>2026-11-17-274ca: what a task past the budget is told — the value, the key it is
    /// set under and what it bounds.</summary>
    public string Exhausted { get; }

    public SubAgentBudget(int maxPerRun, string configKey = RunKey, string scope = "run")
    {
        if (maxPerRun < 0) throw new ArgumentOutOfRangeException(nameof(maxPerRun));
        MaxPerRun = maxPerRun;
        Exhausted = $"the budget of {maxPerRun} sub-agents is spent ({configKey}); "
            + $"it bounds what one {scope} spends on children";
    }

    /// <summary>
    /// Reserve up to <paramref name="requested"/> sub-agent slots.
    /// Returns how many were actually granted (0..requested). Atomic
    /// under the internal lock so concurrent callers cannot oversubscribe.
    /// </summary>
    public int TryReserve(int requested)
    {
        if (requested <= 0) return 0;
        lock (_lock)
        {
            var available = MaxPerRun - _used;
            if (available <= 0) return 0;
            var granted = Math.Min(requested, available);
            _used += granted;
            return granted;
        }
    }
}
