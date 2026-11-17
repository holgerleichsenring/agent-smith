namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-17-dd05b: what the server seeds into a 'ticket-body' run.</summary>
public static partial class ContextKeys
{
    /// <summary><c>TicketBodyRequest</c> — the ticket whose body to render from its branch, and who asked. Seeded by the server.</summary>
    public const string TicketBodyRequest = "TicketBodyRequest";
}
