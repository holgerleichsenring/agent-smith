using AgentSmith.Infrastructure.Persistence.Services;

namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>
/// 2026-10-01-283da: drives <see cref="LegacyAttachmentCopy"/> under the housekeeping leader.
/// Each tick copies batch after batch while uncopied legacy images exist, then removes the copies
/// an older replica orphaned by deleting their conversation. Leader-elected so the work is not
/// done twice for nothing; correct without it, because the primary key is the claim.
/// </summary>
public sealed class LegacyAttachmentCopySweeper(
    IServiceScopeFactory scopes, ILogger<LegacyAttachmentCopySweeper> logger)
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(60);

    public async Task RunAsync(CancellationToken ct)
    {
        logger.LogInformation("LegacyAttachmentCopySweeper started (scan {Scan})", ScanInterval);
        while (!ct.IsCancellationRequested)
        {
            try { await SweepOnceAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Legacy image copy failed"); }

            try { await Task.Delay(ScanInterval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    // Public so tests drive single deterministic sweeps. Returns the legacy rows it found.
    public async Task<int> SweepOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var copy = scope.ServiceProvider.GetRequiredService<LegacyAttachmentCopy>();
        var found = 0;
        while (await copy.CopyBatchAsync(ct) is var batch and > 0) found += batch;
        var orphans = await copy.RemoveOrphansAsync(ct);
        if (found > 0 || orphans > 0)
            logger.LogInformation(
                "Legacy image copy: {Found} row(s) copied or already present, {Orphans} orphaned copy(ies) removed",
                found, orphans);
        return found;
    }
}
