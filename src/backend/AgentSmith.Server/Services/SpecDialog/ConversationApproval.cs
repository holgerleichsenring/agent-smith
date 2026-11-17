using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-17-5701b: what a conversation's state gives the Application's filing — the approval source an approval
/// record carries, and the request a 'file' run files. The one place that reads a ConversationState for them.
/// </summary>
public static class ConversationApproval
{
    /// <param name="proposalId">The proposal being approved, whose approve turn is not an owner word.</param>
    public static ApprovalSource Of(ConversationState state, string? proposalId = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new ApprovalSource(state.JobId, state.UserId, state.Scope?.Repos ?? [], ConversationOwnerWords.Of(state, proposalId));
    }

    public static FilingRequest Request(
        ConversationState state, OutcomeProposal proposal, bool mayStartRuns, string? proposalId = null)
    {
        var source = Of(state, proposalId);
        return new FilingRequest(source.Conversation, source.Principal, source.Repositories, source.OwnerWords, proposal, mayStartRuns);
    }
}
