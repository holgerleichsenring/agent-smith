namespace AgentSmith.Contracts.Models.Design;

/// <summary>
/// 2026-10-01-7f7ab: a failed Figma read. <see cref="Detail"/> is composed by the client from
/// the status alone — a response body never reaches it, and neither does the token.
/// <see cref="RetryAfter"/> is the wait Figma asked for on a rate limit.
/// </summary>
public sealed record FigmaReadFailure(FigmaReadFailureKind Kind, string Detail, TimeSpan? RetryAfter = null)
{
    /// <summary>The word a tool reports the kind as: not_found, forbidden, rate_limited, ….</summary>
    public string KindWord => Kind switch
    {
        FigmaReadFailureKind.NotFound => "not_found",
        FigmaReadFailureKind.Forbidden => "forbidden",
        FigmaReadFailureKind.RateLimited => "rate_limited",
        FigmaReadFailureKind.Unreachable => "unreachable",
        _ => "unknown",
    };
}
