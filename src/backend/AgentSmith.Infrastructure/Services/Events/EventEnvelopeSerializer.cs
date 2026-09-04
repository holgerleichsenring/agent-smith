using System.Collections.Concurrent;
using System.Text.Json;
using AgentSmith.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Events;

/// <summary>
/// Single-field-pair envelope: <c>t</c> (EventType discriminator) and <c>p</c>
/// (typed payload JSON). The broadcaster reads <c>t</c>, looks up the concrete
/// type, and deserialises <c>p</c>. The shape stays additive — adding a new
/// event type costs one entry in <see cref="EventTypeResolver"/>; renaming a payload
/// field doesn't change the envelope.
///
/// <para>2026-09-03-b028: a code the resolver does not know still returns null — the
/// reader cannot invent a type — but it says so first. Silence turned a missing
/// resolver row into a months-old blind spot: the event was published, crossed the
/// transport and was dropped on arrival, and a misrouted event is indistinguishable
/// from an event nobody sent. Reported ONCE PER DISTINCT CODE, because this sits on a
/// hot stream and a producer running ahead of a deployed reader emits continuously.</para>
/// </summary>
public sealed class EventEnvelopeSerializer(ILogger<EventEnvelopeSerializer>? logger = null)
{
    private readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Instance-scoped, not static: the codec is a DI singleton on every path that
    // reads a stream, so one entry per code per process is what this yields — and a
    // test gets a clean slate instead of whatever ran before it.
    private readonly ConcurrentDictionary<string, byte> _reportedCodes = new();

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
        if (concrete is null) return ReportUnresolved("run", typeCode.ToString());
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
        if (!Enum.TryParse<EventType>(typeName, out var type))
            return ReportUnresolved("trail", typeName);
        var concrete = EventTypeResolver.Resolve(type);
        if (concrete is null) return ReportUnresolved("trail", typeName);
        return (RunEvent?)JsonSerializer.Deserialize(payloadJson, concrete);
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
        if (concrete is null)
        {
            ReportUnresolved("system", typeCode.ToString());
            return null;
        }
        return (SystemEvent?)JsonSerializer.Deserialize(payload, concrete, Options);
    }

    /// <summary>
    /// Names the dropped code the first time it is seen on a given path, then stays
    /// quiet about it. Returns null so the caller reads as the drop it is.
    /// </summary>
    private RunEvent? ReportUnresolved(string path, string code)
    {
        if (_reportedCodes.TryAdd($"{path}:{code}", 0))
            logger?.LogWarning(
                "Event type code {Code} on the {Path} path resolves to no record; the event "
                + "was dropped. Add its row to EventTypeResolver, or the producer is ahead of "
                + "this reader.", code, path);
        return null;
    }
}
