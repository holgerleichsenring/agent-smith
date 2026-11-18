namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-11-17-3435a: what a host says about a pull request's CI on one head — one of <see cref="CiStates"/>, the head it
/// was read for, the newest time a check reported (null when none did), and for a red one the failed checks.
/// </summary>
public sealed record PullRequestCi(string State, string? HeadSha, DateTimeOffset? At, IReadOnlyList<CiFailure> Failures)
{
    public static PullRequestCi NoneOn(string? headSha) => new(CiStates.None, headSha, null, []);
}
