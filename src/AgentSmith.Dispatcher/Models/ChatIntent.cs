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

/// <summary>User asked for help or capabilities overview.</summary>
public sealed record HelpIntent : ChatIntent
{
    public static HelpIntent From(string raw, string userId, string channelId, string platform) => new()
    {
        RawText = raw, UserId = userId, ChannelId = channelId, Platform = platform
    };
}

/// <summary>User sent a greeting (hi, hello, hey, ...).</summary>
public sealed record GreetingIntent : ChatIntent
{
    public static GreetingIntent From(string raw, string userId, string channelId, string platform) => new()
    {
        RawText = raw, UserId = userId, ChannelId = channelId, Platform = platform
    };
}

/// <summary>Low-confidence parse — needs user confirmation before proceeding.</summary>
public sealed record ClarificationNeeded : ChatIntent
{
    public required string Suggestion { get; init; }

    public static ClarificationNeeded From(
        string suggestion, string raw, string userId, string channelId, string platform) => new()
    {
        RawText = raw, Suggestion = suggestion,
        UserId = userId, ChannelId = channelId, Platform = platform
    };
}

/// <summary>A recoverable error during intent resolution (e.g. ticket not found).</summary>
public sealed record ErrorIntent : ChatIntent
{
    public required string ErrorMessage { get; init; }

    public static ErrorIntent From(
        string errorMessage, string raw, string userId, string channelId, string platform) => new()
    {
        RawText = raw, ErrorMessage = errorMessage,
        UserId = userId, ChannelId = channelId, Platform = platform
    };
}
