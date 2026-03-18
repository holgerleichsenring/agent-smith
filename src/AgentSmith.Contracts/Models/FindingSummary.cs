namespace AgentSmith.Contracts.Services;

/// <summary>
/// Aggregated severity counts from a list of findings.
/// </summary>
public sealed record FindingSummary(int Total, int High, int Medium, int Low)
{
    public static FindingSummary From(IReadOnlyList<Finding> findings) => new(
        findings.Count,
        findings.Count(f => f.Severity.Equals("HIGH", StringComparison.OrdinalIgnoreCase)),
        findings.Count(f => f.Severity.Equals("MEDIUM", StringComparison.OrdinalIgnoreCase)),
        findings.Count(f => f.Severity.Equals("LOW", StringComparison.OrdinalIgnoreCase)));
}
