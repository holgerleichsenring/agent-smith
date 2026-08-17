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

    /// <summary>p0429: puts every finding the master did not address to a fresh instance
    /// asked to REFUTE it against the real code, so a candidate promoted by the master's
    /// silence cannot reach delivery as a critical unsubstantiated.</summary>
    public const string SubstantiateFindings = "SubstantiateFindingsCommand";
}
