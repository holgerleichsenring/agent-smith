using AgentSmith.Contracts.Models.Configuration;
using Anthropic.SDK.Messaging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Maps a <see cref="CacheConfig"/> to the Anthropic SDK <see cref="PromptCacheType"/>.
/// Shared between <see cref="ClaudeAgentProvider"/> and <see cref="AgenticLoop"/>.
/// </summary>
internal static class CacheTypeResolver
{
    internal static PromptCacheType Resolve(CacheConfig cacheConfig)
    {
        if (!cacheConfig.IsEnabled) return PromptCacheType.None;
        return cacheConfig.Strategy.ToLowerInvariant() switch
        {
            "automatic" => PromptCacheType.AutomaticToolsAndSystem,
            "fine-grained" => PromptCacheType.FineGrained,
            _ => PromptCacheType.None
        };
    }
}
