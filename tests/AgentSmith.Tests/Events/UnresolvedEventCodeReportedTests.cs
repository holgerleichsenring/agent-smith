using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Events;

/// <summary>
/// 2026-09-03-b028: returning null for a code the resolver does not know is right — the
/// reader cannot invent a type — but doing it silently is what turned a missing resolver
/// row into a months-old blind spot. The code is named in the log the first time it is
/// seen, and only then: the run stream is hot, and a producer running ahead of a deployed
/// reader emits the same unknown code continuously.
/// </summary>
public sealed class UnresolvedEventCodeReportedTests
{
    private static string Envelope(int typeCode) => $"{{\"t\":{typeCode},\"p\":{{}}}}";

    [Fact]
    public void AnUnresolvedTypeCode_IsLoggedOncePerCode()
    {
        var logger = new CapturingLogger<EventEnvelopeSerializer>();
        var serializer = new EventEnvelopeSerializer(logger);

        serializer.Deserialize(Envelope(9998)).Should().BeNull();
        serializer.Deserialize(Envelope(9998)).Should().BeNull();
        serializer.Deserialize(Envelope(9999)).Should().BeNull();

        logger.Warnings.Should().HaveCount(2, "one line per distinct code, not per event");
        logger.Warnings[0].Should().Contain("9998");
        logger.Warnings[1].Should().Contain("9999");
    }

    [Fact]
    public void AnUnresolvedTrailTypeName_IsLoggedOncePerName()
    {
        var logger = new CapturingLogger<EventEnvelopeSerializer>();
        var serializer = new EventEnvelopeSerializer(logger);

        serializer.DeserializeRaw("SomeEventFromANewerBuild", "{}").Should().BeNull();
        serializer.DeserializeRaw("SomeEventFromANewerBuild", "{}").Should().BeNull();

        logger.Warnings.Should().ContainSingle()
            .Which.Should().Contain("SomeEventFromANewerBuild").And.Contain("trail");
    }

    [Fact]
    public void AnUnresolvedSystemTypeCode_IsLoggedOncePerCode()
    {
        var logger = new CapturingLogger<EventEnvelopeSerializer>();
        var serializer = new EventEnvelopeSerializer(logger);

        serializer.DeserializeSystem(Envelope(9997)).Should().BeNull();
        serializer.DeserializeSystem(Envelope(9997)).Should().BeNull();

        logger.Warnings.Should().ContainSingle()
            .Which.Should().Contain("9997").And.Contain("system");
    }

    [Fact]
    public void AResolvedTypeCode_SaysNothing()
    {
        var logger = new CapturingLogger<EventEnvelopeSerializer>();
        var serializer = new EventEnvelopeSerializer(logger);
        var known = new TicketInstructionIgnoredEvent(
            SampleEventFactory.RunId, "quote", "reason", SampleEventFactory.Timestamp);

        serializer.Deserialize(serializer.Serialize(known)).Should().NotBeNull();

        logger.Lines.Should().BeEmpty("a resolvable event is not news");
    }
}
