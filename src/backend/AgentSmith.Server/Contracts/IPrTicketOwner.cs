using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Contracts;

/// <summary>
/// The one configured project that owns a ticket branch's pull request: its repository EQUALS the
/// pull request's and it has a code run on the ticket. Shared by every PR rework act — a Request
/// changes and a comment command.
/// </summary>
public interface IPrTicketOwner
{
    /// <param name="allowedProjects">When given (the PR sweep's polling entry), only these projects are candidates.</param>
    Task<ConfiguredRepo?> FindAsync(
        string repoUrl, string ticketId, IReadOnlySet<string>? allowedProjects, CancellationToken cancellationToken);
}
