using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Tickets;

/// <summary>
/// p0454: the line a WAITING ticket comment ends with — who this run is waiting for,
/// written so the platform actually delivers a notification.
/// <para>
/// Only comments that wait for a person get one: open questions, the expectation to
/// ratify, the hand-back. A cancelled run, a failed run and the derived-spec notice
/// report what happened and ask for nothing — the line p0448 drew from the other
/// side, and the reason those three do not reach anyone's inbox at 07:00.
/// </para>
/// <para>
/// The mention markup is the only thing that differs per platform, and it differs the
/// same way in every comment kind — so it is rendered once here rather than in each
/// template.
/// </para>
/// </summary>
public static class TicketMention
{
    /// <summary>
    /// What the comment says when nobody can be named. An unnoticed non-ping is the
    /// same defect one level up: a comment that looks addressed and reaches no one.
    /// </summary>
    public const string NobodyToNotify =
        "No assignee on this ticket — nobody was notified.";

    /// <summary>
    /// Assignee first, reporter when the ticket is unassigned, and the plain statement
    /// that nobody was reached when the provider named neither.
    /// </summary>
    public static string WaitingLine(TrackerType platform, Ticket? ticket)
    {
        var person = ticket?.Assignee ?? ticket?.Reporter;
        return person is null ? NobodyToNotify : $"Waiting for {Render(platform, person)}.";
    }

    private static string Render(TrackerType platform, TicketPerson person) => platform switch
    {
        // The identity GUID is what triggers the mail; the visible text is only what a
        // reader sees.
        TrackerType.AzureDevOps =>
            $"""<a href="#" data-vss-mention="version:2.0,{person.ProviderId}">@{person.DisplayName}</a>""",
        // The pre-GDPR [~username] form stopped resolving on Jira Cloud — it renders as
        // literal text and notifies nobody.
        TrackerType.Jira => $"[~accountid:{person.ProviderId}]",
        _ => $"@{person.ProviderId}",
    };
}
