namespace AgentSmith.Contracts.Models.Preflight;

/// <summary>
/// p0324: result of one preflight check. <see cref="Message"/> is the human-first
/// finding (secret-free); <see cref="FixHint"/> is the actionable next step and is
/// mandatory on failure — a red check the operator cannot act on is worthless.
/// </summary>
public sealed record PreflightCheckResult(
    PreflightStatus Status,
    string Message,
    string? FixHint = null)
{
    public static PreflightCheckResult Pass(string message) =>
        new(PreflightStatus.Pass, message);

    public static PreflightCheckResult Fail(string message, string fixHint) =>
        new(PreflightStatus.Fail, message, fixHint);

    public static PreflightCheckResult Skip(string reason) =>
        new(PreflightStatus.Skip, reason);
}
