using AgentSmith.Contracts.Expectations;

namespace AgentSmith.Application.Services.Expectations;

/// <summary>
/// p0328: outcome of parsing a ratification answer. Exactly one of:
/// a ratified expectation (verbatim/edited/unratified), an explicit rejection
/// (Expectation carries the rejected record for persistence), or an
/// unparseable answer (Error set — the step fails loudly with guidance).
/// </summary>
public sealed record ExpectationRatificationResult(
    RatifiedExpectation? Expectation,
    bool IsRejected,
    string? RejectComment,
    string? Error)
{
    public static ExpectationRatificationResult Ratified(RatifiedExpectation expectation) =>
        new(expectation, false, null, null);

    public static ExpectationRatificationResult Rejected(
        RatifiedExpectation record, string? comment) => new(record, true, comment, null);

    public static ExpectationRatificationResult Unparseable(string error) =>
        new(null, false, null, error);
}
