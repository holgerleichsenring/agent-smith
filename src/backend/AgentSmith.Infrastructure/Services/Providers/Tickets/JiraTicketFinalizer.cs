using System.Net;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Owns Jira's finalize and the classification that goes with it. Jira moves an issue only
/// along a workflow transition, and the issue's own transition list decides whether one lands
/// on the configured status. No match leaves the issue where it is — which the transitioner
/// already reports as false, and which this turns into the caller's answer. The close path
/// answers the same way: it is a transition to the tracker's default status, and a workflow
/// that offers none leaves the issue open just as visibly.
/// <para>
/// A transition Jira answers with 400 — the screen wants a field the run cannot fill — is the
/// same fact arriving as a status code instead of a false: the issue did not move, and it will
/// not move for the next run either. Every other status code propagates, because a 500 or a
/// throttle is a failure that may pass and must not be recorded as a refusal.
/// </para>
/// </summary>
public sealed class JiraTicketFinalizer(
    string defaultStatus,
    Func<TicketId, string, CancellationToken, Task> comment,
    Func<TicketId, string, CancellationToken, Task<bool>> close,
    Func<TicketId, string, CancellationToken, Task<bool>> transition)
{
    public async Task<TicketFinalizeResult> FinalizeAsync(
        TicketId ticketId, string body, string? doneStatus, CancellationToken cancellationToken)
    {
        var wanted = string.IsNullOrWhiteSpace(doneStatus) ? defaultStatus : doneStatus!;
        try
        {
            if (string.IsNullOrWhiteSpace(doneStatus))
                return Answer(await close(ticketId, body, cancellationToken), wanted);

            await comment(ticketId, body, cancellationToken);
            return Answer(await transition(ticketId, doneStatus, cancellationToken), wanted);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return TicketFinalizeResult.Rejected(wanted, nameof(HttpRequestException));
        }
    }

    private static TicketFinalizeResult Answer(bool transitioned, string wanted) =>
        transitioned ? TicketFinalizeResult.Moved() : TicketFinalizeResult.NoTransition(wanted);
}
