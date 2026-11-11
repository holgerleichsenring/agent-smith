using System.Globalization;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// 2026-11-11-205fh: a Slack ts — seconds since the epoch with a microsecond fraction — as a time. One reading for the
/// ts of an inbound event and the ts chat.postMessage returns, so both sides of a typed binding read one clock.
/// </summary>
internal static class SlackTimestamp
{
    internal static DateTimeOffset? ToTime(string? ts) =>
        decimal.TryParse(ts, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.UnixEpoch.AddTicks((long)(seconds * TimeSpan.TicksPerSecond))
            : null;
}
