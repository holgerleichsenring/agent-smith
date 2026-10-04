using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Turns;
using AgentSmith.Tests.TestHelpers;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>2026-10-02-3f06c: the proposal evidence check as the product composes it.</summary>
internal static class ProposalEvidenceTestReview
{
    public static ProposalEvidenceReview Create(ITurnActivityObserverAccessor? activity = null) =>
        new(new EvidenceCheck(new EvidenceReferences()), new EpicChildOrderer(),
            activity ?? TurnActivityRecorder.Silent());
}
