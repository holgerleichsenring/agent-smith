using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Spend;

/// <summary>
/// 2026-11-24-0fbfb: GET /api/runs/spend's answer — every run of the caller's projects started since the instant the
/// viewer names, up to now, summed apart for work and system runs and booked by repositories and pipeline. The
/// viewer's day starts where its browser says, so no time zone reaches the server. A run spanning the start counts
/// where it started, the way its id places it.
/// </summary>
public sealed class RunSpendReader(RunSpendRepository runs, TimeProvider clock)
{
    public async Task<RunSpendView> ReadAsync(ProjectScope scope, DateTimeOffset from, CancellationToken ct)
    {
        // A day ahead: a replica whose clock runs ahead still has its runs counted, and an id that is no timestamp
        // (letters sort above digits) stays out.
        var to = clock.GetUtcNow().AddDays(1);
        var read = await runs.ReadAsync(scope, RunIdGenerator.TimestampOf(from), RunIdGenerator.TimestampOf(to), ct);
        var system = read.Runs.Where(r => PipelinePresets.IsSystem(r.Pipeline)).ToList();
        var work = read.Runs.Where(r => !PipelinePresets.IsSystem(r.Pipeline)).ToList();
        return new RunSpendView(from, read.Runs.Sum(r => r.CostUsd), Part(work), Part(system), read.LlmCalls, Slices(read.Runs));
    }

    private static RunSpendPart Part(IReadOnlyList<RunSpendRow> rows) => new(rows.Sum(r => r.CostUsd), rows.Count);

    private static IReadOnlyList<RunSpendSlice> Slices(IReadOnlyList<RunSpendRow> rows) =>
        [.. rows.GroupBy(r => (Repos: string.Join('\n', r.Repos.Order(StringComparer.Ordinal)), r.Pipeline))
            .Select(g => new RunSpendSlice(
                g.Key.Repos.Length == 0 ? [] : g.Key.Repos.Split('\n'), g.Key.Pipeline, g.Sum(r => r.CostUsd)))];
}
