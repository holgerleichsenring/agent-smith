namespace AgentSmith.Contracts.Tickets;

/// <summary>
/// 2026-10-14-dbceg: how a ticket landed — its pull requests ended with one merged, or a person said
/// it is done by hand.
/// </summary>
public static class TicketLandingKinds
{
    public const string Merged = "merged";
    public const string ByHand = "by-hand";
}
