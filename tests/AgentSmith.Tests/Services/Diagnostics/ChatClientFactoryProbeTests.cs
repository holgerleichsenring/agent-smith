using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Diagnostics;

/// <summary>
/// p0293: ChatClientFactory.ProbeAsync builds the bare provider client and sends a
/// 1-token request. A successful response → Ok; any provider/transport failure →
/// Ok=false with an Error and no exception escaping (the probe contract).
/// </summary>
public sealed class ChatClientFactoryProbeTests
{
    [Fact]
    public async Task ProbeAsync_ProviderResponds_ReturnsOk()
    {
        var sut = CreateFactory(new StubChatClient(new Queue<string>(new[] { "pong" })));

        var result = await sut.ProbeAsync(new AgentConfig { Type = "test", Model = "m" }, CancellationToken.None);

        result.Ok.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task ProbeAsync_ProviderThrows_ReturnsFailureWithoutThrowing()
    {
        var sut = CreateFactory(new ThrowingChatClient());

        var result = await sut.ProbeAsync(new AgentConfig { Type = "test", Model = "m" }, CancellationToken.None);

        result.Ok.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProbeAsync_NoBuilderForType_ReturnsFailure()
    {
        var sut = CreateFactory(new StubChatClient(new Queue<string>()));

        var result = await sut.ProbeAsync(new AgentConfig { Type = "unknown", Model = "m" }, CancellationToken.None);

        result.Ok.Should().BeFalse();
    }

    private static ChatClientFactory CreateFactory(IChatClient client) =>
        new(
            new IChatClientBuilder[] { new FakeBuilder(client) },
            Mock.Of<IEventPublisher>(),
            Mock.Of<IRunContextAccessor>(),
            Mock.Of<IModelPricingResolver>(),
            Mock.Of<ILlmRateLimiterRegistry>(),
            NullLoggerFactory.Instance);

    private sealed class FakeBuilder(IChatClient client) : IChatClientBuilder
    {
        public IReadOnlyList<string> SupportedTypes => new[] { "test" };
        public IChatClient Build(AgentConfig agent, ModelAssignment assignment) => client;
    }

    private sealed class ThrowingChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("401 Unauthorized");

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
