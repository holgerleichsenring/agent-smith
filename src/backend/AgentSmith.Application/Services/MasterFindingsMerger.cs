using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0333: merges a scan-master's triaged observations with the deterministic scanners'
/// raw facts. A High+ raw fact ships unless the master already addressed its
/// (File, StartLine), OR — for in-source static-pattern facts — the master READ the
/// file and chose not to flag it (implicit rejection: it saw the code and dismissed it).
/// git-history secrets and dependency CVEs are NEVER suppressed by the read-set, because
/// reading current source does not refute a historical leak or a vulnerable package. An
/// empty read-set (no evidence the master looked) suppresses nothing, preserving the
/// p0277 promote-all-uncovered safety net.
/// </summary>
public static class MasterFindingsMerger
{
    private const string StaticPatternRole = "static-pattern-scanner";

    /// <summary>
    /// Master-curated set + every uncovered High+ raw fact, minus static-pattern facts
    /// the master reviewed and dismissed. <paramref name="suppressedAsReviewed"/> reports
    /// how many static-pattern facts were dropped as master-reviewed (for honest logging).
    /// </summary>
    public static List<SkillObservation> Merge(
        IReadOnlyList<SkillObservation> master,
        IReadOnlyList<SkillObservation> raw,
        IReadOnlyList<string>? masterReadPaths,
        out int suppressedAsReviewed)
    {
        var masterLocations = master.Where(HasLocation)
            .Select(o => (o.File!, o.StartLine)).ToHashSet();
        var readFiles = NormalizeReadSet(masterReadPaths);
        var result = new List<SkillObservation>(master);
        suppressedAsReviewed = 0;
        foreach (var r in raw)
        {
            if (!IsHighOrAbove(r.Severity)) continue;
            if (HasLocation(r) && masterLocations.Contains((r.File!, r.StartLine))) continue;
            if (IsReviewedStaticPattern(r, readFiles)) { suppressedAsReviewed++; continue; }
            result.Add(r);
        }
        return result;
    }

    private static bool IsReviewedStaticPattern(SkillObservation r, IReadOnlySet<string> readFiles) =>
        readFiles.Count > 0
        && string.Equals(r.Role, StaticPatternRole, StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(r.File)
        && ReadSetContains(readFiles, r.File!);

    // Suffix match on a segment boundary absorbs the sandbox workdir/context prefix
    // mismatch (read-set 'default/x/y.cs' vs scanner 'x/y.cs') without matching a.cs to ba.cs.
    private static bool ReadSetContains(IReadOnlySet<string> readFiles, string file)
    {
        var f = NormalizePath(file);
        foreach (var r in readFiles)
            if (r == f
                || r.EndsWith("/" + f, StringComparison.Ordinal)
                || f.EndsWith("/" + r, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static HashSet<string> NormalizeReadSet(IReadOnlyList<string>? readPaths) =>
        readPaths is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : readPaths.Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(NormalizePath).ToHashSet(StringComparer.Ordinal);

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').TrimStart('.', '/');

    private static bool HasLocation(SkillObservation o) =>
        !string.IsNullOrWhiteSpace(o.File) && o.StartLine > 0;

    private static bool IsHighOrAbove(ObservationSeverity s) =>
        s is ObservationSeverity.Critical or ObservationSeverity.High;
}
