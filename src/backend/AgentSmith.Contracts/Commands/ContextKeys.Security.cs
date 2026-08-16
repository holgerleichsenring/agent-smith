namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Security-pipeline PipelineContext keys: scanner outputs (static / git-history /
/// dependency-audit), aggregated finding summaries, trend analysis, and the
/// SpawnFix auto-remediation payload. Also carries scan-specific identifiers
/// (PR id, branch) used by the security-scan preset.
/// </summary>
public static partial class ContextKeys
{
    public const string ScanPrIdentifier = "ScanPrIdentifier";
    public const string ScanBranch = "ScanBranch";

    public const string StaticScanResult = "StaticScanResult";
    public const string GitHistoryScanResult = "GitHistoryScanResult";
    public const string DependencyAuditResult = "DependencyAuditResult";

    public const string SecurityFindingsSummary = "SecurityFindingsSummary";
    public const string SecurityFindingsByCategory = "SecurityFindingsByCategory";
    public const string SecurityTrend = "SecurityTrend";
    public const string SecurityFixRequests = "SecurityFixRequests";

    public const string SkillObservations = "SkillObservations";

    /// <summary>p0277: the pre-merge raw deterministic scanner observations, stashed by
    /// MergeMasterFindingsHandler before it replaces SkillObservations with the merged
    /// (master-triaged) set. SecuritySnapshotWriter reads this so the snapshot's finding
    /// counts stay on the raw basis the next run's git-based SecurityTrend compares
    /// against — the merge changes DELIVERY only, not the trend metric.</summary>
    public const string RawScannerObservations = "RawScannerObservations";

    /// <summary>p0429: the findings the scan master's SILENCE promoted into delivery —
    /// facts it never addressed, so nobody vouched for them. SubstantiateFindings puts
    /// each to a fresh instance asked to refute it against the real code.</summary>
    public const string UnvouchedFindings = "UnvouchedFindings";

    /// <summary>p0429: the ScanContract ratified before the first scanner runs — what
    /// this scan states it is looking for. Read by AcceptanceCriteria as the run's
    /// contract and accounted for against the execution trail after delivery.</summary>
    public const string ScanContract = "ScanContract";
}
