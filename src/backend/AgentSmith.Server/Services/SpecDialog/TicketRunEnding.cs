using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>How one run ended, as its rows recorded it — read by <see cref="TicketRunEndingsReader"/>.</summary>
public sealed record TicketRunEnding(
    string? Summary, string? CancelReason, TicketRunFailedStep? FailedStep, TicketRunSandboxEnd? SandboxEnd)
{
    public static TicketRunEnding None { get; } = new(null, null, null, null);
}
