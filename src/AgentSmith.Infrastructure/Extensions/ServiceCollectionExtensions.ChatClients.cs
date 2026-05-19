using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure;

public static partial class ServiceCollectionExtensions
{
    // p0119a: Microsoft.Extensions.AI factory + per-provider IChatClient builders.
    // The legacy IAgentProviderFactory / ILlmClientFactory / IAgenticAnalyzerFactory and
    // the entire AgentPromptBuilder / RepositoryToolDispatcher stack were deleted in p0119a.
    // IChatClientFactory takes AgentConfig per-call (not via DI singleton); the factory
    // itself + builders are all DI singletons.
    // p0126a: per-skill loop limits. Defaults match Phase B of the runtime design.
    // Composition roots that load AgentSmithConfig may replace this registration with
    // the YAML-bound instance to honor operator-set limits.
    private static void AddChatClients(IServiceCollection services)
    {
        services.AddSingleton<IChatClientBuilder, ClaudeChatClientBuilder>();
        services.AddSingleton<IChatClientBuilder, OpenAiChatClientBuilder>();
        services.AddSingleton<IChatClientBuilder, GeminiChatClientBuilder>();
        services.AddSingleton<IChatClientBuilder, OllamaChatClientBuilder>();
        services.AddSingleton<IChatClientFactory, ChatClientFactory>();
        services.AddSingleton<LoopLimitsConfig>(_ => new LoopLimitsConfig());
    }
}
