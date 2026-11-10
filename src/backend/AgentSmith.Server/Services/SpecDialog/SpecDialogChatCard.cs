using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-10-ccd9: the approval card a chat thread is shown — posted once, never waited on. Its question id
/// IS the proposal id, so a click names the card it was made on; the thread's posted id and time are written
/// only after the post succeeded, which is what a typed answer in that thread binds to.
/// </summary>
public sealed class SpecDialogChatCard(
    ChatThreadAdapters threads, SpecDialogMessenger messenger, SpecDialogOutcomeComposer composer,
    SpecDialogProposalState proposals, ILogger<SpecDialogChatCard> logger)
{
    /// <summary>The approval card of proposal <paramref name="id"/>, never expiring: its question id is the proposal id.</summary>
    public DialogQuestion CardOf(ConversationState state, OutcomeProposal proposal, string id) =>
        new(id, QuestionType.Approval,
            composer.ComposeConfirmation(proposal).In(SpecDialogMarkup.For(state.Platform)), Context: null,
            Choices: OutcomeShapes.For(proposal, bound: state.TicketKey is not null), DefaultAnswer: "", TimeSpan.Zero);

    public async Task PostAsync(ConversationState state, OutcomeProposal proposal, string id, CancellationToken ct)
    {
        var question = CardOf(state, proposal, id);
        // The page's card is a push that blocks nothing; a reload rebuilds it from the row.
        if (DialogTarget.IsDashboard(state))
        {
            await messenger.AskQuestionAsync(state.Platform, state.ChannelId, state.ThreadId!, question, ct);
            return;
        }
        try
        {
            if (!await threads.TryPostQuestionAsync(new ChatThread(state.Platform, state.ChannelId, state.ThreadId, state.UserId), question, ct))
                return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The approval card of {SessionId} was not posted — typed answers keep the earlier card", state.JobId);
            return;
        }
        await proposals.PostedAsync(state, id, DateTimeOffset.UtcNow, ct);
    }
}
