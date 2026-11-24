using System.Text.RegularExpressions;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Build;

/// <summary>
/// 2026-11-23-933dd: the advisories the image gate ignores — for every id overruled on the revision at either stage,
/// the matching finding of that stage's verdict gives its bare advisory id (after 'osv:' or 'trivy:', up to the first
/// '|') and its aliases, because OSV reports GHSA/PYSEC ids where Trivy reports CVEs. Only ids the pipeline's guard
/// accepts are passed; a secret is never ignorable.
/// </summary>
public sealed partial class PocTrivyIgnore
{
    public IReadOnlySet<string> Of(PocScanSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var overruled = PocOverruledIds.FromJson(subject.OverruledIdsJson);
        return new[] { (PocScanStages.Code, subject.VerdictJson), (PocScanStages.Image, subject.ImageVerdictJson) }
            .SelectMany(stage => Advisories(PocScanVerdict.FromJson(stage.Item2), overruled.Of(stage.Item1)))
            .Where(id => Accepted().IsMatch(id))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<string> Advisories(PocScanVerdict? verdict, IReadOnlyList<string> lifted) =>
        verdict is null ? [] : verdict.Blocks.Concat(verdict.Notes)
            .Where(f => f.Kind != PocScanFindingKinds.Secret && lifted.Contains(f.Id, StringComparer.Ordinal))
            .SelectMany(f => f.Aliases.Prepend(Bare(f.Id)));

    private static string Bare(string id)
    {
        var start = id.IndexOf(':', StringComparison.Ordinal) + 1;
        var end = id.IndexOf('|', StringComparison.Ordinal);
        return end > start ? id[start..end] : id[start..];
    }

    [GeneratedRegex("^[A-Z][A-Z0-9]*-[A-Za-z0-9._:-]{1,80}$")]
    private static partial Regex Accepted();
}
