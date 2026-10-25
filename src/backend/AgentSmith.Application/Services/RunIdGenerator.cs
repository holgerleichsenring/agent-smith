using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services;

/// <summary>
/// Canonical run-identifier format: UTC ISO-8601 timestamp (colons replaced with
/// dashes for filesystem safety) plus a fixed-width 4-hex random suffix.
/// Example: <c>2026-05-20T22-27-43-8a3f</c>. Lexicographically sortable across
/// the second boundary; same-second collisions are absorbed by the suffix
/// (16-bit keyspace).
/// </summary>
public static class RunIdGenerator
{
    private const string TimestampFormat = "yyyy-MM-ddTHH-mm-ss";
    private const int SuffixSpace = 0x10000;

    public static readonly Regex CanonicalRegex = new(
        @"^\d{4}-\d{2}-\d{2}T\d{2}-\d{2}-\d{2}-[0-9a-f]{4}$",
        RegexOptions.Compiled);

    public static string Generate(DateTimeOffset startedAtUtc)
    {
        var suffix = Random.Shared.Next(SuffixSpace).ToString("x4");
        return $"{TimestampOf(startedAtUtc)}-{suffix}";
    }

    /// <summary>
    /// 2026-10-25-0099b: the id prefix of an instant — every id minted at or after it compares at or above it
    /// as text, so a time window is a range of ids, which SQL compares on every provider.
    /// </summary>
    public static string TimestampOf(DateTimeOffset at) =>
        at.UtcDateTime.ToString(TimestampFormat, System.Globalization.CultureInfo.InvariantCulture);

    public static bool IsValid(string runId) => CanonicalRegex.IsMatch(runId);

    /// <summary>
    /// Operator-facing rendering. Maps <c>2026-05-20T22-27-43-8a3f</c> to
    /// <c>2026-05-20 22:27:43 UTC (8a3f)</c>. Falls back to the verbatim value
    /// when the input does not match the canonical shape (e.g. legacy data).
    /// </summary>
    public static string FormatForDisplay(string runId)
    {
        if (!IsValid(runId)) return runId;
        var date = runId[..10];
        var time = runId.Substring(11, 8).Replace('-', ':');
        var suffix = runId[^4..];
        return $"{date} {time} UTC ({suffix})";
    }
}
