using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Factories;

/// <summary>2026-10-25-c5a7i: the masker sits in every chain the factory builds.</summary>
public sealed class ChatClientFactoryMaskingTests
{
    [Theory]
    [InlineData(TaskType.Primary)]
    [InlineData(TaskType.Scout)]
    [InlineData(TaskType.Summarization)]
    [InlineData(TaskType.ContextGeneration)]
    public async Task ChatClientFactory_AnyTask_ChainContainsTheMasker(TaskType task)
    {
        var provider = new SecretValueMaskingChatClientTests.CapturingChatClient();
        var client = Factory(provider).Create(new AgentConfig { Type = "capture", Model = "gpt-4.1" }, task);

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, $"key {SecretValueMaskingChatClientTests.Token}")]);

        provider.Sent.Should().ContainSingle().Which.Text.Should().Be($"key {SecretMasker.ProviderPlaceholder}");
    }

    private static ChatClientFactory Factory(IChatClient provider) => new(
        [new Builder(provider)], EventTestStubs.NoOp, EventTestStubs.RunContext, new ModelPricingResolver(),
        new AgentSmith.Infrastructure.Services.RateLimiting.LlmRateLimiterRegistry(
            NullLogger<AgentSmith.Infrastructure.Services.RateLimiting.LlmRateLimiterRegistry>.Instance),
        new AgentSmith.Infrastructure.Services.RateLimiting.ThrottleWaitReporter(), new NullRunTraceWriter(),
        TurnActivityRecorder.Silent(), new CompactionSummaryRequest(), new WindowDerivedCompaction(),
        new AgentSmith.Infrastructure.Services.ToolImages.ToolImageRelay(new(), new()),
        SecretValueMaskingChatClientTests.Masker(), NullLoggerFactory.Instance);

    private sealed class Builder(IChatClient provider) : IChatClientBuilder
    {
        public IReadOnlyList<string> SupportedTypes { get; } = ["capture"];
        public bool AcceptsImageAfterToolResult => true;
        public IChatClient Build(AgentConfig agent, ModelAssignment assignment) => provider;
    }
}
