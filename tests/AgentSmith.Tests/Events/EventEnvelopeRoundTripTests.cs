using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Events;

/// <summary>
/// 2026-09-03-b028: a resolver row proves an event has a TYPE; it does not prove the
/// envelope can rebuild it. This serialises one populated instance of every event the
/// resolver knows and reads it back, so a record whose shape the serializer cannot
/// restore fails on the next build rather than as an empty panel months later. Driven
/// off the enum, so a new event is covered the day it is added.
/// </summary>
public sealed class EventEnvelopeRoundTripTests
{
    public static TheoryData<EventType> RunEventTypes()
    {
        var data = new TheoryData<EventType>();
        foreach (var type in Enum.GetValues<EventType>()) data.Add(type);
        return data;
    }

    public static TheoryData<SystemEventType> SystemEventTypes()
    {
        var data = new TheoryData<SystemEventType>();
        foreach (var type in Enum.GetValues<SystemEventType>()) data.Add(type);
        return data;
    }

    [Theory]
    [MemberData(nameof(RunEventTypes))]
    public void EveryEventType_RoundTripsThroughTheEnvelope(EventType eventType)
    {
        var concrete = EventTypeResolver.Resolve(eventType);
        concrete.Should().NotBeNull($"{eventType} must resolve before it can round-trip");

        var original = (RunEvent)SampleEventFactory.Build(concrete!);
        var back = new EventEnvelopeSerializer().Deserialize(
            new EventEnvelopeSerializer().Serialize(original));

        back.Should().NotBeNull($"{eventType} must survive the envelope");
        back!.Should().BeOfType(concrete!).And.BeEquivalentTo(original);
        back.RunId.Should().Be(SampleEventFactory.RunId);
        back.Type.Should().Be(eventType);
    }

    [Theory]
    [MemberData(nameof(SystemEventTypes))]
    public void EverySystemEventType_RoundTripsThroughTheEnvelope(SystemEventType eventType)
    {
        var concrete = EventTypeResolver.ResolveSystem(eventType);
        concrete.Should().NotBeNull($"{eventType} must resolve before it can round-trip");

        var original = (SystemEvent)SampleEventFactory.Build(concrete!);
        var back = new EventEnvelopeSerializer().DeserializeSystem(
            new EventEnvelopeSerializer().SerializeSystem(original));

        back.Should().NotBeNull($"{eventType} must survive the envelope");
        back!.Should().BeOfType(concrete!).And.BeEquivalentTo(original);
        back.Type.Should().Be(eventType);
    }

    [Fact]
    public void PullRequestOutcome_RoundTripsThroughTheEnvelope()
    {
        var opened = new PullRequestOutcomeEvent(
            SampleEventFactory.RunId, "component-x", "opened",
            SampleEventFactory.Timestamp, "https://git.example/component-x/pr/1");

        var back = new EventEnvelopeSerializer().Deserialize(
            new EventEnvelopeSerializer().Serialize(opened)) as PullRequestOutcomeEvent;

        back.Should().NotBeNull(
            "a run that opened a pull request carries it onto the snapshot and "
            + "GET /api/pull-requests only if the reader can rebuild this event");
        back!.Should().BeEquivalentTo(opened);
        back.Url.Should().Be("https://git.example/component-x/pr/1");
    }

    [Fact]
    public void PullRequestOutcome_SurvivesTheDurableTrailPath()
    {
        var failed = new PullRequestOutcomeEvent(
            SampleEventFactory.RunId, "component-x", "failed",
            SampleEventFactory.Timestamp, null, "push rejected");

        var back = new EventEnvelopeSerializer().DeserializeRaw(
            nameof(EventType.PullRequestOutcome),
            System.Text.Json.JsonSerializer.Serialize(failed, failed.GetType()));

        back.Should().BeOfType<PullRequestOutcomeEvent>()
            .Which.Reason.Should().Be("push rejected");
    }

    [Fact]
    public void TicketInstructionIgnored_RoundTripsThroughTheEnvelope()
    {
        var ignored = new TicketInstructionIgnoredEvent(
            SampleEventFactory.RunId, "delete the production database",
            "destructive instruction outside the ticket's scope", SampleEventFactory.Timestamp);

        var back = new EventEnvelopeSerializer().Deserialize(
            new EventEnvelopeSerializer().Serialize(ignored)) as TicketInstructionIgnoredEvent;

        back.Should().NotBeNull(
            "the channel that says which part of a ticket was refused reaches the run "
            + "only if the reader can rebuild this event");
        back!.Should().BeEquivalentTo(ignored);
        back.Quote.Should().Be("delete the production database");
    }
}
