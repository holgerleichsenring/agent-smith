using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Access;
using AgentSmith.Server.Services.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-08-26-7a51: the access surface's services, registered UNCONDITIONALLY for p0503a's
/// reason — the route guard enumerates the built route table without a database, and a
/// handler parameter type absent from the container fails that enumeration with a message
/// about unregistered services rather than the rule it enforces.
/// </summary>
internal static class AccessSurfaceExtensions
{
    internal static IServiceCollection AddAccessSurface(this IServiceCollection services)
    {
        // Replaced by the EF store wherever relational persistence is wired in.
        services.TryAddSingleton<IObservedCallerStore, EmptyObservedCallerStore>();
        services.AddSingleton<AccessPeopleComposer>();
        services.AddSingleton<AccessViewComposer>();
        services.AddSingleton<AccessSurfaceReader>();
        services.AddSingleton<NewCustomRoleGuard>();
        services.AddSingleton<AccessGrantWriter>();
        services.AddSingleton<PersonRemover>();
        // The two services that keep the observation table honest: one takes the buffer off
        // the request path, the other enforces the retention window. Registered beside the
        // surface rather than beside the database, because they depend on the mapping in
        // force — and against the empty store they are simply no-ops.
        services.AddHostedService<CallerObservationFlushHostedService>();
        services.AddHostedService<ObservedCallerRetentionHostedService>();
        return services;
    }
}
