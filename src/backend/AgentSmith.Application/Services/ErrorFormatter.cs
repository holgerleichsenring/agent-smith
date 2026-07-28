using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services;

/// <summary>
/// Translates raw exception messages into user-friendly reasons.
/// Pure, stateless, and fully testable.
/// </summary>
public static class ErrorFormatter
{
    // p0387: usage/spend-limit errors carry a reset date worth surfacing, so
    // they are handled ahead of the static rule table (see UsageLimitMessage).
    private static readonly Regex UsageLimitPattern = new(
        @"(?:usage|spend(?:ing)?)\s+limits?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ResetDatePattern = new(
        @"on\s+(\d{4}-\d{2}-\d{2})\s+at\s+(\d{2}:\d{2}(?::\d{2})?)(?:\s*UTC)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly (Regex Pattern, string FriendlyMessage)[] Rules =
    [
        (new Regex(@"TF401179", RegexOptions.Compiled),
            "A pull request for this branch already exists."),
        (new Regex(@"non-fastforwardable", RegexOptions.Compiled),
            "The remote branch has conflicting history. Try again."),
        (new Regex(@"Could not connect|connection refused", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "Could not reach a required service. Check network connectivity."),
        (new Regex(@"401|Unauthorized|authentication", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "Authentication failed. Check your API tokens."),
        (new Regex(@"403|Forbidden", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "Permission denied. Check token scopes."),
        (new Regex(@"rate limit|429|529", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "AI provider rate limit hit. Wait a moment and retry."),
        (new Regex(@"not found|404", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "A required resource was not found. Check ticket number and project name."),
        (new Regex(@"timeout|timed out", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "The operation timed out. The service may be slow — try again."),
        (new Regex(@"No test framework detected", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            "No test framework found in the repository. Tests were skipped."),
    ];

    /// <summary>
    /// Converts a raw error message into a user-friendly explanation.
    /// Falls back to a truncated first line if no rule matches.
    /// </summary>
    public static string Humanize(string rawError)
    {
        foreach (var (pattern, friendly) in Rules)
            if (pattern.IsMatch(rawError))
                return friendly;

        return Truncate(rawError);
    }

    /// <summary>
    /// p0387: run-summary variant. Only a summary carrying a raw provider error
    /// payload (a JSON body with error.message) is translated; every other
    /// summary — curated keystone/ledger prose included — passes through
    /// byte-for-byte, never truncated or rule-matched.
    /// </summary>
    public static string HumanizeRunSummary(string summary)
    {
        var inner = ProviderErrorPayloadParser.TryExtractMessage(summary);
        if (inner is null)
            return summary;

        if (UsageLimitPattern.IsMatch(inner))
            return UsageLimitMessage(inner);

        foreach (var (pattern, friendly) in Rules)
            if (pattern.IsMatch(inner))
                return friendly;

        return Truncate(inner);
    }

    private static string UsageLimitMessage(string inner)
    {
        var reset = ResetDatePattern.Match(inner);
        return reset.Success
            ? $"AI provider usage limit reached — access resumes {reset.Groups[1].Value} {reset.Groups[2].Value} UTC."
            : "AI provider usage limit reached. Wait for the limit to reset or raise it.";
    }

    private static string Truncate(string rawError)
    {
        var first = rawError.Split('\n')[0].Trim();
        return first.Length > 120 ? first[..120] + "..." : first;
    }
}
