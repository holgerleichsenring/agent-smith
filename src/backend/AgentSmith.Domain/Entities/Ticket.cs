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

    /// <summary>
    /// 2026-09-27-481bf: WHAT THE TRACKER CALLS IT — "Bug", "User Story", "Aufgabe" — in the
    /// tracker's own word, because the vocabulary is not ours: Azure DevOps process templates and
    /// Jira projects both define their own types. Null where a tracker gives none, which is every
    /// GitHub issue: the SDK version pinned here exposes no issue type at all.
    /// </summary>
    public string? Kind { get; }

    public Ticket(
        TicketId id,
        string title,
        string description,
        string? acceptanceCriteria,
        string status,
        string source,
        IReadOnlyList<string>? labels = null,
        TicketPerson? assignee = null,
        TicketPerson? reporter = null,
        string? kind = null)
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
        Kind = kind;
    }

    /// <summary>
    /// 2026-09-18-d518: the same ticket with its description replaced — what the fetch door
    /// publishes once the framework's own label note has been taken out of it.
    /// </summary>
    /// <para>Every field travels: this rewrites EVERY fetched ticket, so one left out here is one
    /// silently dropped everywhere.</para>
    public Ticket WithDescription(string description) =>
        new(Id, Title, description, AcceptanceCriteria, Status, Source, Labels, Assignee, Reporter,
            Kind);
}
