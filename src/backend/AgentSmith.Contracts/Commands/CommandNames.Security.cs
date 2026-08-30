namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Security-pipeline command names: SecuritySkillRound (discussion-loop equivalent),
/// the three scanner steps (static / git-history / dependency-audit), the finding-
/// compression + trend + snapshot-write steps, and the SpawnFix auto-remediation step.
/// </summary>
public static partial class CommandNames
{
    public const string SecuritySkillRound = "SecuritySkillRoundCommand";

    public const string StaticPatternScan = "StaticPatternScanCommand";
    public const string GitHistoryScan = "GitHistoryScanCommand";
    public const string DependencyAudit = "DependencyAuditCommand";

    public const string CompressSecurityFindings = "CompressSecurityFindingsCommand";

    /// <summary>p0277: merges the security-master's triaged observation array into
    /// SkillObservations between AgenticMaster and DeliverFindings — master-curated set
    /// plus every uncovered High+ deterministic scanner fact (refine-with-safety-net).</summary>
    public const string MergeMasterFindings = "MergeMasterFindingsCommand";

    public const string SecurityTrend = "SecurityTrendCommand";
    public const string SecuritySnapshotWrite = "SecuritySnapshotWriteCommand";

    public const string SpawnFix = "SpawnFixCommand";

    /// <summary>p0429: states what the scan is looking for BEFORE the first scanner
    /// runs, so a target that goes unanswered is a named miss rather than silence.</summary>
    public const string RatifyScanContract = "RatifyScanContractCommand";

    /// <summary>p0429: accounts for every ratified scan criterion against the execution
    /// trail, so the one delivery gate judges a scan like any other run.</summary>
    public const string AccountScanCoverage = "AccountScanCoverageCommand";

    /// <summary>2026-08-30-18e3: checks the scan master's stated entry map against the paths
    /// the run really read, and raises a finding for every station nothing located. Security
    /// scan only — an api scan runs its source checkout fail-soft and often holds no source,
    /// so a located station is a question it cannot be asked.</summary>
    public const string AccountEntryStations = "AccountEntryStationsCommand";

    /// <summary>2026-08-30-3c12: settles the answers the scan gave to the verification
    /// standard's entries — per entry group and station, met, unmet, undecidable, unanswered
    /// or never attempted — and names each gap. Security scan only, and it follows the entry
    /// map: a station whose home is unknown cannot be asked what holds there.</summary>
    public const string AccountRequirementAnswers = "AccountRequirementAnswersCommand";

    /// <summary>p0429: puts every finding the master did not address to a fresh instance
    /// asked to REFUTE it against the real code, so a candidate promoted by the master's
    /// silence cannot reach delivery as a critical unsubstantiated.</summary>
    public const string SubstantiateFindings = "SubstantiateFindingsCommand";

    /// <summary>2026-08-30-c6ec: states which capability the served interface offers that
    /// no declared first-party client exercises — an observation per difference, paired
    /// with the requirement id that would decide whether it matters.</summary>
    public const string AccountSurfaceDifference = "AccountSurfaceDifferenceCommand";
}
