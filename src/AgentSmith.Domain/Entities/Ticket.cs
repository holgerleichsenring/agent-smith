using AgentSmith.Domain.Models;

namespace AgentSmith.Domain.Entities;

/// <summary>
/// Represents a work item fetched from any ticket provider.
/// Labels carry the platform's user-facing tags as plain strings; they are
/// populated by ListByLifecycleStatusAsync so polling can route by
/// pipeline_from_label like webhooks do.
/// </summary>
public sealed class Ticket
{
    public TicketId Id { get; }
    public string Title { get; }
    public string Description { get; }
    public string? AcceptanceCriteria { get; }
    public string Status { get; }
    public string Source { get; }
    public IReadOnlyList<string> Labels { get; }

    public Ticket(
        TicketId id,
        string title,
        string description,
        string? acceptanceCriteria,
        string status,
        string source,
        IReadOnlyList<string>? labels = null)
    {
        Id = id;
        Title = title;
        Description = description;
        AcceptanceCriteria = acceptanceCriteria;
        Status = status;
        Source = source;
        Labels = labels ?? Array.Empty<string>();
    }
}
