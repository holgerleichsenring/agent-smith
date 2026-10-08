using System.Globalization;
using System.Text.Json;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-e8b9b: the rework act a comment payload describes — its author and the HOST's time
/// for it. Jira writes offsets without a colon ("+0000"), which is normalised; a payload without a
/// parseable time yields no act, never the receipt time.
/// </summary>
public static class PayloadActTime
{
    public static ReworkAct? Act(string? author, string? at) =>
        Parse(at) is { } time ? new ReworkAct(author ?? string.Empty, time) : null;

    public static string? Text(JsonElement element, params string[] path)
    {
        foreach (var segment in path)
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(segment, out element))
                return null;
        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    private static DateTimeOffset? Parse(string? at)
    {
        if (string.IsNullOrWhiteSpace(at)) return null;
        var text = at.Length > 5 && (at[^5] == '+' || at[^5] == '-') && char.IsDigit(at[^1])
            ? at[..^2] + ":" + at[^2..]
            : at;
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed : null;
    }
}
