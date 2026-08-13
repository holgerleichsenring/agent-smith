using System.Diagnostics.Metrics;
using AgentSmith.Application.Services.Metrics;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Metrics;

/// <summary>
/// p0140e: smoke tests for the project's first metrics surface. Verify the
/// meter is named "AgentSmith" (so operators can wire it with
/// <c>OpenTelemetry.AddMeter("AgentSmith")</c>) and that both counters are
/// discoverable through the BCL <see cref="MeterListener"/> API — i.e. they
/// publish to subscribers when incremented.
/// </summary>
[Collection(MeterCollection.Name)]
public sealed class AgentSmithMetricsRegistrationTests
{
    [Fact]
    public void AgentSmithMetrics_PublishesUnderTheAgentSmithMeterName()
    {
        var published = new List<string>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name != AgentSmithMetrics.MeterName) return;
                published.Add(instrument.Name);
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.Start();

        // p0401: the instruments come into existence WITH the service — the listener
        // above is already running, so what it sees is what an operator's exporter
        // sees when the host resolves the metrics service.
        using var metrics = new AgentSmithMetrics();

        published.Should().Contain("agent_smith_ambiguous_resolution_total");
        published.Should().Contain("agent_smith_pipeline_skipped_as_irrelevant_total");
    }

    [Fact]
    public void AgentSmithMetrics_CountersDiscoverableViaMeterListener()
    {
        var measured = new List<string>();
        using var metrics = new AgentSmithMetrics();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name != AgentSmithMetrics.MeterName) return;
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, _, _) =>
            measured.Add(instrument.Name));
        listener.Start();

        metrics.AmbiguousResolution.Add(1);
        metrics.PipelineSkippedAsIrrelevant.Add(1);

        measured.Should().Contain("agent_smith_ambiguous_resolution_total");
        measured.Should().Contain("agent_smith_pipeline_skipped_as_irrelevant_total");
    }
}
