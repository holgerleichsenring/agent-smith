using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0173e: L1StepDetailEvent and the ReportDetailAsync removal — proves the
/// new typed channel round-trips through the envelope serializer and the
/// old string overload is gone from IProgressReporter.
/// </summary>
public sealed class L1StepDetailEventTests
{
    [Fact]
    public void L1StepDetailEvent_RoundTripsThroughEnvelopeSerializer()
    {
        var original = new L1StepDetailEvent(
            RunId: "2026-05-20T10-15-30-1a2b",
            StepIndex: 7,
            Origin: "skill-round",
            Detail: "architect: completed round 2 (3 observations).",
            Timestamp: DateTimeOffset.Parse("2026-05-20T10:16:45Z"));

        var envelope = EventEnvelopeSerializer.Serialize(original);
        var back = EventEnvelopeSerializer.Deserialize(envelope) as L1StepDetailEvent;

        back.Should().NotBeNull();
        back!.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void IProgressReporter_NoReportDetailAsyncMethodDefined()
    {
        var hasDetailMethod = typeof(IProgressReporter)
            .GetMethods()
            .Any(m => m.Name == "ReportDetailAsync");

        hasDetailMethod.Should().BeFalse(
            "p0173e removed ReportDetailAsync — detail rows now flow as typed L1StepDetailEvent on the bus");
    }

    [Fact]
    public void IProgressReporter_NoStringCommandNameParameter()
    {
        var reportProgress = typeof(IProgressReporter)
            .GetMethods()
            .Single(m => m.Name == "ReportProgressAsync");

        var hasStringCommandName = reportProgress.GetParameters()
            .Any(p => p.ParameterType == typeof(string) && p.Name == "commandName");

        hasStringCommandName.Should().BeFalse(
            "p0173e replaced the string commandName parameter with a typed PipelineCommand reference");
    }

    [Fact]
    public void L1StepStartedEvent_RoundTripsThroughEnvelopeSerializer()
    {
        var original = new StepStartedEvent(
            RunId: "2026-05-20T10-15-30-1a2b",
            StepIndex: 3,
            StepName: "AnalyzeCode",
            TotalSteps: 12,
            Timestamp: DateTimeOffset.Parse("2026-05-20T10:16:05Z"));

        var envelope = EventEnvelopeSerializer.Serialize(original);
        var back = EventEnvelopeSerializer.Deserialize(envelope) as StepStartedEvent;

        back.Should().NotBeNull();
        back!.Should().BeEquivalentTo(original);
    }
}
