using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-22-6d85b: the branch a merged pull request merged into — read through the provider's existing base read, the
/// first time a refresh sees the record merged; a branch already stored for the merged record is kept. A read the host
/// refuses (the providers answer null) leaves the branch unknown, and a ticket that requires others is held until a later
/// refresh reads it.
/// <para>2026-11-24-f9c5c: an open pull request's target too — read when the record is first seen and again whenever its head
/// moved (a retarget comes with a push), so the CI loop and the series page know a pull request into a series branch.</para>
/// </summary>
public sealed class PrRecordTargetRead(ILogger<PrRecordTargetRead> logger)
{
    public async Task ApplyAsync(object provider, PrReviewRecord record, PrReviewRecordView? stored, CancellationToken ct)
    {
        if (record.State is not (PrReviewStates.Merged or PrReviewStates.Open) || provider is not ISourceProvider source) return;
        if (Known(record, stored) is { } known)
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
            logger.LogWarning(ex, "The base of {Url} could not be read; it stays unknown until the next refresh", record.Url);
        }
    }

    // The stored target while it still holds: read for this merge, or for this open head.
    private static string? Known(PrReviewRecord record, PrReviewRecordView? stored)
    {
        if (stored?.TargetBranch is not { } known) return null;
        var holds = record.State == PrReviewStates.Merged
            ? stored.State == PrReviewStates.Merged
            : stored.State == PrReviewStates.Open && string.Equals(stored.HeadSha, record.HeadSha, StringComparison.Ordinal);
        return holds ? known : null;
    }
}
