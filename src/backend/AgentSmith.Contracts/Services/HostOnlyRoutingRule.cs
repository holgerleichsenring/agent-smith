using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Refuses a routing rule that names a preset no label can start — see
/// <see cref="PipelinePresets.NotRoutableBecause"/>. A label map entry or a default pipeline naming
/// one sends a ticket to a run that cannot serve it, and nothing at the label says why.
/// <para>
/// One rule, three doors with three severities: the studio SAVE refuses it, the studio DRAFT
/// names it on the field, and the STARTUP check reports it as advisory — a blocking finding
/// disables every trigger on the project, which is worse than the one rule that is wrong.
/// </para>
/// <para>2026-10-26-bdd0a: the reason is the preset's own, so each excluded preset says why it is.</para>
/// </summary>
public static class HostOnlyRoutingRule
{
    public const string LabelMapField = "pipelineFromLabel";
    public const string DefaultField = "defaultPipeline";

    private const string Instead = " Route the label to a preset a ticket can run.";

    /// <summary>Every entry that names a preset no label can start: the field it sits in, and a reason
    /// naming the owner, the label and the value.</summary>
    public static IEnumerable<(string Field, string Reason)> Violations(
        string owner, IReadOnlyDictionary<string, string>? pipelineFromLabel, string? defaultPipeline)
    {
        foreach (var (label, pipeline) in pipelineFromLabel ?? new Dictionary<string, string>())
            if (PipelinePresets.NotRoutableBecause(pipeline) is { } why)
                yield return (LabelMapField,
                    $"{owner}: pipeline_from_label maps label '{label}' to pipeline '{pipeline}', which a label cannot start: {why}.{Instead}");
        if (!string.IsNullOrWhiteSpace(defaultPipeline) && PipelinePresets.NotRoutableBecause(defaultPipeline) is { } because)
            yield return (DefaultField,
                $"{owner}: default_pipeline is '{defaultPipeline}', which a label cannot start: {because}.{Instead}");
    }

    /// <summary>The save door: a tracker whose routing names a preset no label can start is refused.</summary>
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
