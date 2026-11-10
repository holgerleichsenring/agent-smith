using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// How a design turn ends, each way carrying the turn's run id (2026-11-10-d854c): a reply with its outcome, a
/// pipeline failure kept as a Failure turn, a thrown turn kept and sent as a Notice, and a cancelled turn kept as a
/// Notice. A thrown or cancelled turn's notice is written with CancellationToken.None, so a cancel still leaves it.
/// </summary>
public sealed class SpecDialogTurnEnding(
    SpecDialogReadOnlyScopes readOnlyScopes, SpecDialogNotices notices, SpecDialogReplyComposer replies,
    ILogger<SpecDialogTurnEnding> logger)
{
    public const string CancelledNotice = "This design turn was cancelled before it answered.";

    /// <summary>CollectSpecDialogReply writes reply and outcome together; a failed run left both empty. The stamp rides
    /// the outcome because the turn's scopes are gone by filing time.</summary>
    public async Task<SpecDialogTurnResult> OfAsync(
        ConversationState state, SpecDialogReplySlot slot, ResolvedProject project,
        IReadOnlyDictionary<string, ISourceScopeSandbox> templates, CommandResult result, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(slot);
        var ended = slot is { Reply: not null, Outcome: not null }
            ? SpecDialogTurnResult.On(
                state.Platform, slot.Reply, await readOnlyScopes.StampAsync(slot.Outcome, project, templates, ct), slot.Kind,
                slot.Stewarded, subject: slot.Subject, thread: slot.Thread, workPicture: slot.WorkPicture)
            : SpecDialogTurnResult.On(state.Platform, FailureReply(state, result), new AnswerOutcome(),
                SpecDialogTurnKind.Failure, workPicture: slot.WorkPicture); // 548f: a failed turn's picture too
        return ended with { RunId = slot.RunId };
    }

    public async Task ThrownAsync(ConversationState state, Exception ex, string? runId)
    {
        logger.LogError(ex, "Spec-dialog turn failed for session {SessionId} (run {RunId})", state.JobId, runId);
        await notices.TellAsync(state, replies.ComposeTurnFailed(ex.Message), CancellationToken.None, runId);
    }

    public async Task CancelledAsync(ConversationState state, string? runId)
    {
        logger.LogInformation("Spec-dialog turn for session {SessionId} was cancelled (run {RunId})", state.JobId, runId);
        await notices.KeepAsync(state, CancelledNotice, CancellationToken.None, runId);
    }

    private string FailureReply(ConversationState state, CommandResult result)
    {
        logger.LogWarning("Spec-dialog turn for session {SessionId} produced no reply: {Message}",
            state.JobId, result.Message);
        return $"This design turn failed before an answer was produced: {result.Message ?? "unknown error"}";
    }
}
