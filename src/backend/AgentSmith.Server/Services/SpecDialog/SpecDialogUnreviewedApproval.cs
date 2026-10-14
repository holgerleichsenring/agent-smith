using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-13-8762i: an approval of a proposal whose specs are not all reviewed yet is held, never filed. The
/// decision it recorded on the transcript is taken back, as for an answer that answered nothing, and it becomes
/// the edit note naming what is not reviewed — so the next turn reviews those specs.
/// </summary>
public sealed class SpecDialogUnreviewedApproval(
    SpecDialogSessionManager sessions, ILogger<SpecDialogUnreviewedApproval> logger)
{
    public const string NotePrefix = "(held, not approved) review the specs not reviewed yet: ";

    public async Task<ConfirmationResult> HoldAsync(
        ConversationState state, IReadOnlyList<string> notReviewed, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(notReviewed);
        if (state.ThreadId is { } thread
            && await sessions.GetOpenByThreadAsync(state.Platform, thread, ct) is { } current
            && current.Transcript.LastOrDefault(turn => turn.Role == TranscriptRole.User)
                is { Decision: SpecDialogDecision.Approved } approved)
            await sessions.ClearDecisionAsync(state.Platform, thread, approved, ct);
        logger.LogInformation(
            "Approval of session {SessionId} held: {Count} spec(s) not reviewed yet", state.JobId, notReviewed.Count);
        return new OutcomeEditRequested(NotePrefix + string.Join(", ", notReviewed.Select(Name)));
    }

    private static string Name(string entry) =>
        entry == ProposalReviewTokens.Series ? "the series contract" : entry;
}
