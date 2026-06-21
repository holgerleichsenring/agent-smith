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
}
