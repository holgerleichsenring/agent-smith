using System.Text.Json;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.SpecNaming;

/// <summary>
/// 2026-10-12-967aa: a stored or streamed event a later release writes in the spec vocabulary —
/// type name SpecStateChanged/Recorded/Reviewed, payload property SpecId — replays as the one
/// this release writes.
/// </summary>
public sealed class SpecNamedEventReadTests
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-10-12T09:00:00Z");
    private readonly EventEnvelopeSerializer _codec = new();

    [Fact]
    public void EventReplay_SpecStateChangedName_Resolves()
    {
        var written = new PhaseStateChangedEvent("r1", "2026-10-12-967aa", 1, "Read both", PhaseRunState.Done, null, T);
        var raw = SpecShape.Of(JsonSerializer.Serialize(written));

        var replayed = _codec.DeserializeRaw("SpecStateChanged", raw);

        replayed.Should().BeOfType<PhaseStateChangedEvent>()
            .Which.Should().BeEquivalentTo(written);
    }

    [Theory]
    [InlineData("SpecRecorded")]
    [InlineData("PhaseRecorded")]
    public void EventReplay_RecordedUnderEitherName_CarriesTheId(string typeName)
    {
        var raw = SpecShape.Of(JsonSerializer.Serialize(new PhaseRecordedEvent("r1", "s1", "spec: s1", T)));

        _codec.DeserializeRaw(typeName, raw).Should().BeOfType<PhaseRecordedEvent>()
            .Which.PhaseId.Should().Be("s1");
    }

    [Fact]
    public void EventReplay_SpecReviewedName_Resolves() =>
        _codec.DeserializeRaw("SpecReviewed", """{"RunId":"r1","SpecId":"s1","ReportJson":"{}","Timestamp":"2026-10-12T09:00:00Z"}""")
            .Should().BeOfType<PhaseReviewedEvent>().Which.PhaseId.Should().Be("s1");

    [Fact]
    public void Envelope_StepStartedWithSpecId_ReadsAsPhaseId()
    {
        var written = new StepStartedEvent("r1", 3, "Implement", 5, T, "Implement", "AgenticMasterCommand", "s1");
        var envelope = SpecShape.Of(_codec.Serialize(written));

        _codec.Deserialize(envelope).Should().BeOfType<StepStartedEvent>()
            .Which.PhaseId.Should().Be("s1");
    }

    [Fact]
    public void Envelope_DecisionLoggedWithSpecId_ReadsAsPhaseId()
    {
        var envelope = SpecShape.Of(_codec.Serialize(
            new DecisionLoggedEvent("r1", "tooling", "a", null, "b", T, "s1")));

        _codec.Deserialize(envelope).Should().BeOfType<DecisionLoggedEvent>()
            .Which.PhaseId.Should().Be("s1");
    }

    [Fact]
    public void Serialize_StillWritesThePhaseNames()
    {
        var envelope = _codec.Serialize(new PhaseRecordedEvent("r1", "s1", "b", T));

        envelope.Should().Contain("\"phaseId\":\"s1\"").And.NotContain("specId");
        envelope.Should().StartWith($"{{\"t\":{(int)EventType.PhaseRecorded},");
    }
}
