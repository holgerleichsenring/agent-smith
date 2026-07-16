using AgentSmith.Contracts.Progress;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0341: the done-status honesty DIAGNOSTIC. A step marked done should have
/// touched its declared target; this finds done steps whose EXPLICIT target is
/// absent from the actual diff. It matches the target path against the change
/// paths by a DEFINED rule (equal, or a change path ends with "/target" after
/// separator normalisation) — never a fuzzy match of the free-text activity — and
/// SKIPS any done item with no target (so a target-less step is never a false
/// warning). The result is surfaced in result.md only; it never feeds the keystone
/// and never fails a run — completion stays p0340's.
/// </summary>
public static class ProgressLedgerCoverage
{
    public static IReadOnlyList<string> UnbackedDoneSteps(
        ProgressLedger ledger, IReadOnlyList<CodeChange> changes)
    {
        var paths = changes.Select(c => Normalize(c.Path.ToString())).ToList();
        var warnings = new List<string>();
        foreach (var e in ledger.Entries)
        {
            if (e.Status != ProgressStatus.Done || string.IsNullOrWhiteSpace(e.Target)) continue;
            if (!IsCovered(Normalize(e.Target!), paths))
                warnings.Add(
                    $"step {e.Id} \"{e.Activity}\" is marked done but its target '{e.Target}' "
                    + "is absent from the diff");
        }
        return warnings;
    }

    private static bool IsCovered(string target, IReadOnlyList<string> paths) =>
        paths.Any(p =>
            string.Equals(p, target, StringComparison.OrdinalIgnoreCase)
            || p.EndsWith("/" + target, StringComparison.OrdinalIgnoreCase));

    private static string Normalize(string p) => p.Replace('\\', '/').TrimStart('/');
}
