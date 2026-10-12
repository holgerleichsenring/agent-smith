using System.Text.Json;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.SpecNaming;

/// <summary>
/// 2026-10-12-967aa/967ab: a stored or streamed event the release before the rename wrote — type
/// name PhaseStateChanged/Recorded/Reviewed, payload property PhaseId — replays as the one this
/// release writes, until 967ah drops the readers.
/// </summary>
public sealed class SpecNamedEventReadTests
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-10-12T09:00:00Z");
    private readonly EventEnvelopeSerializer _codec = new();

    [Fact]
    public void EventReplay_OldStateChangedName_Resolves()
    {
        var written = new SpecStateChangedEvent("r1", "2026-10-12-967aa", 1, "Read both", SpecRunState.Done, null, T);
        var raw = SpecShape.Of(JsonSerializer.Serialize(written));

        var replayed = _codec.DeserializeRaw("PhaseStateChanged", raw);

        replayed.Should().BeOfType<SpecStateChangedEvent>()
            .Which.Should().BeEquivalentTo(written);
    }

    [Theory]
    [InlineData("SpecRecorded")]
    [InlineData("PhaseRecorded")]
    public void EventReplay_RecordedUnderEitherName_CarriesTheId(string typeName)
    {
        var raw = SpecShape.Of(JsonSerializer.Serialize(new SpecRecordedEvent("r1", "s1", "spec: s1", T)));

        _codec.DeserializeRaw(typeName, raw).Should().BeOfType<SpecRecordedEvent>()
            .Which.SpecId.Should().Be("s1");
    }

    [Fact]
    public void EventReplay_OldReviewedName_Resolves() =>
        _codec.DeserializeRaw("PhaseReviewed", """{"RunId":"r1","PhaseId":"s1","ReportJson":"{}","Timestamp":"2026-10-12T09:00:00Z"}""")
            .Should().BeOfType<SpecReviewedEvent>().Which.SpecId.Should().Be("s1");

    [Fact]
    public void Envelope_StepStartedWithOldId_ReadsAsSpecId()
    {
        var written = new StepStartedEvent("r1", 3, "Implement", 5, T, "Implement", "AgenticMasterCommand", "s1");
        var envelope = SpecShape.Of(_codec.Serialize(written));

        _codec.Deserialize(envelope).Should().BeOfType<StepStartedEvent>()
            .Which.SpecId.Should().Be("s1");
    }

    [Fact]
    public void Envelope_DecisionLoggedWithOldId_ReadsAsSpecId()
    {
        var envelope = SpecShape.Of(_codec.Serialize(
            new DecisionLoggedEvent("r1", "tooling", "a", null, "b", T, "s1")));

        _codec.Deserialize(envelope).Should().BeOfType<DecisionLoggedEvent>()
            .Which.SpecId.Should().Be("s1");
    }

    [Fact]
    public void Serialize_WritesTheSpecNames()
    {
        var envelope = _codec.Serialize(new SpecRecordedEvent("r1", "s1", "b", T));

        envelope.Should().Contain("\"specId\":\"s1\"").And.NotContain("phaseId");
        envelope.Should().StartWith($"{{\"t\":{(int)EventType.SpecRecorded},");
    }
}
