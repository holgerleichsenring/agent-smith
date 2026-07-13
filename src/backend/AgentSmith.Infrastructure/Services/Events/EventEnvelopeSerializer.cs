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

    /// <summary>
    /// Deserialises the DURABLE DB trail's bare payload back to a typed RunEvent,
    /// given the stored EventType NAME. The trail (RunDbProjector) stores the raw
    /// <c>JsonSerializer.Serialize(ev, ev.GetType())</c> — default STJ casing, NOT
    /// the camelCase <c>{t,p}</c> envelope above — plus the type in its own column.
    /// Used to replay a run's execution after the Redis stream's 24h TTL expires
    /// or a Redis flush/restart loses it.
    /// </summary>
    public static RunEvent? DeserializeRaw(string typeName, string? payloadJson)
    {
        if (string.IsNullOrEmpty(payloadJson)) return null;
        if (!Enum.TryParse<EventType>(typeName, out var type)) return null;
        var concrete = ResolveType(type);
        return concrete is null ? null : (RunEvent?)JsonSerializer.Deserialize(payloadJson, concrete);
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

    // p0173a: slice a defined only the enum codes; p0173b adds the
    // poller + webhook records. Slice c will add the chat / config /
    // catalog rows.
    private static Type? ResolveSystemType(SystemEventType type) => type switch
    {
        SystemEventType.PollCycleStarted => typeof(PollCycleStartedEvent),
        SystemEventType.PollCycleFinished => typeof(PollCycleFinishedEvent),
        SystemEventType.TicketScanned => typeof(TicketScannedEvent),
        SystemEventType.TicketSkipped => typeof(TicketSkippedEvent),
        SystemEventType.TicketTriggered => typeof(TicketTriggeredEvent),
        SystemEventType.WebhookReceived => typeof(WebhookReceivedEvent),
        SystemEventType.ChatMessageReceived => typeof(ChatMessageReceivedEvent),
        SystemEventType.ConfigFileRead => typeof(ConfigFileReadEvent),
        SystemEventType.SkillCatalogLoaded => typeof(SkillCatalogLoadedEvent),
        SystemEventType.ConceptVocabularyLoaded => typeof(ConceptVocabularyLoadedEvent),
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
        EventType.L1StepDetail => typeof(L1StepDetailEvent),
        EventType.TicketFetched => typeof(TicketFetchedEvent),
        EventType.CatalogLoaded => typeof(CatalogLoadedEvent),
        EventType.CatalogIssue => typeof(CatalogIssueEvent),
        EventType.SubAgentSpawned => typeof(SubAgentSpawnedEvent),
        EventType.SubAgentObservation => typeof(SubAgentObservationEvent),
        EventType.SubAgentFinding => typeof(SubAgentFindingEvent),
        EventType.SubAgentFileWritten => typeof(SubAgentFileWrittenEvent),
        EventType.SubAgentToolCall => typeof(SubAgentToolCallEvent),
        EventType.SubAgentCompleted => typeof(SubAgentCompletedEvent),
        EventType.RunCancelRequested => typeof(RunCancelRequestedEvent),
        EventType.SandboxVanished => typeof(SandboxVanishedEvent),
        EventType.RunCheckpointed => typeof(RunCheckpointedEvent), // p0327
        _ => null
    };
}
