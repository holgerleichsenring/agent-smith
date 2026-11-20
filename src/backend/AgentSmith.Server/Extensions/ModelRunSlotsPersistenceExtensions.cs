using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-11-20-24bd: the model places in the database replace the in-process default of the agent registrations, so
/// the bound on one deployment holds across replicas.
/// </summary>
public static class ModelRunSlotsPersistenceExtensions
{
    public static IServiceCollection AddDbModelRunSlots(this IServiceCollection services)
    {
        services.TryAddSingleton<ModelRunSlotSignal>();
        services.TryAddSingleton(TimeProvider.System);
        services.RemoveAll<IModelRunSlots>();
        return services.AddSingleton<IModelRunSlots, DbModelRunSlots>();
    }
}
