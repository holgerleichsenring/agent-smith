using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Owns GitLab's finalize and the classification that goes with it. A GitLab issue is moved
/// by a state_event and the API takes two of them — close and reopen. A status name outside
/// that vocabulary is a value GitLab has nowhere to put, so the state write is not spent on a
/// request that can only 400.
/// <para>
/// The COMMENT is sent first and unconditionally: it is the run's one word to a human, and a
/// status this tracker cannot express must not take it down with it.
/// </para>
/// </summary>
public sealed class GitLabTicketFinalizer(
    Func<TicketId, string, CancellationToken, Task> comment,
    Func<TicketId, string, CancellationToken, Task> close,
    Func<TicketId, string, CancellationToken, Task> transition)
{
    private static readonly string[] IssueStates = ["closed", "close", "opened", "open", "reopen"];

    public async Task<TicketFinalizeResult> FinalizeAsync(
        TicketId ticketId, string body, string? doneStatus, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(doneStatus))
        {
            await close(ticketId, body, cancellationToken);
            return TicketFinalizeResult.Moved();
        }

        await comment(ticketId, body, cancellationToken);
        if (!IssueStates.Contains(doneStatus, StringComparer.OrdinalIgnoreCase))
            return TicketFinalizeResult.NotExpressible(doneStatus);

        await transition(ticketId, doneStatus, cancellationToken);
        return TicketFinalizeResult.Moved();
    }
}
