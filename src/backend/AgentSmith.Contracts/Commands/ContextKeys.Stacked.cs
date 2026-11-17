namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-17-9a39b: the key a stacked start rides in a run's initial context.</summary>
public static partial class ContextKeys
{
    /// <summary>2026-11-17-9a39b: the id of the unlanded prerequisite ticket whose pushed branch this run is cut from
    /// and opens its pull request against (string, or a JSON string after the capacity queue's round-trip). Stamped by
    /// the claim under its lock; read through RunParentTicket.</summary>
    public const string StackedOnTicket = "StackedOnTicket";
}
