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
/// <para>
/// p0429: promotion is no longer the END of the question. A fact the master never
/// addressed has no author — nine such facts once shipped as CRITICAL and all nine were
/// wrong — so the merge now NAMES them, and SubstantiateFindings puts each to a fresh
/// instance asked to refute it against the real code.
/// </para>
/// </summary>
public static class MasterFindingsMerger
{
    private const string StaticPatternRole = "static-pattern-scanner";

    /// <summary>
    /// Master-curated set + every uncovered High+ raw fact, minus static-pattern facts
    /// the master reviewed and dismissed — and, separately, which of them the master's
    /// silence promoted rather than vouched for.
    /// </summary>
    public static MasterFindingsMerge Merge(
        IReadOnlyList<SkillObservation> master,
        IReadOnlyList<SkillObservation> raw,
        IReadOnlyList<string>? masterReadPaths)
    {
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(raw);
        var masterLocations = master.Where(HasLocation)
            .Select(o => (o.File!, o.StartLine)).ToHashSet();
        var readFiles = NormalizeReadSet(masterReadPaths);
        var promoted = new List<SkillObservation>();
        var suppressedAsReviewed = 0;
        foreach (var r in raw)
        {
            if (!IsHighOrAbove(r.Severity)) continue;
            if (HasLocation(r) && masterLocations.Contains((r.File!, r.StartLine))) continue;
            if (IsReviewedStaticPattern(r, readFiles)) { suppressedAsReviewed++; continue; }
            promoted.Add(r);
        }
        return new MasterFindingsMerge([.. master, .. promoted], promoted, suppressedAsReviewed);
    }

    private static bool IsReviewedStaticPattern(SkillObservation r, IReadOnlySet<string> readFiles) =>
        readFiles.Count > 0
        && string.Equals(r.Role, StaticPatternRole, StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(r.File)
        && CitedPathMatch.AnyNames(readFiles, r.File!);

    private static HashSet<string> NormalizeReadSet(IReadOnlyList<string>? readPaths) =>
        readPaths is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : readPaths.Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(CitedPathMatch.Normalize).ToHashSet(StringComparer.Ordinal);

    private static bool HasLocation(SkillObservation o) =>
        !string.IsNullOrWhiteSpace(o.File) && o.StartLine > 0;

    private static bool IsHighOrAbove(ObservationSeverity s) =>
        s is ObservationSeverity.Critical or ObservationSeverity.High;
}
