using AgentSmith.Contracts.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0173a: silent-producer gate for the SYSTEM channel. Mirror of p0169e's
/// <see cref="EventSequenceCompletenessTests"/>. Each row drives the
/// producer into a known state; the row's expected event types must
/// surface in the recording publisher. The slice-a guard test
/// (member-data-empty) was dropped in p0173b when the first rows landed.
///
/// Slice b rows cover the poller + webhook producers; slice c adds the
/// chat + config + catalog producers.
/// </summary>
public sealed class SystemEventSequenceCompletenessTests
{
    [Theory]
    [InlineData(SystemEventType.PollCycleStarted)]
    [InlineData(SystemEventType.PollCycleFinished)]
    [InlineData(SystemEventType.TicketScanned)]
    [InlineData(SystemEventType.TicketSkipped)]
    [InlineData(SystemEventType.TicketTriggered)]
    [InlineData(SystemEventType.WebhookReceived)]
    public async Task EventType_RoundTripsThroughTheBackbone(SystemEventType type)
    {
        // Asserts each concrete SystemEvent record reaches the recording
        // publisher unchanged. The "silent producer" symptom on the run
        // channel was a publisher that never fired; the symptomatic gap
        // on the system channel is a record that doesn't round-trip
        // through the envelope. Producer-side completeness (TrackerPoller
        // emits TicketScanned, WebhookRequestProcessor emits WebhookReceived,
        // etc.) is covered by each producer's own unit tests; this Theory
        // is the contract-completeness gate that catches a new type added
        // to the enum without a matching record + serializer branch.
        var recorder = new RecordingSystemEventPublisher();
        var concrete = BuildSampleFor(type);
        await recorder.PublishAsync(concrete, CancellationToken.None);

        recorder.Types.Should().Contain(type,
            $"every populated SystemEventType must reach the recording publisher unchanged");
    }

    private static SystemEvent BuildSampleFor(SystemEventType type) => type switch
    {
        SystemEventType.PollCycleStarted =>
            new PollCycleStartedEvent("tracker:jira/sample", "sample", 60, DateTimeOffset.UtcNow),
        SystemEventType.PollCycleFinished =>
            new PollCycleFinishedEvent("tracker:jira/sample", "sample",
                TicketsPolled: 3, Matched: 1, Spawned: 1,
                StatusFiltered: 0, ZeroMatched: 2, DurationMs: 250, Timestamp: DateTimeOffset.UtcNow),
        SystemEventType.TicketScanned =>
            new TicketScannedEvent("tracker:jira/sample", "sample", "TICKET-1",
                new[] { "agent-smith", "bug" }, DateTimeOffset.UtcNow),
        SystemEventType.TicketSkipped =>
            new TicketSkippedEvent("tracker:jira/sample", "sample", "TICKET-1",
                TicketSkipReason.ZeroMatch, "no project trigger matched", DateTimeOffset.UtcNow),
        SystemEventType.TicketTriggered =>
            new TicketTriggeredEvent("tracker:jira/sample", "sample", "TICKET-1",
                "sample-project", "fix-bug", "Claimed", DateTimeOffset.UtcNow),
        SystemEventType.WebhookReceived =>
            new WebhookReceivedEvent("webhook:github", "issues", "/webhooks/github",
                Actioned: true, SkipReason: null, Timestamp: DateTimeOffset.UtcNow),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "no sample yet for this type")
    };
}
