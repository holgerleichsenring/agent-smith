namespace AgentSmith.Domain.Models;

public sealed record TrxSummary(
    int TotalCount,
    int PassedCount,
    int FailedCount,
    IReadOnlyList<FailedTest> Failures)
{
    public static TrxSummary Empty { get; } = new(0, 0, 0, Array.Empty<FailedTest>());

    public TrxSummary Combine(TrxSummary other) => new(
        TotalCount + other.TotalCount,
        PassedCount + other.PassedCount,
        FailedCount + other.FailedCount,
        Failures.Concat(other.Failures).ToList());
}
