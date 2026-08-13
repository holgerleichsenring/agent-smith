using System.Text.Json;
using AgentSmith.Contracts.Events;

namespace AgentSmith.Infrastructure.Services.Events;

/// <summary>
/// Single-field-pair envelope: <c>t</c> (EventType discriminator) and <c>p</c>
/// (typed payload JSON). The broadcaster reads <c>t</c>, looks up the concrete
/// type, and deserialises <c>p</c>. The shape stays additive — adding a new
/// event type costs one entry in <see cref="EventTypeResolver"/>; renaming a payload
/// field doesn't change the envelope.
/// </summary>
public sealed class EventEnvelopeSerializer
{
    private readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Serialize(RunEvent runEvent)
    {
        var payload = JsonSerializer.Serialize((object)runEvent, runEvent.GetType(), Options);
        return $"{{\"t\":{(int)runEvent.Type},\"p\":{payload}}}";
    }

    public RunEvent? Deserialize(string envelope)
    {
        using var doc = JsonDocument.Parse(envelope);
        var root = doc.RootElement;
        var typeCode = root.GetProperty("t").GetInt32();
        var payload = root.GetProperty("p").GetRawText();
        var concrete = EventTypeResolver.Resolve((EventType)typeCode);
        if (concrete is null) return null;
        return (RunEvent?)JsonSerializer.Deserialize(payload, concrete, Options);
    }

    /// <summary>
    /// Deserialises the DURABLE DB trail's bare payload back to a typed RunEvent,
    /// given the stored EventType NAME. The trail (RunDbProjector) stores the raw
    /// <c>JsonSerializer.Serialize(ev, ev.GetType())</c> — default STJ casing, NOT
    /// the camelCase <c>{t,p}</c> envelope above — plus the type in its own column.
    /// Used to replay a run's execution after the Redis stream's 24h TTL expires
    /// or a Redis flush/restart loses it.
    /// </summary>
    public RunEvent? DeserializeRaw(string typeName, string? payloadJson)
    {
        if (string.IsNullOrEmpty(payloadJson)) return null;
        if (!Enum.TryParse<EventType>(typeName, out var type)) return null;
        var concrete = EventTypeResolver.Resolve(type);
        return concrete is null ? null : (RunEvent?)JsonSerializer.Deserialize(payloadJson, concrete);
    }

    // p0173a: parallel envelope path for SystemEvent. Same JSON shape
    // ({"t":<code>,"p":<payload>}) — a separate top-level method keeps the
    // run-event path type-narrow at the call sites and lets the type
    // resolver stay focused on each hierarchy.
    public string SerializeSystem(SystemEvent systemEvent)
    {
        var payload = JsonSerializer.Serialize((object)systemEvent, systemEvent.GetType(), Options);
        return $"{{\"t\":{(int)systemEvent.Type},\"p\":{payload}}}";
    }

    public SystemEvent? DeserializeSystem(string envelope)
    {
        using var doc = JsonDocument.Parse(envelope);
        var root = doc.RootElement;
        var typeCode = root.GetProperty("t").GetInt32();
        var payload = root.GetProperty("p").GetRawText();
        var concrete = EventTypeResolver.ResolveSystem((SystemEventType)typeCode);
        if (concrete is null) return null;
        return (SystemEvent?)JsonSerializer.Deserialize(payload, concrete, Options);
    }
}
