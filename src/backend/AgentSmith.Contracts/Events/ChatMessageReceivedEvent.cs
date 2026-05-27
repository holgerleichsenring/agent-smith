namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0173c: emitted when a chat platform delivers a message to the agent's
/// endpoint (Slack Events API, Teams Bot Framework, etc.). Source is
/// <c>chat:{platform}</c> (e.g. <c>chat:slack</c>, <c>chat:teams</c>).
///
/// <para><b>NO message content.</b> Same security boundary as p0169e's
/// promptHash decision — message text in the dashboard event stream
/// means operator-readable chat content in whatever the SignalR client
/// logs / inspects. Metadata only: <see cref="Channel"/> +
/// <see cref="MessageType"/> + <see cref="Actioned"/> + optional
/// <see cref="SkipReason"/>.</para>
/// </summary>
public sealed record ChatMessageReceivedEvent(
    string Source,
    string Channel,
    string MessageType,
    bool Actioned,
    string? SkipReason,
    DateTimeOffset Timestamp)
    : SystemEvent(Source, SystemEventType.ChatMessageReceived, Timestamp);
