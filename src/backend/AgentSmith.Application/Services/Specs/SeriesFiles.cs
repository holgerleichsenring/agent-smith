using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-22-b6ad: the files a series HOLDS, rendered in one place for the two writers that put
/// them there — the run's publish inside a sandbox, and filing's checkout-free write onto the
/// ticket branch. One renderer, because the first run after a filing must find the files it would
/// itself have written; two renderers is how two contents come to differ.
/// <para>
/// 2026-10-06-03c7d: per spec its yaml and markdown companion in <c>specs/planned/</c>.
/// </para>
/// <para>
/// 2026-10-06-03c7e: PLANNED specs only. An executed spec lies in <c>specs/done/</c>, which only
/// the record step writes; a re-cut or an amendment never renders it back into planned/.
/// </para>
/// <para>
/// 2026-11-28-db88a: and no series manifest any more. The set lives on the ticket's row; the spec files are its written
/// record, so a set with nothing planned (a hand-back, a sub-series whose specs all ran) renders nothing.
/// </para>
/// </summary>
public sealed class SeriesFiles
{
    /// <summary>Repo-relative path and whole content, in the order a writer may stage them.</summary>
    public IReadOnlyList<SpecSetFile> Render(SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var files = new List<SpecSetFile>(set.Specs.Count * 2);
        foreach (var spec in set.Planned()) // 2026-11-27-3532d: a retired spec keeps its file
        {
            files.Add(new SpecSetFile(
                SeriesPaths.Spec(SeriesPaths.Planned, spec.FileStem), spec.Draft.Yaml.TrimEnd() + "\n"));
            // 2026-10-08-e8b9f: an approved spec has no companion — no empty file stands in for one.
            if (string.IsNullOrWhiteSpace(spec.Markdown)) continue;
            files.Add(new SpecSetFile(
                SeriesPaths.Companion(SeriesPaths.Planned, spec.FileStem), spec.Markdown));
        }
        return files;
    }
}
