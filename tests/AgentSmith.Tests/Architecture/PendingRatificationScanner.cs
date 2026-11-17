using System.Text.RegularExpressions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-11-16-3596: counts the markers of a decision merged without the operator's answer.
/// 'default pending' is not one: it also names a column default ("verdicts: pending | ratified,
/// default pending"), and every real case also says "pending operator ratification".
/// </summary>
internal static class PendingRatificationScanner
{
    private static readonly Regex Marker = new(
        @"pending (operator )?ratification|ratification pending",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static int Count(string text) => Marker.Matches(text).Count;
}
