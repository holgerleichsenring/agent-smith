using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// 2026-11-10-d854c: a design turn's steps, read RUN-WIDE — a turn's steps span every pipeline step of its run, so a
/// per-step read would split them — in pages by trail position, with their count from one aggregate query. A page is
/// bounded like the step pages are; the walk reaches every row.
/// </summary>
public sealed class DialogActivityReader(IServiceScopeFactory scopeFactory, EventEnvelopeSerializer envelopes)
{
    public const int DefaultPageCount = 200;
    public const int MaxPageCount = 500;
    private static readonly string Type = nameof(EventType.DialogActivity);

    public async Task<DialogActivityPage> ReadAsync(string runId, long? afterSeq, int? limit, CancellationToken ct)
    {
        var page = Math.Clamp(limit ?? DefaultPageCount, 1, MaxPageCount);
        using var scope = scopeFactory.CreateScope();
        var steps = scope.ServiceProvider.GetRequiredService<IUnitOfWork>()
            .Set<Infrastructure.Persistence.Entities.RunEvent>().AsNoTracking()
            .Where(e => e.RunId == runId && e.Type == Type);
        var count = await steps.CountAsync(ct);
        var after = afterSeq ?? long.MinValue;
        var rows = await steps.Where(e => e.Seq > after).OrderBy(e => e.Seq).Take(page + 1).ToListAsync(ct);
        var hasMore = rows.Count > page;
        var slice = hasMore ? rows.Take(page).ToList() : rows;
        // The cursor is the ROW's, so a payload that cannot be read still advances the walk.
        return new DialogActivityPage(count,
            [.. slice.Select(row => envelopes.DeserializeRaw(row.Type, row.PayloadJson)).OfType<DialogActivityEvent>()],
            slice.Count > 0 ? slice[^1].Seq : null, hasMore)
        {
            // 2026-11-10-d854b: the span and the sub-agent calls for the turn's one-line summary, by trail position.
            FirstAt = await steps.OrderBy(e => e.Seq).Select(e => (DateTimeOffset?)e.Timestamp).FirstOrDefaultAsync(ct),
            LastAt = await steps.OrderByDescending(e => e.Seq).Select(e => (DateTimeOffset?)e.Timestamp).FirstOrDefaultAsync(ct),
            SubAgentCalls = await steps.CountAsync(e => e.PayloadJson!.Contains(SpawnStep), ct),
        };
    }

    /// <summary>A step of the tool that spawns sub-agents, as the trail row's payload spells it.</summary>
    private const string SpawnStep = "\"Name\":\"spawn_agents\"";
}
