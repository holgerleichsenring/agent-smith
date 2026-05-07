using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Shared helpers for scanner handlers that emit SkillObservations alongside
/// their structured Result records. ParseSeverity uses an explicit mapping table
/// (case-insensitive) so common synonyms from external tools (Nuclei "critical",
/// ZAP "warning", static-pattern "note") land on the right ObservationSeverity.
/// AppendObservations does a read-modify-write of ContextKeys.SkillObservations.
/// </summary>
internal static class ScannerObservationFactory
{
    private static readonly Dictionary<string, ObservationSeverity> SeverityMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["critical"] = ObservationSeverity.High,
            ["crit"] = ObservationSeverity.High,
            ["high"] = ObservationSeverity.High,
            ["error"] = ObservationSeverity.High,
            ["medium"] = ObservationSeverity.Medium,
            ["med"] = ObservationSeverity.Medium,
            ["warning"] = ObservationSeverity.Medium,
            ["warn"] = ObservationSeverity.Medium,
            ["low"] = ObservationSeverity.Low,
            ["note"] = ObservationSeverity.Low,
            ["info"] = ObservationSeverity.Info,
            ["informational"] = ObservationSeverity.Info,
            ["none"] = ObservationSeverity.Info,
        };

    private static readonly HashSet<string> WarnedUnknown =
        new(StringComparer.OrdinalIgnoreCase);

    internal static ObservationSeverity ParseSeverity(string raw, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(raw)) return ObservationSeverity.Info;
        if (SeverityMap.TryGetValue(raw.Trim(), out var mapped)) return mapped;
        if (WarnedUnknown.Add(raw))
            logger?.LogWarning(
                "Unknown severity value '{Severity}' from scanner — defaulted to Info. Add to ScannerObservationFactory.SeverityMap if recurring.",
                raw);
        return ObservationSeverity.Info;
    }

    internal static void AppendObservations(
        PipelineContext pipeline, IReadOnlyList<SkillObservation> additions)
    {
        if (additions.Count == 0) return;
        var existing = pipeline.TryGet<List<SkillObservation>>(
            ContextKeys.SkillObservations, out var list) && list is not null
            ? list
            : [];
        existing.AddRange(additions);
        pipeline.Set(ContextKeys.SkillObservations, existing);
    }
}
