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
            return await JoinAsync(joined!, tickets, cancellationToken);
        if (tickets.Count < 2)
            return CommandResult.Ok($"{tickets.Count} ticket filed: a single ticket is worked in its own conversation, no series run opened.");
        var record = new SeriesRunRecord(SeriesRunIds.Mint(), request.Conversation, context.Project.Name,
            request.Principal, request.MayStartRuns, SeriesRunStates.Running, tickets, clock.GetUtcNow());
        await store.OpenAsync(record, cancellationToken);
        return CommandResult.Ok($"Opened series run {record.Id} over {tickets.Count} tickets.");
    }

    private async Task<CommandResult> JoinAsync(string id, IReadOnlyList<SeriesTicket> tickets, CancellationToken ct)
    {
        var joined = await store.UpdateAsync(id, r => r with
        {
            Tickets = [.. r.Tickets, .. tickets.Where(t => r.Tickets.All(m => m.TicketId != t.TicketId))],
            State = r.IsActive ? SeriesRunStates.Running : r.State,
        }, ct);
        return joined is null
            ? CommandResult.Ok($"Series run {id} does not exist; the {tickets.Count} filed ticket(s) stand on their own.")
            : CommandResult.Ok($"{tickets.Count} ticket(s) joined series run {id}.");
    }

    private IReadOnlyList<SeriesTicket> Members(FilingReport report, bool followUp) =>
        [.. report.Filed.Where(f => !string.IsNullOrEmpty(f.TicketId))
            .Select(f => new SeriesTicket(f.TicketId!, f.Key ?? f.TicketId!, f.Title, followUp, clock.GetUtcNow()))];
}
