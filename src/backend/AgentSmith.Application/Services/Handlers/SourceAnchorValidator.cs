using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Default <see cref="ISourceAnchorValidator"/>: passes through every
/// observation whose evidence_mode is not <see cref="EvidenceMode.AnalyzedFromSource"/>
/// (those are anchored by api_path / schema_name / scanner template_id),
/// and every observation when no read-set is supplied (e.g. tests that
/// bypass the runtime layer). For <c>analyzed_from_source</c> claims it
/// requires that <see cref="SkillObservation.File"/> is set AND appears
/// in the read-set (case-insensitive). Mislabeled observations are not
/// dropped — they are downgraded to <see cref="EvidenceMode.Potential"/>
/// with the unverified <c>File</c> cleared, and a warning is logged so
/// the operator sees the prompt drift.
/// </summary>
public sealed class SourceAnchorValidator : ISourceAnchorValidator
{
    public SkillObservation EnforceAnchor(
        SkillObservation observation,
        IReadOnlyCollection<string>? readPaths,
        string role,
        ILogger? logger)
    {
        if (observation.EvidenceMode != EvidenceMode.AnalyzedFromSource) return observation;
        if (readPaths is null) return observation;

        if (string.IsNullOrWhiteSpace(observation.File))
        {
            logger?.LogWarning(
                "Skill {Role}: downgrading analyzed_from_source observation '{Description}' to potential — no file cited.",
                role, Preview(observation.Description));
            return observation with { EvidenceMode = EvidenceMode.Potential, File = null };
        }

        if (!ReadPathNormalizer.WasRead(readPaths, observation.File))
        {
            logger?.LogWarning(
                "Skill {Role}: downgrading analyzed_from_source observation citing unread file '{File}' to potential (read-set: {ReadCount} entries).",
                role, observation.File, readPaths.Count);
            return observation with { EvidenceMode = EvidenceMode.Potential, File = null };
        }

        return observation;
    }

    private static string Preview(string text) =>
        text.Length > 80 ? text[..80] + "…" : text;
}
