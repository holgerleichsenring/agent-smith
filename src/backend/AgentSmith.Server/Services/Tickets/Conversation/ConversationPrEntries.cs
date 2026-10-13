using System.Text.RegularExpressions;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Webhooks;
using Roles = AgentSmith.Server.Models.TicketConversationRoles;
using Sources = AgentSmith.Server.Models.TicketConversationSources;

namespace AgentSmith.Server.Services.Tickets.Conversation;

/// <summary>
/// 2026-10-13-9a59h: a ticket's pull-request records as conversation entries, one per (PrKey, HostId) —
/// a decision with a body is the note that carries it. A host-written note (an Azure DevOps vote comment, a
/// GitLab system note) is the system's and names nobody. Our command answers are read by their own text:
/// one that started nothing is labelled so, one naming a run belongs to that attempt.
/// </summary>
public static partial class ConversationPrEntries
{
    private const string StartedNothing = "started nothing";

    public static IEnumerable<TicketConversationEntryView> Of(
        IEnumerable<PrReviewRecordView> records, IReadOnlySet<string> attemptRunIds) =>
        records.SelectMany(r => r.Entries.Select(e => Entry(r, e, attemptRunIds)));

    private static TicketConversationEntryView Entry(PrReviewRecordView record, PrReviewEntryView entry, IReadOnlySet<string> attempts)
    {
        var view = new TicketConversationEntryView(
            $"{Sources.PrReview}:{record.PrKey}:{entry.HostId}", entry.HostId, record.PrKey, Sources.PrReview, entry.Kind,
            RoleOf(entry), entry.IsSystem ? null : entry.AuthorLogin, entry.IsOurs, entry.At, null, null, entry.Body,
            entry.File, entry.Line,
            new { entry.Decision, entry.Standing, entry.Resolved, entry.ThreadHostId, entry.Truncated, entry.IsBot },
            record.Url);
        if (!entry.IsOurs || !entry.Body.TrimStart().StartsWith(PrCommandTexts.Marker, StringComparison.Ordinal)) return view;
        if (entry.Body.Contains(StartedNothing, StringComparison.OrdinalIgnoreCase))
            return view with { Kind = TicketConversationKinds.CommandAnswer, Detail = new { StartedNothing = true } };
        var named = NamedRun().Match(entry.Body) is { Success: true } match ? match.Groups[1].Value : null;
        return view with
        {
            Kind = TicketConversationKinds.CommandAnswer,
            AttemptRunId = named is not null && attempts.Contains(named) ? named : null,
            Detail = new { StartedNothing = false, RunId = named },
        };
    }

    private static string RoleOf(PrReviewEntryView entry) =>
        entry.IsSystem ? Roles.System
        : entry.IsOurs ? Roles.Agent
        : entry.Decision is not null || entry.Kind != PrReviewEntryKinds.Comment ? Roles.Reviewer
        : Roles.Person;

    [GeneratedRegex(@"run `([^`]+)`")]
    private static partial Regex NamedRun();
}
