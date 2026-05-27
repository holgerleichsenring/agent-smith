using System.Text.Json;
using AgentSmith.Contracts.Events;

namespace AgentSmith.Infrastructure.Services.Events;

/// <summary>
/// Single-field-pair envelope: <c>t</c> (EventType discriminator) and <c>p</c>
/// (typed payload JSON). The broadcaster reads <c>t</c>, looks up the concrete
/// type, and deserialises <c>p</c>. The shape stays additive — adding a new
/// event type costs one entry in <see cref="ResolveType"/>; renaming a payload
/// field doesn't change the envelope.
/// </summary>
public static class EventEnvelopeSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize(RunEvent runEvent)
    {
        var payload = JsonSerializer.Serialize((object)runEvent, runEvent.GetType(), Options);
        return $"{{\"t\":{(int)runEvent.Type},\"p\":{payload}}}";
    }

    public static RunEvent? Deserialize(string envelope)
    {
        using var doc = JsonDocument.Parse(envelope);
        var root = doc.RootElement;
        var typeCode = root.GetProperty("t").GetInt32();
        var payload = root.GetProperty("p").GetRawText();
        var concrete = ResolveType((EventType)typeCode);
        if (concrete is null) return null;
        return (RunEvent?)JsonSerializer.Deserialize(payload, concrete, Options);
    }

    // p0173a: parallel envelope path for SystemEvent. Same JSON shape
    // ({"t":<code>,"p":<payload>}) — a separate top-level method keeps the
    // run-event path type-narrow at the call sites and lets ResolveType
    // stay focused on each hierarchy.
    public static string SerializeSystem(SystemEvent systemEvent)
    {
        var payload = JsonSerializer.Serialize((object)systemEvent, systemEvent.GetType(), Options);
        return $"{{\"t\":{(int)systemEvent.Type},\"p\":{payload}}}";
    }

    public static SystemEvent? DeserializeSystem(string envelope)
    {
        using var doc = JsonDocument.Parse(envelope);
        var root = doc.RootElement;
        var typeCode = root.GetProperty("t").GetInt32();
        var payload = root.GetProperty("p").GetRawText();
        var concrete = ResolveSystemType((SystemEventType)typeCode);
        if (concrete is null) return null;
        return (SystemEvent?)JsonSerializer.Deserialize(payload, concrete, Options);
    }

    // Slice a defines only the enum codes; concrete records ship in
    // slices b + c next to their producers. Until then, ResolveSystemType
    // always returns null — DeserializeSystem yields null for every
    // payload, which is exactly what an empty stream produces anyway.
    private static Type? ResolveSystemType(SystemEventType type) => type switch
    {
        _ => null
    };

    private static Type? ResolveType(EventType type) => type switch
    {
        EventType.RunStarted => typeof(RunStartedEvent),
        EventType.RunFinished => typeof(RunFinishedEvent),
        EventType.SandboxCreated => typeof(SandboxCreatedEvent),
        EventType.SandboxDisposed => typeof(SandboxDisposedEvent),
        EventType.StepStarted => typeof(StepStartedEvent),
        EventType.StepFinished => typeof(StepFinishedEvent),
        EventType.DecisionLogged => typeof(DecisionLoggedEvent),
        EventType.GateChecked => typeof(GateCheckedEvent),
        EventType.TriageRoute => typeof(TriageRouteEvent),
        EventType.LlmCallStarted => typeof(LlmCallStartedEvent),
        EventType.LlmCallFinished => typeof(LlmCallFinishedEvent),
        EventType.SandboxCommand => typeof(SandboxCommandEvent),
        EventType.SandboxOutput => typeof(SandboxOutputEvent),
        EventType.SandboxResult => typeof(SandboxResultEvent),
        EventType.ToolCall => typeof(ToolCallEvent),
        EventType.ToolResult => typeof(ToolResultEvent),
        EventType.CatalogIssue => typeof(CatalogIssueEvent),
        _ => null
    };
}
