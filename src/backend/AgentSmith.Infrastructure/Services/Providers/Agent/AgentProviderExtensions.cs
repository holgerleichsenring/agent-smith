using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Infrastructure.Services.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Agent providers (chat-client factory + per-provider IChatClient builders) and the
/// loop-limits defaults. p0119a: Microsoft.Extensions.AI factory + per-provider
/// IChatClient builders. The legacy IAgentProviderFactory / ILlmClientFactory /
/// IAgenticAnalyzerFactory and the entire AgentPromptBuilder / RepositoryToolDispatcher
/// stack were deleted in p0119a. IChatClientFactory takes AgentConfig per-call (not via
/// DI singleton); the factory itself + builders are all DI singletons. p0126a: per-skill
/// loop limits — defaults match Phase B of the runtime design; composition roots that
/// load AgentSmithConfig may replace this registration with the YAML-bound instance.
/// p0362: the legacy per-provider compactors (IContextCompactor / IOpenAiContextCompactor)
/// were deleted — no production caller constructed them since the p0341d middleware took
/// over; in-loop compaction is CompactingChatClient in the master chain.
/// </summary>
public static class AgentProviderExtensions
{
    public static IServiceCollection AddAgentProviders(this IServiceCollection services)
    {
        services.AddSingleton<IChatClientBuilder, ClaudeChatClientBuilder>();
        services.AddSingleton<IChatClientBuilder, OpenAiChatClientBuilder>();
        services.AddSingleton<IChatClientBuilder, GeminiChatClientBuilder>();
        services.AddSingleton<IChatClientBuilder, OllamaChatClientBuilder>();
        services.AddSingleton<ILlmRateLimiterRegistry, LlmRateLimiterRegistry>();
        services.AddSingleton<IChatClientFactory, ChatClientFactory>();
        services.AddSingleton<LoopLimitsConfig>(_ => new LoopLimitsConfig());
        return services;
    }
}
