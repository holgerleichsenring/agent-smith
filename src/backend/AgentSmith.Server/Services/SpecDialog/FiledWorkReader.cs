using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042ej: what the conversation that filed work is now following. Session → latest
/// filing → ticket ids → runs, across the projects of one tracker.
/// <para>
/// 2026-09-25-c4a6: and what a conversation that filed NOTHING is following — the ticket it is
/// BOUND to (2026-09-25-8e51b), whose runs are its work whoever filed them. The filing is the key
/// only while there is one; the binding is the key when there is not.
/// </para>
/// <para>
/// A READ OF ITS OWN, not a field on the dialog view: that view is refetched after every hub
/// message, and run lookups there would run on every reply.
/// </para>
/// </summary>
public sealed class FiledWorkReader(
    FiledWorkFiling filing,
    FiledWorkBoundTicket bound,
    FiledWorkTrackerProjects trackerProjects,
    FiledWorkRunsReader runs,
    FiledWorkHandbacks handbacks,
    ApprovedSetForConversation approved)
{
    public async Task<FiledWorkView> ReadAsync(string dialogId, CancellationToken ct)
    {
        // 2026-09-25-8e51d: a conversation BOUND to a ticket has an approved set to show even
        // when it filed nothing itself, so this is read before the filing is looked for.
        var set = await approved.ForAsync(dialogId, ct);
        var rows = await RowsAsync(dialogId, ct);
        return rows.Count == 0
            ? FiledWorkView.Empty(dialogId) with { Approved = set }
            : new FiledWorkView(dialogId, rows, set);
    }

    /// <summary>
    /// The tickets this conversation is following: the ones its latest filing created, or — when
    /// it made none — the one it belongs to. Not both: a filing is what this conversation did,
    /// and a conversation that filed is already shown the tickets it filed.
    /// </summary>
    private async Task<IReadOnlyList<FiledWorkTicketView>> RowsAsync(
        string dialogId, CancellationToken ct)
    {
        var latest = await filing.OfAsync(dialogId, ct);
        if (latest is null) return await BoundRowsAsync(dialogId, ct);
        var rows = new List<FiledWorkTicketView>();
        foreach (var ticket in latest.Filed) rows.Add(await RowAsync(ticket, ct));
        return rows;
    }

    /// <summary>
    /// The bound ticket's own row. Its identity is the TRACKER'S ID: a reference is the url a
    /// filing's created ticket carried, the domain ticket has no such field, and an empty string
    /// would be a react key, a test id and a row-to-read match that every unfiled row shared.
    /// The reach is the projects sharing the conversation's tracker, never all of them — a bare
    /// ticket number means different work on two trackers.
    /// </summary>
    private async Task<IReadOnlyList<FiledWorkTicketView>> BoundRowsAsync(
        string dialogId, CancellationToken ct)
    {
        if (await bound.OfAsync(dialogId, ct) is not { } ticket) return [];
        var reach = trackerProjects.OnTracker(ticket.Tracker);
        return
        [
            new FiledWorkTicketView(
                ticket.TicketId, null, ticket.Title, ticket.TicketId, ticket.Project,
                FiledWorkStart.NotFiledHere,
                await runs.ForAsync(reach, ticket.TicketId, ct),
                await handbacks.ForAsync(reach, ticket.TicketId, ct)),
        ];
    }

    /// <summary>
    /// A ticket with no id or no project was filed before 2026-09-17-042eg and shows its
    /// references only; a slice record (2026-09-17-0e79d, filed until 2026-09-22-b3d7 and never
    /// after it) is not work at all, so nothing looks for a run of it.
    /// </summary>
    private async Task<FiledWorkTicketView> RowAsync(FiledTicket ticket, CancellationToken ct)
    {
        if (ticket.TicketId is not { } id || ticket.Project is not { } project
            || ticket.Start?.State == FiledStartState.Record)
            return Row(ticket, [], null);
        var reach = trackerProjects.SharingTrackerWith(project);
        return Row(
            ticket,
            await runs.ForAsync(reach, id, ct),
            await handbacks.ForAsync(reach, id, ct));
    }

    private static FiledWorkTicketView Row(
        FiledTicket ticket, IReadOnlyList<FiledWorkRunView> runs, FiledWorkHandbackView? handback) =>
        new(ticket.Reference, ticket.Key, ticket.Title, ticket.TicketId, ticket.Project,
            ticket.Start, runs, handback);
}
