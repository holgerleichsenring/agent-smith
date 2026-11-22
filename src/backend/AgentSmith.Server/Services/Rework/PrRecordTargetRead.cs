using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-22-6d85b: the branch a merged pull request merged into — read once, through the provider's existing base read,
/// the first time a refresh sees the record merged; a branch already stored is kept. A read the host refuses (the providers
/// answer null) leaves the branch unknown, and a ticket that requires others is held until a later refresh reads it.
/// </summary>
public sealed class PrRecordTargetRead(ILogger<PrRecordTargetRead> logger)
{
    public async Task ApplyAsync(object provider, PrReviewRecord record, PrReviewRecordView? stored, CancellationToken ct)
    {
        if (record.State != PrReviewStates.Merged || provider is not ISourceProvider source) return;
        if (stored?.TargetBranch is { } known)
        {
            record.TargetBranch = known;
            return;
        }
        try
        {
            record.TargetBranch = await source.ReadPullRequestBaseAsync(record.Url, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The branch {Url} merged into could not be read; its landing reads it as unknown", record.Url);
        }
    }
}
