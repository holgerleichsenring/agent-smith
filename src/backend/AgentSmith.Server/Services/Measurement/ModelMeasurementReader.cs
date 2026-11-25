using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Series;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Server.Services.Measurement;

/// <summary>
/// 2026-11-25-9992k: the numbers two models are compared by, per run and per series — the questions put to the operator,
/// what was delivered (specs done in a run, tickets landed in a series), what it cost and what one delivered ticket cost,
/// and per model the calls, tokens (cached and reasoning included) and cost from the recorded calls. The series' landed
/// count is the series page's own (<see cref="SeriesRunDetailReader"/>); its runs are its tickets' runs started since the
/// series began, so a second series over the same tickets is measured apart. Sums are taken after the read: SQLite cannot
/// aggregate a decimal.
/// </summary>
public sealed class ModelMeasurementReader(IUnitOfWork unitOfWork, SeriesRunDetailReader series, ISeriesRunStore store)
{
    public async Task<RunMeasurementView?> RunAsync(string runId, CancellationToken ct)
    {
        var run = await unitOfWork.Set<Run>().AsNoTracking().Where(r => r.Id == runId)
            .Select(r => new { r.Id, r.Project, r.TicketId, r.CostTotalUsd, r.RunMetricsJson }).FirstOrDefaultAsync(ct);
        if (run is null) return null;
        var specsDone = await unitOfWork.Set<RunSpec>().AsNoTracking()
            .CountAsync(s => s.RunId == runId && s.Status == "done", ct);
        return new RunMeasurementView(run.Id, run.Project, run.TicketId, Questions(run.RunMetricsJson), specsDone,
            run.CostTotalUsd, await ModelsAsync([runId], ct));
    }

    public async Task<SeriesMeasurementView?> SeriesAsync(string id, CancellationToken ct)
    {
        if (await store.GetAsync(id, ct) is not { } record
            || await series.ReadAsync(id, ct, withHolds: false) is not { } detail) return null;
        var ticketIds = record.Tickets.Select(t => t.TicketId).ToList();
        // A run of one of its tickets from before the series is another attempt's (a re-run series of the same tickets).
        // Filtered after the read: SQLite cannot compare a DateTimeOffset, and a series' ticket runs are few.
        var runs = (await unitOfWork.Set<Run>().AsNoTracking()
            .Where(r => r.Project == record.Project && ticketIds.Contains(r.TicketId))
            .Select(r => new { r.Id, r.RunMetricsJson, r.CostTotalUsd, r.StartedAt }).ToListAsync(ct))
            .Where(r => r.StartedAt >= record.StartedAt).ToList();
        var ticketQuestions = runs.Sum(r => Questions(r.RunMetricsJson));
        var cost = runs.Sum(r => r.CostTotalUsd) + record.MasterCostUsd;
        var landed = detail.Counts.Landed;
        return new SeriesMeasurementView(record.Id, record.Project, detail.Counts.Total, landed,
            ticketQuestions + record.MasterQuestionsAsked, record.MasterQuestionsAsked, cost, record.MasterCostUsd,
            landed > 0 ? Math.Round(cost / landed, 4) : null, runs.Count, await ModelsAsync([.. runs.Select(r => r.Id)], ct));
    }

    private static int Questions(string? metricsJson) =>
        RunStoryJson.TryDeserialize<RunMetrics>(metricsJson)?.QuestionsAsked ?? 0;

    private async Task<IReadOnlyList<ModelUsageView>> ModelsAsync(IReadOnlyCollection<string> runIds, CancellationToken ct)
    {
        var calls = await unitOfWork.Set<RunLlmCall>().AsNoTracking().Where(c => runIds.Contains(c.RunId))
            .Select(c => new { c.Model, c.TokensIn, c.CachedTokensIn, c.CacheCreationTokensIn, c.TokensOut, c.ReasoningTokensOut, c.CostUsd })
            .ToListAsync(ct);
        return [.. calls.GroupBy(c => c.Model ?? string.Empty, StringComparer.Ordinal)
            .Select(g => new ModelUsageView(g.Key, g.Count(), g.Sum(c => c.TokensIn + c.CachedTokensIn + c.CacheCreationTokensIn),
                g.Sum(c => c.CachedTokensIn), g.Sum(c => c.TokensOut), g.Sum(c => c.ReasoningTokensOut), g.Sum(c => c.CostUsd)))
            .OrderByDescending(m => m.CostUsd).ThenBy(m => m.Model, StringComparer.Ordinal)];
    }
}

/// <summary>2026-11-25-9992k: one model's share of a run or series. <paramref name="InputTokens"/> is the whole prompt,
/// cached share included; <paramref name="ReasoningTokens"/> is inside <paramref name="OutputTokens"/>.</summary>
public sealed record ModelUsageView(
    string Model, int Calls, long InputTokens, long CachedInputTokens, long OutputTokens, long ReasoningTokens, decimal CostUsd);

/// <summary>2026-11-25-9992k: one run's comparison numbers.</summary>
public sealed record RunMeasurementView(
    string RunId, string Project, string TicketId, int QuestionsAsked, int SpecsDelivered, decimal CostUsd,
    IReadOnlyList<ModelUsageView> Models);

/// <summary>2026-11-25-9992k: one series' comparison numbers. <paramref name="CostUsd"/> is its ticket runs and its master;
/// <paramref name="Models"/> covers the ticket runs, the master's beats carry no series id.</summary>
public sealed record SeriesMeasurementView(
    string SeriesId, string Project, int Tickets, int TicketsDelivered, int QuestionsAsked, int MasterQuestionsAsked,
    decimal CostUsd, decimal MasterCostUsd, decimal? CostPerDeliveredTicketUsd, int TicketRuns,
    IReadOnlyList<ModelUsageView> Models);
