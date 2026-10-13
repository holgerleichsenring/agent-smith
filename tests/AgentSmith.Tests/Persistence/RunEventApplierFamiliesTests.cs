using AgentSmith.Contracts.Events;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-13-9a59d: the applier only dispatches — each projected event type reaches exactly the
/// families that name it, and a trail-only event reaches none.
/// </summary>
public sealed class RunEventApplierFamiliesTests
{
    private static readonly Dictionary<Type, string[]> Expected = new()
    {
        [typeof(RunStartedEvent)] = ["RunRowFamily"],
        [typeof(TicketFetchedEvent)] = ["RunRowFamily", "RunAttemptFamily", "RunConversationProjection"],
        [typeof(RunFinishedEvent)] = ["RunRowFamily", "RunSandboxFamily"],
        [typeof(PullRequestOutcomeEvent)] = ["RunRowFamily"],
        [typeof(RunCancelRequestedEvent)] = ["RunRowFamily"],
        [typeof(RunCheckpointedEvent)] = ["RunRowFamily", "RunConversationProjection"],
        [typeof(ExpectationRatifiedEvent)] = ["RunRowFamily"],
        [typeof(RunStoryRecordedEvent)] = ["RunRowFamily"],
        [typeof(PipelineStepsPlannedEvent)] = ["RunRowFamily"],
        [typeof(RunBudgetResolvedEvent)] = ["RunRowFamily"],
        [typeof(RunWorkShapeResolvedEvent)] = ["RunRowFamily"],
        [typeof(StepStartedEvent)] = ["RunStepFamily"],
        [typeof(StepFinishedEvent)] = ["RunStepFamily"],
        [typeof(LlmCallFinishedEvent)] = ["RunStepFamily"],
        [typeof(DecisionLoggedEvent)] = ["RunStepFamily"],
        [typeof(SandboxResultEvent)] = ["RunSandboxFamily"],
        [typeof(SandboxCreatedEvent)] = ["RunSandboxFamily"],
        [typeof(SandboxDisposedEvent)] = ["RunSandboxFamily"],
        [typeof(SandboxVanishedEvent)] = ["RunSandboxFamily"],
        [typeof(SpecStateChangedEvent)] = ["RunSpecFamily"],
        [typeof(SpecRecordedEvent)] = ["RunSpecFamily"],
        [typeof(SpecReviewedEvent)] = ["RunSpecFamily"],
        [typeof(SpecSetPublishedEvent)] = ["RunSpecFamily", "RunAttemptFamily"],
        [typeof(MasterPassEndedEvent)] = ["RunPassFamily"],
        [typeof(LedgerTransitionsRecordedEvent)] = ["RunPassFamily"],
        [typeof(SpecHandedBackEvent)] = ["RunConversationProjection"],
        [typeof(SpecCutReviewedEvent)] = ["RunConversationProjection"],
    };

    [Fact]
    public void RunEventApplier_EveryProjectedEventType_ReachesExactlyItsFamilies()
    {
        var applier = RunEventAppliers.Default();
        var eventTypes = typeof(RunEvent).Assembly.GetTypes()
            .Where(t => t.IsSealed && typeof(RunEvent).IsAssignableFrom(t));

        foreach (var type in eventTypes)
            applier.FamiliesOf(type).Select(f => f.GetType().Name).Should().Equal(
                Expected.GetValueOrDefault(type) ?? [], "{0} must reach exactly its families", type.Name);
    }
}
