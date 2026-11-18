using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-17-3435a: the CI half of a record's refresh — an open pull request whose provider reads CI gets its CI on
/// the head the state read saw, bounded by the project's merge.ci_excerpt_chars and merge.ci_failed_checks_read. A head
/// already stored red is read without its logs (its evidence is stored). A CI read the host refuses is logged and
/// leaves the stored CI as it was; the state read stands either way.
/// </summary>
public sealed class PrRecordCiRead(ILogger<PrRecordCiRead> logger)
{
    public async Task ApplyAsync(
        ResolvedProject project, object provider, PrReviewRecord record, PrReviewRecordView? stored, CancellationToken ct)
    {
        if (record.State != PrReviewStates.Open || provider is not IPullRequestCiReader reader) return;
        var evidenceStored = stored?.Ci is { State: CiStates.Red } ci && ci.IsOn(record.HeadSha);
        var request = new CiReadRequest(record.HeadSha, project.Merge.CiExcerptChars,
            evidenceStored ? 0 : project.Merge.CiFailedChecksRead);
        try
        {
            var read = await reader.ReadCiAsync(record.Url, request, ct);
            record.CiState = read.State;
            record.CiHeadSha = read.HeadSha;
            record.CiAtTicks = record.CheckedAtTicks;
            record.CiFailuresJson = PrRecordCiJson.Failures(read.Failures);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The CI of {Url} could not be read; the stored CI stands", record.Url);
        }
    }
}
