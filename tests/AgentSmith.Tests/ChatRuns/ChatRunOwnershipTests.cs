using System.Reflection;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.ChatLaunch;
using FluentAssertions;

namespace AgentSmith.Tests.ChatRuns;

/// <summary>
/// A chat-started run is followed through its binding and nothing else. The orphan-job
/// detector acts on conversation states and bus subscriptions; a chat run creates neither, so
/// it can never be declared a crashed container.
/// </summary>
public sealed class ChatRunOwnershipTests
{
    [Fact]
    public void ChatRunTypes_TouchNoConversationStateAndNoBusSubscription()
    {
        var chatTypes = typeof(ChatRunStart).Assembly.GetTypes().Where(t =>
            t.Namespace is "AgentSmith.Server.Services.ChatLaunch" or "AgentSmith.Server.Services.ChatRuns");

        chatTypes.SelectMany(t => t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .Should().NotContain([typeof(ConversationStateManager), typeof(MessageBusListener)]);
    }
}
