using System.Text.RegularExpressions;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Persistence;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0390: reads the cached work spec for a run from the artifact store. Mirrors
/// <see cref="PlanMarkdownReader"/> — the content of record lives in git on the
/// ticket branch, and this slot is the run detail's copy, written when the
/// revision is committed. Null when the run derived no spec or the cache has
/// expired; the dashboard then shows only the plan.
/// <para>
/// p0395: copies cached before the "Definition of done" rendering carry the raw
/// yaml key as a literal "- done: " list prefix; the read path strips it so old
/// runs render clean without rewriting their stored artifacts.
/// </para>
/// </summary>
public sealed partial class SpecMarkdownReader(IRunArtifactStore store)
{
    [GeneratedRegex(@"^(\s*)- done: ", RegexOptions.Multiline)]
    private static partial Regex LegacyDonePrefixRegex();

    public async Task<string?> ReadAsync(string runId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(runId)) return null;
        if (!RunIdGenerator.IsValid(runId)) return null;
        var markdown = await store.ReadSpecMarkdownAsync(runId, ct);
        return markdown is null ? null : LegacyDonePrefixRegex().Replace(markdown, "$1- ");
    }
}
