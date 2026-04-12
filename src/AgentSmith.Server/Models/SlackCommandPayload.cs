namespace AgentSmith.Dispatcher.Models;

/// <summary>
/// Represents the parsed form data from a Slack slash command invocation.
/// </summary>
public sealed record SlackCommandPayload(
    string TriggerId,
    string Command,
    string Text,
    string UserId,
    string ChannelId);
