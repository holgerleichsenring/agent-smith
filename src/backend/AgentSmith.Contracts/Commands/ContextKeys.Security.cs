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
}
