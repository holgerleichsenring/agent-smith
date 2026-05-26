using AgentSmith.Infrastructure.Models;
using StackExchange.Redis;

namespace AgentSmith.Server.Services;

/// <summary>
/// p0169b: deserialize a Redis Stream entry into a <see cref="BusMessage"/>.
/// Mirrors RedisMessageBus's private deserializer — kept narrow so the
/// SSE subscriber can read entries without pulling Infrastructure into
/// the Server endpoint dependency graph.
/// </summary>
internal static class MessageBusEntrySerializer
{
    public static BusMessage? TryDeserialize(string jobId, NameValueEntry[] values)
    {
        try
        {
            var dict = values.ToDictionary(e => (string)e.Name!, e => (string?)e.Value);
            if (!dict.TryGetValue("type", out var typeStr) ||
                !Enum.TryParse<BusMessageType>(typeStr, out var type))
                return null;

            return type switch
            {
                BusMessageType.Progress => BusMessage.Progress(
                    jobId,
                    int.TryParse(dict.GetValueOrDefault("step"), out var s) ? s : 0,
                    int.TryParse(dict.GetValueOrDefault("total"), out var t) ? t : 0,
                    dict.GetValueOrDefault("text") ?? ""),

                BusMessageType.Detail => BusMessage.Detail(
                    jobId, dict.GetValueOrDefault("text") ?? ""),

                BusMessageType.Question => BusMessage.Question(
                    jobId,
                    dict.GetValueOrDefault("questionId") ?? "",
                    dict.GetValueOrDefault("text") ?? ""),

                BusMessageType.Done => BusMessage.Done(
                    jobId,
                    dict.GetValueOrDefault("prUrl"),
                    dict.GetValueOrDefault("summary") ?? ""),

                BusMessageType.Error => BusMessage.Error(
                    jobId,
                    dict.GetValueOrDefault("text") ?? "", 0, 0, string.Empty),

                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }
}
