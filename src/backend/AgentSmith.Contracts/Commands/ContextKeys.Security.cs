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

    /// <summary>2026-08-30-03e4: why this scan's triage did not happen. Set by
    /// MergeMasterFindingsHandler when the scan master ran under the observation schema
    /// and produced nothing the merge could read, so raw scanner findings were delivered
    /// untriaged. ABSENT on a healthy run — its presence is the whole signal: the coverage
    /// account refuses the triage criterion and the delivered artefact carries the mark.</summary>
    public const string ScanTriageDegraded = "ScanTriageDegraded";

    /// <summary>p0429: the ScanContract ratified before the first scanner runs — what
    /// this scan states it is looking for. Read by AcceptanceCriteria as the run's
    /// contract and accounted for against the execution trail after delivery.</summary>
    public const string ScanContract = "ScanContract";

    /// <summary>2026-08-30-18e3: the station claims the scan master recorded through its
    /// own tool call, exactly as it stated them. Holds a StationClaimLog — a live collector
    /// the master and its sub-agents append to, never a finished artefact.</summary>
    public const string StationClaims = "StationClaims";

    /// <summary>2026-08-30-18e3: the checked map — per entry group, the six stations of a
    /// request, each located against the paths the scan really read or explicitly not.
    /// A REPORTING surface: rendered into the delivered artefact and raising findings of
    /// its own, never routed into the account the delivery gate reads.</summary>
    public const string RequestStationMap = "RequestStationMap";

    /// <summary>2026-08-30-0ea8: the release of the verification standard this run
    /// consulted, set by the lens the moment it is asked for entries. 5.0 renumbered the
    /// whole standard against its predecessor, so a requirement id an answer cites means
    /// nothing without the version that issued it.</summary>
    public const string VerificationCatalogueVersion = "VerificationCatalogueVersion";

    /// <summary>2026-08-30-03e1: the findings that named an entry of the verification
    /// standard, recorded through the scan's own tool call exactly as it stated them. Holds
    /// a CitedFindingLog — a live collector the master and its sub-agents append to, never
    /// a finished artefact.</summary>
    public const string RequirementCitations = "RequirementCitations";

    /// <summary>2026-08-30-03e1: the settled account — per entry group and station, whether
    /// the scan examined it and what it cited there. A REPORTING surface like the entry map
    /// it stands on: rendered into the delivered artefact and raising findings of its own,
    /// never routed into the account the delivery gate reads.</summary>
    public const string ScanExaminationAccount = "ScanExaminationAccount";
}
