using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-22-b6ad: the files a series HOLDS, rendered in one place for the two writers that put
/// them there — the run's publish inside a sandbox, and filing's checkout-free write onto the
/// ticket branch. One renderer, because the first run after a filing must find the files it would
/// itself have written; two renderers is how two contents come to differ.
/// <para>
/// 2026-10-06-03c7d: the manifest at <c>series/{base}.yaml</c>, and per spec its yaml and markdown
/// companion in <c>specs/planned/</c>. The accounting is part of the manifest.
/// </para>
/// </summary>
public sealed class SeriesFiles(SeriesManifest manifest)
{
    /// <summary>Repo-relative path and whole content, in the order a writer may commit them.</summary>
    public IReadOnlyList<SpecSetFile> Render(SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (string.IsNullOrWhiteSpace(set.Series))
            throw new InvalidOperationException($"Spec set {set.Key} carries no series to name its manifest");
        var files = new List<SpecSetFile>(1 + (set.Phases.Count * 2))
        {
            new(SeriesPaths.Manifest(set.Series), manifest.Serialize(set)),
        };
        foreach (var phase in set.Phases)
        {
            files.Add(new SpecSetFile(
                SeriesPaths.Spec(SeriesPaths.Planned, phase.FileStem), phase.Draft.Yaml.TrimEnd() + "\n"));
            files.Add(new SpecSetFile(
                SeriesPaths.Companion(SeriesPaths.Planned, phase.FileStem), phase.Markdown));
        }
        return files;
    }
}
