namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Context keys a run started from a chat channel carries.
/// </summary>
public static partial class ContextKeys
{
    /// <summary>bool riding the claim request — a person asked for this ticket's run by name
    /// (a chat command), so the pipeline is theirs to choose: the claim does not ask whether a
    /// label routes to it, and the capacity queue does not drop the waiting run when the ticket
    /// sits outside its trigger statuses. Arrives as a JsonElement after the queue's JSON
    /// round-trip; read it with <c>RequestedByNameExtensions.IsRequestedByName</c>.</summary>
    public const string RequestedByName = "RequestedByName";
}
