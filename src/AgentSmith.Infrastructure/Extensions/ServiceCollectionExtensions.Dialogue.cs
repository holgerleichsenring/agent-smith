using AgentSmith.Contracts.Dialogue;
using AgentSmith.Infrastructure.Services.Dialogue;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure;

public static partial class ServiceCollectionExtensions
{
    // Dialogue (p58) — RedisDialogueTransport is registered by AgentSmith.Cli/
    // ServiceProviderFactory alongside the other Redis-dependent services (p0101).
    // The CLI overrides with ConsoleDialogueTransport for interactive modes.
    private static void AddDialogue(IServiceCollection services)
    {
        services.AddScoped<IDialogueTrail, InMemoryDialogueTrail>();
    }
}
