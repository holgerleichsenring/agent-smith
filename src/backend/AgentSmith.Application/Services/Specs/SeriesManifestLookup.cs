using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-06-03c7d: what looking for a ticket's manifest found — the manifest and the base its
/// file name carries, or the branch answer that stands in its place (nothing, or unreadable).
/// </summary>
public sealed record SeriesManifestLookup(
    string? Base, SeriesManifestDocument? Document, SpecSetOnBranch? Otherwise)
{
    public static SeriesManifestLookup Found(string seriesBase, SeriesManifestDocument document) =>
        new(seriesBase, document, null);

    public static SeriesManifestLookup Not(SpecSetOnBranch answer) => new(null, null, answer);
}
