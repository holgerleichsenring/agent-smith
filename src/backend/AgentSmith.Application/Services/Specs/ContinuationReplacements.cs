using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-27-3532d: where the replacements an approval appended stand when <see cref="ContinuationMerge"/> applies it —
/// discarded when the branch moved past the retirement, else right behind the spec each supersedes.
/// </summary>
public static class ContinuationReplacements
{
    // 2026-11-27-3532d: a replacement of a spec that ran since the approval is discarded and maps back to that spec; one
    // of a spec a run retired meanwhile is discarded and maps to the replacement that run wrote — one replacement stands.
    public static (List<DerivedSpec> Standing, List<(string ApprovedId, string StandsInstead)> Discarded) Standing(
        SpecSet branch, IReadOnlyList<(string ApprovedId, DerivedSpec Spec)> appended, List<string> notes)
    {
        var standing = new List<DerivedSpec>();
        var discarded = new List<(string, string)>();
        var retired = branch.Retired();
        foreach (var (approvedId, spec) in appended)
        {
            if (spec.Draft.Supersedes.FirstOrDefault(id => branch.Executed.Contains(id, StringComparer.Ordinal)) is { } ran)
            {
                notes.Add($"spec {ran} ran since the approval was given, so it is kept as it ran and its retirement is "
                    + $"discarded with its replacement {spec.SpecId}");
                discarded.Add((approvedId, ran));
            }
            else if (spec.Draft.Supersedes.FirstOrDefault(retired.Contains) is { } gone)
            {
                var other = branch.Specs.Last(s => s.Draft.Supersedes.Contains(gone, StringComparer.Ordinal)).SpecId;
                notes.Add($"spec {gone} was retired for {other} since the approval was given, so the approval's replacement "
                    + $"{spec.SpecId} is discarded");
                discarded.Add((approvedId, other));
            }
            else standing.Add(spec);
        }
        return (standing, discarded);
    }

    // The appended specs after the kept ones, a replacement right behind the spec it supersedes.
    public static IReadOnlyList<DerivedSpec> Placed(IReadOnlyList<DerivedSpec> kept, IReadOnlyList<DerivedSpec> appended)
    {
        var ids = kept.Select(s => s.SpecId).ToHashSet(StringComparer.Ordinal);
        var placed = SpecSupersession.Ordered([.. kept, .. appended.Where(s => s.Draft.Supersedes.Any(ids.Contains))]);
        return [.. placed, .. appended.Where(s => !s.Draft.Supersedes.Any(ids.Contains))];
    }
}
