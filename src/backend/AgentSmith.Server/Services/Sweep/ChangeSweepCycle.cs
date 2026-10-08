using AgentSmith.Contracts.Sweep;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sweep;

/// <summary>
/// 2026-10-08-9e6e: one cycle over a set of change sources, under a deadline. The start rotates
/// every cycle, so a source the deadline cuts off is first the next time and none starves. Each
/// source reads from its cursor (first sight: the cursor is set to now, nothing is read), nudges
/// what changed, and moves the cursor — forward only — after the read. A failing source is logged
/// and leaves its cursor where it was.
/// </summary>
public sealed class ChangeSweepCycle(IServiceScopeFactory scopes, TimeProvider time, ILogger<ChangeSweepCycle> logger)
{
    public const int PagesPerSource = 10;
    private int _rotation;

    public async Task RunAsync(IReadOnlyList<SweepSource> sources, TimeSpan deadline, CancellationToken ct)
    {
        if (sources.Count == 0) return;
        var until = time.GetUtcNow() + deadline;
        var start = _rotation++ % sources.Count;
        for (var i = 0; i < sources.Count && time.GetUtcNow() < until && !ct.IsCancellationRequested; i++)
            await OneAsync(sources[(start + i) % sources.Count], ct);
    }

    private async Task OneAsync(SweepSource source, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var cursors = scope.ServiceProvider.GetRequiredService<ISweepCursors>();
        var readStart = time.GetUtcNow();
        try
        {
            if (await cursors.GetAsync(source.Key, ct) is not { } from)
            {
                await cursors.AdvanceAsync(source.Key, new SweepPosition(readStart), ct);
                return;
            }
            var page = await source.ReadAsync(from.At, from.Resume, PagesPerSource, ct);
            foreach (var item in page.Items) await source.NudgeAsync(item, ct);
            await cursors.AdvanceAsync(source.Key, SweepCursorPolicy.After(from, page, readStart), ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Change sweep of {Source} failed; its cursor stays", source.Key);
        }
    }
}
