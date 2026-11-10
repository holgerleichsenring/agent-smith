using System.Runtime.CompilerServices;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Factories;

/// <summary>2026-11-10-e706: Ollama and Gemini calls had no bound agent-smith set; a call that never
/// answered held its design turn until the wall-time watchdog cut it.</summary>
public sealed class NetworkTimeoutChatClientTests
{
    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(200);

    [Fact]
    public async Task GetResponseAsync_ProviderNeverAnswers_FailsAfterTheBound()
    {
        var client = new NetworkTimeoutChatClient(new SilentChatClient(), Short);

        var act = () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "q")]);

        (await act.Should().ThrowAsync<TimeoutException>()).WithMessage("*sent nothing*network timeout*");
    }

    [Fact]
    public async Task GetResponseAsync_CallerCancels_StaysACancellation()
    {
        var client = new NetworkTimeoutChatClient(new SilentChatClient(), TimeSpan.FromMinutes(5));
        using var caller = new CancellationTokenSource();
        var call = client.GetResponseAsync([new ChatMessage(ChatRole.User, "q")], cancellationToken: caller.Token);

        await caller.CancelAsync();
        var act = () => call;

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetStreamingResponseAsync_StreamGoesSilent_FailsAfterTheBound()
    {
        var client = new NetworkTimeoutChatClient(new SilentChatClient(), Short);
        var received = new List<string>();

        var act = async () =>
        {
            await foreach (var update in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "q")]))
                received.Add(update.Text);
        };

        await act.Should().ThrowAsync<TimeoutException>();
        received.Should().Equal(["first"], "what arrived before the silence was delivered");
    }

    [Fact]
    public void OllamaBuilder_Build_BoundsTheClientByTheAgentsNetworkTimeout()
    {
        var built = new OllamaChatClientBuilder().Build(
            new AgentConfig { NetworkTimeoutSeconds = 42 }, new ModelAssignment { Model = "llama3" });

        built.GetService<NetworkTimeoutChatClient>()!.Bound.Should().Be(TimeSpan.FromSeconds(42));
    }

    /// <summary>A provider that answers nothing: one streamed update, then silence.</summary>
    private sealed class SilentChatClient : IChatClient
    {
        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new ChatResponse();
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, "first");
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
