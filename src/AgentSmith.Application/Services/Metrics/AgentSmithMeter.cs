using System.Diagnostics.Metrics;

namespace AgentSmith.Application.Services.Metrics;

/// <summary>
/// p0140e: the project's first metrics surface. One static <see cref="System.Diagnostics.Metrics.Meter"/>
/// named "AgentSmith" exposing two counters:
///   - agent_smith_ambiguous_resolution_total — increments per matched (project, pipeline) pair
///     when ProjectResolver returns more than one match.
///   - agent_smith_pipeline_skipped_as_irrelevant_total — increments when the empty-plan gate
///     decides a run has no actionable work; carries a (project, pipeline, reason) label set.
/// BCL-only — operators choose their own exporter (e.g. OpenTelemetry.AddMeter("AgentSmith")).
/// </summary>
public static class AgentSmithMeter
{
    public const string MeterName = "AgentSmith";

    public static readonly Meter Meter = new(
        MeterName,
        typeof(AgentSmithMeter).Assembly.GetName().Version?.ToString() ?? "0.0.0");

    public static readonly Counter<long> AmbiguousResolution = Meter.CreateCounter<long>(
        "agent_smith_ambiguous_resolution_total",
        description: "Per-matched (project, pipeline) increment when ProjectResolver returns more than one match.");

    public static readonly Counter<long> PipelineSkippedAsIrrelevant = Meter.CreateCounter<long>(
        "agent_smith_pipeline_skipped_as_irrelevant_total",
        description: "Increment when a pipeline's Plan phase produces no actionable work (reason label).");
}
