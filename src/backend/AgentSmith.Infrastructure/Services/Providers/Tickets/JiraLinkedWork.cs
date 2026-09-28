using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-09-28-1da5c: Jira REFUSES, and names what it would need.
/// <para>
/// Branches and pull requests do not live on a Jira issue. They live behind the development-tools
/// integration, on an endpoint Atlassian does not document, keyed by an issue's numeric id rather
/// than its key, and present only where somebody connected a source host to the site. A framework
/// that guessed at it would be reporting an empty board on every installation that has not.
/// </para>
/// <para>
/// It implements the port rather than being absent from it, for the reason
/// <see cref="JiraTicketRewriter"/> already records: a tracker with no implementation is reported
/// as a fact about OUR wiring, and what an operator needs is the fact about THEIR Jira.
/// </para>
/// </summary>
public sealed class JiraLinkedWork : ITicketLinkedWork
{
    public Task<TicketLinkedWorkResult> ForAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticketId);
        return Task.FromResult(TicketLinkedWorkResult.Refused(
            "Jira keeps branches and pull requests in its development-tools integration rather "
            + "than on the issue, behind an endpoint this framework does not speak. What the board "
            + "shows for this ticket cannot be read from here."));
    }
}
