using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using AgentSmith.Infrastructure.Persistence.Services;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-08-25-61f1: composes the event applier with its real projections for the many tests
/// that want "the applier as the composition root builds it". It used to be spelled out as a
/// row of positional <c>new()</c>s at twenty-odd call sites, so every projection extracted
/// from the applier cost twenty edits and told the reader nothing.
/// </summary>
internal static class RunEventAppliers
{
    public static RunEventApplier Default(ICapacityBudget? budget = null, ITicketActivityNudge? activity = null) =>
        new(Families(budget, activity));

    public static IReadOnlyList<IRunEventFamily> Families(
        ICapacityBudget? budget = null, ITicketActivityNudge? activity = null) =>
    [
        new RunRowFamily(
            new RunStartProjection(new(), NullLogger<RunStartProjection>.Instance, activity),
            finalization: new(new(), budget), checkpoints: new(), expectations: new(),
            pullRequests: new(), classification: new()),
        new RunStepFamily(steps: new(new()), llmCalls: new(new(), new(), new()), decisions: new(new())),
        new RunSandboxFamily(new(new(), new(), new()), new()),
        new RunSpecFamily(new(), new(new())),
        new RunAttemptFamily(new()),
        new RunPassFamily(new(new()), new(new())), // 2026-10-13-9a59f
        new RunConversationProjection(new()), // 2026-10-13-9a59g
        new RunSegmentProjection(new()), // 2026-10-13-9a59j
        new RunSpecReviewRoundProjection(new()), // 2026-10-13-8762e
        new RunDefaultDecisionProjection(new()), // 2026-10-13-8762o
        new MemoryEvidenceProjection(new(), new()), // 2026-10-25-9324b
    ];
}
