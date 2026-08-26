using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Access;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-08-26-7a51: the access surface's half of the relational store — the observed
/// callers, and the admin invariant wrapped around the document store.
/// <para>
/// The invariant is a decorator rather than a check on the settings route because three of
/// the four writes that reach a role mapping never touch that route: an import decomposes
/// straight into the doc store, a revert writes a prior document through, and the
/// bootstrap migration writes the file's mapping in. Here every one of them passes it.
/// </para>
/// </summary>
internal static class AccessPersistenceExtensions
{
    internal static IServiceCollection AddAccessPersistence(this IServiceCollection services)
    {
        services.AddSingleton<EfConfigDocumentStore>();
        services.RemoveAll<IConfigDocumentStore>();
        services.AddSingleton<IConfigDocumentStore>(sp => new AdminReachableConfigDocumentStore(
            sp.GetRequiredService<EfConfigDocumentStore>(),
            sp.GetRequiredService<AdminRoute>(),
            sp.GetRequiredService<ConfigDocJson>()));

        services.AddScoped<ObservedCallerRepository>();
        services.RemoveAll<IObservedCallerStore>();
        services.AddSingleton<IObservedCallerStore, EfObservedCallerStore>();
        return services;
    }
}
