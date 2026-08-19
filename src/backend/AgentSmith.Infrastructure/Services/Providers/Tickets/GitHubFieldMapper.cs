using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0147f: maps Octokit <see cref="Issue"/> instances onto the canonical
/// <see cref="Ticket"/>. Stateless; no per-provider configuration —
/// labels / state / body are read straight off the SDK type.
/// </summary>
public sealed class GitHubFieldMapper : ITicketFieldMapper<Issue>
{
    public Ticket Map(TicketId ticketId, Issue issue)
    {
        var labels = issue.Labels?.Select(l => l.Name).ToList() ?? [];
        return new Ticket(
            ticketId,
            issue.Title,
            issue.Body ?? "",
            acceptanceCriteria: null,
            issue.State.StringValue,
            "GitHub",
            labels,
            Person(issue.Assignee),
            Person(issue.User));
    }

    // p0454: GitHub mentions by @login; Name is optional on an account, so the login
    // doubles as the display name when nobody filled one in.
    private static TicketPerson? Person(User? user) =>
        user is null ? null : TicketPerson.From(
            string.IsNullOrWhiteSpace(user.Name) ? user.Login : user.Name, user.Login);
}
