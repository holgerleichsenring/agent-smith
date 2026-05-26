using System.Text;
using System.Text.Json;
using AgentSmith.Infrastructure.Models;

namespace AgentSmith.Server.Services;

/// <summary>
/// p0169b: serialises a BusMessage into the SSE wire format
/// (event: name\ndata: { json }\n\n).
/// Event names follow the spec's vocabulary (progress / done / error).
/// tool_call + skill_observation events are reserved in the contract but
/// not yet emitted from the Redis bus — they will land when the pipeline
/// publishes them directly to the stream.
/// </summary>
public static class SseEventWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Format(BusMessage message)
    {
        var (eventName, payload) = ToEvent(message);
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var sb = new StringBuilder();
        sb.Append("event: ").Append(eventName).Append('\n');
        sb.Append("data: ").Append(json).Append('\n').Append('\n');
        return sb.ToString();
    }

    private static (string Event, object Payload) ToEvent(BusMessage m) => m.Type switch
    {
        BusMessageType.Progress => ("progress", new
        {
            step = m.Step ?? 0,
            total = m.Total ?? 0,
            command_name = m.StepName ?? m.Text,
        }),
        BusMessageType.Detail => ("tool_call", new
        {
            tool_name = "detail",
            args_preview = Truncate(m.Text, 200),
        }),
        BusMessageType.Done => ("done", new
        {
            run_id = m.JobId,
            summary = m.Summary ?? string.Empty,
            pr_url = m.PrUrl,
        }),
        BusMessageType.Error => ("error", new
        {
            run_id = m.JobId,
            error_context = m.Text,
        }),
        _ => ("progress", new { step = 0, total = 0, command_name = m.Text }),
    };

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "...";
}
