namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-17-dd05b: the step of the 'ticket-body' run.</summary>
public static partial class CommandNames
{
    /// <summary>Re-renders a ticket's body region from the specs on its branch and stamps the branch with its fingerprint.</summary>
    public const string RenderTicketBody = "RenderTicketBodyCommand";
}
