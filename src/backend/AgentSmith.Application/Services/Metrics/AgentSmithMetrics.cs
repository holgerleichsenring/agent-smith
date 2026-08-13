using System.Diagnostics.Metrics;

namespace AgentSmith.Application.Services.Metrics;

/// <summary>
/// p0140e: the project's first metrics surface. One <see cref="System.Diagnostics.Metrics.Meter"/>
/// named "AgentSmith" exposing two counters:
///   - agent_smith_ambiguous_resolution_total — increments per matched (project, pipeline) pair
///     when ProjectResolver returns more than one match.
///   - agent_smith_pipeline_skipped_as_irrelevant_total — increments when the empty-plan gate
///     decides a run has no actionable work; carries a (project, pipeline, reason) label set.
/// BCL-only — operators choose their own exporter (e.g. OpenTelemetry.AddMeter("AgentSmith")).
/// <para>
/// p0401: registered as a singleton and injected. The meter used to be a static
/// field, which made the instrument set a process-wide fact no test could isolate
/// and no host could opt out of.
/// </para>
/// </summary>
public sealed class AgentSmithMetrics : IDisposable
{
    public const string MeterName = "AgentSmith";

    private readonly Meter _meter;

    public AgentSmithMetrics()
    {
        _meter = new Meter(
            MeterName,
            typeof(AgentSmithMetrics).Assembly.GetName().Version?.ToString() ?? "0.0.0");
        AmbiguousResolution = _meter.CreateCounter<long>(
            "agent_smith_ambiguous_resolution_total",
            description: "Per-matched (project, pipeline) increment when ProjectResolver returns more than one match.");
        PipelineSkippedAsIrrelevant = _meter.CreateCounter<long>(
            "agent_smith_pipeline_skipped_as_irrelevant_total",
            description: "Increment when a pipeline's Plan phase produces no actionable work (reason label).");
    }

    public Counter<long> AmbiguousResolution { get; }

    public Counter<long> PipelineSkippedAsIrrelevant { get; }

    public void Dispose() => _meter.Dispose();
}
