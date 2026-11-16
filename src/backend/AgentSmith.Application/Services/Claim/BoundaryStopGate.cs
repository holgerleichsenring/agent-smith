using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// 2026-10-13-8762g: refuses a claim of a ticket whose newest code attempt stopped at a boundary — it ended
/// where <see cref="Contracts.Runs.SeriesBoundaryEnd"/> holds with specs it never started. The poller, a
/// webhook and the capacity pump would otherwise pick it straight back up; a person's start does. Retry is
/// not named: it refuses a success or watchdog end. Beside <see cref="UnmovedTicketGate"/>, in the one door.
/// <para>2026-11-16-f3fbb: it also refuses a ticket whose newest attempt stopped on a precondition, with the same
/// admit list — no second gate over the same row.</para>
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
        return latest switch
        {
            { StoppedAtBoundary: true } => ClaimResult.Rejected(ClaimRejectionReason.TicketStoppedAtBoundary, Sentence),
            { StoppedOnPrecondition: true } => ClaimResult.Rejected(
                ClaimRejectionReason.TicketStuckOnPrecondition, PreconditionSentence(latest)),
            _ => null,
        };
    }

    /// <summary>2026-11-16-f3fbb: the refusal of a ticket held on a precondition, naming what did not hold.</summary>
    public static string PreconditionSentence(PreviousAttempt attempt) =>
        $"This ticket stopped on a precondition: {PreconditionHeadline.Of(attempt.EndKind, attempt.EndDetail)}. "
        + "It starts by itself once that holds, or when you reply.";
}
