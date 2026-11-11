using System.Globalization;
using System.Text.Json.Nodes;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-11-10-ccd9 review: the moment Teams took an inbound message — the activity's <c>timestamp</c>, an ISO 8601
/// UTC time the Bot Framework sets on every activity it sends. Null when it is missing or unreadable.
/// </summary>
internal static class TeamsActivityTime
{
    internal static DateTimeOffset? Of(JsonNode activity) =>
        DateTimeOffset.TryParse(activity["timestamp"]?.GetValue<string>(), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var at) ? at : null;
}
