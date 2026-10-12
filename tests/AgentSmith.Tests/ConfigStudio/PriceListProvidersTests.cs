using AgentSmith.Application.Services.Pricing;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Extensions;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-10-12-6bfae: which price-list ids each agent type's API accepts as written. The picker
/// ranks those first; an Anthropic agent was handed the Bedrock spelling because nothing did.
/// </summary>
public sealed class PriceListProvidersTests
{
    [Fact]
    public void PriceListProviders_PerBuilder_PinsTheDeclaredSet()
    {
        var byType = RegisteredBuilders().PriceListProvidersByType();

        byType["claude"].Should().Equal("anthropic");
        byType["anthropic"].Should().Equal("anthropic");
        byType["openai"].Should().Equal("openai");
        byType["azure_openai"].Should().Equal("openai");
        byType["gemini"].Should().Equal("vertex_ai-language-models");
        byType["google"].Should().Equal("vertex_ai-language-models");
        byType["ollama"].Should().BeEmpty("every ollama id in the list carries the ollama/ routing prefix");
        byType["copilot"].Should().BeEmpty("every github_copilot id carries a routing prefix");
        byType["external_worker"].Should().BeEmpty();
    }

    [Fact]
    public void PriceListProviders_EveryDeclaredProvider_HoldsIdsTheApiTakesAsWritten()
    {
        var models = new BundledModelPriceList().Models;

        foreach (var provider in RegisteredBuilders().SelectMany(b => b.PriceListProviders).Distinct())
            models.Where(m => m.Provider == provider)
                .Should().Contain(m => !m.Id.Contains('/'), $"'{provider}' must offer bare ids");
    }

    [Fact]
    public void PriceListProvidersByType_LooksUpCaseInsensitively() =>
        RegisteredBuilders().PriceListProvidersByType()["Anthropic"].Should().Equal("anthropic");

    private static IReadOnlyList<IChatClientBuilder> RegisteredBuilders()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRunContextAccessor, AgentSmith.Application.Services.Events.AsyncLocalRunContextAccessor>();
        services.AddAgentProviders();
        using var provider = services.BuildServiceProvider();
        return [.. provider.GetServices<IChatClientBuilder>()];
    }
}
