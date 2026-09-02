using AgentSmith.Contracts.Services;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-28-cc40: the catalog root a SCORED scan reads — the release embedded in this
/// build, extracted once per process.
/// <para>
/// The harness's own two roots cannot carry a measurement. The stub root is an empty temp
/// directory and the checked-in fixture root has no <c>patterns/</c> at all, so the static
/// pattern scanner loads ZERO definitions under both and a detection score would have been
/// 0/N by construction, with nothing in the run saying so.
/// </para>
/// <para>
/// The tarball is the SUPPORTED source and the same one a deployed binary materialises, so
/// the eval reads the patterns and the master bodies an operator's run reads. Copying
/// pattern files into this repository would have made a second catalog that drifts.
/// </para>
/// </summary>
internal sealed class EmbeddedSkillsCatalogPath(
    IEmbeddedSkillsCatalog catalog, ICatalogTarballExtractor extractor) : ISkillsCatalogPath
{
    private static readonly Lock Gate = new();
    private static string? _root;

    public string Root => Extracted();

    public string Origin => $"embedded catalog {catalog.Version} at {Root}";

    /// <summary>Extraction is per PROCESS, not per composition: a scored run composes the
    /// harness several times and unpacking the release each time buys nothing.</summary>
    private string Extracted()
    {
        lock (Gate)
        {
            if (_root is not null) return _root;
            var directory = Path.Combine(
                Path.GetTempPath(), $"agentsmith-eval-catalog-{catalog.Version}");
            if (!Directory.Exists(Path.Combine(directory, "patterns")))
            {
                using var stream = catalog.Open();
                extractor.Extract(stream, directory);
            }
            _root = directory;
            return _root;
        }
    }
}
