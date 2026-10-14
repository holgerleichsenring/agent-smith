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

    /// <summary>
    /// Where a person opens the ticket in its tracker, or null when the read gave none. GitHub and
    /// GitLab state it in the issue payload; Jira and Azure DevOps compose it from the connection.
    /// </summary>
    public string? WebUrl { get; }

    /// <summary>
    /// Whether the tracker itself counts the ticket done or closed — Jira's Done status category,
    /// an Azure DevOps state in the Completed or Removed category, a closed GitHub or GitLab issue.
    /// It consults no configured status name; whether agent-smith put it there is not its question.
    /// </summary>
    public bool IsClosed { get; }

    /// <summary>The format the description was written in, one of <see cref="TicketTextFormat"/>.</summary>
    public string DescriptionFormat { get; }

    /// <summary>The format the acceptance criteria were written in, one of <see cref="TicketTextFormat"/>.</summary>
    public string CriteriaFormat { get; }

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
        string? kind = null,
        string? webUrl = null,
        bool isClosed = false,
        string descriptionFormat = TicketTextFormat.Text,
        string criteriaFormat = TicketTextFormat.Text)
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
        WebUrl = webUrl;
        IsClosed = isClosed;
        DescriptionFormat = descriptionFormat;
        CriteriaFormat = criteriaFormat;
    }

    /// <summary>
    /// The same ticket with the given fields replaced; a null argument keeps the current value.
    /// The ONE copy of a ticket: the fetch door rewrites every fetched ticket through it, so a field
    /// left out here would be silently dropped everywhere.
    /// </summary>
    public Ticket With(string? description = null, string? webUrl = null, bool? isClosed = null) =>
        new(Id, Title, description ?? Description, AcceptanceCriteria, Status, Source, Labels,
            Assignee, Reporter, Kind, webUrl ?? WebUrl, isClosed ?? IsClosed, DescriptionFormat, CriteriaFormat);
}
