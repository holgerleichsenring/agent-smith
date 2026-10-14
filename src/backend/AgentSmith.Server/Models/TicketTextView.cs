using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Models;

/// <summary>
/// The ticket itself as its page shows it: read from the tracker, else the newest copy an attempt kept, else
/// unavailable with a classified reason. Description and criteria carry the format their source wrote them in.
/// </summary>
/// <param name="Source">One of <see cref="TicketTextSources"/>.</param>
/// <param name="At">When the tracker answer was read, or when the attempt kept its copy; null when unavailable.</param>
/// <param name="RunId">The attempt whose copy a snapshot is.</param>
/// <param name="Attempt">That attempt's number, counted as the detail counts them.</param>
/// <param name="Reason">Why the tracker could not be read, one of <see cref="TicketTextReasons"/>.</param>
public sealed record TicketTextView(
    string Source,
    string? Title,
    string? Description,
    string DescriptionFormat,
    string? AcceptanceCriteria,
    string CriteriaFormat,
    string? WorkItemType,
    string? State,
    IReadOnlyList<string>? Labels,
    string? Assignee,
    string? WebUrl,
    DateTimeOffset? At,
    string? RunId = null,
    int? Attempt = null,
    string? Reason = null)
{
    public static TicketTextView FromTracker(Ticket ticket, DateTimeOffset at) =>
        new(TicketTextSources.Tracker, ticket.Title, ticket.Description, ticket.DescriptionFormat,
            ticket.AcceptanceCriteria, ticket.CriteriaFormat, ticket.Kind, ticket.Status, ticket.Labels,
            ticket.Assignee?.DisplayName, ticket.WebUrl, at);

    public static TicketTextView FromSnapshot(
        string? title, string? description, string? criteria, DateTimeOffset at, string runId, int attempt) =>
        new(TicketTextSources.Snapshot, title, description, TicketTextFormat.Text, criteria, TicketTextFormat.Text,
            null, null, null, null, null, at, runId, attempt);

    public static TicketTextView Unavailable(string reason) =>
        new(TicketTextSources.Unavailable, null, null, TicketTextFormat.Text, null, TicketTextFormat.Text,
            null, null, null, null, null, null, Reason: reason);
}
