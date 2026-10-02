using AgentSmith.Server.Services.Adapters;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Handlers;

/// <summary>
/// Sends help, greeting, unknown, and clarification messages to the user, on the platform
/// the message came from. Extracted from SlackMessageDispatcher for single-responsibility.
/// </summary>
public sealed class HelpHandler(
    PlatformAdapters adapters,
    ILogger<HelpHandler> logger)
{
    public async Task SendHelpAsync(string platform, string channelId, CancellationToken ct)
    {
        await adapters.SendMessageAsync(platform, channelId,
            ":robot_face: *Agent Smith — here's what I can do:*\n\n" +
            "*Fix a ticket*\n  `fix #58` or `fix #58 in my-project`\n\n" +
            "*List tickets*\n  `list tickets` or `list tickets in my-project`\n\n" +
            "*Create a ticket*\n  `create ticket \"Add logging\" in my-project`\n\n" +
            "*Help*\n  `help` or `?`\n\n" +
            "_I also understand free-form text — just describe what you need._", ct);
    }

    public async Task SendGreetingAsync(string platform, string channelId, CancellationToken ct)
    {
        await adapters.SendMessageAsync(platform, channelId,
            ":wave: Hey! I'm Agent Smith — AI orchestration for code, legal, security, and workflows.\n" +
            "Type `help` to see what I can do.", ct);
    }

    public async Task SendUnknownAsync(
        string platform, string channelId, string originalInput, CancellationToken ct)
    {
        await adapters.SendMessageAsync(platform, channelId,
            $":shrug: I didn't understand: \"{originalInput}\"\n\n" +
            "Type `help` to see what I can do.", ct);
    }

    /// <summary>2026-10-02-5ab2d: Confirm was clicked, but nothing is pending any more.</summary>
    public Task SendClarificationExpiredAsync(string platform, string channelId, CancellationToken ct) =>
        adapters.SendMessageAsync(platform, channelId,
            "This request is no longer pending — please send it again.", ct);

    public async Task SendClarificationAsync(
        string platform, string channelId, string suggestion, CancellationToken ct)
    {
        await adapters.SendClarificationAsync(platform, channelId, suggestion, ct);
        logger.LogInformation("Sent clarification to {ChannelId}: {Suggestion}", channelId, suggestion);
    }
}
