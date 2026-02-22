using AgentSmith.Domain.Models;

namespace AgentSmith.Domain.Entities;

/// <summary>
/// Represents a work item fetched from any ticket provider.
/// </summary>
public sealed class Ticket
{
    public TicketId Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string? AcceptanceCriteria { get; }
    public string Status { get; }
    public string Source { get; }

    public Ticket(
        TicketId id,
        string title,
        string description,
        string? acceptanceCriteria,
        string status,
        string source)
    {
        Id = id;
        Title = title;
        Description = description;
        AcceptanceCriteria = acceptanceCriteria;
        Status = status;
        Source = source;
    }
}
