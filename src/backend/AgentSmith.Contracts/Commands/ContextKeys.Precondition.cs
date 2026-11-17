namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-16-f3fbc: the key a precondition release rides in a run's initial context.</summary>
public static partial class ContextKeys
{
    /// <summary>2026-11-16-f3fbc: true when agent-smith restarted this attempt itself because the precondition the
    /// previous attempt stopped on now holds (bool, or a JSON true after the capacity queue's round-trip). Seeded only
    /// by the spawn funnel; read by the attempt's cause.</summary>
    public const string PreconditionRelease = "PreconditionRelease";
}
