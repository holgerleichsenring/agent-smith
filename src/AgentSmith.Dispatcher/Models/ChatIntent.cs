namespace AgentSmith.Dispatcher.Models;

/// <summary>
/// Discriminated union of all chat intents the dispatcher can handle.
/// </summary>
public abstract record ChatIntent
{
    public required string RawText { get; init; }
    public required string UserId { get; init; }
    public required string ChannelId { get; init; }
    public required string Platform { get; init; }
}

/// <summary>
/// User wants to fix a specific ticket: "fix #65 in todo-list"
/// Spawns a K8s Job. Interactive (progress + questions).
/// </summary>
public sealed record FixTicketIntent : ChatIntent
{
    public required int TicketId { get; init; }
    public required string Project { get; init; }
}

/// <summary>
/// User wants to list tickets: "list tickets in todo-list"
/// Executed directly in the dispatcher. No K8s Job.
/// </summary>
public sealed record ListTicketsIntent : ChatIntent
{
    public required string Project { get; init; }
}

/// <summary>
/// User wants to create a ticket: "create ticket 'Add logging' in todo-list"
/// Executed directly in the dispatcher. No K8s Job.
/// </summary>
public sealed record CreateTicketIntent : ChatIntent
{
    public required string Project { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// Input did not match any known pattern.
/// </summary>
public sealed record UnknownIntent : ChatIntent
{
    public static UnknownIntent From(string raw, string userId, string channelId, string platform) => new()
    {
        RawText = raw,
        UserId = userId,
        ChannelId = channelId,
        Platform = platform
    };
}
