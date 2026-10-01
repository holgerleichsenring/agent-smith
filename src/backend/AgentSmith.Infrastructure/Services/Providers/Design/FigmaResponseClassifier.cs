using System.Net;
using AgentSmith.Contracts.Models.Design;

namespace AgentSmith.Infrastructure.Services.Providers.Design;

/// <summary>
/// 2026-10-01-7f7ab: maps a failed Figma response to its failure kind from the status and the
/// Retry-After header alone. The body is never read: Figma's error text is not the model's to
/// see, and a body is where a misbehaving proxy would echo a request header back.
/// </summary>
internal static class FigmaResponseClassifier
{
    public static FigmaReadFailure Classify(HttpResponseMessage response) => response.StatusCode switch
    {
        HttpStatusCode.NotFound => new(FigmaReadFailureKind.NotFound, "Figma knows no such file or node"),
        HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized => new(
            FigmaReadFailureKind.Forbidden, "the token is invalid or has no access to this file"),
        HttpStatusCode.TooManyRequests => RateLimited(response),
        _ => new(FigmaReadFailureKind.Unknown, $"Figma answered HTTP {(int)response.StatusCode}"),
    };

    private static FigmaReadFailure RateLimited(HttpResponseMessage response)
    {
        var wait = response.Headers.RetryAfter switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date - DateTimeOffset.UtcNow,
            _ => (TimeSpan?)null,
        };
        if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;
        return new FigmaReadFailure(FigmaReadFailureKind.RateLimited,
            wait is { } w ? $"Figma asks to wait {Math.Ceiling(w.TotalSeconds)}s" : "Figma rate limit reached",
            wait);
    }
}
