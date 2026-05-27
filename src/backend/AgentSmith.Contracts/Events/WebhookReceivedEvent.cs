namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0173b: emitted once per webhook HTTP delivery. Source is
/// <c>webhook:{platform}</c> (e.g. <c>webhook:github</c>);
/// <see cref="EventType"/> is the platform-specific event header value
/// (<c>X-GitHub-Event: issues</c> etc.). <see cref="Actioned"/> tells
/// the dashboard whether the delivery resulted in pipeline activity;
/// <see cref="SkipReason"/> is populated when Actioned=false to surface
/// the specific no-action reason without forcing the operator to read
/// server logs.
/// </summary>
public sealed record WebhookReceivedEvent(
    string Source,
    string EventType,
    string Path,
    bool Actioned,
    string? SkipReason,
    DateTimeOffset Timestamp)
    : SystemEvent(Source, SystemEventType.WebhookReceived, Timestamp);
