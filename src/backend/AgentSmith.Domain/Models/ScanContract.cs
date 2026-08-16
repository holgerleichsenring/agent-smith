namespace AgentSmith.Domain.Models;

/// <summary>
/// p0429: what a scan run states it is looking for, ratified before its first scanner
/// runs — the scan's half of the acceptance contract p0420/p0421 gave code runs.
/// </summary>
public sealed record ScanContract(IReadOnlyList<ScanCriterion> Criteria)
{
    public static ScanContract Empty { get; } = new([]);

    public IReadOnlyList<string> Statements => [.. Criteria.Select(c => c.Statement)];
}
