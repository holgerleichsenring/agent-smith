namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-23-933db: the steps of the PoC runs.</summary>
public static partial class CommandNames
{
    /// <summary>Checks a PoC's uploaded archive in process and records its revision validated or refused.</summary>
    public const string PocIntake = "PocIntakeCommand";

    /// <summary>2026-11-23-933dc: scans a validated PoC revision for secrets and dependency vulnerabilities and blocks it
    /// before anything is committed.</summary>
    public const string PocCodeScan = "PocCodeScanCommand";

    /// <summary>2026-11-23-933dc: commits a passed PoC revision into its folder of the target's source repo.</summary>
    public const string PocSourceCommit = "PocSourceCommitCommand";
}
