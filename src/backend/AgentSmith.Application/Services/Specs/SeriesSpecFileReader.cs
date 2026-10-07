using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-06-03c7d: reads ONE spec of a series back off the branch — the single spec file whose
/// name starts with its id in a state directory, the same-stem markdown companion, its mocks and
/// the segments the manifest says it carries. The label is the file name's; the manifest lists
/// ids only. Two spec files for one id is refused: which of them is the spec is not this reader's
/// guess to make.
/// <para>
/// 2026-10-06-03c7e: a spec is resolved across the state directories. Its copy in
/// <c>specs/done/</c> wins — an executed spec is append-only, and a planned copy beside it is a
/// leftover the next revision removes. Only <c>done/</c> reads as executed; <c>active/</c> does not.
/// </para>
/// </summary>
public sealed class SeriesSpecFileReader(
    PhaseDraftReader draftReader,
    ILogger<SeriesSpecFileReader> logger)
{
    /// <param name="listed">Repo-relative paths of the state directories' files.</param>
    public async Task<SeriesSpecRead> ReadAsync(
        ISandboxFileReader files, IReadOnlyList<string> listed, string id,
        SeriesManifestDocument doc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(doc);
        var candidates = Candidates(listed, id);
        if (candidates.Count == 0)
            return SeriesSpecRead.Failed($"spec {id} is listed in its series manifest and has no file under {SeriesPaths.SpecsRoot}/");
        if (candidates.Count > 1)
            return SeriesSpecRead.Failed($"spec {id} has {candidates.Count} files: {string.Join(", ", candidates)}");
        return await ReadFileAsync(files, listed, candidates[0], id, doc, cancellationToken);
    }

    private static List<string> Candidates(IReadOnlyList<string> listed, string id)
    {
        var all = listed
            .Where(p => SeriesPaths.IsSpecFile(SeriesPaths.FileName(p))
                && SeriesPaths.BelongsTo(SeriesPaths.FileName(p), id))
            .Distinct(StringComparer.Ordinal).ToList();
        var done = all.Where(IsDone).ToList();
        return done.Count > 0 ? done : all;
    }

    private static bool IsDone(string path) =>
        path.StartsWith(SeriesPaths.Done + "/", StringComparison.Ordinal);

    private async Task<SeriesSpecRead> ReadFileAsync(
        ISandboxFileReader files, IReadOnlyList<string> listed, string path, string id,
        SeriesManifestDocument doc, CancellationToken ct)
    {
        var directory = path[..path.LastIndexOf('/')];
        var stem = SeriesPaths.StemOf(SeriesPaths.FileName(path));
        var yaml = await files.TryReadAsync(path, ct);
        if (string.IsNullOrWhiteSpace(yaml)) return SeriesSpecRead.Failed($"{path} is empty or unreadable");
        var markdown = await files.TryReadAsync(SeriesPaths.Companion(directory, stem), ct) ?? string.Empty;
        try
        {
            var draft = draftReader.Read(yaml);
            var inDirectory = listed.Where(p => p.StartsWith(directory + "/", StringComparison.Ordinal)).ToList();
            return SeriesSpecRead.Read(new SpecPhase(draft, LabelOf(stem, id), markdown, Carried(doc, draft.PhaseId),
                SpecPhaseMocks.Of(inDirectory, directory, draft.PhaseId)), IsDone(path));
        }
        catch (Exception ex) when (ex is InvalidOperationException or YamlDotNet.Core.YamlException)
        {
            logger.LogWarning(ex, "{Path} is not a readable spec", path);
            return SeriesSpecRead.Failed($"{path} did not read back as a spec");
        }
    }

    private static IReadOnlyList<int> Carried(SeriesManifestDocument doc, string phaseId) =>
        [.. doc.Carried
            .Where(c => string.Equals(c.Phase, phaseId, StringComparison.Ordinal))
            .Select(c => c.Segment)];

    private static string LabelOf(string stem, string id) =>
        stem.StartsWith($"{id}-", StringComparison.Ordinal) ? stem[(id.Length + 1)..] : stem;
}
