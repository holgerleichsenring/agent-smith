namespace AgentSmith.Contracts.Events;

/// <summary>
/// Type discriminator for <see cref="SystemEvent"/> records. Separate range
/// from <see cref="EventType"/> — system events live on a different Redis
/// stream and are deserialised via a different serializer path, so the
/// numeric values never collide on the wire even if they overlap.
///
/// Slice a defines only the enum codes; concrete record types ship in slices
/// b (poller + webhook) and c (chat + config + catalog) next to their
/// producers — the records are payloads of these codes.
/// </summary>
public enum SystemEventType
{
    // p0173b — poller + webhook instrumentation (40-range)
    PollCycleStarted = 40,
    PollCycleFinished = 41,
    TicketScanned = 42,
    TicketSkipped = 43,
    TicketTriggered = 44,
    WebhookReceived = 45,

    // p0173c — channel + config + catalog instrumentation (50-range)
    ChatMessageReceived = 50,
    ConfigFileRead = 51,
    SkillCatalogLoaded = 52,
    ConceptVocabularyLoaded = 53,

    // p0353 — config live-reload: pending (write bumped the epoch) -> applied
    // (leader rebuilt for that epoch), correlated by epoch.
    ConfigChanged = 54,
    ConfigReloaded = 55,
}
