using System.Text.Json.Nodes;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Teams' thread poster. A Teams conversation id is already thread-scoped, so the channel is
/// the whole address — except for the service URL, which is known only from an inbound
/// activity. A binding carries the URL it was started under, and it is registered again before
/// posting, so a replica that never heard from the conversation still replies to its region.
/// </summary>
public sealed class TeamsThreadAdapter(
    TeamsApiClient api,
    TeamsServiceUrls serviceUrls,
    TeamsCardBuilder cards) : IChatThreadAdapter
{
    public string Platform => DispatcherDefaults.PlatformTeams;

    public string? ReplyEndpointFor(string channelId) => serviceUrls.For(channelId);

    public Task PostAsync(ChatThread thread, string text, CancellationToken cancellationToken) =>
        SendAsync(thread, new JsonObject { ["type"] = "message", ["text"] = text }, cancellationToken);

    public Task PostQuestionAsync(ChatThread thread, DialogQuestion question, CancellationToken cancellationToken) =>
        SendAsync(thread,
            TeamsApiClient.WrapCardInActivity(cards.BuildQuestionCard(question), $"Question: {question.Text}"),
            cancellationToken);

    private Task SendAsync(ChatThread thread, JsonObject activity, CancellationToken cancellationToken)
    {
        if (thread.ReplyEndpoint is { Length: > 0 } endpoint) serviceUrls.Register(thread.ChannelId, endpoint);
        return api.SendActivityAsync(thread.ChannelId, activity, cancellationToken);
    }
}
