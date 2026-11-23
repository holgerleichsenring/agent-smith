using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Tickets.Text;

/// <summary>
/// 2026-11-22-a720b: with an approved set the ticket's text is a transcript of its specs, and the branch set's ticket
/// fingerprint is the text agent-smith last wrote. A text the tracker holds now that differs from it was edited by a
/// person — never overwritten, never drawn as a notice: it is put to the ticket's conversation once per edit (the ticket's
/// ledger spends a key per fingerprint), as a question a reply settles like any other. It runs off the page's read, on
/// the check worker, and reads the tracker past its cache. A ticket a run holds is not judged: the run may have written
/// the text and not yet the branch, and the next read asks again.
/// </summary>
public sealed class TicketTextEditNotice(
    TicketProjects projects, TicketTrackerTextReader tracker, TicketBranchSeries branch, ReworkLedgerRepository ledger,
    TicketTextEditPost post, FiledWorkTrackerProjects trackerProjects, IActiveRunLease leases)
{
    /// <summary>The ledger key prefix one edit is spent under.</summary>
    public const string KeyPrefix = "ticket-text-edited|";

    /// <summary>2026-11-23-31dcg: the mark beside an edit's key once the conversation answered it, under the same fingerprint.</summary>
    public const string AnsweredPrefix = "ticket-text-answered|";

    /// <summary>2026-11-23-31dcg: the fingerprints of the edits the ticket's spent <paramref name="keys"/> put to the
    /// conversation and that no answered mark settles yet.</summary>
    public static IReadOnlyList<string> Open(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var all = keys.ToList();
        var answered = all.Where(k => k.StartsWith(AnsweredPrefix, StringComparison.Ordinal))
            .Select(k => k[AnsweredPrefix.Length..]).ToHashSet(StringComparer.Ordinal);
        return [.. all.Where(k => k.StartsWith(KeyPrefix, StringComparison.Ordinal)).Select(k => k[KeyPrefix.Length..])
            .Where(fingerprint => !answered.Contains(fingerprint)).Distinct(StringComparer.Ordinal)];
    }

    public async Task CheckAsync(string project, string ticketId, CancellationToken ct)
    {
        if (projects.Of(project) is not { } resolved || await HeldAsync(project, ticketId, ct)) return;
        if ((await tracker.ReadAsync(resolved, ticketId, ct, fresh: true)).Ticket is not { } ticket) return;
        var key = TicketKey.For(resolved.Tracker.Type.ToString().ToLowerInvariant(), ticketId);
        var read = await branch.ReadAsync(resolved, key, new TicketId(ticketId), ct);
        if (read.Set is not { Specs.Count: > 0, Approval: not null, TicketFingerprint: { } written } set) return;
        var fingerprint = TicketTextFingerprint.Of(ticket);
        if (string.Equals(fingerprint, written, StringComparison.Ordinal)) return;
        if (!await ledger.TryMarkSpokenAsync(project, ticketId, KeyPrefix + fingerprint, ct)) return;
        await post.PostAsync(resolved, ticketId, Message(ticket, set), ct);
    }

    private async Task<bool> HeldAsync(string project, string ticketId, CancellationToken ct)
    {
        foreach (var sharing in trackerProjects.SharingTrackerWith(project))
            if (await leases.GetByTicketAsync(sharing.Name, new TicketId(ticketId), ct) is not null) return true;
        return false;
    }

    /// <summary>What the conversation is told: that the text changed, the goals it no longer states, the text now.</summary>
    public static string Message(Ticket ticket, SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(set);
        var text = string.Join("\n", new[] { ticket.Description, ticket.AcceptanceCriteria }
            .Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => TicketHtmlConverter.ToText(f)));
        var unsaid = TicketSetDivergence.Between(text, [.. set.Specs.Select(s => s.Draft.Goal)]);
        var goals = unsaid is null ? string.Empty
            : $"\nIt no longer states {unsaid.Unsaid.Count} of its {unsaid.Specs} spec goal(s):\n- " + string.Join("\n- ", unsaid.Unsaid);
        return $"The ticket text was changed in the tracker since agent-smith last wrote it from the approved specs.{goals}\n"
            + "Should this go into the specs? Reply here with what should change; once a new revision is approved, "
            + $"the ticket text is written from the specs again.\n\nThe ticket text now:\n{ticket.Title}\n{text}";
    }
}
