using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042ej: what the conversation that filed work is now following. Session → latest
/// filing → ticket ids → runs, across the projects of one tracker.
/// <para>
/// A READ OF ITS OWN, not a field on the dialog view: that view is refetched after every hub
/// message, and run lookups there would run on every reply.
/// </para>
/// </summary>
public sealed class FiledWorkReader(
    FiledWorkFiling filing,
    FiledWorkTrackerProjects trackerProjects,
    FiledWorkRunsReader runs,
    FiledWorkHandbacks handbacks)
{
    public async Task<FiledWorkView> ReadAsync(string dialogId, CancellationToken ct)
    {
        var latest = await filing.OfAsync(dialogId, ct);
        if (latest is null) return FiledWorkView.Empty(dialogId);
        var rows = new List<FiledWorkTicketView>();
        foreach (var ticket in latest.Filed) rows.Add(await RowAsync(ticket, ct));
        return new FiledWorkView(dialogId, rows);
    }

    /// <summary>
    /// A ticket with no id or no project was filed before 2026-09-17-042eg and shows its
    /// references only; a slice record (2026-09-17-0e79d) is not work at all, so nothing looks
    /// for a run of it.
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
