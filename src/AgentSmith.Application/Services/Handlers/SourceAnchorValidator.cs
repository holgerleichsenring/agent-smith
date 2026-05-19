using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Default <see cref="ISourceAnchorValidator"/>: passes through every
/// observation whose evidence_mode is not <see cref="EvidenceMode.AnalyzedFromSource"/>
/// (those are anchored by api_path / schema_name / scanner template_id),
/// passes through every observation when no read-set is supplied (e.g.
/// tests that bypass the runtime layer), and otherwise requires that
/// <see cref="SkillObservation.File"/> appears in the read-set (case-
/// insensitive). Failures are logged at Warning so operators see the
/// drop without it being load-bearing.
/// </summary>
public sealed class SourceAnchorValidator : ISourceAnchorValidator
{
    public bool IsAnchored(
        SkillObservation observation,
        IReadOnlyCollection<string>? readPaths,
        string role,
        ILogger? logger)
    {
        if (observation.EvidenceMode != EvidenceMode.AnalyzedFromSource) return true;
        if (readPaths is null) return true;
        if (string.IsNullOrWhiteSpace(observation.File))
        {
            logger?.LogWarning(
                "Skill {Role}: dropping analyzed_from_source observation '{Description}' — no file cited.",
                role, Preview(observation.Description));
            return false;
        }
        if (!Contains(readPaths, observation.File))
        {
            logger?.LogWarning(
                "Skill {Role}: dropping analyzed_from_source observation citing unread file '{File}' (read-set: {ReadCount} entries).",
                role, observation.File, readPaths.Count);
            return false;
        }
        return true;
    }

    private static bool Contains(IReadOnlyCollection<string> readPaths, string file) =>
        readPaths.Any(p => string.Equals(p, file, StringComparison.OrdinalIgnoreCase));

    private static string Preview(string text) =>
        text.Length > 80 ? text[..80] + "…" : text;
}
