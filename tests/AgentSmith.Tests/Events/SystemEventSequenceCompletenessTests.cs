using AgentSmith.Contracts.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0173a: silent-producer gate for the SYSTEM channel. Mirrors the p0169e
/// pattern (<see cref="EventSequenceCompletenessTests"/>) — when slices b
/// and c land their producers, each one gets a row in
/// <see cref="DropOneProducer_RemainingTypesPresent"/>: given a producer
/// substituted by a no-op publisher, the event types that producer is
/// solely responsible for must drop out of the recorded set.
///
/// Slice a ships the scaffold with EMPTY MemberData — the
/// <see cref="MemberDataIsEmpty_UntilProducersLand_GuardTest"/> asserts the
/// expected slice-a state ("no rows yet"). Slice b removes the guard and
/// adds its rows; slice c adds the rest.
/// </summary>
public sealed class SystemEventSequenceCompletenessTests
{
    public static IEnumerable<object[]> ProducerRows() => Array.Empty<object[]>();

    [Theory(Skip = "Slices b + c populate MemberData with concrete producers; until then the guard test enforces the empty-scaffold state.")]
    [MemberData(nameof(ProducerRows))]
    public Task DropOneProducer_RemainingTypesPresent(
        SystemProducerId dropped, params SystemEventType[] missingTypes)
    {
        _ = dropped;
        _ = missingTypes;
        // Slices b + c implement the body when they add their first row.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Slice-a guard: as long as no producers are wired, the theory's
    /// MemberData is empty. When slice b adds its first producer + row,
    /// this guard turns red — that's the signal to delete the guard
    /// and unskip the Theory.
    /// </summary>
    [Fact]
    public void MemberDataIsEmpty_UntilProducersLand_GuardTest()
    {
        ProducerRows().Should().BeEmpty(
            "slice a ships the scaffold with no rows; slices b + c add their producer rows alongside the producer wiring");
    }
}

public enum SystemProducerId
{
    // Slice b will add: TrackerPoller, WebhookHandlers.
    // Slice c will add: ChatHandlers, ConfigLoaders, SkillCatalogLoader, ConceptVocabularyLoader.
}
