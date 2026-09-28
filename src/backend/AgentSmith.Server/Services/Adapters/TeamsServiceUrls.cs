using System.Collections.Concurrent;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// The Bot Framework service URL each Teams conversation is answered through, as the inbound
/// activities reported it. A singleton, because the typed <see cref="TeamsApiClient"/> is
/// transient: a map held per client only served whichever client happened to be told.
/// </summary>
public sealed class TeamsServiceUrls
{
    private readonly ConcurrentDictionary<string, string> _byConversation = new();

    public void Register(string conversationId, string serviceUrl) =>
        _byConversation[conversationId] = serviceUrl.TrimEnd('/');

    public string? For(string conversationId) =>
        _byConversation.TryGetValue(conversationId, out var url) ? url : null;
}
