using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services.Pocs.Lifetime;

/// <summary>2026-11-23-c81ee: a PoC's lifetime — its computation, the pass's plan, the expiry re-check, the extend and the
/// purge stores — and the no-database defaults of its stores.</summary>
public static class PocLifetimeExtensions
{
    public static IServiceCollection AddPocLifetime(this IServiceCollection services)
    {
        services.TryAddSingleton<IPocLifetimeStore, NoPocLifetimeStore>();
        services.TryAddSingleton<IPocUploadPurgeStore, NoPocUploadPurgeStore>();
        return services.AddSingleton<PocLifetime>().AddSingleton<PocLifetimePlan>().AddSingleton<PocLifetimeReasons>()
            .AddTransient<PocLifetimeRecheck>()
            .AddTransient<ICommandHandler<PocExtendContext>, PocExtendHandler>();
    }
}
