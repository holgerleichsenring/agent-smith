using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-22-6ad7: reads ONE phase of a set back off the ticket branch — its yaml, its markdown
/// companion and the segments the index says it carries.
/// <para>
/// Extracted from <see cref="SpecSetReader"/>, which now answers a question of its own: which of
/// the ways a branch can hold no set this one is. Reading a file back as a spec is the other
/// question, and it is the same one for every phase in the list.
/// </para>
/// </summary>
public sealed class SpecSetPhaseFileReader(
    PhaseDraftReader draftReader,
    ILogger<SpecSetPhaseFileReader> logger)
{
    /// <summary>The phase, or null when the file is missing or is not a readable spec.</summary>
    public async Task<SpecPhase?> ReadAsync(
        ISandboxFileReader files, SpecSetKey key, string stem,
        SpecSetIndexDocument doc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(doc);
        var yaml = await files.TryReadAsync(key.YamlPath(stem), cancellationToken);
        if (string.IsNullOrWhiteSpace(yaml)) return null;
        var markdown = await files.TryReadAsync(key.MarkdownPath(stem), cancellationToken)
            ?? string.Empty;
        try
        {
            var draft = draftReader.Read(yaml!);
            return new SpecPhase(draft, SlugOf(stem, draft.PhaseId), markdown, Carried(doc, draft.PhaseId));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Path} is not a readable phase spec", key.YamlPath(stem));
            return null;
        }
    }

    private static IReadOnlyList<int> Carried(SpecSetIndexDocument doc, string phaseId) =>
        [.. doc.Carried
            .Where(c => string.Equals(c.Phase, phaseId, StringComparison.Ordinal))
            .Select(c => c.Segment)];

    private static string SlugOf(string stem, string phaseId) =>
        stem.StartsWith($"{phaseId}-", StringComparison.Ordinal)
            ? stem[(phaseId.Length + 1)..]
            : stem;
}
