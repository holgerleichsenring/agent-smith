using System.ComponentModel;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Admin.Read;

/// <summary>2026-11-17-f3a8b: a ticket's own record — its attempts and other runs, and its pull requests with their state.</summary>
public sealed class AdminTicketTools(TicketDetailReader tickets, PrReviewRecordRepository pullRequests)
{
    [Description("Reads a ticket's record in a project: its branch, every attempt and other run with status and cost, "
        + "and its pull requests with their recorded state (open, merged, closed) and head.")]
    public async Task<string> ReadTicketRecords(string project, string ticketId, CancellationToken cancellationToken = default)
    {
        var detail = await tickets.ReadAsync(project, ticketId, cancellationToken);
        var records = await pullRequests.ByTicketAsync(project, ticketId, cancellationToken);
        return detail is null && records.Count == 0
            ? $"No ticket '{ticketId}' is recorded in project '{project}'."
            : AdminToolJson.Render(new { Ticket = detail, PullRequests = records });
    }
}
