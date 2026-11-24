namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933dc: what a PoC scan finding is — a blocking secret, a blocking dependency vulnerability, or a
/// note that blocks nothing.</summary>
public static class PocScanFindingKinds
{
    public const string Secret = "secret";
    public const string Vulnerability = "vulnerability";
    public const string Note = "note";
}
