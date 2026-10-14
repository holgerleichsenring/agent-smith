using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// 2026-10-13-8762g: refuses a claim of a ticket whose newest code attempt stopped at a boundary — it ended
/// where <see cref="Contracts.Runs.SeriesBoundaryEnd"/> holds with specs it never started. The poller, a
/// webhook and the capacity pump would otherwise pick it straight back up; a person's start does. Retry is
/// not named: it refuses a success or watchdog end. Beside <see cref="UnmovedTicketGate"/>, in the one door.
/// </summary>
internal sealed class BoundaryStopGate(IPreviousAttemptReader attempts)
{
    public const string Sentence =
        "This ticket stopped at a boundary; start it by name in chat or with a comment on the ticket.";

    /// <summary>The starts the gate lets through: a person's — a start by name in chat, a rework act — and a
    /// series' automatic start (2026-10-13-8762v).</summary>
    public static bool AdmitsStart(IReadOnlyDictionary<string, object>? initialContext) =>
        initialContext.IsRequestedByName() || initialContext.IsSeriesAutoStart();

    /// <summary>The refusal, or null when the ticket may be claimed.</summary>
    public async Task<ClaimResult?> RefusalAsync(ClaimRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PipelineName != PipelinePresets.CodeName || AdmitsStart(request.InitialContext)) return null;
        var latest = await attempts.LatestAsync(
            request.ProjectName, request.TicketId.Value, request.ExistingRunId, cancellationToken);
        return latest is { StoppedAtBoundary: true }
            ? ClaimResult.Rejected(ClaimRejectionReason.TicketStoppedAtBoundary, Sentence)
            : null;
    }
}
