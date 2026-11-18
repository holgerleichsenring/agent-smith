using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Series;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-17-0207a: a beat's first step — the series record named by the beat's seed, its tickets' facts, and what
/// needs the master's judgement, handed on as one <see cref="SeriesBeat"/>. A series that ended since the beat was
/// launched ends the beat quietly: nothing is said and nothing acts.
/// </summary>
public sealed class ReadSeriesHandler(ISeriesRunStore store, ISeriesTicketFacts facts, TimeProvider clock)
    : ICommandHandler<ReadSeriesContext>
{
    public async Task<CommandResult> ExecuteAsync(ReadSeriesContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<string>(ContextKeys.SeriesRunId, out var id) || string.IsNullOrEmpty(id))
            return CommandResult.Fail($"A series-run beat is started with ContextKeys.{nameof(ContextKeys.SeriesRunId)}; this one has none.");
        var record = await store.GetAsync(id!, cancellationToken);
        if (record is null) return CommandResult.Fail($"Series run {id} does not exist.");
        if (!record.IsActive) return CommandResult.Ok($"Series run {id} is {record.State}; this beat has nothing to do.");
        var beat = new SeriesBeat(record, await facts.ReadAsync(record.Project, record.Tickets, cancellationToken), clock.GetUtcNow());
        beat.Reasons.AddRange(SeriesJudgement.NewlyRed(record, beat.Rows));
        context.Pipeline.Set(ContextKeys.SeriesBeat, beat);
        return CommandResult.Ok($"Series run {id}: {beat.Facts.Count(f => f.Landed)} of {beat.Facts.Count} landed"
            + (beat.Reasons.Count == 0 ? string.Empty : $"; to judge: {string.Join("; ", beat.Reasons)}"));
    }
}
