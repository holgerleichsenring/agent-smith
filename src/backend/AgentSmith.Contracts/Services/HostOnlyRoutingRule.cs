using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Refuses a routing rule that names a preset no label can start — see
/// <see cref="PipelinePresets.NeedsHostContext"/>. A label map entry or a default pipeline naming
/// one sends a ticket to a run that fails when it starts, and nothing at the label says why.
/// <para>
/// One rule, three doors with three severities: the studio SAVE refuses it, the studio DRAFT
/// names it on the field, and the STARTUP check reports it as advisory — a blocking finding
/// disables every trigger on the project, which is worse than the one rule that is wrong.
/// </para>
/// </summary>
public static class HostOnlyRoutingRule
{
    public const string LabelMapField = "pipelineFromLabel";
    public const string DefaultField = "defaultPipeline";

    private const string Why =
        ", which a label cannot start: its run is launched with context only a host supplies — a "
        + "design conversation's transcript and reply slot — and no ticket carries that. Start it "
        + "from the chat or the dashboard, and route the label to a preset a ticket can run.";

    /// <summary>Every entry that names a host-launched preset: the field it sits in, and a reason
    /// naming the owner, the label and the value.</summary>
    public static IEnumerable<(string Field, string Reason)> Violations(
        string owner, IReadOnlyDictionary<string, string>? pipelineFromLabel, string? defaultPipeline)
    {
        foreach (var (label, pipeline) in pipelineFromLabel ?? new Dictionary<string, string>())
            if (PipelinePresets.NeedsHostContext(pipeline))
                yield return (LabelMapField,
                    $"{owner}: pipeline_from_label maps label '{label}' to pipeline '{pipeline}'{Why}");
        if (!string.IsNullOrWhiteSpace(defaultPipeline) && PipelinePresets.NeedsHostContext(defaultPipeline))
            yield return (DefaultField, $"{owner}: default_pipeline is '{defaultPipeline}'{Why}");
    }

    /// <summary>The save door: a tracker whose routing names a host-launched preset is refused.</summary>
    public static void ValidateTracker(TrackerEntity tracker)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        var reasons = Violations(Owner(tracker), tracker.PipelineFromLabel, tracker.DefaultPipeline)
            .Select(v => v.Reason).ToList();
        if (reasons.Count > 0) throw new ConfigurationException(string.Join(" ", reasons));
    }

    /// <summary>How a tracker is named in the reason.</summary>
    public static string Owner(TrackerEntity tracker) => $"Tracker '{tracker.Id}'";
}
