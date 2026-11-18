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
/// </summary>
public sealed class OpenSeriesRunHandler(ISeriesRunStore store, TimeProvider clock) : ICommandHandler<OpenSeriesRunContext>
{
    public async Task<CommandResult> ExecuteAsync(OpenSeriesRunContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<FilingRequest>(ContextKeys.FilingRequest, out var request) || request is null
            || !pipeline.TryGet<FilingReportSlot>(ContextKeys.FilingReport, out var slot) || slot?.Report is not { } report)
            return CommandResult.Ok("No filing to own: no series run opened.");
        var tickets = Members(report, followUp: pipeline.Has(ContextKeys.SeriesRunId));
        if (pipeline.TryGet<string>(ContextKeys.SeriesRunId, out var joined) && !string.IsNullOrEmpty(joined))
            return await JoinAsync(joined!, tickets, NotStarted(report), cancellationToken);
        if (tickets.Count < 2)
            return CommandResult.Ok($"{tickets.Count} ticket filed: a single ticket is worked in its own conversation, no series run opened.");
        var record = new SeriesRunRecord(SeriesRunIds.Mint(), request.Conversation, context.Project.Name,
            request.Principal, request.MayStartRuns, SeriesRunStates.Running, tickets, clock.GetUtcNow());
        await store.OpenAsync(record, cancellationToken);
        return CommandResult.Ok($"Opened series run {record.Id} over {tickets.Count} tickets.");
    }

    private async Task<CommandResult> JoinAsync(
        string id, IReadOnlyList<SeriesTicket> tickets, IReadOnlyDictionary<string, string?> starts, CancellationToken ct)
    {
        var joined = await store.UpdateAsync(id, r => r with
        {
            Tickets = [.. r.Tickets, .. tickets.Where(t => r.Tickets.All(m => m.TicketId != t.TicketId))],
            State = r.IsActive ? SeriesRunStates.Running : r.State,
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

    private IReadOnlyList<SeriesTicket> Members(FilingReport report, bool followUp) =>
        [.. report.Filed.Where(f => !string.IsNullOrEmpty(f.TicketId))
            .Select(f => new SeriesTicket(f.TicketId!, f.Key ?? f.TicketId!, f.Title, followUp, clock.GetUtcNow()))];
}
