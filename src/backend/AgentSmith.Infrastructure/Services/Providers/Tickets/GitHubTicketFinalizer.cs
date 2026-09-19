using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Owns GitHub's finalize and the classification that goes with it. A GitHub issue has two
/// states, open and closed; every other status name is applied as a LABEL, which leaves the
/// issue open — and the issue's STATE is what this system reads as the ticket's status. So
/// GitHub sends no rejection to learn the answer from; the vocabulary is the answer.
/// <para>
/// Both writes still go out, exactly as before: the comment is the run's one word to a human,
/// and the label is the mark an operator configured. Only the ANSWER is new.
/// </para>
/// </summary>
public sealed class GitHubTicketFinalizer(
    Func<TicketId, string, CancellationToken, Task> comment,
    Func<TicketId, string, CancellationToken, Task> close,
    Func<TicketId, string, CancellationToken, Task> transition)
{
    private static readonly string[] IssueStates = ["open", "closed"];

    public async Task<TicketFinalizeResult> FinalizeAsync(
        TicketId ticketId, string body, string? doneStatus, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(doneStatus))
        {
            await close(ticketId, body, cancellationToken);
            return TicketFinalizeResult.Moved();
        }

        await comment(ticketId, body, cancellationToken);
        await transition(ticketId, doneStatus, cancellationToken);
        return IssueStates.Contains(doneStatus, StringComparer.OrdinalIgnoreCase)
            ? TicketFinalizeResult.Moved()
            : TicketFinalizeResult.NotExpressible(doneStatus);
    }
}
