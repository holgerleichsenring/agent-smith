using AgentSmith.Contracts.Dialogue;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Services.Dialogue;

/// <summary>
/// Dialogue trail (p58). InMemoryDialogueTrail is the CLI/test default; Server
/// composition overrides with RedisDialogueTransport-backed flows in AddRedis,
/// and the CLI's spawned-job mode overrides with RedisDialogueTransport too.
/// Scoped per pipeline-run.
/// </summary>
public static class DialogueTransportExtensions
{
    public static IServiceCollection AddDialogueTransport(this IServiceCollection services)
    {
        services.AddScoped<IDialogueTrail, InMemoryDialogueTrail>();
        return services;
    }
}
