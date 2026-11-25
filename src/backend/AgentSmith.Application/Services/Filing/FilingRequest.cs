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
/// <param name="Proposal">What was approved; null for a person's act's follow-up (<see cref="FollowUp"/>).</param>
/// <param name="MayStartRuns">The approver's runs.control: whether a filed ticket may be moved into a trigger status.</param>
/// <param name="Basis">The series a worked ticket's approval continues; null for a new filing.</param>
public sealed record FilingRequest(
    string Conversation, string Principal, IReadOnlyList<string> ScopeRepos, IReadOnlyList<OwnerWord> OwnerWords,
    OutcomeProposal? Proposal, bool MayStartRuns, ContinuationBasis? Basis = null)
{
    /// <summary>2026-11-17-e061d: a closed ticket's follow-up a person's act asked for, filed instead of a proposal.</summary>
    public FilingFollowUp? FollowUp { get; init; }

    /// <summary>2026-11-24-f9c5b: the series branch a follow-up filed into a series is written from, in each repository that
    /// has it, so its pull request into that branch carries no default-branch commits the series lacks; null otherwise.</summary>
    public Domain.Models.BranchName? SpecBranchFrom { get; init; }

    /// <summary>2026-11-25-9992f: a ticket the series master adds to its series — a member like any other, not the series
    /// check's follow-up, so it is neither marked a follow-up nor recorded as a verdict's.</summary>
    public bool IsSeriesMasterAddition { get; init; }

    /// <summary>The approval a record of this filing carries.</summary>
    public ApprovalSource Approval => new(Conversation, Principal, ScopeRepos, OwnerWords);
}
