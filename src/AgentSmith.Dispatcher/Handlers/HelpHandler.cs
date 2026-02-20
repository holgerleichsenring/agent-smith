using AgentSmith.Dispatcher.Adapters;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Dispatcher.Handlers;

/// <summary>
/// Sends help, greeting, unknown, and clarification messages to the user.
/// Extracted from SlackMessageDispatcher for single-responsibility.
/// </summary>
public sealed class HelpHandler(
    IPlatformAdapter adapter,
    ILogger<HelpHandler> logger)
{
    public async Task SendHelpAsync(string channelId, CancellationToken ct = default)
    {
        await adapter.SendMessageAsync(channelId,
            ":robot_face: *Agent Smith — here's what I can do:*\n\n" +
            "*Fix a ticket*\n  `fix #58` or `fix #58 in my-project`\n\n" +
            "*List tickets*\n  `list tickets` or `list tickets in my-project`\n\n" +
            "*Create a ticket*\n  `create ticket \"Add logging\" in my-project`\n\n" +
            "*Help*\n  `help` or `?`\n\n" +
            "_I also understand free-form text — just describe what you need._", ct);
    }

    public async Task SendGreetingAsync(string channelId, CancellationToken ct = default)
    {
        await adapter.SendMessageAsync(channelId,
            ":wave: Hey! I'm Agent Smith — an autonomous coding agent.\n" +
            "Type `help` to see what I can do.", ct);
    }

    public async Task SendUnknownAsync(
        string channelId, string originalInput, CancellationToken ct = default)
    {
        await adapter.SendMessageAsync(channelId,
            $":shrug: I didn't understand: \"{originalInput}\"\n\n" +
            "Type `help` to see what I can do.", ct);
    }

    public async Task SendClarificationAsync(
        string channelId, string suggestion, CancellationToken ct = default)
    {
        await adapter.SendClarificationAsync(channelId, suggestion, ct);
        logger.LogInformation("Sent clarification to {ChannelId}: {Suggestion}", channelId, suggestion);
    }
}
