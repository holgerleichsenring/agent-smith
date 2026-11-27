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
            evidenceStored ? 0 : project.Merge.CiFailedChecksRead) { HeadSettled = Settled(project, record, stored) };
        try
        {
            var read = await reader.ReadCiAsync(record.Url, request, ct);
            record.CiState = read.State;
            record.CiHeadSha = read.HeadSha;
            record.CiAtTicks = record.CheckedAtTicks;
            record.CiFailuresJson = PrRecordCiJson.Failures(evidenceStored && stored!.Ci!.IsOn(read.HeadSha)
                ? KeptEvidence(read.Failures, stored.Ci.Failures) : read.Failures);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The CI of {Url} could not be read; the stored CI stands", record.Url);
        }
    }

    /// <summary>2026-11-26-49d3a: a head read again red took no logs; each check still failing keeps the evidence stored for it,
    /// a check failing anew comes as read, one that passed since is gone.</summary>
    internal static IReadOnlyList<CiFailure> KeptEvidence(IReadOnlyList<CiFailure> read, IReadOnlyList<CiFailure> stored) =>
        [.. read.Select(f => stored.FirstOrDefault(s => s.Name == f.Name) ?? f)];

    /// <summary>2026-11-25-7a92a: the head this read sees is the one stored, and has stood since it moved there (or since the pull
    /// request opened) for merge.ci_settle_seconds — only then may a host call "nothing on this head" not started.</summary>
    internal static bool Settled(ResolvedProject project, PrReviewRecord record, PrReviewRecordView? stored) =>
        Settled(record, stored, project.Merge.CiSettleSeconds);

    /// <summary>2026-11-27-e451: the one settle rule, for the CI read and for the master's no-CI wake
    /// (<see cref="SeriesPrChange"/>) alike.</summary>
    internal static bool Settled(PrReviewRecord record, PrReviewRecordView? stored, int settleSeconds) =>
        stored is { HeadSha: { } was } && was == record.HeadSha && (stored.HeadMovedAt ?? stored.StateAt) is { } since
        && new DateTimeOffset(record.CheckedAtTicks, TimeSpan.Zero) - since >= TimeSpan.FromSeconds(settleSeconds);
}
