using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Series;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: the 'file' run's last step. A filing seeded with a series id — the series' own verify filing its
/// follow-up — joins that series; otherwise a filing that created two or more tickets opens a series run for its
/// conversation, owned by the approver. One ticket is no series: it has its own ticket conversation. Never fails the
/// filing: the tickets exist either way.
/// <para>2026-11-24-f9c5b: the filing step already opened or joined the series before starting its tickets
/// (<see cref="SeriesMembership"/>); this step reports the opened one, and a join here adds nothing new and keeps the
/// follow-up's start on the verdict. A series it opens itself (no membership ran) gets its branch all the same.</para>
/// </summary>
public sealed class OpenSeriesRunHandler(ISeriesRunStore store, TimeProvider clock) : ICommandHandler<OpenSeriesRunContext>
{
    public async Task<CommandResult> ExecuteAsync(OpenSeriesRunContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<FilingRequest>(ContextKeys.FilingRequest, out var request) || request is null
            || !pipeline.TryGet<FilingReportSlot>(ContextKeys.FilingReport, out var slot) || slot?.Report is not { } report)
            return CommandResult.Ok("No filing to own: no series run opened.");
        var tickets = Members(report.Filed, followUp: pipeline.Has(ContextKeys.SeriesRunId), clock);
        if (pipeline.TryGet<string>(ContextKeys.SeriesRunId, out var joined) && !string.IsNullOrEmpty(joined))
            return await JoinAsync(joined!, tickets, NotStarted(report), cancellationToken);
        if (pipeline.TryGet<string>(ContextKeys.SeriesRunOpened, out var opened) && !string.IsNullOrEmpty(opened))
            return CommandResult.Ok($"Opened series run {opened} over {tickets.Count} tickets.");
        if (tickets.Count < 2)
            return CommandResult.Ok($"{tickets.Count} ticket filed: a single ticket is worked in its own conversation, no series run opened.");
        var id = await new SeriesMembership(store, clock).OpenAsync(request, context.Project.Name, tickets, cancellationToken);
        return CommandResult.Ok($"Opened series run {id} over {tickets.Count} tickets.");
    }

    private async Task<CommandResult> JoinAsync(
        string id, IReadOnlyList<SeriesTicket> tickets, IReadOnlyDictionary<string, string?> starts, CancellationToken ct)
    {
        var joined = await store.UpdateAsync(id, r => Joined(r, tickets) with
        {
            Verdicts = FiledFor(r.Verdicts, tickets, starts), // 0207d: the check's follow-up, and whether it started
        }, ct);
        return joined is null
            ? CommandResult.Ok($"Series run {id} does not exist; the {tickets.Count} filed ticket(s) stand on their own.")
            : CommandResult.Ok($"{tickets.Count} ticket(s) joined series run {id}.");
    }

    private static IReadOnlyList<SeriesVerdict> FiledFor(
        IReadOnlyList<SeriesVerdict> verdicts, IReadOnlyList<SeriesTicket> tickets, IReadOnlyDictionary<string, string?> starts) =>
        verdicts.Count == 0 || tickets.Count == 0 || verdicts[^1].FollowUpTicketId is not null ? verdicts
            : [.. verdicts.SkipLast(1), verdicts[^1] with
            {
                FollowUpTicketId = tickets[0].TicketId, FollowUpStart = starts.GetValueOrDefault(tickets[0].TicketId),
            }];

    // A filed ticket that did not start, with its reason; a started one maps to null.
    private static IReadOnlyDictionary<string, string?> NotStarted(FilingReport report) =>
        report.Filed.Where(f => f.TicketId is not null).ToDictionary(f => f.TicketId!,
            f => f.Start is { State: FiledStartState.Started } or null ? null : f.Start.Reason, StringComparer.Ordinal);

    /// <summary>The filed tickets as series members.</summary>
    internal static IReadOnlyList<SeriesTicket> Members(IReadOnlyList<FiledTicket> filed, bool followUp, TimeProvider clock) =>
        [.. filed.Where(f => !string.IsNullOrEmpty(f.TicketId))
            .Select(f => new SeriesTicket(f.TicketId!, f.Key ?? f.TicketId!, f.Title, followUp, clock.GetUtcNow()))];

    /// <summary>The series with the tickets it lacks added and running again — a join keeps a pause (1b72a).</summary>
    internal static SeriesRunRecord Joined(SeriesRunRecord r, IReadOnlyList<SeriesTicket> tickets) => r with
    {
        Tickets = [.. r.Tickets, .. tickets.Where(t => r.Tickets.All(m => m.TicketId != t.TicketId))],
        State = r.IsActive && r.State != SeriesRunStates.Paused ? SeriesRunStates.Running : r.State,
        PausedFrom = r.State == SeriesRunStates.Paused ? SeriesRunStates.Running : r.PausedFrom,
    };
}
