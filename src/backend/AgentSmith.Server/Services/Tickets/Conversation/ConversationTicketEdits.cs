using System.Text.Json;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using Kinds = AgentSmith.Server.Models.TicketConversationKinds;
using Roles = AgentSmith.Server.Models.TicketConversationRoles;
using Sources = AgentSmith.Server.Models.TicketConversationSources;

namespace AgentSmith.Server.Services.Tickets.Conversation;

/// <summary>
/// The ticket edits between attempts, out of <see cref="ConversationRunEntries"/> (2026-11-22-e812): wherever the text
/// an attempt left behind differs from the next attempt's fetch, someone edited the ticket in between.
/// </summary>
public static class ConversationTicketEdits
{
    // 2026-11-22-e812: an attempt that rewrote the ticket from its approved specs left that text behind, so the next
    // attempt compares with it, and the diff's before is that text — a person's edit is only what a person changed.
    public static IEnumerable<TicketConversationEntryView> Of(IReadOnlyList<Run> attempts, ConversationRunRows rows)
    {
        var fetched = attempts.Select(run => (Run: run, Attempt: rows.Attempts.FirstOrDefault(a => a.RunId == run.Id),
                Text: FirstOf(rows, run.Id, RunConversationKinds.TicketText),
                Left: FirstOf(rows, run.Id, RunConversationKinds.TicketRewritten, last: true) ?? FirstOf(rows, run.Id, RunConversationKinds.TicketText)))
            .Where(f => f.Attempt?.TicketFingerprint is not null).ToList();
        for (var i = 1; i < fetched.Count; i++)
        {
            var (before, after) = (fetched[i - 1], fetched[i]);
            if ((before.Attempt!.LeftTicketFingerprint ?? before.Attempt.TicketFingerprint) == after.Attempt!.TicketFingerprint) continue;
            yield return new TicketConversationEntryView(
                $"{Sources.Ticket}:{after.Run.Id}:{after.Text?.EventSeq?.ToString() ?? "fetch"}", null, null, Sources.Ticket,
                Kinds.Edited, Roles.Person, null, false, after.Text?.At ?? after.Run.ActsReadAt ?? after.Run.StartedAt,
                after.Run.Id, null, null, Detail: Diff(before.Run, before.Left, after.Run, after.Text));
        }
    }

    private static RunConversationEntry? FirstOf(ConversationRunRows rows, string runId, string kind, bool last = false)
    {
        var ordered = rows.Entries.Where(e => e.RunId == runId && e.Kind == kind).OrderBy(e => e.EventSeq).ThenBy(e => e.Id);
        return last ? ordered.LastOrDefault() : ordered.FirstOrDefault();
    }

    private static Dictionary<string, object> Diff(Run beforeRun, RunConversationEntry? before, Run afterRun, RunConversationEntry? after)
    {
        var diff = new Dictionary<string, object>(StringComparer.Ordinal);
        Changed(diff, "title", beforeRun.TicketTitle, afterRun.TicketTitle);
        Changed(diff, "description", before?.Text, after?.Text);
        Changed(diff, "criteria", ConversationRunEntries.Criteria(before), ConversationRunEntries.Criteria(after));
        return diff;
    }

    private static void Changed(Dictionary<string, object> diff, string field, string? before, string? after)
    {
        if (!string.Equals(before ?? string.Empty, after ?? string.Empty, StringComparison.Ordinal))
            diff[field] = new { Before = before, After = after };
    }
}
