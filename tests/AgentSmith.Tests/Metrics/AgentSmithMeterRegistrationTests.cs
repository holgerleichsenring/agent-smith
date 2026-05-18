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
public sealed class AgentSmithMeterRegistrationTests
{
    [Fact]
    public void AgentSmithMeter_HasNameAgentSmith()
    {
        AgentSmithMeter.Meter.Name.Should().Be("AgentSmith");
        AgentSmithMeter.MeterName.Should().Be("AgentSmith");
    }

    [Fact]
    public void AgentSmithMeter_CountersDiscoverableViaMeterListener()
    {
        var publishedInstruments = new List<string>();
        var measuredInstruments = new List<string>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name != AgentSmithMeter.MeterName) return;
                publishedInstruments.Add(instrument.Name);
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, _, _) =>
            measuredInstruments.Add(instrument.Name));
        listener.Start();

        AgentSmithMeter.AmbiguousResolution.Add(1);
        AgentSmithMeter.PipelineSkippedAsIrrelevant.Add(1);

        publishedInstruments.Should().Contain("agent_smith_ambiguous_resolution_total");
        publishedInstruments.Should().Contain("agent_smith_pipeline_skipped_as_irrelevant_total");
        measuredInstruments.Should().Contain("agent_smith_ambiguous_resolution_total");
        measuredInstruments.Should().Contain("agent_smith_pipeline_skipped_as_irrelevant_total");
    }
}
