namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-17-9a39b: the key a stacked start rides in a run's initial context.</summary>
public static partial class ContextKeys
{
    /// <summary>2026-11-17-9a39b: the id of the unlanded prerequisite ticket whose pushed branch this run is cut from
    /// and opens its pull request against (string, or a JSON string after the capacity queue's round-trip). Stamped by
    /// the claim under its lock; read through RunParentTicket. 2026-11-23-31dca: a map repo -> ticket id (a JSON object after
    /// the round-trip), one base per repo; the old single-id string still reads as that id for every repo.</summary>
    public const string StackedOnTicket = "StackedOnTicket";

    /// <summary>2026-11-24-f9c5b: the series branch a ticket of a series with one is cut from and opens its pull requests
    /// against (string), stamped by the claim under its lock; read through RunParentTicket.</summary>
    public const string SeriesBranch = "SeriesBranch";
}
