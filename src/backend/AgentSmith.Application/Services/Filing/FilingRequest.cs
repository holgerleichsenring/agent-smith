using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Filing;

/// <summary>
/// 2026-11-17-5701b: what a 'file' run files — an approved proposal and the conversation it was approved in. The
/// server builds it from the conversation and seeds it under ContextKeys.FilingRequest; the run's project is the one
/// the filing project resolves to.
/// </summary>
/// <param name="Conversation">The design conversation's id.</param>
/// <param name="Principal">The person who approved.</param>
/// <param name="ScopeRepos">The conversation's scope; empty means the project's own repositories.</param>
/// <param name="OwnerWords">The operator's own messages, the approval itself left out.</param>
/// <param name="Proposal">What was approved.</param>
/// <param name="MayStartRuns">The approver's runs.control: whether a filed ticket may be moved into a trigger status.</param>
/// <param name="Basis">The series a worked ticket's approval continues; null for a new filing.</param>
public sealed record FilingRequest(
    string Conversation, string Principal, IReadOnlyList<string> ScopeRepos, IReadOnlyList<OwnerWord> OwnerWords,
    OutcomeProposal Proposal, bool MayStartRuns, ContinuationBasis? Basis = null)
{
    /// <summary>The approval a record of this filing carries.</summary>
    public ApprovalSource Approval => new(Conversation, Principal, ScopeRepos, OwnerWords);
}
