using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59n: the last launch of a set of runs — each run's newest segment and its sandboxes that
/// ended vanished or OOM-killed — in two queries, for <see cref="InfrastructureEnd"/>. A run without a segment
/// row has no entry.
/// </summary>
public sealed class TicketLaunchEnds(IServiceScopeFactory scopes)
{
    private static readonly string[] Ends = [SandboxEndReasons.Vanished, SandboxEndReasons.OomKilled];

    public async Task<IReadOnlyDictionary<string, RunLastLaunch>> ForAsync(IReadOnlyList<string> runIds, CancellationToken ct)
    {
        if (runIds.Count == 0) return new Dictionary<string, RunLastLaunch>(StringComparer.Ordinal);
        using var scope = scopes.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var segments = await uow.Set<RunSegment>().AsNoTracking().Where(s => runIds.Contains(s.RunId)).ToListAsync(ct);
        var sandboxes = (await uow.Set<RunSandbox>().AsNoTracking()
                .Where(s => runIds.Contains(s.RunId) && s.EndReason != null && Ends.Contains(s.EndReason))
                .ToListAsync(ct))
            .ToLookup(s => s.RunId, StringComparer.Ordinal);
        return segments.GroupBy(s => s.RunId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key,
                g => new RunLastLaunch(g.OrderBy(s => s.EventSeq).ThenBy(s => s.Id).Last(), [.. sandboxes[g.Key]]),
                StringComparer.Ordinal);
    }
}
