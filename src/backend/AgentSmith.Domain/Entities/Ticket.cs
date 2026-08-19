using AgentSmith.Domain.Models;

namespace AgentSmith.Domain.Entities;

/// <summary>
/// Represents a work item fetched from any ticket provider.
/// Labels carry the platform's user-facing tags as plain strings; they are
/// populated by ListByLifecycleStatusAsync so polling can route by
/// pipeline_from_label like webhooks do.
/// <para>
/// p0454: the ticket also carries the people it names. Without them a comment that
/// waits for an answer is addressed to nobody, and a parked run is only noticed by
/// whoever happens to open the dashboard.
/// </para>
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

    /// <summary>Who the ticket is assigned to, or null when nobody is.</summary>
    public TicketPerson? Assignee { get; }

    /// <summary>Who opened the ticket, or null when the provider did not say.</summary>
    public TicketPerson? Reporter { get; }

    public Ticket(
        TicketId id,
        string title,
        string description,
        string? acceptanceCriteria,
        string status,
        string source,
        IReadOnlyList<string>? labels = null,
        TicketPerson? assignee = null,
        TicketPerson? reporter = null)
    {
        Id = id;
        Title = title;
        Description = description;
        AcceptanceCriteria = acceptanceCriteria;
        Status = status;
        Source = source;
        Labels = labels ?? Array.Empty<string>();
        Assignee = assignee;
        Reporter = reporter;
    }
}
